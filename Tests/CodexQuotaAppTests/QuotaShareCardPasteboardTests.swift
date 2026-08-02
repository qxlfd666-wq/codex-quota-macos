import AppKit
import Foundation
import Testing

@testable import CodexQuotaApp

@MainActor
@Suite("Quota share-card pasteboard")
struct QuotaShareCardPasteboardTests {
  @Test("Does not commit when staging PNG data fails")
  func stagingFailure() {
    var didClear = false
    var didCommit = false
    let result = QuotaShareCardPasteboard.writePNG(
      Data([1, 2, 3]),
      stageData: { _ in false },
      clearContents: { didClear = true },
      commit: {
        didCommit = true
        return true
      }
    )

    #expect(!result)
    #expect(!didClear)
    #expect(!didCommit)
  }

  @Test("Reports a pasteboard commit failure")
  func commitFailure() {
    let result = QuotaShareCardPasteboard.writePNG(
      Data([1, 2, 3]),
      stageData: { _ in true },
      clearContents: {},
      commit: { false }
    )

    #expect(!result)
  }

  @Test("Succeeds only after staging and committing")
  func success() {
    let result = QuotaShareCardPasteboard.writePNG(
      Data([1, 2, 3]),
      stageData: { $0 == Data([1, 2, 3]) },
      clearContents: {},
      commit: { true }
    )

    #expect(result)
  }

  @Test("Replaces old clipboard text with only the PNG")
  func replacesOldClipboardText() throws {
    let pasteboard = NSPasteboard(name: .init("CodexQuotaTests.\(UUID().uuidString)"))
    defer {
      pasteboard.clearContents()
      pasteboard.releaseGlobally()
    }
    pasteboard.clearContents()
    #expect(pasteboard.setString("previous private text", forType: .string))

    let pngData = Data([137, 80, 78, 71])
    #expect(QuotaShareCardPasteboard.writePNG(pngData, to: pasteboard))

    #expect(pasteboard.string(forType: .string) == nil)
    #expect(pasteboard.data(forType: .png) == pngData)
  }
}
