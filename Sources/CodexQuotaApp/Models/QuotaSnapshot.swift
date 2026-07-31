import Foundation

/// Usage information for one Codex rate-limit window.
public struct QuotaWindow: Codable, Equatable, Sendable {
  public let usedPercent: Int
  public let windowDurationMinutes: Int?
  public let resetsAt: Date?

  public init(
    usedPercent: Int,
    windowDurationMinutes: Int?,
    resetsAt: Date?
  ) {
    self.usedPercent = min(max(usedPercent, 0), 100)
    self.windowDurationMinutes = windowDurationMinutes
    self.resetsAt = resetsAt
  }

  public var remainingPercent: Int {
    100 - usedPercent
  }

  public var durationDescription: String {
    guard let minutes = windowDurationMinutes, minutes > 0 else {
      return "额度窗口"
    }

    if minutes.isMultiple(of: 24 * 60) {
      return "\(minutes / (24 * 60)) 天窗口"
    }

    if minutes.isMultiple(of: 60) {
      return "\(minutes / 60) 小时窗口"
    }

    return "\(minutes) 分钟窗口"
  }
}

/// Account and quota data used by the app's account badge.
public struct QuotaSnapshot: Codable, Equatable, Sendable {
  public let displayName: String
  public let email: String?
  public let planName: String
  public let remainingPercent: Int
  public let usedPercent: Int
  public let primary: QuotaWindow?
  public let secondary: QuotaWindow?
  public let fetchedAt: Date

  public init(
    displayName: String,
    email: String?,
    planName: String,
    remainingPercent: Int,
    usedPercent: Int,
    primary: QuotaWindow?,
    secondary: QuotaWindow?,
    fetchedAt: Date
  ) {
    self.displayName = displayName
    self.email = email
    self.planName = planName
    self.remainingPercent = min(max(remainingPercent, 0), 100)
    self.usedPercent = min(max(usedPercent, 0), 100)
    self.primary = primary
    self.secondary = secondary
    self.fetchedAt = fetchedAt
  }
}
