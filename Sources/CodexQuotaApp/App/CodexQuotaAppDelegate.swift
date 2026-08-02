import AppKit
import Combine

@MainActor
final class CodexQuotaAppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
  private let store = CodexQuotaStore()
  private let appearanceStore = BadgeAppearanceStore()
  private var overlayController: CodexOverlayController?
  private var statusItem: NSStatusItem?
  private var quotaMenuItem: NSMenuItem?
  private var detailMenuItem: NSMenuItem?
  private var shareCardMenuItem: NSMenuItem?
  private var storeCancellables = Set<AnyCancellable>()
  private var shareCardFeedbackTask: Task<Void, Never>?
  private var isShowingShareCardCopyConfirmation = false

  func applicationWillFinishLaunching(_ notification: Notification) {
    NSApp.setActivationPolicy(.accessory)
  }

  func applicationDidFinishLaunching(_ notification: Notification) {
    configureStatusItem()
    bindQuotaPresentation()

    let overlayController = CodexOverlayController(
      store: store,
      appearanceStore: appearanceStore
    )
    self.overlayController = overlayController
    overlayController.start()
    store.start()
  }

  func applicationWillTerminate(_ notification: Notification) {
    shareCardFeedbackTask?.cancel()
    overlayController?.stop()
  }

  func menuNeedsUpdate(_ menu: NSMenu) {
    applyQuotaPresentation()
  }

  private func configureStatusItem() {
    let statusItem = NSStatusBar.system.statusItem(withLength: 48)
    statusItem.button?.title = "…%"
    statusItem.button?.font = .monospacedDigitSystemFont(ofSize: 12, weight: .semibold)
    statusItem.button?.alignment = .center
    statusItem.button?.toolTip = "Codex 剩余额度"

    let menu = NSMenu()
    menu.delegate = self

    let quotaMenuItem = NSMenuItem(title: "正在读取 Codex 额度…", action: nil, keyEquivalent: "")
    quotaMenuItem.isEnabled = false
    menu.addItem(quotaMenuItem)

    let detailMenuItem = NSMenuItem(title: "请稍候", action: nil, keyEquivalent: "")
    detailMenuItem.isEnabled = false
    menu.addItem(detailMenuItem)
    menu.addItem(.separator())

    let refreshItem = NSMenuItem(
      title: "刷新额度",
      action: #selector(refreshQuota),
      keyEquivalent: "r"
    )
    refreshItem.keyEquivalentModifierMask = [.command]
    refreshItem.target = self
    menu.addItem(refreshItem)

    let shareCardItem = NSMenuItem(
      title: "复制分享卡片",
      action: #selector(copyShareCard),
      keyEquivalent: ""
    )
    shareCardItem.target = self
    shareCardItem.isEnabled = false
    menu.addItem(shareCardItem)

    let customizeColorItem = NSMenuItem(
      title: "自定义颜色…",
      action: #selector(customizeColor),
      keyEquivalent: ""
    )
    customizeColorItem.target = self
    menu.addItem(customizeColorItem)

    let openCodexItem = NSMenuItem(
      title: "打开 Codex",
      action: #selector(openCodex),
      keyEquivalent: ""
    )
    openCodexItem.target = self
    menu.addItem(openCodexItem)
    menu.addItem(.separator())

    let quitItem = NSMenuItem(
      title: "退出额度徽标",
      action: #selector(quitApplication),
      keyEquivalent: "q"
    )
    quitItem.keyEquivalentModifierMask = [.command]
    quitItem.target = self
    menu.addItem(quitItem)

    statusItem.menu = menu
    self.statusItem = statusItem
    self.quotaMenuItem = quotaMenuItem
    self.detailMenuItem = detailMenuItem
    self.shareCardMenuItem = shareCardItem
  }

  private func bindQuotaPresentation() {
    store.$snapshot
      .combineLatest(store.$isRefreshing, store.$errorMessage)
      .sink { [weak self] _, _, _ in
        self?.applyQuotaPresentation()
      }
      .store(in: &storeCancellables)
  }

  private func applyQuotaPresentation() {
    let presentation = MenuBarQuotaPresentation.make(
      snapshot: store.snapshot,
      isRefreshing: store.isRefreshing,
      errorMessage: store.errorMessage,
      shareCardWasCopied: isShowingShareCardCopyConfirmation
    )
    statusItem?.button?.title = presentation.buttonTitle
    statusItem?.button?.toolTip = presentation.toolTip
    statusItem?.button?.setAccessibilityLabel(presentation.accessibilityLabel)
    quotaMenuItem?.title = presentation.quotaTitle
    detailMenuItem?.title = presentation.detailTitle
    shareCardMenuItem?.title = presentation.shareCardTitle
    shareCardMenuItem?.isEnabled = presentation.canShare
  }

  @objc private func refreshQuota() {
    store.refresh()
  }

  @objc private func customizeColor() {
    overlayController?.showColorPicker()
  }

  @objc private func copyShareCard() {
    guard let snapshot = store.snapshot else { return }
    let content = QuotaShareCardContent(
      remainingPercent: snapshot.remainingPercent,
      fetchedAt: snapshot.fetchedAt
    )
    guard
      let pngData = QuotaShareCardRenderer.pngData(
        content: content,
        accentColor: appearanceStore.color
      )
    else {
      shareCardCopyFailed()
      return
    }

    guard QuotaShareCardPasteboard.writePNG(pngData) else {
      shareCardCopyFailed()
      return
    }
    showShareCardCopyConfirmation()
  }

  private func showShareCardCopyConfirmation() {
    shareCardFeedbackTask?.cancel()
    isShowingShareCardCopyConfirmation = true
    applyQuotaPresentation()

    shareCardFeedbackTask = Task { [weak self] in
      do {
        try await Task.sleep(for: .milliseconds(1_500))
      } catch {
        return
      }
      guard !Task.isCancelled else { return }
      self?.isShowingShareCardCopyConfirmation = false
      self?.applyQuotaPresentation()
    }
  }

  private func shareCardCopyFailed() {
    shareCardFeedbackTask?.cancel()
    shareCardFeedbackTask = nil
    isShowingShareCardCopyConfirmation = false
    applyQuotaPresentation()
    NSSound.beep()
  }

  @objc private func openCodex() {
    if let codex = NSRunningApplication.runningApplications(
      withBundleIdentifier: CodexOverlayController.codexBundleIdentifier
    ).first {
      codex.activate()
      return
    }

    let applicationURL = URL(fileURLWithPath: "/Applications/ChatGPT.app")
    NSWorkspace.shared.openApplication(
      at: applicationURL,
      configuration: .init()
    )
  }

  @objc private func quitApplication() {
    NSApp.terminate(nil)
  }
}
