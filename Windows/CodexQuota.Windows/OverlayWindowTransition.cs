namespace CodexQuota.Windows;

internal enum OverlayWindowTransitionPhase
{
    Minimizing,
    Restoring
}

internal readonly record struct OverlayWindowTransition(
    OverlayWindowTransitionPhase Phase,
    long StartedAtMilliseconds,
    bool ObservedUnavailable,
    long RevealNotBeforeMilliseconds,
    Rectangle StableBounds,
    float StableScale,
    int StableSamples);

internal readonly record struct OverlayAnimationTiming(
    int FadeOutMilliseconds,
    int FadeInMilliseconds,
    int RestoreRevealDelayMilliseconds,
    int RequiredStableSamples,
    int MinimizeRecoveryGraceMilliseconds)
{
    internal static OverlayAnimationTiming Coordinated { get; } = new(
        FadeOutMilliseconds: 90,
        FadeInMilliseconds: 100,
        RestoreRevealDelayMilliseconds: 150,
        RequiredStableSamples: 2,
        MinimizeRecoveryGraceMilliseconds: 300);
}

internal static class OverlayWindowTransitionRules
{
    internal static OverlayWindowTransition BeginMinimize(
        long nowMilliseconds,
        bool observedUnavailable = false) =>
        new(
            OverlayWindowTransitionPhase.Minimizing,
            nowMilliseconds,
            observedUnavailable,
            RevealNotBeforeMilliseconds: 0,
            StableBounds: Rectangle.Empty,
            StableScale: 0f,
            StableSamples: 0);

    internal static OverlayWindowTransition BeginRestore(
        long nowMilliseconds,
        int revealDelayMilliseconds) =>
        new(
            OverlayWindowTransitionPhase.Restoring,
            nowMilliseconds,
            ObservedUnavailable: true,
            RevealNotBeforeMilliseconds:
                nowMilliseconds + Math.Max(0, revealDelayMilliseconds),
            StableBounds: Rectangle.Empty,
            StableScale: 0f,
            StableSamples: 0);

    internal static OverlayWindowTransition ObserveMinimizeState(
        OverlayWindowTransition transition,
        bool isAvailable,
        long nowMilliseconds,
        int recoveryGraceMilliseconds,
        int revealDelayMilliseconds)
    {
        if (transition.Phase != OverlayWindowTransitionPhase.Minimizing)
            return transition;

        if (!isAvailable)
            return transition with { ObservedUnavailable = true };

        var graceElapsed =
            nowMilliseconds - transition.StartedAtMilliseconds >=
            Math.Max(0, recoveryGraceMilliseconds);
        return transition.ObservedUnavailable || graceElapsed
            ? BeginRestore(nowMilliseconds, revealDelayMilliseconds)
            : transition;
    }

    internal static OverlayWindowTransition ObserveRestorePlacement(
        OverlayWindowTransition transition,
        TrackedWindow candidate)
    {
        if (transition.Phase != OverlayWindowTransitionPhase.Restoring)
            return transition;

        if (transition.StableBounds == candidate.Bounds &&
            Math.Abs(transition.StableScale - candidate.Scale) < 0.001f)
        {
            return transition with { StableSamples = transition.StableSamples + 1 };
        }

        return transition with
        {
            StableBounds = candidate.Bounds,
            StableScale = candidate.Scale,
            StableSamples = 1
        };
    }

    internal static bool IsRestoreReady(
        OverlayWindowTransition transition,
        long nowMilliseconds,
        int requiredStableSamples) =>
        transition.Phase == OverlayWindowTransitionPhase.Restoring &&
        nowMilliseconds >= transition.RevealNotBeforeMilliseconds &&
        transition.StableSamples >= Math.Max(1, requiredStableSamples);

    internal static bool ShouldRestartMinimizeAfterRestore(
        OverlayWindowTransition transition,
        bool isUnavailable,
        long nowMilliseconds,
        int unavailableTimeoutMilliseconds) =>
        transition.Phase == OverlayWindowTransitionPhase.Restoring &&
        isUnavailable &&
        (transition.StableSamples > 0 ||
         nowMilliseconds - transition.StartedAtMilliseconds >=
         Math.Max(0, unavailableTimeoutMilliseconds));
}
