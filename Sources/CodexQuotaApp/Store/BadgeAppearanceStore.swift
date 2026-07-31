import AppKit
import Combine
import Foundation

@MainActor
final class BadgeAppearanceStore: ObservableObject {
  static let defaultColor = NSColor.systemRed

  @Published private(set) var color: NSColor

  private let defaults: UserDefaults
  private let storageKey: String

  init(
    defaults: UserDefaults = .standard,
    storageKey: String = "badgeAccentColor"
  ) {
    self.defaults = defaults
    self.storageKey = storageKey
    color =
      defaults.string(forKey: storageKey).flatMap(NSColor.init(rgbHex:))
      ?? Self.defaultColor
  }

  func updateColor(_ newColor: NSColor) {
    guard let rgbColor = newColor.usingColorSpace(.sRGB) else { return }

    let opaqueColor = NSColor(
      srgbRed: rgbColor.redComponent,
      green: rgbColor.greenComponent,
      blue: rgbColor.blueComponent,
      alpha: 1
    )

    color = opaqueColor
    defaults.set(opaqueColor.rgbHex, forKey: storageKey)
  }
}

extension NSColor {
  fileprivate convenience init?(rgbHex: String) {
    let hex = rgbHex.trimmingCharacters(in: .whitespacesAndNewlines)
      .replacingOccurrences(of: "#", with: "")
    guard hex.count == 6, let value = UInt64(hex, radix: 16) else { return nil }

    self.init(
      srgbRed: CGFloat((value >> 16) & 0xFF) / 255,
      green: CGFloat((value >> 8) & 0xFF) / 255,
      blue: CGFloat(value & 0xFF) / 255,
      alpha: 1
    )
  }

  fileprivate var rgbHex: String? {
    guard let rgbColor = usingColorSpace(.sRGB) else { return nil }

    let red = Int((rgbColor.redComponent * 255).rounded())
    let green = Int((rgbColor.greenComponent * 255).rounded())
    let blue = Int((rgbColor.blueComponent * 255).rounded())
    return String(format: "#%02X%02X%02X", red, green, blue)
  }
}
