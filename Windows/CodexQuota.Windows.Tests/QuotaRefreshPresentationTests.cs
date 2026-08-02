using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class QuotaRefreshPresentationTests
{
    private static readonly ShareCardData LastQuota = new(
        68,
        new DateTimeOffset(2026, 8, 2, 12, 34, 0, TimeSpan.FromHours(8)));

    [Fact]
    public void FirstRefreshUsesLoadingPlaceholderAndDisablesSharing()
    {
        var presentation = QuotaRefreshPresentation.Refreshing(null);

        Assert.Equal(TrayIconDisplayState.Loading, presentation.TrayIcon.State);
        Assert.Equal("…", presentation.TrayIcon.Label);
        Assert.False(presentation.CanShare);
        Assert.Null(presentation.VisibleQuotaPercent);
        Assert.Contains("正在读取", presentation.QuotaMenuText);
    }

    [Fact]
    public void AutomaticRefreshKeepsLastNumberAndSharingAvailable()
    {
        var presentation = QuotaRefreshPresentation.Refreshing(LastQuota);

        Assert.Equal(TrayIconDisplayState.Quota, presentation.TrayIcon.State);
        Assert.Equal("68", presentation.TrayIcon.Label);
        Assert.True(presentation.CanShare);
        Assert.Equal(68, presentation.VisibleQuotaPercent);
        Assert.Contains("上次 68%", presentation.DetailMenuText);
        Assert.Contains("上次额度 68%", presentation.TooltipText);
    }

    [Fact]
    public void FailedUpdateKeepsLastNumberAndClearlyMarksItStale()
    {
        var presentation = QuotaRefreshPresentation.Failed(LastQuota, "network unavailable");

        Assert.Equal(TrayIconDisplayState.Quota, presentation.TrayIcon.State);
        Assert.Equal("68", presentation.TrayIcon.Label);
        Assert.True(presentation.CanShare);
        Assert.Equal(68, presentation.VisibleQuotaPercent);
        Assert.Contains("上次", presentation.QuotaMenuText);
        Assert.Contains("更新失败", presentation.DetailMenuText);
        Assert.Contains("上次", presentation.TooltipText);
        Assert.Contains("更新失败", presentation.TooltipText);
    }

    [Fact]
    public void FailedFirstReadUsesErrorPlaceholderAndDisablesSharing()
    {
        var presentation = QuotaRefreshPresentation.Failed(null, "network unavailable");

        Assert.Equal(TrayIconDisplayState.Error, presentation.TrayIcon.State);
        Assert.Equal("!", presentation.TrayIcon.Label);
        Assert.False(presentation.CanShare);
        Assert.Null(presentation.VisibleQuotaPercent);
        Assert.Contains("无法读取", presentation.QuotaMenuText);
        Assert.Contains("network unavailable", presentation.DetailMenuText);
    }
}
