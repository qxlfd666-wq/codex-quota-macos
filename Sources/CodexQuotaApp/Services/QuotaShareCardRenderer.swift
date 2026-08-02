import AppKit
import Foundation

/// A privacy allow-list for share-card data. The renderer deliberately cannot
/// receive a display name, email address, plan name, or any account identifier.
struct QuotaShareCardContent: Equatable, Sendable {
  let remainingPercent: Int
  let fetchedAt: Date

  init(remainingPercent: Int, fetchedAt: Date) {
    self.remainingPercent = min(max(remainingPercent, 0), 100)
    self.fetchedAt = fetchedAt
  }
}

struct QuotaShareCardText: Equatable, Sendable {
  let title: String
  let percentage: String
  let updatedAt: String
  let privacyNotice: String

  var allStrings: [String] {
    [title, percentage, updatedAt, privacyNotice]
  }

  static func make(
    content: QuotaShareCardContent,
    locale: Locale = Locale(identifier: "zh_CN"),
    timeZone: TimeZone = .current
  ) -> Self {
    let formatter = DateFormatter()
    formatter.locale = locale
    formatter.timeZone = timeZone
    formatter.dateFormat = "yyyy年M月d日 HH:mm"

    return Self(
      title: "Codex Quota",
      percentage: "\(content.remainingPercent)%",
      updatedAt: "更新于 \(formatter.string(from: content.fetchedAt))",
      privacyNotice: "不包含姓名、邮箱、套餐或账户标识"
    )
  }
}

@MainActor
enum QuotaShareCardRenderer {
  static let canvasSize = CGSize(width: 1_200, height: 630)
  static let minimumAccentContrastRatio = 4.5
  static let cardBackgroundColor = NSColor(
    srgbRed: 0.075,
    green: 0.08,
    blue: 0.105,
    alpha: 1
  )

  static func pngData(
    content: QuotaShareCardContent,
    accentColor: NSColor
  ) -> Data? {
    let width = Int(canvasSize.width)
    let height = Int(canvasSize.height)
    guard
      let bitmap = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: width,
        pixelsHigh: height,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0
      ),
      let context = NSGraphicsContext(bitmapImageRep: bitmap)
    else {
      return nil
    }

    let text = QuotaShareCardText.make(content: content)
    let accent = readableAccentColor(accentColor)
    let bounds = CGRect(origin: .zero, size: canvasSize)

    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = context
    context.imageInterpolation = NSImageInterpolation.high

    let background = NSGradient(
      colors: [
        NSColor(srgbRed: 0.025, green: 0.028, blue: 0.036, alpha: 1),
        NSColor(srgbRed: 0.075, green: 0.065, blue: 0.085, alpha: 1),
      ]
    )
    background?.draw(in: bounds, angle: 18)

    accent.withAlphaComponent(0.16).setFill()
    NSBezierPath(ovalIn: CGRect(x: 775, y: 180, width: 560, height: 560)).fill()

    let cardRect = CGRect(x: 64, y: 64, width: 1_072, height: 502)
    cardBackgroundColor.withAlphaComponent(0.92).setFill()
    NSBezierPath(roundedRect: cardRect, xRadius: 34, yRadius: 34).fill()

    draw(
      text.title,
      in: CGRect(x: 116, y: 462, width: 500, height: 54),
      font: .systemFont(ofSize: 37, weight: .semibold),
      color: .white
    )
    draw(
      text.percentage,
      in: CGRect(x: 108, y: 244, width: 984, height: 190),
      font: .monospacedDigitSystemFont(ofSize: 150, weight: .bold),
      color: accent
    )

    let trackRect = CGRect(x: 116, y: 204, width: 968, height: 22)
    NSColor.white.withAlphaComponent(0.12).setFill()
    NSBezierPath(roundedRect: trackRect, xRadius: 11, yRadius: 11).fill()

    let progressWidth = trackRect.width * CGFloat(content.remainingPercent) / 100
    if progressWidth > 0 {
      accent.setFill()
      NSBezierPath(
        roundedRect: CGRect(
          x: trackRect.minX,
          y: trackRect.minY,
          width: max(progressWidth, trackRect.height),
          height: trackRect.height
        ),
        xRadius: 11,
        yRadius: 11
      ).fill()
    }

    draw(
      text.updatedAt,
      in: CGRect(x: 116, y: 132, width: 560, height: 42),
      font: .systemFont(ofSize: 25, weight: .medium),
      color: NSColor.white.withAlphaComponent(0.72)
    )
    draw(
      text.privacyNotice,
      in: CGRect(x: 580, y: 132, width: 504, height: 42),
      font: .systemFont(ofSize: 21, weight: .regular),
      color: NSColor.white.withAlphaComponent(0.48),
      alignment: .right
    )

    context.flushGraphics()
    NSGraphicsContext.restoreGraphicsState()
    bitmap.size = canvasSize
    guard
      let pngData = bitmap.representation(
        using: NSBitmapImageRep.FileType.png,
        properties: [:]
      )
    else {
      return nil
    }
    return removingMetadataChunks(from: pngData)
  }

  private static func opaqueSRGB(_ color: NSColor) -> NSColor {
    guard let converted = color.usingColorSpace(.sRGB) else {
      return .systemRed
    }
    return NSColor(
      srgbRed: converted.redComponent,
      green: converted.greenComponent,
      blue: converted.blueComponent,
      alpha: 1
    )
  }

  static func readableAccentColor(_ color: NSColor) -> NSColor {
    let accent = opaqueSRGB(color)
    guard
      contrastRatio(accent, against: cardBackgroundColor)
        < minimumAccentContrastRatio
    else {
      return accent
    }

    var lowerBound: CGFloat = 0
    var upperBound: CGFloat = 1
    var result = NSColor.white
    for _ in 0..<16 {
      let whiteAmount = (lowerBound + upperBound) / 2
      let candidate = mix(accent, with: .white, amount: whiteAmount)
      if contrastRatio(candidate, against: cardBackgroundColor)
        >= minimumAccentContrastRatio
      {
        result = candidate
        upperBound = whiteAmount
      } else {
        lowerBound = whiteAmount
      }
    }
    return result
  }

  static func contrastRatio(_ foreground: NSColor, against background: NSColor) -> Double {
    let lighter = max(relativeLuminance(foreground), relativeLuminance(background))
    let darker = min(relativeLuminance(foreground), relativeLuminance(background))
    return (lighter + 0.05) / (darker + 0.05)
  }

  private static func mix(
    _ color: NSColor,
    with otherColor: NSColor,
    amount: CGFloat
  ) -> NSColor {
    let first = opaqueSRGB(color)
    let second = opaqueSRGB(otherColor)
    return NSColor(
      srgbRed: first.redComponent + (second.redComponent - first.redComponent) * amount,
      green: first.greenComponent + (second.greenComponent - first.greenComponent) * amount,
      blue: first.blueComponent + (second.blueComponent - first.blueComponent) * amount,
      alpha: 1
    )
  }

  private static func relativeLuminance(_ color: NSColor) -> Double {
    let converted = opaqueSRGB(color)
    let components = [
      Double(converted.redComponent),
      Double(converted.greenComponent),
      Double(converted.blueComponent),
    ].map { component in
      component <= 0.04045
        ? component / 12.92
        : pow((component + 0.055) / 1.055, 2.4)
    }
    return 0.2126 * components[0] + 0.7152 * components[1] + 0.0722 * components[2]
  }

  private static func draw(
    _ string: String,
    in rect: CGRect,
    font: NSFont,
    color: NSColor,
    alignment: NSTextAlignment = .left
  ) {
    let paragraph = NSMutableParagraphStyle()
    paragraph.alignment = alignment
    paragraph.lineBreakMode = .byTruncatingTail
    (string as NSString).draw(
      in: rect,
      withAttributes: [
        .font: font,
        .foregroundColor: color,
        .paragraphStyle: paragraph,
      ]
    )
  }

  private static func removingMetadataChunks(from pngData: Data) -> Data {
    let signatureLength = 8
    guard pngData.count >= signatureLength else { return pngData }

    let forbiddenChunkTypes: Set<String> = ["tEXt", "zTXt", "iTXt", "eXIf"]
    var sanitized = Data(pngData.prefix(signatureLength))
    var offset = signatureLength

    while offset + 12 <= pngData.count {
      let lengthBytes = pngData[offset..<(offset + 4)]
      let contentLength = lengthBytes.reduce(0) { ($0 << 8) | Int($1) }
      let chunkEnd = offset + 12 + contentLength
      guard chunkEnd <= pngData.count else { return pngData }

      let typeData = pngData[(offset + 4)..<(offset + 8)]
      let type = String(data: typeData, encoding: .ascii)
      if type.map({ !forbiddenChunkTypes.contains($0) }) ?? false {
        sanitized.append(pngData[offset..<chunkEnd])
      }
      offset = chunkEnd
    }

    return offset == pngData.count ? sanitized : pngData
  }
}
