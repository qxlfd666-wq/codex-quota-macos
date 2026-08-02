using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class TrayIconRendererTests
{
    private static readonly Color Accent = Color.FromArgb(31, 111, 235);

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public void RendersCrispTransparentNumericFrameAtRequestedTraySize(int size)
    {
        using var bitmap = TrayIconRenderer.RenderFrame(
            size,
            TrayIconContent.Quota(68),
            Accent);

        Assert.Equal(new Size(size, size), bitmap.Size);
        Assert.Equal(PixelFormat.Format32bppPArgb, bitmap.PixelFormat);
        var corner = bitmap.GetPixel(0, 0);
        Assert.Equal((0, 0, 0, 0), (corner.A, corner.R, corner.G, corner.B));

        var pixels = Enumerable.Range(0, size)
            .SelectMany(y => Enumerable.Range(0, size).Select(x => bitmap.GetPixel(x, y)))
            .ToArray();
        Assert.Contains(pixels, pixel =>
            pixel.A > 230 &&
            Math.Abs(pixel.R - Accent.R) < 8 &&
            Math.Abs(pixel.G - Accent.G) < 8 &&
            Math.Abs(pixel.B - Accent.B) < 8);
        Assert.Contains(pixels, pixel =>
            pixel.A > 230 && pixel.R > 235 && pixel.G > 235 && pixel.B > 235);
    }

    [Theory]
    [InlineData(-1, "0")]
    [InlineData(0, "0")]
    [InlineData(68, "68")]
    [InlineData(100, "100")]
    [InlineData(101, "100")]
    public void QuotaContentUsesClampedNumberWithoutPercentSign(int remainingPercent, string label)
    {
        Assert.Equal(label, TrayIconContent.Quota(remainingPercent).Label);
    }

    [Fact]
    public void LoadingAndErrorUseDistinctClearPlaceholders()
    {
        Assert.Equal("…", TrayIconContent.Loading.Label);
        Assert.Equal("!", TrayIconContent.Error.Label);
        Assert.NotEqual(TrayIconContent.Loading.Label, TrayIconContent.Error.Label);
    }

    [Theory]
    [InlineData(0, 255, 0, true)]
    [InlineData(0, 255, 255, true)]
    [InlineData(255, 59, 48, true)]
    [InlineData(31, 111, 235, false)]
    [InlineData(12, 14, 20, false)]
    public void ChoosesTheMoreReadableTextColor(
        int red,
        int green,
        int blue,
        bool expectsDarkText)
    {
        var background = Color.FromArgb(red, green, blue);
        var foreground = TrayIconRenderer.ContrastingTextColor(background);
        var darkText = Color.FromArgb(255, 12, 14, 20);
        var lightText = Color.White;

        Assert.Equal(expectsDarkText ? darkText.ToArgb() : lightText.ToArgb(), foreground.ToArgb());
        Assert.True(
            ShareCardRenderer.ContrastRatio(foreground, background) >=
            ShareCardRenderer.ContrastRatio(
                expectsDarkText ? lightText : darkText,
                background));
    }

    [Theory]
    [InlineData(16, 16)]
    [InlineData(20, 20)]
    [InlineData(24, 24)]
    [InlineData(32, 32)]
    [InlineData(48, 48)]
    [InlineData(64, 64)]
    [InlineData(96, 64)]
    public void SelectsAUsefulSystemTrayFrameSize(int systemSize, int expected)
    {
        Assert.Equal(expected, TrayIconRenderer.PreferredSize(new Size(systemSize, systemSize)));
    }

    [Fact]
    public void FinalSystemTrayIconPreservesTransparentCorners()
    {
        using var icon = TrayIconRenderer.CreateForSystemTray(
            TrayIconContent.Quota(100),
            Accent);
        using var bitmap = icon.ToBitmap();

        Assert.InRange(icon.Width, 16, 64);
        Assert.InRange(icon.Height, 16, 64);
        Assert.Equal(0, bitmap.GetPixel(0, 0).A);
    }

    [Fact]
    public void RepeatedSystemTrayIconCreationCanBeDisposedSafely()
    {
        for (var iteration = 0; iteration < 64; iteration++)
        {
            using var icon = TrayIconRenderer.CreateForSystemTray(
                TrayIconContent.Quota(iteration),
                Accent);
            Assert.NotEqual(nint.Zero, icon.Handle);
        }
    }
}
