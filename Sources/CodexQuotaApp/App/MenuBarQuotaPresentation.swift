import Foundation

struct MenuBarQuotaPresentation: Equatable {
  let buttonTitle: String
  let accessibilityLabel: String
  let toolTip: String
  let quotaTitle: String
  let detailTitle: String
  let shareCardTitle: String
  let canShare: Bool

  static func make(
    snapshot: QuotaSnapshot?,
    isRefreshing: Bool,
    errorMessage: String?,
    shareCardWasCopied: Bool = false
  ) -> Self {
    let presentation: Self
    if let snapshot {
      let quotaTitle = "Codex 剩余 \(snapshot.remainingPercent)%"
      if isRefreshing {
        let refreshingTitle =
          "Codex 上次剩余 \(snapshot.remainingPercent)% · 正在更新"
        presentation = Self(
          buttonTitle: "\(snapshot.remainingPercent)%",
          accessibilityLabel: refreshingTitle,
          toolTip: refreshingTitle,
          quotaTitle: "正在更新 Codex 额度…",
          detailTitle:
            "显示上次 \(snapshot.remainingPercent)% · \(snapshot.fetchedAt.formatted(date: .omitted, time: .shortened)) 更新",
          shareCardTitle: "复制分享卡片",
          canShare: true
        )
      } else if let errorMessage {
        let staleQuotaTitle =
          "Codex 上次剩余 \(snapshot.remainingPercent)% · 更新失败"
        presentation = Self(
          buttonTitle: "\(snapshot.remainingPercent)%",
          accessibilityLabel: staleQuotaTitle,
          toolTip: staleQuotaTitle,
          quotaTitle: staleQuotaTitle,
          detailTitle:
            "\(snapshot.fetchedAt.formatted(date: .omitted, time: .shortened)) 上次更新 · \(errorMessage)",
          shareCardTitle: "复制分享卡片",
          canShare: true
        )
      } else {
        presentation = Self(
          buttonTitle: "\(snapshot.remainingPercent)%",
          accessibilityLabel: quotaTitle,
          toolTip: quotaTitle,
          quotaTitle: quotaTitle,
          detailTitle:
            "\(snapshot.planName) · \(snapshot.fetchedAt.formatted(date: .omitted, time: .shortened)) 更新",
          shareCardTitle: "复制分享卡片",
          canShare: true
        )
      }
    } else if isRefreshing {
      presentation = Self(
        buttonTitle: "…%",
        accessibilityLabel: "正在读取 Codex 额度",
        toolTip: "正在读取 Codex 额度",
        quotaTitle: "正在读取 Codex 额度…",
        detailTitle: "请稍候",
        shareCardTitle: "复制分享卡片",
        canShare: false
      )
    } else {
      presentation = Self(
        buttonTitle: "—%",
        accessibilityLabel: "暂时无法读取 Codex 额度",
        toolTip: "暂时无法读取 Codex 额度",
        quotaTitle: "暂时无法读取额度",
        detailTitle: errorMessage ?? "请确认已登录 Codex",
        shareCardTitle: "复制分享卡片",
        canShare: false
      )
    }

    guard shareCardWasCopied, presentation.canShare else { return presentation }
    return Self(
      buttonTitle: presentation.buttonTitle,
      accessibilityLabel: presentation.accessibilityLabel,
      toolTip: "分享卡片已复制",
      quotaTitle: presentation.quotaTitle,
      detailTitle: presentation.detailTitle,
      shareCardTitle: "已复制分享卡片",
      canShare: true
    )
  }
}
