using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class ShareCardRendererTests
{
    [Fact]
    public void RendersClipboardSizedCardWithCurrentAccentProgress()
    {
        var accent = Color.FromArgb(37, 211, 102);
        var data = new ShareCardData(
            68,
            new DateTimeOffset(2026, 8, 2, 12, 34, 0, TimeSpan.Zero));

        using var bitmap = ShareCardRenderer.Render(data, accent);

        Assert.Equal(new Size(1200, 630), bitmap.Size);
        Assert.Equal(PixelFormat.Format32bppPArgb, bitmap.PixelFormat);
        Assert.Equal(byte.MaxValue, bitmap.GetPixel(0, 0).A);

        var filledProgressPixel = bitmap.GetPixel(120, 407);
        Assert.InRange(Math.Abs(filledProgressPixel.R - accent.R), 0, 2);
        Assert.InRange(Math.Abs(filledProgressPixel.G - accent.G), 0, 2);
        Assert.InRange(Math.Abs(filledProgressPixel.B - accent.B), 0, 2);

        var emptyProgressPixel = bitmap.GetPixel(800, 407);
        Assert.NotEqual(
            (accent.R, accent.G, accent.B),
            (emptyProgressPixel.R, emptyProgressPixel.G, emptyProgressPixel.B));
    }

    [Fact]
    public void ShareDataTypeCannotCarryIdentityText()
    {
        var properties = typeof(ShareCardData).GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.Equal(
            new[] { "RemainingPercent", "UpdatedAt" },
            properties.Select(property => property.Name).Order().ToArray());
        Assert.DoesNotContain(properties, property => property.PropertyType == typeof(string));
        Assert.DoesNotContain(properties, property =>
            property.Name.Contains("name", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("email", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("account", StringComparison.OrdinalIgnoreCase));

        var render = typeof(ShareCardRenderer).GetMethod(
            "Render",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(render);
        Assert.Equal(
            new[] { typeof(ShareCardData), typeof(Color) },
            render.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    [Fact]
    public void CardTextIsLimitedToQuotaTimeAndPrivacyNotice()
    {
        var updatedAt = new DateTimeOffset(2026, 8, 2, 12, 34, 0, TimeSpan.Zero);

        Assert.Equal("Codex Quota", ShareCardRenderer.Title);
        Assert.StartsWith("更新时间 · ", ShareCardRenderer.FormatUpdatedAt(updatedAt));
        Assert.Equal(
            "不包含姓名、邮箱、套餐或账户标识",
            ShareCardRenderer.PrivacyNotice);
    }

    [Fact]
    public void BlackAccentIsLightenedToReadableContrastOnDarkCard()
    {
        var corrected = ShareCardRenderer.EnsureAccentContrast(Color.Black);

        Assert.NotEqual(Color.Black.ToArgb(), corrected.ToArgb());
        Assert.True(
            ShareCardRenderer.ContrastRatio(
                corrected,
                ShareCardRenderer.AccentContrastBackground) >=
            ShareCardRenderer.MinimumAccentContrast);

        using var bitmap = ShareCardRenderer.Render(
            new ShareCardData(68, DateTimeOffset.UtcNow),
            Color.Black);
        var progressPixel = bitmap.GetPixel(120, 407);
        Assert.Equal(
            (corrected.R, corrected.G, corrected.B),
            (progressPixel.R, progressPixel.G, progressPixel.B));
    }

    [Fact]
    public void AlreadyReadableAccentIsNotChanged()
    {
        var accent = Color.FromArgb(37, 211, 102);

        Assert.Equal(
            accent.ToArgb(),
            ShareCardRenderer.EnsureAccentContrast(accent).ToArgb());
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(130, 100)]
    public void OutOfRangePercentIsClampedForRendering(int requested, int expectedFillPercent)
    {
        var accent = Color.FromArgb(255, 59, 48);
        using var bitmap = ShareCardRenderer.Render(
            new ShareCardData(requested, DateTimeOffset.UtcNow),
            accent);
        var sample = bitmap.GetPixel(expectedFillPercent == 0 ? 90 : 900, 407);

        if (expectedFillPercent == 0)
        {
            Assert.NotEqual((accent.R, accent.G, accent.B), (sample.R, sample.G, sample.B));
        }
        else
        {
            Assert.InRange(Math.Abs(sample.R - accent.R), 0, 2);
            Assert.InRange(Math.Abs(sample.G - accent.G), 0, 2);
            Assert.InRange(Math.Abs(sample.B - accent.B), 0, 2);
        }
    }

    [Fact]
    public void VeryLowQuotaStillRendersAValidRoundedProgressFill()
    {
        using var bitmap = ShareCardRenderer.Render(
            new ShareCardData(1, DateTimeOffset.UtcNow),
            Color.FromArgb(255, 59, 48));

        Assert.Equal(ShareCardRenderer.CardSize, bitmap.Size);
        Assert.Equal(byte.MaxValue, bitmap.GetPixel(88, 407).A);
    }
}
