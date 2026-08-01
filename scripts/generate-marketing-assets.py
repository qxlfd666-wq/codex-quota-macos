#!/usr/bin/env python3
"""Build the repository's deterministic social card and animated UI demo.

Run from the repository root with Python 3 and Pillow installed.
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[1]
IMAGES = ROOT / "docs" / "images"
MARKETING = IMAGES / "marketing"

BACKGROUND_PATH = MARKETING / "quota-visual-background.png"
BADGE_PATH = IMAGES / "quota-badge.png"
PICKER_PATH = IMAGES / "color-picker.png"
SOCIAL_PATH = IMAGES / "social-preview.png"
GALLERY_PATH = MARKETING / "product-hunt-gallery.png"
DEMO_PATH = IMAGES / "demo.gif"

FONT_CANDIDATES = (
    Path("/System/Library/Fonts/SFNS.ttf"),
    Path("/System/Library/Fonts/HelveticaNeue.ttc"),
    Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
)
MONO_FONT_CANDIDATES = (
    Path("/System/Library/Fonts/SFNSMono.ttf"),
    Path("/System/Library/Fonts/Menlo.ttc"),
    Path("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"),
)
CJK_FONT_CANDIDATES = (
    Path("/System/Library/Fonts/PingFang.ttc"),
    Path("/System/Library/Fonts/STHeiti Light.ttc"),
    Path("/System/Library/Fonts/Supplemental/Arial Unicode.ttf"),
    Path("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"),
)

WHITE = (247, 248, 250, 255)
MUTED = (178, 183, 193, 255)
RED = (255, 77, 90, 255)
RED_SOFT = (255, 121, 130, 255)
PANEL = (21, 23, 28, 235)


def font(size: int, *, mono: bool = False, cjk: bool = False) -> ImageFont.FreeTypeFont:
    candidates = CJK_FONT_CANDIDATES if cjk else (MONO_FONT_CANDIDATES if mono else FONT_CANDIDATES)
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default(size=size)


def cover(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    target_w, target_h = size
    scale = max(target_w / image.width, target_h / image.height)
    resized = image.resize(
        (round(image.width * scale), round(image.height * scale)),
        Image.Resampling.LANCZOS,
    )
    left = (resized.width - target_w) // 2
    top = (resized.height - target_h) // 2
    return resized.crop((left, top, left + target_w, top + target_h))


def contain(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    copy = image.copy()
    copy.thumbnail(size, Image.Resampling.LANCZOS)
    return copy


def rounded_image(image: Image.Image, radius: int) -> Image.Image:
    image = image.convert("RGBA")
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        (0, 0, image.width - 1, image.height - 1),
        radius=radius,
        fill=255,
    )
    image.putalpha(mask)
    return image


def paste_card(
    canvas: Image.Image,
    card: Image.Image,
    xy: tuple[int, int],
    *,
    radius: int,
    shadow: int = 24,
    border: bool = True,
) -> None:
    x, y = xy
    card = rounded_image(card, radius)
    shadow_layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    shadow_mask = Image.new("L", card.size, 0)
    ImageDraw.Draw(shadow_mask).rounded_rectangle(
        (0, 0, card.width - 1, card.height - 1),
        radius=radius,
        fill=205,
    )
    shadow_mask = shadow_mask.filter(ImageFilter.GaussianBlur(shadow))
    shadow_layer.paste((0, 0, 0, 190), (x, y + 10), shadow_mask)
    canvas.alpha_composite(shadow_layer)
    canvas.alpha_composite(card, (x, y))
    if border:
        draw = ImageDraw.Draw(canvas)
        draw.rounded_rectangle(
            (x, y, x + card.width - 1, y + card.height - 1),
            radius=radius,
            outline=(255, 255, 255, 38),
            width=2,
        )


def draw_pill(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int],
    text: str,
    *,
    fill: tuple[int, int, int, int],
    foreground: tuple[int, int, int, int],
    text_font: ImageFont.FreeTypeFont,
    pad_x: int = 16,
    pad_y: int = 9,
) -> tuple[int, int, int, int]:
    x, y = xy
    box = draw.textbbox((0, 0), text, font=text_font)
    width = box[2] - box[0] + pad_x * 2
    height = box[3] - box[1] + pad_y * 2
    rect = (x, y, x + width, y + height)
    draw.rounded_rectangle(rect, radius=height // 2, fill=fill)
    draw.text((x + pad_x, y + pad_y - box[1]), text, font=text_font, fill=foreground)
    return rect


def build_social_preview(background: Image.Image, badge: Image.Image) -> None:
    canvas = cover(background, (1280, 640)).convert("RGBA")

    # Reinforce readable negative space without hiding the generated texture.
    shade = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    pixels = shade.load()
    for x in range(canvas.width):
        alpha = round(150 * max(0.0, 1.0 - x / 860))
        for y in range(canvas.height):
            pixels[x, y] = (3, 4, 7, alpha)
    canvas.alpha_composite(shade)

    draw = ImageDraw.Draw(canvas)
    eyebrow_font = font(23, cjk=True)
    title_font = font(78)
    tagline_font = font(34, cjk=True)
    micro_font = font(21, cjk=True)
    percent_font = font(27, mono=True)

    draw.text((76, 80), "开源项目  /  macOS + WINDOWS", font=eyebrow_font, fill=RED_SOFT)
    draw.text((72, 135), "Codex Quota", font=title_font, fill=WHITE, stroke_width=1)
    draw.text((76, 244), "随时知道剩多少，", font=tagline_font, fill=MUTED)
    draw.text((76, 287), "专注不中断。", font=tagline_font, fill=WHITE)

    pill = draw_pill(
        draw,
        (76, 366),
        "21%",
        fill=RED,
        foreground=WHITE,
        text_font=percent_font,
        pad_x=18,
        pad_y=9,
    )
    bar_x = pill[2] + 18
    bar_y = pill[1] + 14
    draw.rounded_rectangle((bar_x, bar_y, bar_x + 180, bar_y + 8), radius=4, fill=(255, 255, 255, 24))
    draw.rounded_rectangle((bar_x, bar_y, bar_x + 38, bar_y + 8), radius=4, fill=RED)
    draw.text((76, 505), "本机读取  •  轻量常驻  •  不修改 CODEX", font=micro_font, fill=MUTED)

    badge_card = contain(badge, (520, 268))
    badge_card = ImageEnhance.Contrast(badge_card).enhance(1.02)
    paste_card(canvas, badge_card, (706, 329), radius=24, shadow=20)

    canvas.convert("RGB").save(SOCIAL_PATH, quality=95)


def build_gallery(background: Image.Image, badge: Image.Image, picker: Image.Image) -> None:
    canvas = cover(background, (1270, 760)).convert("RGBA")
    overlay = Image.new("RGBA", canvas.size, (6, 7, 10, 88))
    canvas.alpha_composite(overlay)
    draw = ImageDraw.Draw(canvas)

    draw.text((72, 60), "Codex Quota", font=font(68), fill=WHITE)
    draw.text((76, 145), "剩余额度，就在你习惯看的位置。", font=font(31, cjk=True), fill=MUTED)
    draw_pill(
        draw,
        (76, 205),
        "开源 · macOS + Windows 测试版",
        fill=RED,
        foreground=WHITE,
        text_font=font(20, cjk=True),
        pad_x=18,
        pad_y=10,
    )

    badge_card = contain(badge, (700, 360))
    paste_card(canvas, badge_card, (70, 342), radius=26, shadow=22)
    picker_card = contain(picker, (262, 416))
    paste_card(canvas, picker_card, (902, 279), radius=20, shadow=18)

    draw.rounded_rectangle((782, 418, 918, 474), radius=28, fill=(255, 77, 90, 235))
    draw.text((810, 430), "点击", font=font(22, cjk=True), fill=(14, 15, 18, 255))
    draw.line((900, 450, 930, 434), fill=(255, 77, 90, 235), width=5)
    canvas.convert("RGB").save(GALLERY_PATH, quality=95)


def tint_badge(source: Image.Image, target: tuple[int, int, int], strength: float) -> Image.Image:
    """Recolor only the red quota badge area while preserving its shading."""
    image = source.convert("RGBA").copy()
    px = image.load()
    # This box deliberately excludes the avatar and the rest of the Codex screenshot.
    x0, y0, x1, y1 = 142, 216, 239, 258
    for y in range(max(0, y0), min(image.height, y1)):
        for x in range(max(0, x0), min(image.width, x1)):
            r, g, b, a = px[x, y]
            redness = max(0, r - max(g, b)) / 255
            if redness < 0.08:
                continue
            luminance = (r + g + b) / (3 * 255)
            blend = min(1.0, redness * 2.8) * strength
            tinted = tuple(round(channel * (0.55 + luminance * 0.75)) for channel in target)
            px[x, y] = (
                round(r * (1 - blend) + tinted[0] * blend),
                round(g * (1 - blend) + tinted[1] * blend),
                round(b * (1 - blend) + tinted[2] * blend),
                a,
            )
    return image


def ease(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3 - 2 * value)


def mix(a: float, b: float, value: float) -> float:
    return a + (b - a) * value


def build_demo(background: Image.Image, badge: Image.Image, picker: Image.Image) -> None:
    width, height = 800, 450
    fps = 8
    seconds = 11
    frames: list[Image.Image] = []
    base = cover(background, (width, height)).convert("RGBA")
    base = ImageEnhance.Brightness(base).enhance(0.58)
    badge_size = (528, 272)
    picker_size = (164, 260)
    badge_x, badge_y = 44, 116
    picker_target_x, picker_y = 604, 92

    title_font = font(31)
    small_font = font(18, cjk=True)

    for frame_index in range(fps * seconds):
        t = frame_index / fps
        frame = base.copy()
        draw = ImageDraw.Draw(frame)
        draw.text((44, 38), "Codex Quota", font=title_font, fill=WHITE)

        if t < 3.1:
            caption = "剩余额度一眼可见"
        elif t < 7.2:
            caption = "点击选择任意颜色"
        else:
            caption = "颜色偏好保存在本机"
        draw.text((44, 80), caption, font=small_font, fill=RED_SOFT)

        hue_progress = ease((t - 5.0) / 1.5) * ease((9.7 - t) / 1.0)
        target_color = (179, 104, 255)
        tinted_badge = tint_badge(badge, target_color, hue_progress)
        badge_card = contain(tinted_badge, badge_size)
        paste_card(frame, badge_card, (badge_x, badge_y), radius=18, shadow=14)

        # The native color picker slides in after the badge click, then exits.
        if 2.8 <= t <= 7.8:
            enter = ease((t - 2.8) / 0.65)
            leave = ease((7.8 - t) / 0.65)
            visibility = min(enter, leave)
            picker_x = round(mix(width + 24, picker_target_x, visibility))
            picker_card = contain(picker, picker_size)
            picker_card.putalpha(round(255 * visibility))
            paste_card(frame, picker_card, (picker_x, picker_y), radius=14, shadow=12)

        # Cursor motion makes the interaction legible without faking app content.
        if t < 3.0:
            cursor_progress = ease(t / 2.5)
            cursor_x = mix(340, 196, cursor_progress)
            cursor_y = mix(370, 305, cursor_progress)
        elif t < 5.8:
            cursor_progress = ease((t - 3.0) / 2.2)
            cursor_x = mix(196, 686, cursor_progress)
            cursor_y = mix(305, 204, cursor_progress)
        else:
            cursor_x, cursor_y = 724, 198

        pulse = max(0.0, 1.0 - abs(t - 2.75) / 0.32)
        pulse += max(0.0, 1.0 - abs(t - 5.4) / 0.32)
        if pulse:
            radius = round(12 + 18 * pulse)
            draw.ellipse(
                (cursor_x - radius, cursor_y - radius, cursor_x + radius, cursor_y + radius),
                outline=(255, 77, 90, round(210 * pulse)),
                width=3,
            )
        draw.ellipse((cursor_x - 5, cursor_y - 5, cursor_x + 5, cursor_y + 5), fill=WHITE)
        draw.ellipse((cursor_x - 3, cursor_y - 3, cursor_x + 3, cursor_y + 3), fill=(18, 19, 23, 255))

        # Fade the final half-second toward the first frame for a softer loop.
        if t > 9.45:
            fade = ease((t - 9.45) / 0.55)
            first_like = base.copy()
            first_draw = ImageDraw.Draw(first_like)
            first_draw.text((44, 38), "Codex Quota", font=title_font, fill=WHITE)
            first_draw.text((44, 80), "剩余额度一眼可见", font=small_font, fill=RED_SOFT)
            first_badge = contain(badge, badge_size)
            paste_card(first_like, first_badge, (badge_x, badge_y), radius=18, shadow=14)
            frame = Image.blend(frame, first_like, fade)

        frames.append(frame.convert("P", palette=Image.Palette.ADAPTIVE, colors=128))

    frames[0].save(
        DEMO_PATH,
        save_all=True,
        append_images=frames[1:],
        duration=round(1000 / fps),
        loop=0,
        optimize=True,
        disposal=2,
    )


def main() -> None:
    for required in (BACKGROUND_PATH, BADGE_PATH, PICKER_PATH):
        if not required.exists():
            raise SystemExit(f"Missing source image: {required}")

    MARKETING.mkdir(parents=True, exist_ok=True)
    background = Image.open(BACKGROUND_PATH).convert("RGBA")
    badge = Image.open(BADGE_PATH).convert("RGBA")
    picker = Image.open(PICKER_PATH).convert("RGBA")

    build_social_preview(background, badge)
    build_gallery(background, badge, picker)
    build_demo(background, badge, picker)

    for output in (SOCIAL_PATH, GALLERY_PATH, DEMO_PATH):
        print(f"Built {output.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
