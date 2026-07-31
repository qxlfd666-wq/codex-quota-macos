import AppKit
import Foundation
import Testing

@testable import CodexQuotaApp

@Suite("Badge appearance")
struct BadgeAppearanceStoreTests {
  @MainActor
  @Test("Persists a picked color")
  func persistsPickedColor() throws {
    let suiteName = "BadgeAppearanceStoreTests.\(UUID().uuidString)"
    let defaults = try #require(UserDefaults(suiteName: suiteName))
    defer { defaults.removePersistentDomain(forName: suiteName) }

    let storageKey = "testBadgeColor"
    let store = BadgeAppearanceStore(defaults: defaults, storageKey: storageKey)
    store.updateColor(
      NSColor(srgbRed: 0.24, green: 0.56, blue: 0.82, alpha: 0.3)
    )

    let restoredStore = BadgeAppearanceStore(
      defaults: defaults,
      storageKey: storageKey
    )
    let restoredColor = try #require(restoredStore.color.usingColorSpace(.sRGB))

    #expect(abs(restoredColor.redComponent - 0.24) < 0.005)
    #expect(abs(restoredColor.greenComponent - 0.56) < 0.005)
    #expect(abs(restoredColor.blueComponent - 0.82) < 0.005)
    #expect(restoredColor.alphaComponent == 1)
  }
}
