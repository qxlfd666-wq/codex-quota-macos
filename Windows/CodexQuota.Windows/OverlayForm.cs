using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodexQuota.Windows;

internal sealed class OverlayForm : Form
{
    private const int NormalTrackingInterval = 200;
    private const int MovingTrackingInterval = 15;
    private readonly System.Windows.Forms.Timer _trackingTimer;
    private readonly NativeMethods.WinEventCallback _winEventCallback;
    private GCHandle _winEventCallbackHandle;
    private int? _remainingPercent;
    private Color _accentColor = Color.FromArgb(255, 59, 48);
    private OverlayDisplayState _displayState = OverlayDisplayState.Loading;
    private Rectangle _lastBadgeBounds = Rectangle.Empty;
    private nint _trackedCodexWindow;
    private nint _hookedCodexWindow;
    private uint _hookedCodexProcessId;
    private nint _moveSizeEventHook;
    private nint _locationChangeEventHook;
    private long _lastMovementEventTick;
    private long _nextHookRetryTick;
    private bool _hasLayeredContent;
    private bool _eventHooksEnabled;
    private bool _isMoveSizing;
    private bool _handlingWinEvent;
    private bool _placementUpdateQueued;
    private bool _fullPlacementUpdateQueued;
    private bool _disposed;

    public OverlayForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(CodexWindowRules.BadgeWidth, CodexWindowRules.BadgeHeight);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Cursor = Cursors.Hand;
        AccessibleName = "Codex 剩余额度";

        Click += (_, _) => ChooseColorRequested?.Invoke(this, EventArgs.Empty);
        _winEventCallback = HandleCodexWindowEvent;
        _winEventCallbackHandle = GCHandle.Alloc(_winEventCallback);
        _trackingTimer = new System.Windows.Forms.Timer { Interval = NormalTrackingInterval };
        _trackingTimer.Tick += (_, _) => HandleTrackingTimerTick();
    }

    public event EventHandler? ChooseColorRequested;

    internal nint TrackedCodexWindow => _trackedCodexWindow;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            const int wsExLayered = 0x00080000;
            const int wsExNoActivate = 0x08000000;
            var parameters = base.CreateParams;
            parameters.ExStyle |= wsExToolWindow | wsExLayered | wsExNoActivate;
            return parameters;
        }
    }

    public void StartTracking()
    {
        _ = Handle;
        _trackingTimer.Start();
        TrackPlacement();

        // Out-of-context WinEvents are delivered through the installing
        // thread's message loop. Queue installation until Application.Run is
        // actively pumping messages.
        BeginInvoke((MethodInvoker)(() =>
        {
            if (_disposed)
                return;

            _eventHooksEnabled = true;
            TrackPlacement();
        }));
    }

    internal void RefreshPlacement() => TrackPlacement();

    public void SetQuota(int remainingPercent)
    {
        _remainingPercent = Math.Clamp(remainingPercent, 0, 100);
        _displayState = OverlayDisplayState.Quota;
        AccessibleDescription = $"Codex 剩余 {_remainingPercent}%";
        RedrawCurrentBadge();
    }

    public void SetLoading()
    {
        if (_remainingPercent.HasValue)
            return;

        _displayState = OverlayDisplayState.Loading;
        AccessibleDescription = "正在读取 Codex 剩余额度";
        RedrawCurrentBadge();
    }

    public void SetError(string message)
    {
        if (_remainingPercent.HasValue)
            return;

        _displayState = OverlayDisplayState.Error;
        AccessibleDescription = message;
        RedrawCurrentBadge();
    }

    public void SetAccentColor(Color color)
    {
        _accentColor = color;
        RedrawCurrentBadge();
    }

    private void UpdatePlacement(bool animateLoading)
    {
        if (!NativeMethods.TryGetCodexForegroundWindow(out var codexWindow))
        {
            HideOverlay();
            return;
        }

        _trackedCodexWindow = codexWindow.Handle;
        if (_eventHooksEnabled)
            EnsureWindowEventHooks(codexWindow.Handle);

        ApplyPlacement(codexWindow, animateLoading);
    }

    private void ApplyPlacement(TrackedWindow codexWindow, bool animateLoading)
    {

        // Codex currently has no public API for its account-row coordinates.
        // These offsets follow the stable lower-left sidebar geometry.
        var badgeBounds = CodexWindowRules.BadgeBounds(codexWindow.Bounds, codexWindow.Scale);
        var placementAction = BadgePlacementRules.Decide(
            _lastBadgeBounds,
            badgeBounds,
            _hasLayeredContent,
            animateLoading,
            _displayState == OverlayDisplayState.Loading);

        if (placementAction == BadgePlacementAction.RenderAndMove)
        {
            _hasLayeredContent = DrawLayeredBadge(badgeBounds);
            if (_hasLayeredContent)
                _lastBadgeBounds = badgeBounds;
        }

        if (!_hasLayeredContent)
        {
            HideOverlay();
            return;
        }

        if (placementAction != BadgePlacementAction.None ||
            !NativeMethods.IsWindowVisible(Handle))
        {
            var positionApplied = NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.HwndTopmost,
                badgeBounds.X,
                badgeBounds.Y,
                badgeBounds.Width,
                badgeBounds.Height,
                NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
            if (positionApplied)
                _lastBadgeBounds = badgeBounds;
        }
    }

    private void HandleTrackingTimerTick()
    {
        if (_isMoveSizing &&
            Environment.TickCount64 - _lastMovementEventTick > NormalTrackingInterval * 3L)
        {
            // MOVESIZEEND can be lost if the target HWND is destroyed or the
            // desktop switches mid-drag. A quiet-period watchdog prevents the
            // 15 ms fallback timer from becoming permanent.
            StopFastTracking();
        }

        if (_isMoveSizing && TryMoveTrackedOverlay())
            return;

        TrackPlacement(animateLoading: !_isMoveSizing);
    }

    private bool TryMoveTrackedOverlay()
    {
        var trackedWindow = _trackedCodexWindow;
        if (trackedWindow == 0 ||
            !_hasLayeredContent ||
            _lastBadgeBounds.IsEmpty ||
            !NativeMethods.IsForegroundWindowOrOwnedBy(trackedWindow) ||
            !NativeMethods.TryGetTrackedCodexWindow(
                trackedWindow,
                _hookedCodexProcessId,
                out var codexWindow))
        {
            return false;
        }

        var badgeBounds = CodexWindowRules.BadgeBounds(codexWindow.Bounds, codexWindow.Scale);
        var placementAction = BadgePlacementRules.Decide(
            _lastBadgeBounds,
            badgeBounds,
            _hasLayeredContent,
            animateLoading: false,
            _displayState == OverlayDisplayState.Loading);
        if (placementAction == BadgePlacementAction.RenderAndMove)
            return false;

        if (placementAction == BadgePlacementAction.None)
            return true;

        var positionApplied = NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HwndTopmost,
            badgeBounds.X,
            badgeBounds.Y,
            badgeBounds.Width,
            badgeBounds.Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        if (positionApplied)
            _lastBadgeBounds = badgeBounds;
        return positionApplied;
    }

    private void EnsureWindowEventHooks(nint codexWindow)
    {
        NativeMethods.GetWindowThreadProcessId(codexWindow, out var processId);
        if (processId == 0)
            return;

        if (_hookedCodexWindow == codexWindow && _hookedCodexProcessId == processId)
        {
            if (_moveSizeEventHook != 0 && _locationChangeEventHook != 0)
                return;
            if (Environment.TickCount64 < _nextHookRetryTick)
                return;
        }

        StopFastTracking();
        StopWindowEventHooks();
        _hookedCodexWindow = codexWindow;
        _hookedCodexProcessId = processId;

        var moveSizeHook = NativeMethods.HookCodexWindowMoveEvents(
            CodexWindowEventRules.EventSystemMoveSizeStart,
            CodexWindowEventRules.EventSystemMoveSizeEnd,
            _winEventCallback,
            processId);
        var moveSizeError = moveSizeHook == 0 ? Marshal.GetLastWin32Error() : 0;
        var locationChangeHook = NativeMethods.HookCodexWindowMoveEvents(
            CodexWindowEventRules.EventObjectLocationChange,
            CodexWindowEventRules.EventObjectLocationChange,
            _winEventCallback,
            processId);
        var locationChangeError = locationChangeHook == 0 ? Marshal.GetLastWin32Error() : 0;

        if (moveSizeHook == 0 || locationChangeHook == 0)
        {
            if (moveSizeHook != 0)
                _ = NativeMethods.UnhookWinEvent(moveSizeHook);
            if (locationChangeHook != 0)
                _ = NativeMethods.UnhookWinEvent(locationChangeHook);

            Debug.WriteLine(
                "Unable to install Codex movement hooks; polling fallback remains active. " +
                $"Win32 errors: move={moveSizeError}, location={locationChangeError}");
            _nextHookRetryTick = Environment.TickCount64 + 5_000;
            return;
        }

        _moveSizeEventHook = moveSizeHook;
        _locationChangeEventHook = locationChangeHook;
        _nextHookRetryTick = 0;
    }

    private void HandleCodexWindowEvent(
        nint hook,
        uint eventType,
        nint eventWindow,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        try
        {
            if (_disposed || !IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)(() => HandleCodexWindowEvent(
                    hook,
                    eventType,
                    eventWindow,
                    objectId,
                    childId,
                    eventThread,
                    eventTime)));
                return;
            }

            var action = CodexWindowEventRules.Classify(
                eventType,
                eventWindow,
                objectId,
                childId,
                _trackedCodexWindow,
                Handle);
            if (action == CodexWindowEventAction.Ignore)
                return;

            // Native WinEvent callbacks may re-enter. Move-only placement is
            // safe here because the hook skips this process, so our SetWindowPos
            // cannot recursively feed this hook. Rendering and full window
            // discovery remain queued until after the callback returns.
            if (_handlingWinEvent)
            {
                if (action is CodexWindowEventAction.BeginMove or CodexWindowEventAction.Move)
                    StartFastTracking();
                else if (action == CodexWindowEventAction.EndMove)
                    StopFastTracking();
                QueuePlacementUpdate(action == CodexWindowEventAction.EndMove);
                return;
            }

            _handlingWinEvent = true;
            try
            {
                switch (action)
                {
                    case CodexWindowEventAction.BeginMove:
                        StartFastTracking();
                        if (!TryMoveTrackedOverlay())
                            QueuePlacementUpdate(fullUpdate: false);
                        break;

                    case CodexWindowEventAction.Move:
                        StartFastTracking();
                        if (!TryMoveTrackedOverlay())
                            QueuePlacementUpdate(fullUpdate: false);
                        break;

                    case CodexWindowEventAction.EndMove:
                        StopFastTracking();
                        QueuePlacementUpdate(fullUpdate: true);
                        break;
                }
            }
            finally
            {
                _handlingWinEvent = false;
            }
        }
        catch (Exception exception)
        {
            // Exceptions must never cross the native WinEvent callback boundary.
            Debug.WriteLine($"Unable to process a Codex movement event: {exception}");
            QueuePlacementUpdate(fullUpdate: true);
        }
    }

    private void StartFastTracking()
    {
        _lastMovementEventTick = Environment.TickCount64;
        _isMoveSizing = true;
        if (_trackingTimer.Interval != MovingTrackingInterval)
            _trackingTimer.Interval = MovingTrackingInterval;
    }

    private void StopFastTracking()
    {
        _isMoveSizing = false;
        if (!_disposed && _trackingTimer.Interval != NormalTrackingInterval)
            _trackingTimer.Interval = NormalTrackingInterval;
    }

    private void QueuePlacementUpdate(bool fullUpdate)
    {
        if (_disposed || !IsHandleCreated)
            return;

        _fullPlacementUpdateQueued |= fullUpdate;
        if (_placementUpdateQueued)
            return;

        _placementUpdateQueued = true;
        try
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                var runFullUpdate = _fullPlacementUpdateQueued;
                _placementUpdateQueued = false;
                _fullPlacementUpdateQueued = false;
                if (_disposed)
                    return;

                if (!runFullUpdate && TryMoveTrackedOverlay())
                    return;

                TrackPlacement(animateLoading: !_isMoveSizing);
            }));
        }
        catch (InvalidOperationException)
        {
            _placementUpdateQueued = false;
            _fullPlacementUpdateQueued = false;
        }
    }

    private void StopWindowEventHooks()
    {
        if (_moveSizeEventHook != 0)
            _ = NativeMethods.UnhookWinEvent(_moveSizeEventHook);
        if (_locationChangeEventHook != 0)
            _ = NativeMethods.UnhookWinEvent(_locationChangeEventHook);

        _moveSizeEventHook = 0;
        _locationChangeEventHook = 0;
        _hookedCodexWindow = 0;
        _hookedCodexProcessId = 0;
        _nextHookRetryTick = 0;
    }

    private void RedrawCurrentBadge()
    {
        if (!IsHandleCreated || _lastBadgeBounds.IsEmpty)
            return;

        _hasLayeredContent = DrawLayeredBadge(_lastBadgeBounds);
        if (!_hasLayeredContent)
            HideOverlay();
    }

    private bool DrawLayeredBadge(Rectangle badgeBounds)
    {
        try
        {
            using var bitmap = OverlayRenderer.Render(
                badgeBounds.Size,
                _accentColor,
                _remainingPercent,
                _displayState,
                Environment.TickCount64);
            return NativeMethods.TryUpdateLayeredWindow(
                Handle,
                bitmap,
                badgeBounds.Location);
        }
        catch (Exception exception)
        {
            // The overlay is auxiliary UI. A transient GDI/DWM failure must not
            // terminate the tray process or leave stale quota visible.
            Debug.WriteLine($"Unable to render the Codex quota overlay: {exception}");
            return false;
        }
    }

    private void TrackPlacement(bool animateLoading = true)
    {
        try
        {
            UpdatePlacement(animateLoading);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Unable to track the Codex window: {exception}");
            HideOverlay();
        }
    }

    private void HideOverlay()
    {
        StopFastTracking();
        _lastBadgeBounds = Rectangle.Empty;
        _trackedCodexWindow = 0;
        _hasLayeredContent = false;
        if (IsHandleCreated && NativeMethods.IsWindowVisible(Handle))
            NativeMethods.ShowWindow(Handle, NativeMethods.SwHide);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            _trackingTimer.Stop();
            StopWindowEventHooks();
            if (_winEventCallbackHandle.IsAllocated)
                _winEventCallbackHandle.Free();
            _trackingTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal enum OverlayDisplayState
{
    Loading,
    Quota,
    Error
}

internal static class OverlayRenderer
{
    private const int SupersamplingFactor = 4;
    private const float LogicalWidth = CodexWindowRules.BadgeWidth;
    private const float LogicalHeight = CodexWindowRules.BadgeHeight;
    private const float CapsuleInset = 2f;

    internal static Bitmap Render(
        Size size,
        Color accentColor,
        int? remainingPercent,
        OverlayDisplayState displayState,
        long animationTimeMilliseconds)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        var renderSize = new Size(
            checked(size.Width * SupersamplingFactor),
            checked(size.Height * SupersamplingFactor));
        using var supersampled = new Bitmap(
            renderSize.Width,
            renderSize.Height,
            PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(supersampled))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.GammaCorrected;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var scale = renderSize.Height / LogicalHeight;
            DrawContents(
                graphics,
                renderSize,
                scale,
                accentColor,
                remainingPercent,
                displayState,
                animationTimeMilliseconds);
        }

        var result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        using var resultGraphics = Graphics.FromImage(result);
        resultGraphics.Clear(Color.Transparent);
        resultGraphics.CompositingMode = CompositingMode.SourceCopy;
        resultGraphics.CompositingQuality = CompositingQuality.GammaCorrected;
        // Bilinear downsampling keeps alpha within the source range. Bicubic
        // interpolation can overshoot alpha near tiny glyphs and create bright specks.
        resultGraphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        resultGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        resultGraphics.DrawImage(
            supersampled,
            new Rectangle(Point.Empty, size),
            new Rectangle(Point.Empty, renderSize),
            GraphicsUnit.Pixel);
        return result;
    }

    private static void DrawContents(
        Graphics graphics,
        Size renderSize,
        float scale,
        Color accentColor,
        int? remainingPercent,
        OverlayDisplayState displayState,
        long animationTimeMilliseconds)
    {
        var capsule = new RectangleF(
            CapsuleInset * scale,
            CapsuleInset * scale,
            renderSize.Width - (CapsuleInset * 2 * scale),
            renderSize.Height - (CapsuleInset * 2 * scale));
        using var capsulePath = RoundedRectangle(capsule, capsule.Height / 2f);
        using var backgroundBrush = new SolidBrush(Color.FromArgb(23, accentColor));
        graphics.FillPath(backgroundBrush, capsulePath);

        using var borderPen = new Pen(Color.FromArgb(46, accentColor), 0.5f * scale);
        graphics.DrawPath(borderPen, capsulePath);

        if (displayState == OverlayDisplayState.Loading)
        {
            DrawLoadingIndicator(graphics, renderSize, scale, accentColor, animationTimeMilliseconds);
            return;
        }

        var text = displayState == OverlayDisplayState.Error
            ? "!"
            : remainingPercent is { } remaining ? $"{remaining}%" : "--";
        using var textBrush = new SolidBrush(Color.FromArgb(225, accentColor));
        using var font = new Font("Segoe UI", 12f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textFormat = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        graphics.DrawString(
            text,
            font,
            textBrush,
            new RectangleF(0, 3.5f * scale, renderSize.Width, 15.5f * scale),
            textFormat);

        if (displayState != OverlayDisplayState.Quota)
            return;

        var track = new RectangleF(
            9 * scale,
            22 * scale,
            (LogicalWidth - 18) * scale,
            2.5f * scale);
        using var trackBrush = new SolidBrush(Color.FromArgb(36, accentColor));
        graphics.FillRoundedRectangle(trackBrush, track, track.Height / 2f);
        if (remainingPercent is > 0)
        {
            var percent = Math.Clamp(remainingPercent.Value, 0, 100);
            var fill = new RectangleF(track.X, track.Y, track.Width * percent / 100f, track.Height);
            using var fillBrush = new SolidBrush(Color.FromArgb(199, accentColor));
            graphics.FillRoundedRectangle(fillBrush, fill, fill.Height / 2f);
        }
    }

    private static void DrawLoadingIndicator(
        Graphics graphics,
        Size renderSize,
        float scale,
        Color accentColor,
        long animationTimeMilliseconds)
    {
        var diameter = 10f * scale;
        var bounds = new RectangleF(
            (renderSize.Width - diameter) / 2f,
            (renderSize.Height - diameter) / 2f,
            diameter,
            diameter);
        var angle = animationTimeMilliseconds / 4f % 360;
        using var pen = new Pen(Color.FromArgb(215, accentColor), 1.5f * scale)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawArc(pen, bounds, angle, 265);
    }

    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal static class GraphicsExtensions
{
    internal static void FillRoundedRectangle(
        this Graphics graphics,
        Brush brush,
        RectangleF rectangle,
        float radius)
    {
        var diameter = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
