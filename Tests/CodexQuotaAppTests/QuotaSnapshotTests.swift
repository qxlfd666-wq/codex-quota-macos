import Foundation
import Testing

@testable import CodexQuotaApp

@Suite("Quota models")
struct QuotaSnapshotTests {
  @Test("Quota windows clamp server percentages")
  func clampsPercentages() {
    let belowRange = QuotaWindow(
      usedPercent: -8,
      windowDurationMinutes: 300,
      resetsAt: nil
    )
    let aboveRange = QuotaWindow(
      usedPercent: 118,
      windowDurationMinutes: 10_080,
      resetsAt: nil
    )

    #expect(belowRange.usedPercent == 0)
    #expect(belowRange.remainingPercent == 100)
    #expect(aboveRange.usedPercent == 100)
    #expect(aboveRange.remainingPercent == 0)
  }

  @Test("Common window lengths have readable labels")
  func durationLabels() {
    #expect(makeWindow(minutes: 300).durationDescription == "5 小时窗口")
    #expect(makeWindow(minutes: 10_080).durationDescription == "7 天窗口")
    #expect(makeWindow(minutes: 90).durationDescription == "90 分钟窗口")
  }

  private func makeWindow(minutes: Int) -> QuotaWindow {
    QuotaWindow(
      usedPercent: 25,
      windowDurationMinutes: minutes,
      resetsAt: nil
    )
  }
}
