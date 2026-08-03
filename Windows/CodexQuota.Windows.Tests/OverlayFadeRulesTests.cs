using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class OverlayFadeRulesTests
{
    private const int FullDuration = 200;

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(50, 40, false)]
    [InlineData(100, 128, false)]
    [InlineData(150, 215, false)]
    [InlineData(200, 255, true)]
    [InlineData(250, 255, true)]
    public void SamplesFadeInWithSmoothEndpoints(
        long elapsed,
        byte expectedAlpha,
        bool expectedComplete)
    {
        var transition = OverlayFadeRules.Start(0, 255, 1_000, FullDuration);

        var sample = OverlayFadeRules.Sample(transition, 1_000 + elapsed);

        Assert.Equal(expectedAlpha, sample.Alpha);
        Assert.Equal(expectedComplete, sample.IsComplete);
    }

    [Theory]
    [InlineData(0, 255, false)]
    [InlineData(50, 215, false)]
    [InlineData(100, 128, false)]
    [InlineData(150, 40, false)]
    [InlineData(200, 0, true)]
    public void SamplesFadeOutWithSmoothEndpoints(
        long elapsed,
        byte expectedAlpha,
        bool expectedComplete)
    {
        var transition = OverlayFadeRules.Start(255, 0, 2_000, FullDuration);

        var sample = OverlayFadeRules.Sample(transition, 2_000 + elapsed);

        Assert.Equal(expectedAlpha, sample.Alpha);
        Assert.Equal(expectedComplete, sample.IsComplete);
    }

    [Theory]
    [InlineData(0, 255, false)]
    [InlineData(25, 108, false)]
    [InlineData(50, 32, false)]
    [InlineData(75, 4, false)]
    [InlineData(100, 0, true)]
    public void FastOutCurveBecomesNearlyInvisibleEarly(
        long elapsed,
        byte expectedAlpha,
        bool expectedComplete)
    {
        var transition = OverlayFadeRules.Start(
            255,
            0,
            nowMilliseconds: 1_000,
            fullDurationMilliseconds: 100,
            curve: OverlayFadeCurve.FastOut);

        var sample = OverlayFadeRules.Sample(transition, 1_000 + elapsed);

        Assert.Equal(expectedAlpha, sample.Alpha);
        Assert.Equal(expectedComplete, sample.IsComplete);
    }

    [Fact]
    public void TimeBeforeStartClampsToStartingAlpha()
    {
        var transition = OverlayFadeRules.Start(25, 200, 500, FullDuration);

        var sample = OverlayFadeRules.Sample(transition, 400);

        Assert.Equal(25, sample.Alpha);
        Assert.False(sample.IsComplete);
    }

    [Fact]
    public void RepeatingTheSameTargetDoesNotRestartAnimation()
    {
        var transition = OverlayFadeRules.Start(255, 0, 1_000, FullDuration);

        var repeated = OverlayFadeRules.Retarget(
            transition,
            presentedAlpha: 173,
            targetAlpha: 0,
            nowMilliseconds: 1_100,
            FullDuration);

        Assert.Equal(transition, repeated);
    }

    [Fact]
    public void ReversingFadeUsesLastPresentedAlphaAndScaledDuration()
    {
        var fadeOut = OverlayFadeRules.Start(255, 0, 1_000, FullDuration);

        var fadeIn = OverlayFadeRules.Retarget(
            fadeOut,
            presentedAlpha: 140,
            targetAlpha: 255,
            nowMilliseconds: 1_100,
            FullDuration);

        Assert.Equal(140, fadeIn.FromAlpha);
        Assert.Equal(255, fadeIn.TargetAlpha);
        Assert.Equal(90, fadeIn.DurationMilliseconds);
        Assert.Equal(
            255,
            OverlayFadeRules.Sample(fadeIn, 1_190).Alpha);
    }

    [Fact]
    public void ReversingFadeInToFadeOutIsContinuous()
    {
        var fadeIn = OverlayFadeRules.Start(0, 255, 1_000, FullDuration);
        var current = OverlayFadeRules.Sample(fadeIn, 1_050);

        var fadeOut = OverlayFadeRules.Retarget(
            fadeIn,
            presentedAlpha: current.Alpha,
            targetAlpha: 0,
            nowMilliseconds: 1_050,
            FullDuration);

        Assert.Equal(current.Alpha, fadeOut.FromAlpha);
        Assert.Equal(0, fadeOut.TargetAlpha);
        Assert.Equal(31, fadeOut.DurationMilliseconds);
    }

    [Fact]
    public void AlphaRemainsMonotonicAndWithinByteRange()
    {
        var fadeOut = OverlayFadeRules.Start(255, 0, 0, FullDuration);
        var previous = byte.MaxValue;

        for (var time = 0; time <= FullDuration; time += 5)
        {
            var current = OverlayFadeRules.Sample(fadeOut, time).Alpha;
            Assert.InRange(current, byte.MinValue, byte.MaxValue);
            Assert.True(current <= previous);
            previous = current;
        }
    }

    [Fact]
    public void FadeInAlphaRemainsMonotonicAndWithinByteRange()
    {
        var fadeIn = OverlayFadeRules.Start(0, 255, 0, FullDuration);
        var previous = byte.MinValue;

        for (var time = 0; time <= FullDuration; time += 5)
        {
            var current = OverlayFadeRules.Sample(fadeIn, time).Alpha;
            Assert.InRange(current, byte.MinValue, byte.MaxValue);
            Assert.True(current >= previous);
            previous = current;
        }
    }

    [Theory]
    [InlineData(0, 255)]
    [InlineData(255, 0)]
    public void NonPositiveDurationSettlesImmediately(byte fromAlpha, byte targetAlpha)
    {
        var transition = OverlayFadeRules.Start(
            fromAlpha,
            targetAlpha,
            nowMilliseconds: 10,
            fullDurationMilliseconds: 0);

        var sample = OverlayFadeRules.Sample(transition, 10);

        Assert.Equal(0, transition.DurationMilliseconds);
        Assert.Equal(targetAlpha, sample.Alpha);
        Assert.True(sample.IsComplete);
    }

    [Fact]
    public void CompletedTransitionCanReverseFromItsExactTarget()
    {
        var fadeOut = OverlayFadeRules.Start(255, 0, 0, FullDuration);

        var fadeIn = OverlayFadeRules.Retarget(
            fadeOut,
            presentedAlpha: 0,
            targetAlpha: 255,
            nowMilliseconds: FullDuration + 50,
            FullDuration);

        Assert.Equal(0, fadeIn.FromAlpha);
        Assert.Equal(255, fadeIn.TargetAlpha);
        Assert.Equal(FullDuration, fadeIn.DurationMilliseconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void SettledAlphaCompletesWithoutSchedulingFrames(byte alpha)
    {
        var transition = OverlayFadeRules.Start(alpha, alpha, 100, FullDuration);

        var sample = OverlayFadeRules.Sample(transition, 100);

        Assert.Equal(0, transition.DurationMilliseconds);
        Assert.Equal(alpha, sample.Alpha);
        Assert.True(sample.IsComplete);
    }

}
