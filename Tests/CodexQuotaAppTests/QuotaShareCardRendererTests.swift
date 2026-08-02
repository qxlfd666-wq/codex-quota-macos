import AppKit
import Foundation
import Testing

@testable import CodexQuotaApp

@Suite("Quota share card")
struct QuotaShareCardRendererTests {
  @Test("Only privacy-approved text reaches the renderer")
  func excludesAccountIdentity() throws {
    let privateName = "PRIVATE_DISPLAY_NAME_7F9C"
    let privateEmail = "private-7f9c@example.invalid"
    let privatePlan = "PRIVATE_PLAN_7F9C"
    let snapshot = QuotaSnapshot(
      displayName: privateName,
      email: privateEmail,
      planName: privatePlan,
      remainingPercent: 68,
      usedPercent: 32,
      primary: nil,
      secondary: nil,
      fetchedAt: Date(timeIntervalSince1970: 1_700_000_000)
    )
    let content = QuotaShareCardContent(
      remainingPercent: snapshot.remainingPercent,
      fetchedAt: snapshot.fetchedAt
    )
    let timeZone = try #require(TimeZone(secondsFromGMT: 8 * 60 * 60))
    let text = QuotaShareCardText.make(
      content: content,
      locale: Locale(identifier: "zh_CN"),
      timeZone: timeZone
    )

    #expect(
      text.allStrings == [
        "Codex Quota",
        "68%",
        "更新于 2023年11月15日 06:13",
        "不包含姓名、邮箱、套餐或账户标识",
      ]
    )
    for renderedString in text.allStrings {
      #expect(!renderedString.contains(privateName))
      #expect(!renderedString.contains(privateEmail))
      #expect(!renderedString.contains(privatePlan))
    }
    #expect(
      Set(Mirror(reflecting: content).children.compactMap(\.label))
        == ["remainingPercent", "fetchedAt"]
    )
  }

  @MainActor
  @Test("Renders a decodable 1200 by 630 PNG using the accent color")
  func rendersPNG() throws {
    let content = QuotaShareCardContent(
      remainingPercent: 68,
      fetchedAt: Date(timeIntervalSince1970: 1_700_000_000)
    )
    let redPNG = try #require(
      QuotaShareCardRenderer.pngData(content: content, accentColor: .systemRed)
    )
    let bluePNG = try #require(
      QuotaShareCardRenderer.pngData(content: content, accentColor: .systemBlue)
    )
    let image = try #require(NSBitmapImageRep(data: redPNG))

    #expect(redPNG.starts(with: [137, 80, 78, 71, 13, 10, 26, 10]))
    #expect(image.pixelsWide == 1_200)
    #expect(image.pixelsHigh == 630)
    #expect(redPNG != bluePNG)

    let forbiddenMetadataChunks: Set<String> = ["tEXt", "zTXt", "iTXt", "eXIf"]
    #expect(pngChunkTypes(in: redPNG).isDisjoint(with: forbiddenMetadataChunks))
  }

  @MainActor
  @Test("Raises dark accent colors to a readable contrast")
  func readableDarkAccent() throws {
    let accent = QuotaShareCardRenderer.readableAccentColor(.black)
    let contrast = QuotaShareCardRenderer.contrastRatio(
      accent,
      against: QuotaShareCardRenderer.cardBackgroundColor
    )
    let srgbAccent = try #require(accent.usingColorSpace(.sRGB))

    #expect(contrast >= QuotaShareCardRenderer.minimumAccentContrastRatio)
    #expect(srgbAccent.redComponent > 0)
    #expect(srgbAccent.greenComponent > 0)
    #expect(srgbAccent.blueComponent > 0)
  }

  private func pngChunkTypes(in data: Data) -> Set<String> {
    guard data.count >= 8 else { return [] }
    var offset = 8
    var result = Set<String>()

    while offset + 12 <= data.count {
      let lengthBytes = data[offset..<(offset + 4)]
      let length = lengthBytes.reduce(0) { ($0 << 8) | Int($1) }
      guard offset + 12 + length <= data.count else { break }

      let typeData = data[(offset + 4)..<(offset + 8)]
      if let type = String(data: typeData, encoding: .ascii) {
        result.insert(type)
      }
      offset += 12 + length
    }
    return result
  }
}
