using System.Drawing;
using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class OverlayWindowTransitionTests
{
    private static readonly TrackedWindow StableWindow =
        new((nint)101, new Rectangle(20, 30, 1200, 800), 1.25f);

    [Fact]
    public void MissingMinimizeEndSelfHealsAfterWindowBecomesAvailable()
    {
        var transition = OverlayWindowTransitionRules.BeginMinimize(1_000);

        transition = OverlayWindowTransitionRules.ObserveMinimizeState(
            transition,
            isAvailable: false,
            nowMilliseconds: 1_030,
            recoveryGraceMilliseconds: 300,
            revealDelayMilliseconds: 150);
        Assert.True(transition.ObservedUnavailable);

        transition = OverlayWindowTransitionRules.ObserveMinimizeState(
            transition,
            isAvailable: true,
            nowMilliseconds: 1_200,
            recoveryGraceMilliseconds: 300,
            revealDelayMilliseconds: 150);

        Assert.Equal(OverlayWindowTransitionPhase.Restoring, transition.Phase);
        Assert.Equal(1_350, transition.RevealNotBeforeMilliseconds);
    }

    [Fact]
    public void PreIconicMinimizeStartCannotImmediatelyReSelectTheWindow()
    {
        var transition = OverlayWindowTransitionRules.BeginMinimize(1_000);

        var observed = OverlayWindowTransitionRules.ObserveMinimizeState(
            transition,
            isAvailable: true,
            nowMilliseconds: 1_050,
            recoveryGraceMilliseconds: 300,
            revealDelayMilliseconds: 150);

        Assert.Equal(OverlayWindowTransitionPhase.Minimizing, observed.Phase);
        Assert.False(observed.ObservedUnavailable);
    }

    [Fact]
    public void CancelledMinimizeRecoversAfterGraceEvenWithoutStateEvents()
    {
        var transition = OverlayWindowTransitionRules.BeginMinimize(1_000);

        transition = OverlayWindowTransitionRules.ObserveMinimizeState(
            transition,
            isAvailable: true,
            nowMilliseconds: 1_300,
            recoveryGraceMilliseconds: 300,
            revealDelayMilliseconds: 150);

        Assert.Equal(OverlayWindowTransitionPhase.Restoring, transition.Phase);
        Assert.Equal(1_450, transition.RevealNotBeforeMilliseconds);
    }

    [Fact]
    public void RestoreRequiresDelayAndTwoStablePlacementSamples()
    {
        var transition = OverlayWindowTransitionRules.BeginRestore(
            nowMilliseconds: 1_000,
            revealDelayMilliseconds: 150);

        transition = OverlayWindowTransitionRules.ObserveRestorePlacement(
            transition,
            StableWindow);
        Assert.False(OverlayWindowTransitionRules.IsRestoreReady(
            transition,
            nowMilliseconds: 1_150,
            requiredStableSamples: 2));

        transition = OverlayWindowTransitionRules.ObserveRestorePlacement(
            transition,
            StableWindow);
        Assert.True(OverlayWindowTransitionRules.IsRestoreReady(
            transition,
            nowMilliseconds: 1_150,
            requiredStableSamples: 2));
    }

    [Fact]
    public void MinimizeEndDeadlineSurvivesUntilRestoreBecomesAvailable()
    {
        var transition = OverlayWindowTransitionRules.BeginRestore(
            nowMilliseconds: 1_000,
            revealDelayMilliseconds: 150);

        Assert.False(OverlayWindowTransitionRules.ShouldRestartMinimizeAfterRestore(
            transition,
            isUnavailable: true,
            nowMilliseconds: 1_100,
            unavailableTimeoutMilliseconds: 600));
        Assert.Equal(1_150, transition.RevealNotBeforeMilliseconds);
    }

    [Fact]
    public void ASecondMinimizeCanBeInferredAfterRestoreWasVisible()
    {
        var transition = OverlayWindowTransitionRules.BeginRestore(1_000, 150);
        transition = OverlayWindowTransitionRules.ObserveRestorePlacement(
            transition,
            StableWindow);

        Assert.True(OverlayWindowTransitionRules.ShouldRestartMinimizeAfterRestore(
            transition,
            isUnavailable: true,
            nowMilliseconds: 1_100,
            unavailableTimeoutMilliseconds: 600));
    }

    [Fact]
    public void StaleRestoreEventFallsBackToFreshRestoreTiming()
    {
        var transition = OverlayWindowTransitionRules.BeginRestore(1_000, 150);

        Assert.True(OverlayWindowTransitionRules.ShouldRestartMinimizeAfterRestore(
            transition,
            isUnavailable: true,
            nowMilliseconds: 1_600,
            unavailableTimeoutMilliseconds: 600));
    }

    [Fact]
    public void PlacementChangeRestartsRestoreStabilitySampling()
    {
        var transition = OverlayWindowTransitionRules.BeginRestore(1_000, 150);
        transition = OverlayWindowTransitionRules.ObserveRestorePlacement(
            transition,
            StableWindow);
        transition = OverlayWindowTransitionRules.ObserveRestorePlacement(
            transition,
            StableWindow with
            {
                Bounds = StableWindow.Bounds with { X = StableWindow.Bounds.X + 1 }
            });

        Assert.Equal(1, transition.StableSamples);
    }

    [Fact]
    public void CoordinatedAnimationUsesPerceptualMaskingTiming()
    {
        var timing = OverlayAnimationTiming.Coordinated;

        Assert.Equal(90, timing.FadeOutMilliseconds);
        Assert.Equal(100, timing.FadeInMilliseconds);
        Assert.Equal(150, timing.RestoreRevealDelayMilliseconds);
        Assert.Equal(2, timing.RequiredStableSamples);
    }
}
