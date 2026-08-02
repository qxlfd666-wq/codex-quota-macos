import Foundation
import Testing

@testable import CodexQuotaApp

@Suite("Menu-bar quota presentation")
struct MenuBarQuotaPresentationTests {
  @Test("Shows the latest percentage immediately")
  func latestPercentage() {
    let first = MenuBarQuotaPresentation.make(
      snapshot: snapshot(remainingPercent: 68),
      isRefreshing: false,
      errorMessage: nil
    )
    let updated = MenuBarQuotaPresentation.make(
      snapshot: snapshot(remainingPercent: 67),
      isRefreshing: false,
      errorMessage: nil
    )

    #expect(first.buttonTitle == "68%")
    #expect(updated.buttonTitle == "67%")
    #expect(updated.accessibilityLabel == "Codex 剩余 67%")
    #expect(updated.canShare)
  }

  @Test("Loading and unavailable states have distinct placeholders")
  func statePlaceholders() {
    let loading = MenuBarQuotaPresentation.make(
      snapshot: nil,
      isRefreshing: true,
      errorMessage: nil
    )
    let unavailable = MenuBarQuotaPresentation.make(
      snapshot: nil,
      isRefreshing: false,
      errorMessage: "网络不可用"
    )

    #expect(loading.buttonTitle == "…%")
    #expect(unavailable.buttonTitle == "—%")
    #expect(loading.buttonTitle != unavailable.buttonTitle)
    #expect(unavailable.detailTitle == "网络不可用")
    #expect(!loading.canShare)
    #expect(!unavailable.canShare)
  }

  @Test("Keeps a known percentage visible during refresh")
  func keepsSnapshotDuringRefresh() {
    let presentation = MenuBarQuotaPresentation.make(
      snapshot: snapshot(remainingPercent: 42),
      isRefreshing: true,
      errorMessage: nil
    )

    #expect(presentation.buttonTitle == "42%")
    #expect(presentation.quotaTitle.contains("正在更新"))
    #expect(presentation.toolTip.contains("正在更新"))
    #expect(presentation.canShare)
  }

  @Test("A new refresh takes priority over the previous failure message")
  func refreshAfterFailure() {
    let presentation = MenuBarQuotaPresentation.make(
      snapshot: snapshot(remainingPercent: 42),
      isRefreshing: true,
      errorMessage: "上次刷新失败"
    )

    #expect(presentation.buttonTitle == "42%")
    #expect(presentation.quotaTitle.contains("正在更新"))
    #expect(presentation.toolTip.contains("正在更新"))
    #expect(!presentation.toolTip.contains("更新失败"))
    #expect(presentation.canShare)
  }

  @Test("Marks a retained snapshot as stale after refresh failure")
  func staleSnapshotAfterFailure() {
    let presentation = MenuBarQuotaPresentation.make(
      snapshot: snapshot(remainingPercent: 42),
      isRefreshing: false,
      errorMessage: "网络不可用"
    )

    #expect(presentation.buttonTitle == "42%")
    #expect(presentation.toolTip.contains("上次剩余 42%"))
    #expect(presentation.toolTip.contains("更新失败"))
    #expect(presentation.quotaTitle.contains("更新失败"))
    #expect(presentation.detailTitle.contains("上次更新"))
    #expect(presentation.detailTitle.contains("网络不可用"))
    #expect(presentation.canShare)
  }

  @Test("Shows copy confirmation without replacing the percentage")
  func copyConfirmation() {
    let presentation = MenuBarQuotaPresentation.make(
      snapshot: snapshot(remainingPercent: 42),
      isRefreshing: false,
      errorMessage: nil,
      shareCardWasCopied: true
    )

    #expect(presentation.buttonTitle == "42%")
    #expect(presentation.shareCardTitle.contains("已复制"))
    #expect(presentation.toolTip.contains("已复制"))
  }

  private func snapshot(remainingPercent: Int) -> QuotaSnapshot {
    QuotaSnapshot(
      displayName: "Test User",
      email: "test@example.com",
      planName: "Codex Plus",
      remainingPercent: remainingPercent,
      usedPercent: 100 - remainingPercent,
      primary: nil,
      secondary: nil,
      fetchedAt: Date(timeIntervalSince1970: 1_700_000_000)
    )
  }
}
