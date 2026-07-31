import SwiftUI

struct QuotaOverlayBadgeView: View {
  @ObservedObject var store: CodexQuotaStore
  @ObservedObject var appearanceStore: BadgeAppearanceStore

  let onChooseColor: () -> Void

  var body: some View {
    Button(action: onChooseColor) {
      Group {
        if store.snapshot == nil && store.isRefreshing {
          ProgressView()
            .controlSize(.mini)
            .tint(accentColor)
        } else {
          VStack(spacing: 1) {
            Text(badgeText)
              .font(.system(size: 10, weight: .semibold, design: .rounded))
              .monospacedDigit()
              .foregroundStyle(accentColor.opacity(0.88))

            GeometryReader { geometry in
              ZStack(alignment: .leading) {
                Capsule(style: .continuous)
                  .fill(accentColor.opacity(0.14))

                if remainingFraction > 0 {
                  Capsule(style: .continuous)
                    .fill(accentColor.opacity(0.78))
                    .frame(width: geometry.size.width * remainingFraction)
                }
              }
            }
            .frame(width: 34, height: 2)
          }
        }
      }
      .frame(width: 44, height: 18)
      .background(
        Capsule(style: .continuous)
          .fill(accentColor.opacity(0.09))
      )
      .overlay(
        Capsule(style: .continuous)
          .stroke(accentColor.opacity(0.18), lineWidth: 0.5)
      )
      .padding(2)
    }
    .buttonStyle(.plain)
    .focusable(false)
    .contentShape(Capsule(style: .continuous))
    .help("点击自定义颜色")
    .accessibilityElement(children: .ignore)
    .accessibilityLabel(accessibilityLabel)
    .accessibilityHint("点击打开颜色选择器")
  }

  private var accentColor: Color {
    Color(nsColor: appearanceStore.color)
  }

  private var badgeText: String {
    if let remainingPercent = store.snapshot?.remainingPercent {
      return "\(remainingPercent)%"
    }
    return store.errorMessage == nil ? "--" : "!"
  }

  private var remainingFraction: CGFloat {
    CGFloat(store.snapshot?.remainingPercent ?? 0) / 100
  }

  private var accessibilityLabel: String {
    if let remainingPercent = store.snapshot?.remainingPercent {
      return "Codex 剩余额度 \(remainingPercent)%"
    }
    return store.errorMessage ?? "正在读取 Codex 剩余额度"
  }
}
