namespace CodexQuota.Windows;

internal readonly record struct TrackedWindow(nint Handle, Rectangle Bounds, float Scale);

internal static class OverlayWindowSelectionRules
{
    internal static TrackedWindow? Select(
        TrackedWindow? foregroundCodex,
        TrackedWindow? trackedCodex,
        TrackedWindow? otherVisibleCodex) =>
        foregroundCodex ?? trackedCodex ?? otherVisibleCodex;
}

internal readonly record struct OverlayFadeTransition(
    byte FromAlpha,
    byte TargetAlpha,
    long StartedAtMilliseconds,
    int DurationMilliseconds,
    OverlayFadeCurve Curve);

internal readonly record struct OverlayFadeSample(byte Alpha, bool IsComplete);

internal enum OverlayFadeCurve
{
    SmoothStep,
    FastOut
}

internal static class OverlayFadeRules
{
    internal static OverlayFadeTransition Start(
        byte fromAlpha,
        byte targetAlpha,
        long nowMilliseconds,
        int fullDurationMilliseconds,
        OverlayFadeCurve curve = OverlayFadeCurve.SmoothStep)
    {
        var distance = Math.Abs(targetAlpha - fromAlpha);
        var duration = distance == 0 || fullDurationMilliseconds <= 0
            ? 0
            : Math.Max(
                1,
                (int)Math.Round(
                    fullDurationMilliseconds * distance / (double)byte.MaxValue,
                    MidpointRounding.AwayFromZero));
        return new OverlayFadeTransition(
            fromAlpha,
            targetAlpha,
            nowMilliseconds,
            duration,
            curve);
    }

    internal static OverlayFadeTransition Retarget(
        OverlayFadeTransition transition,
        byte presentedAlpha,
        byte targetAlpha,
        long nowMilliseconds,
        int fullDurationMilliseconds,
        OverlayFadeCurve curve = OverlayFadeCurve.SmoothStep)
    {
        if (transition.TargetAlpha == targetAlpha)
            return transition;

        return Start(
            presentedAlpha,
            targetAlpha,
            nowMilliseconds,
            fullDurationMilliseconds,
            curve);
    }

    internal static OverlayFadeSample Sample(
        OverlayFadeTransition transition,
        long nowMilliseconds)
    {
        if (transition.DurationMilliseconds <= 0)
            return new OverlayFadeSample(transition.TargetAlpha, IsComplete: true);

        var elapsed = Math.Clamp(
            nowMilliseconds - transition.StartedAtMilliseconds,
            0,
            transition.DurationMilliseconds);
        if (elapsed >= transition.DurationMilliseconds)
            return new OverlayFadeSample(transition.TargetAlpha, IsComplete: true);

        var progress = elapsed / (double)transition.DurationMilliseconds;
        var easedProgress = transition.Curve switch
        {
            OverlayFadeCurve.FastOut => 1d - Math.Pow(1d - progress, 3d),
            _ => progress * progress * (3d - (2d * progress))
        };
        var alpha = (int)Math.Round(
            transition.FromAlpha +
            ((transition.TargetAlpha - transition.FromAlpha) * easedProgress),
            MidpointRounding.AwayFromZero);
        return new OverlayFadeSample(
            (byte)Math.Clamp(alpha, byte.MinValue, byte.MaxValue),
            IsComplete: false);
    }
}

internal enum CodexWindowEventAction
{
    Ignore,
    BeginMove,
    Move,
    EndMove,
    BeginMinimize,
    EndMinimize
}

internal static class CodexWindowEventRules
{
    internal const uint EventSystemMoveSizeStart = 0x000A;
    internal const uint EventSystemMoveSizeEnd = 0x000B;
    internal const uint EventSystemMinimizeStart = 0x0016;
    internal const uint EventSystemMinimizeEnd = 0x0017;
    internal const uint EventObjectLocationChange = 0x800B;
    internal const int ObjectIdWindow = 0;
    internal const int ChildIdSelf = 0;

    internal static CodexWindowEventAction Classify(
        uint eventType,
        nint eventWindow,
        int objectId,
        int childId,
        nint trackedWindow,
        nint overlayWindow,
        bool isKnownVisibilityWindow = false)
    {
        if (eventWindow == 0 ||
            eventWindow == overlayWindow ||
            (eventWindow != trackedWindow &&
             !(isKnownVisibilityWindow &&
               eventType is EventSystemMinimizeStart or EventSystemMinimizeEnd)))
        {
            return CodexWindowEventAction.Ignore;
        }

        return eventType switch
        {
            EventSystemMoveSizeStart => CodexWindowEventAction.BeginMove,
            EventSystemMoveSizeEnd => CodexWindowEventAction.EndMove,
            EventSystemMinimizeStart => CodexWindowEventAction.BeginMinimize,
            EventSystemMinimizeEnd => CodexWindowEventAction.EndMinimize,
            EventObjectLocationChange
                when objectId == ObjectIdWindow && childId == ChildIdSelf
                => CodexWindowEventAction.Move,
            _ => CodexWindowEventAction.Ignore
        };
    }
}

internal enum BadgePlacementAction
{
    None,
    MoveOnly,
    RenderAndMove
}

internal static class BadgePlacementRules
{
    internal static BadgePlacementAction Decide(
        Rectangle previousBounds,
        Rectangle nextBounds,
        bool hasLayeredContent,
        bool animateLoading,
        bool isLoading)
    {
        if (!hasLayeredContent ||
            previousBounds.IsEmpty ||
            previousBounds.Size != nextBounds.Size ||
            (animateLoading && isLoading))
        {
            return BadgePlacementAction.RenderAndMove;
        }

        return previousBounds.Location == nextBounds.Location
            ? BadgePlacementAction.None
            : BadgePlacementAction.MoveOnly;
    }
}

internal static class CodexWindowRules
{
    internal const int BadgeWidth = 64;
    internal const int BadgeHeight = 28;
    private const int MinimumWindowWidth = 700;
    private const int MinimumWindowHeight = 500;
    private const int BadgeLeftOffset = 68;
    private const float AccountRowCenterFromBottom = 23f;

    internal static bool IsSupportedForegroundProcess(
        string? processName,
        int processId,
        int currentProcessId)
    {
        if (processId <= 0 || processId == currentProcessId)
            return false;

        return string.Equals(processName, "Codex", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(processName, "ChatGPT", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsEligibleMainWindow(Rectangle bounds, float scale)
    {
        scale = NormalizeScale(scale);
        return bounds.Width / scale >= MinimumWindowWidth &&
               bounds.Height / scale >= MinimumWindowHeight;
    }

    internal static bool IsTrackableWindowState(
        bool exists,
        bool isVisible,
        bool isMinimized,
        bool isCloaked) =>
        exists && isVisible && !isMinimized && !isCloaked;

    internal static TrackedWindow? SelectMainWindow(IEnumerable<TrackedWindow> candidates) =>
        candidates
            .Where(candidate => IsEligibleMainWindow(candidate.Bounds, candidate.Scale))
            .FirstOrDefault() is { Handle: not 0 } selected
                ? selected
                : null;

    internal static Rectangle BadgeBounds(Rectangle codexBounds, float scale)
    {
        scale = NormalizeScale(scale);
        var width = (int)Math.Round(BadgeWidth * scale);
        var height = (int)Math.Round(BadgeHeight * scale);
        var bottomOffset = (int)Math.Round(
            (AccountRowCenterFromBottom * scale) + (height / 2f),
            MidpointRounding.AwayFromZero);
        return new Rectangle(
            codexBounds.Left + (int)Math.Round(BadgeLeftOffset * scale),
            codexBounds.Bottom - bottomOffset,
            width,
            height);
    }

    internal static float NormalizeScale(float scale) =>
        float.IsFinite(scale) && scale > 0 ? scale : 1f;
}
