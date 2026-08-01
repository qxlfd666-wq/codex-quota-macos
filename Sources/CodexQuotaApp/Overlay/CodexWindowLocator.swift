import CoreGraphics
import Foundation

struct CodexWindowRecord: Equatable {
  let id: CGWindowID
  let quartzFrame: CGRect
}

protocol CodexWindowInfoProviding {
  func onScreenWindowRows() -> [[String: Any]]
  func windowRow(for id: CGWindowID) -> [String: Any]?
}

struct SystemCodexWindowInfoProvider: CodexWindowInfoProviding {
  func onScreenWindowRows() -> [[String: Any]] {
    let options: CGWindowListOption = [.optionOnScreenOnly, .excludeDesktopElements]
    return CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] ?? []
  }

  func windowRow(for id: CGWindowID) -> [String: Any]? {
    let options: CGWindowListOption = [.optionIncludingWindow, .excludeDesktopElements]
    return (CGWindowListCopyWindowInfo(options, id) as? [[String: Any]])?.first
  }
}

final class CodexWindowLocator {
  static let minimumWindowSize = CGSize(width: 700, height: 500)
  static let rediscoveryInterval: TimeInterval = 0.5

  private let provider: any CodexWindowInfoProviding
  private var trackedProcessIdentifier: pid_t?
  private var trackedWindowID: CGWindowID?
  private var lastDiscoveryTimestamp: TimeInterval?

  init(provider: any CodexWindowInfoProviding = SystemCodexWindowInfoProvider()) {
    self.provider = provider
  }

  func reset() {
    trackedProcessIdentifier = nil
    trackedWindowID = nil
    lastDiscoveryTimestamp = nil
  }

  func windowFrame(processIdentifier: pid_t, timestamp: TimeInterval) -> CGRect? {
    if trackedProcessIdentifier != processIdentifier {
      reset()
      trackedProcessIdentifier = processIdentifier
    }

    if let trackedWindowID {
      guard
        let row = provider.windowRow(for: trackedWindowID),
        let cachedRecord = record(from: row, processIdentifier: processIdentifier)
      else {
        self.trackedWindowID = nil
        return discoverWindow(processIdentifier: processIdentifier, timestamp: timestamp)
      }

      guard shouldRediscover(at: timestamp) else {
        return cachedRecord.quartzFrame
      }

      return discoverWindow(
        processIdentifier: processIdentifier,
        timestamp: timestamp,
        fallback: cachedRecord
      )
    }

    guard shouldRediscover(at: timestamp) else {
      return nil
    }

    return discoverWindow(processIdentifier: processIdentifier, timestamp: timestamp)
  }

  private func shouldRediscover(at timestamp: TimeInterval) -> Bool {
    guard let lastDiscoveryTimestamp else {
      return true
    }

    return timestamp - lastDiscoveryTimestamp >= Self.rediscoveryInterval
  }

  private func discoverWindow(
    processIdentifier: pid_t,
    timestamp: TimeInterval,
    fallback: CodexWindowRecord? = nil
  ) -> CGRect? {
    lastDiscoveryTimestamp = timestamp

    if let record = provider.onScreenWindowRows().lazy.compactMap({ row in
      self.record(from: row, processIdentifier: processIdentifier)
    }).first {
      trackedWindowID = record.id
      return record.quartzFrame
    }

    trackedWindowID = fallback?.id
    return fallback?.quartzFrame
  }

  private func record(
    from row: [String: Any],
    processIdentifier: pid_t
  ) -> CodexWindowRecord? {
    guard
      (row[kCGWindowOwnerPID as String] as? NSNumber)?.int32Value == processIdentifier,
      (row[kCGWindowLayer as String] as? NSNumber)?.intValue == 0,
      (row[kCGWindowIsOnscreen as String] as? Bool) == true,
      let windowNumber = row[kCGWindowNumber as String] as? NSNumber,
      let quartzFrame = quartzWindowFrame(from: row),
      quartzFrame.width >= Self.minimumWindowSize.width,
      quartzFrame.height >= Self.minimumWindowSize.height
    else {
      return nil
    }

    return CodexWindowRecord(id: windowNumber.uint32Value, quartzFrame: quartzFrame)
  }

  private func quartzWindowFrame(from row: [String: Any]) -> CGRect? {
    guard let bounds = row[kCGWindowBounds as String] as? [String: Any],
      let x = number(bounds["X"]),
      let y = number(bounds["Y"]),
      let width = number(bounds["Width"]),
      let height = number(bounds["Height"])
    else {
      return nil
    }

    return CGRect(x: x, y: y, width: width, height: height)
  }

  private func number(_ value: Any?) -> CGFloat? {
    if let number = value as? NSNumber {
      return CGFloat(number.doubleValue)
    }
    if let number = value as? Double {
      return CGFloat(number)
    }
    return nil
  }
}
