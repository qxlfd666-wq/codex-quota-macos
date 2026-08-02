using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class TrayIconRendererTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public void RendersCrispTransparentFrameAtRequestedTraySize(int size)
    {
        using var bitmap = TrayIconRenderer.RenderFrame(size);

        Assert.Equal(new Size(size, size), bitmap.Size);
        Assert.Equal(PixelFormat.Format32bppPArgb, bitmap.PixelFormat);
        var corner = bitmap.GetPixel(0, 0);
        Assert.Equal((0, 0, 0, 0), (corner.A, corner.R, corner.G, corner.B));

        var pixels = Enumerable.Range(0, size)
            .SelectMany(y => Enumerable.Range(0, size).Select(x => bitmap.GetPixel(x, y)))
            .ToArray();
        Assert.Contains(pixels, pixel => pixel.A > 200 && pixel.R > 170 && pixel.G < 100);
        Assert.Contains(pixels, pixel =>
            pixel.A > 200 && pixel.R > 235 && pixel.G > 235 && pixel.B > 235);
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
        using var icon = TrayIconRenderer.CreateForSystemTray();
        using var bitmap = icon.ToBitmap();

        Assert.InRange(icon.Width, 16, 64);
        Assert.InRange(icon.Height, 16, 64);
        Assert.Equal(0, bitmap.GetPixel(0, 0).A);
    }
}
