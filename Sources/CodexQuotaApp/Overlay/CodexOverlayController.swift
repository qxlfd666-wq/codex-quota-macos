import AppKit
import CoreGraphics
import SwiftUI

struct CodexOverlayLayout {
  static let badgeSize = CGSize(width: 48, height: 22)

  static func badgeFrame(for codexWindowFrame: CGRect) -> CGRect {
    CGRect(
      x: codexWindowFrame.minX + 60,
      y: codexWindowFrame.minY + 12,
      width: badgeSize.width,
      height: badgeSize.height
    )
  }
}

@MainActor
final class CodexOverlayController: NSObject, NSWindowDelegate {
  static let codexBundleIdentifier = "com.openai.codex"

  private let panel: NSPanel
  private let appearanceStore: BadgeAppearanceStore
  private var applicationToReactivate: NSRunningApplication?
  private var trackingTimer: Timer?

  init(store: CodexQuotaStore, appearanceStore: BadgeAppearanceStore) {
    self.appearanceStore = appearanceStore
    panel = NSPanel(
      contentRect: CGRect(origin: .zero, size: CodexOverlayLayout.badgeSize),
      styleMask: [.borderless, .nonactivatingPanel],
      backing: .buffered,
      defer: false
    )

    super.init()

    panel.contentView = NSHostingView(
      rootView: QuotaOverlayBadgeView(
        store: store,
        appearanceStore: appearanceStore,
        onChooseColor: { [weak self] in
          self?.showColorPicker()
        }
      )
    )
    panel.isOpaque = false
    panel.backgroundColor = .clear
    panel.hasShadow = false
    panel.hidesOnDeactivate = false
    panel.ignoresMouseEvents = false
    panel.becomesKeyOnlyIfNeeded = true
    panel.isReleasedWhenClosed = false
    panel.level = .floating
    panel.animationBehavior = .none
    panel.collectionBehavior = [
      .canJoinAllSpaces,
      .fullScreenAuxiliary,
      .ignoresCycle,
      .transient,
    ]
  }

  func start() {
    guard trackingTimer == nil else { return }

    updatePlacement()
    let timer = Timer(
      timeInterval: 0.2,
      target: self,
      selector: #selector(trackingTimerFired),
      userInfo: nil,
      repeats: true
    )
    RunLoop.main.add(timer, forMode: .common)
    trackingTimer = timer
  }

  func stop() {
    trackingTimer?.invalidate()
    trackingTimer = nil
    panel.orderOut(nil)
  }

  func showColorPicker() {
    let colorPanel = NSColorPanel.shared
    applicationToReactivate = NSWorkspace.shared.frontmostApplication
    colorPanel.showsAlpha = false
    colorPanel.isContinuous = true
    colorPanel.color = appearanceStore.color
    colorPanel.setTarget(self)
    colorPanel.setAction(#selector(colorPanelChanged))
    colorPanel.delegate = self

    NSApp.activate()
    colorPanel.makeKeyAndOrderFront(nil)
  }

  @objc private func colorPanelChanged(_ sender: NSColorPanel) {
    appearanceStore.updateColor(sender.color)
  }

  func windowWillClose(_ notification: Notification) {
    guard let colorPanel = notification.object as? NSColorPanel,
      colorPanel === NSColorPanel.shared
    else {
      return
    }

    colorPanel.setTarget(nil)
    colorPanel.delegate = nil
    applicationToReactivate?.activate()
    applicationToReactivate = nil
  }

  @objc private func trackingTimerFired() {
    updatePlacement()
  }

  private func updatePlacement() {
    guard isCodexFrontmost,
      let codexWindowFrame = frontmostCodexWindowFrame()
    else {
      if panel.isVisible {
        panel.orderOut(nil)
      }
      return
    }

    let targetFrame = CodexOverlayLayout.badgeFrame(for: codexWindowFrame)

    if !panel.frame.approximatelyEquals(targetFrame) {
      panel.setFrame(targetFrame, display: true)
    }

    if !panel.isVisible {
      panel.orderFrontRegardless()
    }
  }

  private var isCodexFrontmost: Bool {
    NSWorkspace.shared.frontmostApplication?.bundleIdentifier == Self.codexBundleIdentifier
  }

  private func frontmostCodexWindowFrame() -> CGRect? {
    guard
      let application = NSRunningApplication.runningApplications(
        withBundleIdentifier: Self.codexBundleIdentifier
      ).first(where: { !$0.isTerminated })
    else {
      return nil
    }

    let options: CGWindowListOption = [.optionOnScreenOnly, .excludeDesktopElements]
    guard
      let windowRows = CGWindowListCopyWindowInfo(options, kCGNullWindowID)
        as? [[String: Any]]
    else {
      return nil
    }

    for row in windowRows {
      guard
        (row[kCGWindowOwnerPID as String] as? NSNumber)?.int32Value
          == application.processIdentifier,
        (row[kCGWindowLayer as String] as? NSNumber)?.intValue == 0,
        let quartzFrame = quartzWindowFrame(from: row),
        quartzFrame.width >= 700,
        quartzFrame.height >= 500
      else {
        continue
      }

      return appKitFrame(fromQuartzFrame: quartzFrame)
    }

    return nil
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

  private func appKitFrame(fromQuartzFrame quartzFrame: CGRect) -> CGRect {
    let midpoint = CGPoint(x: quartzFrame.midX, y: quartzFrame.midY)

    for screen in NSScreen.screens {
      guard
        let displayNumber = screen.deviceDescription[
          NSDeviceDescriptionKey("NSScreenNumber")
        ] as? NSNumber
      else {
        continue
      }

      let displayBounds = CGDisplayBounds(CGDirectDisplayID(displayNumber.uint32Value))
      guard displayBounds.contains(midpoint) || displayBounds.intersects(quartzFrame) else {
        continue
      }

      let x = screen.frame.minX + quartzFrame.minX - displayBounds.minX
      let distanceFromDisplayTop = quartzFrame.minY - displayBounds.minY
      let y = screen.frame.maxY - distanceFromDisplayTop - quartzFrame.height
      return CGRect(x: x, y: y, width: quartzFrame.width, height: quartzFrame.height)
    }

    let mainDisplayBounds = CGDisplayBounds(CGMainDisplayID())
    return CGRect(
      x: quartzFrame.minX,
      y: mainDisplayBounds.height - quartzFrame.maxY,
      width: quartzFrame.width,
      height: quartzFrame.height
    )
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

extension CGRect {
  fileprivate func approximatelyEquals(_ other: CGRect, tolerance: CGFloat = 0.5) -> Bool {
    abs(minX - other.minX) <= tolerance
      && abs(minY - other.minY) <= tolerance
      && abs(width - other.width) <= tolerance
      && abs(height - other.height) <= tolerance
  }
}
