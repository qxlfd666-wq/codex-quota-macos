import AppKit
import Foundation

@MainActor
enum QuotaShareCardPasteboard {
  static func writePNG(
    _ data: Data,
    to pasteboard: NSPasteboard = .general
  ) -> Bool {
    let item = NSPasteboardItem()
    return writePNG(
      data,
      stageData: { item.setData($0, forType: .png) },
      clearContents: { pasteboard.clearContents() },
      commit: { pasteboard.writeObjects([item]) }
    )
  }

  static func writePNG(
    _ data: Data,
    stageData: (Data) -> Bool,
    clearContents: () -> Void,
    commit: () -> Bool
  ) -> Bool {
    guard stageData(data) else { return false }
    clearContents()
    return commit()
  }
}
