import CoreGraphics
import Testing

@testable import CodexQuotaApp

@Suite("Codex overlay layout")
struct CodexOverlayLayoutTests {
  @Test("Places the badge beside the lower-left account name")
  func accountBadgePlacement() {
    let codexWindow = CGRect(x: 120, y: 80, width: 1_400, height: 900)

    let badge = CodexOverlayLayout.badgeFrame(for: codexWindow)

    #expect(badge == CGRect(x: 180, y: 92, width: 48, height: 22))
  }

  @Test("Placement follows a moved Codex window")
  func followsMovedWindow() {
    let firstWindow = CGRect(x: 0, y: 40, width: 1_200, height: 800)
    let movedWindow = firstWindow.offsetBy(dx: 360, dy: 180)

    let firstBadge = CodexOverlayLayout.badgeFrame(for: firstWindow)
    let movedBadge = CodexOverlayLayout.badgeFrame(for: movedWindow)

    #expect(movedBadge.origin.x - firstBadge.origin.x == 360)
    #expect(movedBadge.origin.y - firstBadge.origin.y == 180)
  }
}
