import CoreGraphics
import Foundation
import Testing

@testable import CodexQuotaApp

@Suite("Codex window locator")
struct CodexWindowLocatorTests {
  @Test("Uses the cached window ID while the Codex window moves")
  func followsCachedWindow() {
    let provider = FakeCodexWindowInfoProvider()
    let processIdentifier: pid_t = 42
    let firstFrame = CGRect(x: 100, y: 80, width: 1_200, height: 800)
    let movedFrame = firstFrame.offsetBy(dx: 240, dy: 160)
    provider.onScreenRows = [
      windowRow(id: 41, processIdentifier: processIdentifier, frame: firstFrame)
    ]
    provider.rowsByID[41] = windowRow(
      id: 41,
      processIdentifier: processIdentifier,
      frame: movedFrame
    )
    let locator = CodexWindowLocator(provider: provider)

    #expect(locator.windowFrame(processIdentifier: processIdentifier, timestamp: 0) == firstFrame)
    #expect(
      locator.windowFrame(processIdentifier: processIdentifier, timestamp: 0.01) == movedFrame
    )
    #expect(provider.onScreenRequestCount == 1)
    #expect(provider.requestedWindowIDs == [41])
  }

  @Test("Re-discovers immediately when the cached window disappears")
  func replacesInvalidCachedWindow() {
    let provider = FakeCodexWindowInfoProvider()
    let processIdentifier: pid_t = 42
    let firstFrame = CGRect(x: 100, y: 80, width: 1_200, height: 800)
    let replacementFrame = CGRect(x: 300, y: 160, width: 1_300, height: 850)
    provider.onScreenRows = [
      windowRow(id: 41, processIdentifier: processIdentifier, frame: firstFrame)
    ]
    let locator = CodexWindowLocator(provider: provider)

    #expect(locator.windowFrame(processIdentifier: processIdentifier, timestamp: 0) == firstFrame)

    provider.onScreenRows = [
      windowRow(id: 99, processIdentifier: processIdentifier, frame: replacementFrame)
    ]
    #expect(
      locator.windowFrame(processIdentifier: processIdentifier, timestamp: 0.01)
        == replacementFrame
    )
    #expect(provider.onScreenRequestCount == 2)
    #expect(provider.requestedWindowIDs == [41])
  }

  @Test("Periodically checks which Codex window is frontmost")
  func switchesToFrontmostWindow() {
    let provider = FakeCodexWindowInfoProvider()
    let processIdentifier: pid_t = 42
    let firstFrame = CGRect(x: 100, y: 80, width: 1_200, height: 800)
    let frontmostFrame = CGRect(x: 400, y: 180, width: 1_300, height: 850)
    let firstRow = windowRow(
      id: 41,
      processIdentifier: processIdentifier,
      frame: firstFrame
    )
    provider.onScreenRows = [firstRow]
    provider.rowsByID[41] = firstRow
    let locator = CodexWindowLocator(provider: provider)

    #expect(locator.windowFrame(processIdentifier: processIdentifier, timestamp: 0) == firstFrame)

    provider.onScreenRows = [
      windowRow(id: 99, processIdentifier: processIdentifier, frame: frontmostFrame),
      firstRow,
    ]
    #expect(
      locator.windowFrame(
        processIdentifier: processIdentifier,
        timestamp: CodexWindowLocator.rediscoveryInterval
      ) == frontmostFrame
    )
    #expect(provider.onScreenRequestCount == 2)
  }

  @Test("Clears the cached window when Codex restarts")
  func resetsForNewProcess() {
    let provider = FakeCodexWindowInfoProvider()
    let firstFrame = CGRect(x: 100, y: 80, width: 1_200, height: 800)
    let restartedFrame = CGRect(x: 200, y: 120, width: 1_250, height: 820)
    provider.onScreenRows = [windowRow(id: 41, processIdentifier: 42, frame: firstFrame)]
    let locator = CodexWindowLocator(provider: provider)

    #expect(locator.windowFrame(processIdentifier: 42, timestamp: 0) == firstFrame)

    provider.onScreenRows = [windowRow(id: 77, processIdentifier: 84, frame: restartedFrame)]
    #expect(locator.windowFrame(processIdentifier: 84, timestamp: 0.01) == restartedFrame)
    #expect(provider.onScreenRequestCount == 2)
    #expect(provider.requestedWindowIDs.isEmpty)
  }

  private func windowRow(
    id: CGWindowID,
    processIdentifier: pid_t,
    frame: CGRect,
    isOnScreen: Bool = true,
    layer: Int = 0
  ) -> [String: Any] {
    [
      kCGWindowNumber as String: NSNumber(value: id),
      kCGWindowOwnerPID as String: NSNumber(value: processIdentifier),
      kCGWindowLayer as String: NSNumber(value: layer),
      kCGWindowIsOnscreen as String: isOnScreen,
      kCGWindowBounds as String: [
        "X": NSNumber(value: frame.minX),
        "Y": NSNumber(value: frame.minY),
        "Width": NSNumber(value: frame.width),
        "Height": NSNumber(value: frame.height),
      ],
    ]
  }
}

private final class FakeCodexWindowInfoProvider: CodexWindowInfoProviding {
  var onScreenRows: [[String: Any]] = []
  var rowsByID: [CGWindowID: [String: Any]] = [:]
  private(set) var onScreenRequestCount = 0
  private(set) var requestedWindowIDs: [CGWindowID] = []

  func onScreenWindowRows() -> [[String: Any]] {
    onScreenRequestCount += 1
    return onScreenRows
  }

  func windowRow(for id: CGWindowID) -> [String: Any]? {
    requestedWindowIDs.append(id)
    return rowsByID[id]
  }
}
