using System.Drawing;
using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class OverlayVisibilityTests
{
    private static readonly TrackedWindow ForegroundCodex =
        new((nint)101, new Rectangle(20, 30, 1200, 800), 1f);

    private static readonly TrackedWindow TrackedCodex =
        new((nint)202, new Rectangle(40, 50, 1200, 800), 1f);

    [Fact]
    public void KeepsTrackedCodexWhenAnotherApplicationGetsFocus()
    {
        var selected = OverlayWindowSelectionRules.Select(
            foregroundCodex: null,
            trackedCodex: TrackedCodex,
            otherVisibleCodex: null);

        Assert.Equal(TrackedCodex, selected);
    }

    [Fact]
    public void PrefersForegroundCodexWhenSwitchingBetweenCodexWindows()
    {
        var selected = OverlayWindowSelectionRules.Select(
            ForegroundCodex,
            TrackedCodex,
            otherVisibleCodex: null);

        Assert.Equal(ForegroundCodex, selected);
    }

    [Fact]
    public void HidesWhenNoUsableCodexWindowRemains()
    {
        var selected = OverlayWindowSelectionRules.Select(
            foregroundCodex: null,
            trackedCodex: null,
            otherVisibleCodex: null);

        Assert.Null(selected);
    }

    [Fact]
    public void FallsBackToAnotherVisibleCodexWhenTrackedWindowIsUnavailable()
    {
        var selected = OverlayWindowSelectionRules.Select(
            foregroundCodex: null,
            trackedCodex: null,
            otherVisibleCodex: ForegroundCodex);

        Assert.Equal(ForegroundCodex, selected);
    }

    [Theory]
    [InlineData(true, true, false, false, true)]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, true, false)]
    public void TracksOnlyExistingVisibleNonMinimizedNonCloakedWindows(
        bool exists,
        bool isVisible,
        bool isMinimized,
        bool isCloaked,
        bool expected)
    {
        Assert.Equal(
            expected,
            CodexWindowRules.IsTrackableWindowState(
                exists,
                isVisible,
                isMinimized,
                isCloaked));
    }
}
