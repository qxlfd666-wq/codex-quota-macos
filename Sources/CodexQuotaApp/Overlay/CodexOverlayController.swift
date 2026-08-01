import AppKit
import CoreGraphics
import QuartzCore
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
  private let windowLocator = CodexWindowLocator()
  private var applicationToReactivate: NSRunningApplication?
  private var trackingDisplayLink: CADisplayLink?
  private var workspaceActivationObserver: NSObjectProtocol?

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
    guard trackingDisplayLink == nil else { return }

    updatePlacement()
    let displayLink = panel.displayLink(
      target: self,
      selector: #selector(displayLinkFired(_:))
    )
    displayLink.add(to: .main, forMode: .common)
    displayLink.isPaused = !isCodexFrontmost
    trackingDisplayLink = displayLink

    workspaceActivationObserver = NSWorkspace.shared.notificationCenter.addObserver(
      forName: NSWorkspace.didActivateApplicationNotification,
      object: nil,
      queue: .main
    ) { [weak self] _ in
      MainActor.assumeIsolated {
        self?.frontmostApplicationChanged()
      }
    }
  }

  func stop() {
    trackingDisplayLink?.invalidate()
    trackingDisplayLink = nil
    if let workspaceActivationObserver {
      NSWorkspace.shared.notificationCenter.removeObserver(workspaceActivationObserver)
      self.workspaceActivationObserver = nil
    }
    windowLocator.reset()
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

  @objc private func displayLinkFired(_ displayLink: CADisplayLink) {
    updatePlacement()
  }

  private func frontmostApplicationChanged() {
    let isCodexActive = isCodexFrontmost
    trackingDisplayLink?.isPaused = !isCodexActive

    if isCodexActive {
      windowLocator.reset()
      updatePlacement()
    } else {
      panel.orderOut(nil)
    }
  }

  private func updatePlacement() {
    guard
      let application = frontmostCodexApplication,
      let quartzFrame = windowLocator.windowFrame(
        processIdentifier: application.processIdentifier,
        timestamp: ProcessInfo.processInfo.systemUptime
      )
    else {
      if panel.isVisible {
        panel.orderOut(nil)
      }
      return
    }

    let codexWindowFrame = appKitFrame(fromQuartzFrame: quartzFrame)
    let targetOrigin = CodexOverlayLayout.badgeFrame(for: codexWindowFrame).origin

    if !panel.frame.origin.approximatelyEquals(targetOrigin) {
      panel.setFrameOrigin(targetOrigin)
    }

    if !panel.isVisible {
      panel.orderFrontRegardless()
    }
  }

  private var isCodexFrontmost: Bool {
    frontmostCodexApplication != nil
  }

  private var frontmostCodexApplication: NSRunningApplication? {
    guard
      let application = NSWorkspace.shared.frontmostApplication,
      application.bundleIdentifier == Self.codexBundleIdentifier,
      !application.isTerminated
    else {
      return nil
    }

    return application
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
}

extension CGPoint {
  fileprivate func approximatelyEquals(_ other: CGPoint, tolerance: CGFloat = 0.5) -> Bool {
    abs(x - other.x) <= tolerance && abs(y - other.y) <= tolerance
  }
}
