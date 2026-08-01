import Foundation

private final class ProcessOutputBuffer: @unchecked Sendable {
  private let lock = NSLock()
  private var data = Data()
  private var signaledResponseIDs = Set<Int>()

  func append(_ chunk: Data, watching responseIDs: [Int]) -> [Int] {
    lock.lock()
    defer { lock.unlock() }

    data.append(chunk)
    var newlyAvailable: [Int] = []
    for responseID in responseIDs
    where !signaledResponseIDs.contains(responseID) && containsResponse(id: responseID) {
      signaledResponseIDs.insert(responseID)
      newlyAvailable.append(responseID)
    }
    return newlyAvailable
  }

  func snapshot() -> Data {
    lock.lock()
    defer { lock.unlock() }
    return data
  }

  private func containsResponse(id expectedID: Int) -> Bool {
    guard let output = String(data: data, encoding: .utf8) else { return false }

    return output.split(whereSeparator: \.isNewline).contains { line in
      guard let lineData = String(line).data(using: .utf8),
        let object = try? JSONSerialization.jsonObject(with: lineData),
        let response = object as? [String: Any]
      else {
        return false
      }

      if let id = response["id"] as? Int {
        return id == expectedID
      }
      if let id = response["id"] as? NSNumber {
        return id.intValue == expectedID
      }
      return false
    }
  }
}

enum CodexQuotaError: LocalizedError, Sendable {
  case codexNotFound
  case launchFailed(String)
  case requestTimedOut
  case invalidResponse
  case serverError(String)
  case quotaUnavailable

  var errorDescription: String? {
    switch self {
    case .codexNotFound:
      return "未找到 Codex。请先安装 ChatGPT、Codex 或 Codex CLI。"
    case .launchFailed(let reason):
      return "无法启动本机 Codex：\(reason)"
    case .requestTimedOut:
      return "读取额度超时，请检查网络后重试。"
    case .invalidResponse:
      return "Codex 返回了无法识别的额度数据。"
    case .serverError(let message):
      return "Codex 无法读取额度：\(message)"
    case .quotaUnavailable:
      return "当前登录方式没有可显示的套餐额度。请使用 ChatGPT 账户登录 Codex。"
    }
  }
}

actor CodexAppServerClient {
  private static let initializeRequestID = 1
  private static let accountRequestID = 2
  private static let rateLimitsRequestID = 3
  private static let requestTimeout: TimeInterval = 15

  func fetchSnapshot() async throws -> QuotaSnapshot {
    try await Task.detached(priority: .userInitiated) {
      let executableURL = try Self.locateCodexExecutable()
      return try Self.fetchSynchronously(executableURL: executableURL)
    }.value
  }

  private static func fetchSynchronously(executableURL: URL) throws -> QuotaSnapshot {
    let process = Process()
    let inputPipe = Pipe()
    let outputPipe = Pipe()
    let errorPipe = Pipe()

    process.executableURL = executableURL
    process.arguments = ["app-server"]
    process.standardInput = inputPipe
    process.standardOutput = outputPipe
    process.standardError = errorPipe

    let outputBuffer = ProcessOutputBuffer()
    let initializeReady = DispatchSemaphore(value: 0)
    let rateLimitsReady = DispatchSemaphore(value: 0)
    outputPipe.fileHandleForReading.readabilityHandler = { handle in
      let chunk = handle.availableData
      guard !chunk.isEmpty else { return }

      let availableIDs = outputBuffer.append(
        chunk,
        watching: [initializeRequestID, rateLimitsRequestID]
      )
      if availableIDs.contains(initializeRequestID) {
        initializeReady.signal()
      }
      if availableIDs.contains(rateLimitsRequestID) {
        rateLimitsReady.signal()
      }
    }

    do {
      try process.run()
    } catch {
      throw CodexQuotaError.launchFailed(error.localizedDescription)
    }

    do {
      let initializeRequest = """
        {"method":"initialize","id":\(initializeRequestID),"params":{"clientInfo":{"name":"codex_quota_macos","title":"Codex Quota","version":"1.0.1"}}}

        """
      try inputPipe.fileHandleForWriting.write(contentsOf: Data(initializeRequest.utf8))
    } catch {
      if process.isRunning {
        process.terminate()
      }
      throw CodexQuotaError.launchFailed(error.localizedDescription)
    }

    let initializeWaitResult = initializeReady.wait(timeout: .now() + requestTimeout)
    if initializeWaitResult == .timedOut {
      outputPipe.fileHandleForReading.readabilityHandler = nil
      try? inputPipe.fileHandleForWriting.close()
      if process.isRunning {
        process.terminate()
        process.waitUntilExit()
      }
      throw CodexQuotaError.requestTimedOut
    }

    let initializeResponses = decodeResponses(from: outputBuffer.snapshot())
    if let serverError = responseError(for: initializeRequestID, in: initializeResponses) {
      outputPipe.fileHandleForReading.readabilityHandler = nil
      try? inputPipe.fileHandleForWriting.close()
      if process.isRunning {
        process.terminate()
        process.waitUntilExit()
      }
      throw CodexQuotaError.serverError("初始化失败：\(serverError)")
    }
    guard hasSuccessfulResponse(for: initializeRequestID, in: initializeResponses) else {
      outputPipe.fileHandleForReading.readabilityHandler = nil
      try? inputPipe.fileHandleForWriting.close()
      if process.isRunning {
        process.terminate()
        process.waitUntilExit()
      }
      throw CodexQuotaError.invalidResponse
    }

    let request = """
      {"method":"initialized","params":{}}
      {"method":"account/read","id":\(accountRequestID),"params":{"refreshToken":false}}
      {"method":"account/rateLimits/read","id":\(rateLimitsRequestID)}

      """

    do {
      try inputPipe.fileHandleForWriting.write(contentsOf: Data(request.utf8))
    } catch {
      outputPipe.fileHandleForReading.readabilityHandler = nil
      if process.isRunning {
        process.terminate()
      }
      throw CodexQuotaError.launchFailed(error.localizedDescription)
    }

    let waitResult = rateLimitsReady.wait(timeout: .now() + requestTimeout)
    outputPipe.fileHandleForReading.readabilityHandler = nil
    try? inputPipe.fileHandleForWriting.close()

    if waitResult == .timedOut {
      if process.isRunning {
        process.terminate()
        process.waitUntilExit()
      }
      throw CodexQuotaError.requestTimedOut
    }

    let exitDeadline = Date().addingTimeInterval(2)
    while process.isRunning && Date() < exitDeadline {
      Thread.sleep(forTimeInterval: 0.02)
    }
    if process.isRunning {
      process.terminate()
    }
    process.waitUntilExit()

    let outputData = outputBuffer.snapshot()
    let errorData = errorPipe.fileHandleForReading.readDataToEndOfFile()

    let responses = decodeResponses(from: outputData)
    if let serverError = responseError(for: rateLimitsRequestID, in: responses) {
      throw CodexQuotaError.serverError(serverError)
    }

    guard
      let rateLimitsResult = responseResult(
        for: rateLimitsRequestID,
        in: responses
      )
    else {
      if process.terminationStatus != 0,
        let stderr = shortDiagnostic(from: errorData)
      {
        throw CodexQuotaError.launchFailed(stderr)
      }
      throw CodexQuotaError.invalidResponse
    }

    let accountResult = responseResult(for: accountRequestID, in: responses) ?? [:]
    return try parseSnapshot(
      accountResult: accountResult,
      rateLimitsResult: rateLimitsResult
    )
  }

  /// Converts app-server results into the small, stable model consumed by the UI.
  /// Kept internal so parser behavior can be verified without launching Codex.
  static func parseSnapshot(
    accountResult: [String: Any],
    rateLimitsResult: [String: Any],
    fetchedAt: Date = Date()
  ) throws -> QuotaSnapshot {
    let account = accountResult["account"] as? [String: Any]
    let email = nonEmptyString(account?["email"])

    let buckets = rateLimitsResult["rateLimitsByLimitId"] as? [String: Any]
    let codexBucket = buckets?["codex"] as? [String: Any]
    let fallbackBucket = rateLimitsResult["rateLimits"] as? [String: Any]

    guard let bucket = codexBucket ?? fallbackBucket else {
      throw CodexQuotaError.quotaUnavailable
    }

    let primary = parseWindow(bucket["primary"])
    let secondary = parseWindow(bucket["secondary"])
    let windows = [primary, secondary].compactMap { $0 }

    guard !windows.isEmpty else {
      throw CodexQuotaError.quotaUnavailable
    }

    // A request is blocked when either window is exhausted. Showing the most-used
    // window gives the single sidebar badge the conservative remaining value.
    let usedPercent = windows.map(\.usedPercent).max() ?? 0
    let remainingPercent = 100 - usedPercent

    let rawPlan =
      nonEmptyString(bucket["planType"])
      ?? nonEmptyString(account?["planType"])

    return QuotaSnapshot(
      displayName: displayName(for: email),
      email: email,
      planName: planName(for: rawPlan),
      remainingPercent: remainingPercent,
      usedPercent: usedPercent,
      primary: primary,
      secondary: secondary,
      fetchedAt: fetchedAt
    )
  }

  private static func parseWindow(_ value: Any?) -> QuotaWindow? {
    guard let object = value as? [String: Any],
      let usedPercent = integer(object["usedPercent"])
    else {
      return nil
    }

    let duration = integer(object["windowDurationMins"])
    let resetsAt = number(object["resetsAt"])
      .map { Date(timeIntervalSince1970: $0) }

    return QuotaWindow(
      usedPercent: usedPercent,
      windowDurationMinutes: duration,
      resetsAt: resetsAt
    )
  }

  private static func decodeResponses(from data: Data) -> [[String: Any]] {
    guard let output = String(data: data, encoding: .utf8) else { return [] }

    return
      output
      .split(whereSeparator: \.isNewline)
      .compactMap { line -> [String: Any]? in
        guard let lineData = String(line).data(using: .utf8),
          let object = try? JSONSerialization.jsonObject(with: lineData)
        else {
          return nil
        }
        return object as? [String: Any]
      }
  }

  private static func responseResult(
    for requestID: Int,
    in responses: [[String: Any]]
  ) -> [String: Any]? {
    responses.first { integer($0["id"]) == requestID }?["result"] as? [String: Any]
  }

  private static func responseError(
    for requestID: Int,
    in responses: [[String: Any]]
  ) -> String? {
    guard let response = responses.first(where: { integer($0["id"]) == requestID }),
      let error = response["error"] as? [String: Any]
    else {
      return nil
    }
    return nonEmptyString(error["message"]) ?? "未知错误"
  }

  private static func hasSuccessfulResponse(
    for requestID: Int,
    in responses: [[String: Any]]
  ) -> Bool {
    responses.contains { response in
      integer(response["id"]) == requestID && response["result"] != nil
    }
  }

  private static func locateCodexExecutable() throws -> URL {
    let environment = ProcessInfo.processInfo.environment
    let home = FileManager.default.homeDirectoryForCurrentUser.path
    var candidates: [String] = []

    if let override = nonEmptyString(environment["CODEX_QUOTA_CODEX_PATH"]) {
      candidates.append((override as NSString).expandingTildeInPath)
    }

    if let path = environment["PATH"] {
      candidates.append(
        contentsOf:
          path
          .split(separator: ":")
          .map { String($0) + "/codex" })
    }

    candidates.append(contentsOf: [
      "/Applications/Codex.app/Contents/Resources/codex",
      "/Applications/ChatGPT.app/Contents/Resources/codex",
      "\(home)/Applications/Codex.app/Contents/Resources/codex",
      "\(home)/Applications/ChatGPT.app/Contents/Resources/codex",
      "/opt/homebrew/bin/codex",
      "/usr/local/bin/codex",
      "\(home)/.local/bin/codex",
    ])

    var seen = Set<String>()
    for candidate in candidates where seen.insert(candidate).inserted {
      if FileManager.default.isExecutableFile(atPath: candidate) {
        return URL(fileURLWithPath: candidate)
      }
    }

    throw CodexQuotaError.codexNotFound
  }

  private static func displayName(for email: String?) -> String {
    if let email,
      let localPart = email.split(separator: "@", maxSplits: 1).first
    {
      let words =
        localPart
        .split(whereSeparator: { ".-_".contains($0) })
        .map { String($0) }
      if !words.isEmpty {
        return words.joined(separator: " ")
      }
    }

    let systemName = NSFullUserName().trimmingCharacters(in: .whitespacesAndNewlines)
    return systemName.isEmpty ? "Codex 用户" : systemName
  }

  private static func planName(for rawPlan: String?) -> String {
    guard let rawPlan else { return "Codex 套餐" }

    let knownNames: [String: String] = [
      "free": "Codex Free",
      "plus": "Codex Plus",
      "pro": "Codex Pro",
      "team": "Codex Team",
      "business": "Codex Business",
      "enterprise": "Codex Enterprise",
      "edu": "Codex Edu",
    ]

    return knownNames[rawPlan.lowercased()] ?? "Codex \(rawPlan)"
  }

  private static func integer(_ value: Any?) -> Int? {
    if let value = value as? Int { return value }
    if let value = value as? NSNumber { return value.intValue }
    if let value = value as? String { return Int(value) }
    return nil
  }

  private static func number(_ value: Any?) -> Double? {
    if let value = value as? Double { return value }
    if let value = value as? NSNumber { return value.doubleValue }
    if let value = value as? String { return Double(value) }
    return nil
  }

  private static func nonEmptyString(_ value: Any?) -> String? {
    guard let value = value as? String else { return nil }
    let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
    return trimmed.isEmpty ? nil : trimmed
  }

  private static func shortDiagnostic(from data: Data) -> String? {
    guard
      let value = String(data: data, encoding: .utf8)?
        .trimmingCharacters(in: .whitespacesAndNewlines),
      !value.isEmpty
    else {
      return nil
    }
    return String(value.prefix(240))
  }
}
