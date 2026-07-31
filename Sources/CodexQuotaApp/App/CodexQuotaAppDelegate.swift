import AppKit

@MainActor
final class CodexQuotaAppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
  private let store = CodexQuotaStore()
  private let appearanceStore = BadgeAppearanceStore()
  private var overlayController: CodexOverlayController?
  private var statusItem: NSStatusItem?
  private var quotaMenuItem: NSMenuItem?
  private var detailMenuItem: NSMenuItem?

  func applicationWillFinishLaunching(_ notification: Notification) {
    NSApp.setActivationPolicy(.accessory)
  }

  func applicationDidFinishLaunching(_ notification: Notification) {
    configureStatusItem()

    let overlayController = CodexOverlayController(
      store: store,
      appearanceStore: appearanceStore
    )
    self.overlayController = overlayController
    overlayController.start()
    store.start()
  }

  func applicationWillTerminate(_ notification: Notification) {
    overlayController?.stop()
  }

  func menuNeedsUpdate(_ menu: NSMenu) {
    if let snapshot = store.snapshot {
      quotaMenuItem?.title = "Codex 剩余 \(snapshot.remainingPercent)%"
      detailMenuItem?.title =
        "\(snapshot.planName) · \(snapshot.fetchedAt.formatted(date: .omitted, time: .shortened)) 更新"
      statusItem?.button?.toolTip = "Codex 剩余 \(snapshot.remainingPercent)%"
    } else if store.isRefreshing {
      quotaMenuItem?.title = "正在读取 Codex 额度…"
      detailMenuItem?.title = "请稍候"
    } else {
      quotaMenuItem?.title = "暂时无法读取额度"
      detailMenuItem?.title = store.errorMessage ?? "请确认已登录 Codex"
    }
  }

  private func configureStatusItem() {
    let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
    statusItem.button?.image = NSImage(
      systemSymbolName: "percent",
      accessibilityDescription: "Codex 剩余额度"
    )
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
  }

  @objc private func refreshQuota() {
    store.refresh()
  }

  @objc private func customizeColor() {
    overlayController?.showColorPicker()
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
