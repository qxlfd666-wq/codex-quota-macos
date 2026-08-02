namespace CodexQuota.Windows;

internal readonly record struct TrackedWindow(nint Handle, Rectangle Bounds, float Scale);

internal enum CodexWindowEventAction
{
    Ignore,
    BeginMove,
    Move,
    EndMove
}

internal static class CodexWindowEventRules
{
    internal const uint EventSystemMoveSizeStart = 0x000A;
    internal const uint EventSystemMoveSizeEnd = 0x000B;
    internal const uint EventObjectLocationChange = 0x800B;
    internal const int ObjectIdWindow = 0;
    internal const int ChildIdSelf = 0;

    internal static CodexWindowEventAction Classify(
        uint eventType,
        nint eventWindow,
        int objectId,
        int childId,
        nint trackedWindow,
        nint overlayWindow)
    {
        if (trackedWindow == 0 ||
            eventWindow == 0 ||
            eventWindow == overlayWindow ||
            eventWindow != trackedWindow)
        {
            return CodexWindowEventAction.Ignore;
        }

        return eventType switch
        {
            EventSystemMoveSizeStart => CodexWindowEventAction.BeginMove,
            EventSystemMoveSizeEnd => CodexWindowEventAction.EndMove,
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
