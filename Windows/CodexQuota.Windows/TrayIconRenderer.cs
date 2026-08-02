using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CodexQuota.Windows;

internal static class TrayIconRenderer
{
    private const int SupersamplingFactor = 4;
    private const float DesignSize = 16f;

    internal static Icon CreateForSystemTray()
    {
        var size = PreferredSize(SystemInformation.SmallIconSize);
        using var bitmap = RenderFrame(size, SystemInformation.HighContrast);
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

    internal static Bitmap RenderFrame(int size, bool highContrast = false)
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

            var unit = supersampledSize / DesignSize;
            var backgroundColor = highContrast
                ? SystemColors.Highlight
                : Color.FromArgb(209, 52, 56);
            var foregroundColor = highContrast
                ? SystemColors.HighlightText
                : Color.White;
            var tile = new RectangleF(unit, unit, 14 * unit, 14 * unit);
            using var tilePath = RoundedRectangle(tile, 3.75f * unit);
            using var tileBrush = new SolidBrush(backgroundColor);
            graphics.FillPath(tileBrush, tilePath);

            using var glyphPen = new Pen(foregroundColor, (size == 16 ? 1.65f : 1.8f) * unit)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawEllipse(
                glyphPen,
                4.1f * unit,
                3.8f * unit,
                7.6f * unit,
                7.6f * unit);
            graphics.DrawLine(
                glyphPen,
                9.5f * unit,
                9.4f * unit,
                11.8f * unit,
                11.7f * unit);
        }

        var result = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
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
