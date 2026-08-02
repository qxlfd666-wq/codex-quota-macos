using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;

namespace CodexQuota.Windows;

// This deliberately contains no user-identity fields. Constructing a share
// card first narrows a full quota snapshot to these two safe values.
internal readonly record struct ShareCardData(
    int RemainingPercent,
    DateTimeOffset UpdatedAt);

internal static class ShareCardRenderer
{
    internal static readonly Size CardSize = new(1200, 630);
    internal const string Title = "Codex Quota";
    internal const string PrivacyNotice = "不包含姓名、邮箱、套餐或账户标识";
    internal const double MinimumAccentContrast = 4.5;
    internal static readonly Color AccentContrastBackground =
        Color.FromArgb(255, 24, 18, 25);

    internal static Bitmap Render(ShareCardData data, Color accentColor)
    {
        var remainingPercent = Math.Clamp(data.RemainingPercent, 0, 100);
        var displayAccent = EnsureAccentContrast(accentColor);
        var bitmap = new Bitmap(
            CardSize.Width,
            CardSize.Height,
            PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CompositingQuality = CompositingQuality.GammaCorrected;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using (var background = new LinearGradientBrush(
                       new Rectangle(Point.Empty, CardSize),
                       Color.FromArgb(255, 7, 9, 14),
                       Color.FromArgb(255, 24, 18, 25),
                       LinearGradientMode.ForwardDiagonal))
            {
                graphics.FillRectangle(background, new Rectangle(Point.Empty, CardSize));
            }

            DrawAtmosphere(graphics, displayAccent, remainingPercent);

            using var titleFont = new Font("Segoe UI", 34, FontStyle.Regular, GraphicsUnit.Pixel);
            using var percentFont = new Font("Segoe UI", 164, FontStyle.Bold, GraphicsUnit.Pixel);
            using var metadataFont = new Font("Segoe UI", 24, FontStyle.Regular, GraphicsUnit.Pixel);
            using var titleBrush = new SolidBrush(Color.FromArgb(242, 247, 248, 252));
            using var accentBrush = new SolidBrush(displayAccent);
            using var metadataBrush = new SolidBrush(Color.FromArgb(188, 211, 214, 222));
            using var percentFormat = new StringFormat(StringFormat.GenericTypographic);

            graphics.DrawString(Title, titleFont, titleBrush, new PointF(84, 72));
            graphics.DrawString(
                $"{remainingPercent}%",
                percentFont,
                accentBrush,
                new PointF(72, 142),
                percentFormat);

            var track = new RectangleF(84, 397, 830, 20);
            using (var trackBrush = new SolidBrush(Color.FromArgb(52, 255, 255, 255)))
                graphics.FillRoundedRectangle(trackBrush, track, track.Height / 2);
            if (remainingPercent > 0)
            {
                var fill = new RectangleF(
                    track.X,
                    track.Y,
                    track.Width * remainingPercent / 100f,
                    track.Height);
                graphics.FillRoundedRectangle(
                    accentBrush,
                    fill,
                    Math.Min(fill.Width, fill.Height) / 2);
            }

            graphics.DrawString(
                FormatUpdatedAt(data.UpdatedAt),
                metadataFont,
                metadataBrush,
                new PointF(84, 477));
            graphics.DrawString(
                PrivacyNotice,
                metadataFont,
                metadataBrush,
                new PointF(84, 528));

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    internal static string FormatUpdatedAt(DateTimeOffset updatedAt) =>
        $"更新时间 · {updatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}";

    internal static Color EnsureAccentContrast(Color accentColor)
    {
        var requested = Color.FromArgb(255, accentColor);
        if (ContrastRatio(requested, AccentContrastBackground) >= MinimumAccentContrast)
            return requested;

        for (var whiteMix = 1; whiteMix <= byte.MaxValue; whiteMix++)
        {
            var candidate = Color.FromArgb(
                byte.MaxValue,
                MixChannel(requested.R, whiteMix),
                MixChannel(requested.G, whiteMix),
                MixChannel(requested.B, whiteMix));
            if (ContrastRatio(candidate, AccentContrastBackground) >= MinimumAccentContrast)
                return candidate;
        }

        return Color.White;
    }

    internal static double ContrastRatio(Color first, Color second)
    {
        var lighter = Math.Max(RelativeLuminance(first), RelativeLuminance(second));
        var darker = Math.Min(RelativeLuminance(first), RelativeLuminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static int MixChannel(byte channel, int whiteMix) =>
        (channel * (byte.MaxValue - whiteMix) + (byte.MaxValue * whiteMix)) /
        byte.MaxValue;

    private static double RelativeLuminance(Color color) =>
        (0.2126 * LinearChannel(color.R)) +
        (0.7152 * LinearChannel(color.G)) +
        (0.0722 * LinearChannel(color.B));

    private static double LinearChannel(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static void DrawAtmosphere(
        Graphics graphics,
        Color accentColor,
        int remainingPercent)
    {
        using var glowPath = new GraphicsPath();
        glowPath.AddEllipse(760, 20, 520, 520);
        using (var glow = new PathGradientBrush(glowPath)
        {
            CenterColor = Color.FromArgb(88, accentColor),
            SurroundColors = new[] { Color.FromArgb(0, accentColor) }
        })
        {
            graphics.FillPath(glow, glowPath);
        }

        using var ringPen = new Pen(Color.FromArgb(72, accentColor), 2);
        graphics.DrawEllipse(ringPen, 842, 105, 304, 304);
        using var arcPen = new Pen(Color.FromArgb(228, accentColor), 13)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        if (remainingPercent > 0)
        {
            graphics.DrawArc(
                arcPen,
                875,
                138,
                238,
                238,
                -90,
                360 * remainingPercent / 100f);
        }

        using var dividerPen = new Pen(Color.FromArgb(36, 255, 255, 255), 1);
        graphics.DrawLine(dividerPen, 84, 454, 1116, 454);
    }
}
