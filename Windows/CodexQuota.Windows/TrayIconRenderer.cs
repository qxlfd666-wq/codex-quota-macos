using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace CodexQuota.Windows;

internal enum TrayIconDisplayState
{
    Loading,
    Quota,
    Error
}

internal readonly record struct TrayIconContent(
    TrayIconDisplayState State,
    int? RemainingPercent)
{
    internal static TrayIconContent Loading =>
        new(TrayIconDisplayState.Loading, null);

    internal static TrayIconContent Error =>
        new(TrayIconDisplayState.Error, null);

    internal static TrayIconContent Quota(int remainingPercent) =>
        new(TrayIconDisplayState.Quota, Math.Clamp(remainingPercent, 0, 100));

    internal string Label => State switch
    {
        TrayIconDisplayState.Quota when RemainingPercent is { } percent =>
            Math.Clamp(percent, 0, 100).ToString(System.Globalization.CultureInfo.InvariantCulture),
        TrayIconDisplayState.Error => "!",
        _ => "…"
    };
}

internal static class TrayIconRenderer
{
    private const int SupersamplingFactor = 4;
    private const float DesignSize = 16f;

    internal static Icon CreateForSystemTray(
        TrayIconContent content,
        Color accentColor)
    {
        var size = PreferredSize(SystemInformation.SmallIconSize);
        using var bitmap = RenderFrame(
            size,
            content,
            accentColor,
            SystemInformation.HighContrast);
        var handle = bitmap.GetHicon();
        try
        {
            using var source = Icon.FromHandle(handle);
            return (Icon)source.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    internal static int PreferredSize(Size systemSmallIconSize) =>
        Math.Clamp(Math.Max(systemSmallIconSize.Width, systemSmallIconSize.Height), 16, 64);

    internal static Bitmap RenderFrame(
        int size,
        TrayIconContent content,
        Color accentColor,
        bool highContrast = false)
    {
        if (size is < 16 or > 64)
            throw new ArgumentOutOfRangeException(nameof(size));

        var supersampledSize = checked(size * SupersamplingFactor);
        using var supersampled = new Bitmap(
            supersampledSize,
            supersampledSize,
            PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(supersampled))
        {
            graphics.Clear(Color.FromArgb(0, 0, 0, 0));
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.GammaCorrected;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var unit = supersampledSize / DesignSize;
            var backgroundColor = highContrast
                ? SystemColors.Highlight
                : Color.FromArgb(255, accentColor);
            var foregroundColor = highContrast
                ? SystemColors.HighlightText
                : ContrastingTextColor(backgroundColor);
            var tile = new RectangleF(0.75f * unit, 0.75f * unit, 14.5f * unit, 14.5f * unit);
            using var tilePath = RoundedRectangle(tile, 3.9f * unit);
            using var tileBrush = new SolidBrush(backgroundColor);
            graphics.FillPath(tileBrush, tilePath);

            var label = content.Label;
            var logicalFontSize = label.Length switch
            {
                >= 3 => 7.1f,
                2 => 8.6f,
                _ => 10.5f
            };
            using var font = new Font(
                "Segoe UI",
                logicalFontSize * unit,
                FontStyle.Bold,
                GraphicsUnit.Pixel);
            using var labelBrush = new SolidBrush(foregroundColor);
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            graphics.DrawString(
                label,
                font,
                labelBrush,
                new RectangleF(0, -0.35f * unit, supersampledSize, supersampledSize),
                format);
        }

        var result = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        try
        {
            using var resultGraphics = Graphics.FromImage(result);
            resultGraphics.Clear(Color.FromArgb(0, 0, 0, 0));
            resultGraphics.CompositingMode = CompositingMode.SourceCopy;
            resultGraphics.CompositingQuality = CompositingQuality.GammaCorrected;
            resultGraphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            resultGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            resultGraphics.DrawImage(
                supersampled,
                new Rectangle(0, 0, size, size),
                new Rectangle(0, 0, supersampledSize, supersampledSize),
                GraphicsUnit.Pixel);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    internal static Color ContrastingTextColor(Color background)
    {
        var darkText = Color.FromArgb(255, 12, 14, 20);
        var lightText = Color.White;
        return ShareCardRenderer.ContrastRatio(background, darkText) >=
            ShareCardRenderer.ContrastRatio(background, lightText)
            ? darkText
            : lightText;
    }

    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
