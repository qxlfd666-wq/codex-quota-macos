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
    private const int FadeFrameInterval = 15;
    private const int VisibilityEventTrackingDuration = 600;
    private readonly System.Windows.Forms.Timer _trackingTimer;
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private readonly NativeMethods.WinEventCallback _winEventCallback;
    private readonly Dictionary<nint, OverlayWindowTransition> _windowTransitions = new();
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
    private nint _minimizeEventHook;
    private long _lastMovementEventTick;
    private long _nextHookRetryTick;
    private long _visibilityTrackingUntilTick;
    private OverlayFadeTransition? _fadeTransition;
    private byte _overlayAlpha;
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
        _fadeTimer = new System.Windows.Forms.Timer { Interval = FadeFrameInterval };
        _fadeTimer.Tick += (_, _) => HandleFadeTimerTick();
    }

    public event EventHandler? ChooseColorRequested;

    internal nint TrackedCodexWindow => _trackedCodexWindow;

    private static OverlayAnimationTiming CurrentAnimationTiming =>
        OverlayAnimationTiming.Coordinated;

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
        var now = Environment.TickCount64;
        var timing = CurrentAnimationTiming;
        ReconcileWindowTransitions(now, timing);
        ObserveTrackedWindowUnavailable(now);

        var minimizingWindows = _windowTransitions
            .Where(pair => pair.Value.Phase == OverlayWindowTransitionPhase.Minimizing)
            .Select(pair => pair.Key)
            .ToHashSet();
        TrackedWindow? foregroundCodex = null;
        TrackedWindow? trackedCodex = null;
        TrackedWindow? otherVisibleCodex = null;
        if (NativeMethods.TryGetCodexForegroundWindow(out var foregroundWindow) &&
            !minimizingWindows.Contains(foregroundWindow.Handle))
        {
            foregroundCodex = foregroundWindow;
        }

        if (foregroundCodex is null &&
            _trackedCodexWindow != 0 &&
            !minimizingWindows.Contains(_trackedCodexWindow) &&
            NativeMethods.TryGetVisibleTrackedCodexWindow(
                _trackedCodexWindow,
                out var trackedWindow))
        {
            trackedCodex = trackedWindow;
        }

        if (foregroundCodex is null &&
            trackedCodex is null &&
            NativeMethods.TryGetAnyVisibleCodexWindow(
                minimizingWindows,
                out var visibleWindow))
        {
            otherVisibleCodex = visibleWindow;
        }

        var selectedWindow = OverlayWindowSelectionRules.Select(
            foregroundCodex,
            trackedCodex,
            otherVisibleCodex);
        if (selectedWindow is not { } codexWindow)
        {
            var clearTrackedWindow = _trackedCodexWindow == 0 ||
                                     !NativeMethods.IsCodexWindow(_trackedCodexWindow);
            HideOverlay(clearTrackedWindow);
            return;
        }

        _trackedCodexWindow = codexWindow.Handle;
        if (_eventHooksEnabled)
            EnsureWindowEventHooks(codexWindow.Handle);

        if (_windowTransitions.TryGetValue(codexWindow.Handle, out var transition) &&
            transition.Phase == OverlayWindowTransitionPhase.Restoring)
        {
            if (!OverlayWindowTransitionRules.IsRestoreReady(
                    transition,
                    now,
                    timing.RequiredStableSamples))
            {
                HideOverlay(clearTrackedWindow: false);
                return;
            }

            _windowTransitions.Remove(codexWindow.Handle);
        }

        ApplyPlacement(codexWindow, animateLoading);
    }

    private void ApplyPlacement(TrackedWindow codexWindow, bool animateLoading)
    {
        // Codex currently has no public API for its account-row coordinates.
        // These offsets follow the stable lower-left sidebar geometry.
        var badgeBounds = CodexWindowRules.BadgeBounds(codexWindow.Bounds, codexWindow.Scale);
        var overlayWasVisible = NativeMethods.IsWindowVisible(Handle);
        if (!overlayWasVisible)
            _overlayAlpha = byte.MinValue;

        var placementAction = BadgePlacementRules.Decide(
            _lastBadgeBounds,
            badgeBounds,
            _hasLayeredContent,
            animateLoading,
            _displayState == OverlayDisplayState.Loading);
        if (!overlayWasVisible && placementAction != BadgePlacementAction.RenderAndMove)
            placementAction = BadgePlacementAction.RenderAndMove;

        if (placementAction == BadgePlacementAction.RenderAndMove)
        {
            _hasLayeredContent = DrawLayeredBadge(badgeBounds);
            if (_hasLayeredContent)
                _lastBadgeBounds = badgeBounds;
        }

        if (!_hasLayeredContent)
        {
            HideOverlay(clearTrackedWindow: false, animate: false);
            return;
        }

        var overlayReady = overlayWasVisible;
        if (placementAction != BadgePlacementAction.None ||
            !overlayWasVisible)
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
            {
                _lastBadgeBounds = badgeBounds;
                overlayReady = true;
            }
        }

        if (!overlayReady)
        {
            HideOverlay(clearTrackedWindow: false, animate: false);
            return;
        }

        FadeOverlayTo(byte.MaxValue);
    }

    private void HandleTrackingTimerTick()
    {
        var now = Environment.TickCount64;
        if (_isMoveSizing &&
            now - _lastMovementEventTick > NormalTrackingInterval * 3L)
        {
            // MOVESIZEEND can be lost if the target HWND is destroyed or the
            // desktop switches mid-drag. A quiet-period watchdog prevents the
            // 15 ms fallback timer from becoming permanent.
            StopFastTracking();
        }

        if (_visibilityTrackingUntilTick != 0 && now >= _visibilityTrackingUntilTick)
        {
            _visibilityTrackingUntilTick = 0;
            UpdateTrackingTimerInterval();
        }

        if (_isMoveSizing && TryMoveTrackedOverlay())
            return;

        TrackPlacement(animateLoading: !_isMoveSizing);
    }

    private bool TryMoveTrackedOverlay()
    {
        var trackedWindow = _trackedCodexWindow;
        if (trackedWindow == 0 ||
            _windowTransitions.ContainsKey(trackedWindow) ||
            !_hasLayeredContent ||
            _lastBadgeBounds.IsEmpty ||
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
            var movementHooksReady =
                _moveSizeEventHook != 0 && _locationChangeEventHook != 0;
            if (movementHooksReady && _minimizeEventHook != 0)
                return;
            if (Environment.TickCount64 < _nextHookRetryTick)
                return;

            // A failed minimize hook must not tear down the working movement
            // hooks and reintroduce visible drag lag.
            if (movementHooksReady)
            {
                InstallMinimizeEventHook(processId);
                return;
            }
        }

        StopFastTracking();
        StopWindowEventHooks();
        _hookedCodexWindow = codexWindow;
        _hookedCodexProcessId = processId;

        var moveSizeHook = NativeMethods.HookCodexWindowEvents(
            CodexWindowEventRules.EventSystemMoveSizeStart,
            CodexWindowEventRules.EventSystemMoveSizeEnd,
            _winEventCallback,
            processId);
        var moveSizeError = moveSizeHook == 0 ? Marshal.GetLastWin32Error() : 0;
        var locationChangeHook = NativeMethods.HookCodexWindowEvents(
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
        InstallMinimizeEventHook(processId);
    }

    private void InstallMinimizeEventHook(uint processId)
    {
        var minimizeHook = NativeMethods.HookCodexWindowEvents(
            CodexWindowEventRules.EventSystemMinimizeStart,
            CodexWindowEventRules.EventSystemMinimizeEnd,
            _winEventCallback,
            processId);
        if (minimizeHook == 0)
        {
            Debug.WriteLine(
                "Unable to install the Codex minimize hook; placement polling fallback remains active. " +
                $"Win32 error: {Marshal.GetLastWin32Error()}");
            _nextHookRetryTick = Environment.TickCount64 + 5_000;
            return;
        }

        _minimizeEventHook = minimizeHook;
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

            var isVisibilityEvent =
                eventType is CodexWindowEventRules.EventSystemMinimizeStart or
                    CodexWindowEventRules.EventSystemMinimizeEnd;
            var isKnownVisibilityWindow =
                _windowTransitions.ContainsKey(eventWindow) ||
                (isVisibilityEvent &&
                 NativeMethods.IsEligibleCodexMainWindow(eventWindow));
            var action = CodexWindowEventRules.Classify(
                eventType,
                eventWindow,
                objectId,
                childId,
                _trackedCodexWindow,
                Handle,
                isKnownVisibilityWindow);
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
                else if (action == CodexWindowEventAction.BeginMinimize)
                    BeginMinimizeTransition(eventWindow);
                else if (action == CodexWindowEventAction.EndMinimize)
                    BeginRestoreTransition(eventWindow);
                QueuePlacementUpdate(
                    action is CodexWindowEventAction.EndMove or
                    CodexWindowEventAction.BeginMinimize or
                    CodexWindowEventAction.EndMinimize);
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

                    case CodexWindowEventAction.BeginMinimize:
                        BeginMinimizeTransition(eventWindow);
                        QueuePlacementUpdate(fullUpdate: true);
                        break;

                    case CodexWindowEventAction.EndMinimize:
                        BeginRestoreTransition(eventWindow);
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
            Debug.WriteLine($"Unable to process a Codex window event: {exception}");
            QueuePlacementUpdate(fullUpdate: true);
        }
    }

    private void StartFastTracking()
    {
        _lastMovementEventTick = Environment.TickCount64;
        _isMoveSizing = true;
        UpdateTrackingTimerInterval();
    }

    private void StopFastTracking()
    {
        _isMoveSizing = false;
        UpdateTrackingTimerInterval();
    }

    private void StartVisibilityEventTracking()
    {
        _visibilityTrackingUntilTick =
            Environment.TickCount64 + VisibilityEventTrackingDuration;
        UpdateTrackingTimerInterval();
    }

    private void BeginMinimizeTransition(nint codexWindow)
    {
        if (codexWindow == 0)
            return;

        _windowTransitions[codexWindow] =
            OverlayWindowTransitionRules.BeginMinimize(Environment.TickCount64);
        StartVisibilityEventTracking();
    }

    private void BeginRestoreTransition(nint codexWindow)
    {
        if (codexWindow == 0)
            return;

        var timing = CurrentAnimationTiming;
        _windowTransitions[codexWindow] =
            OverlayWindowTransitionRules.BeginRestore(
                Environment.TickCount64,
                timing.RestoreRevealDelayMilliseconds);
        StartVisibilityEventTracking();
    }

    private void ObserveTrackedWindowUnavailable(long now)
    {
        var trackedWindow = _trackedCodexWindow;
        if (trackedWindow == 0 ||
            _windowTransitions.ContainsKey(trackedWindow) ||
            !NativeMethods.IsCodexWindow(trackedWindow) ||
            !NativeMethods.IsWindowUnavailableForOverlay(trackedWindow))
        {
            return;
        }

        _windowTransitions[trackedWindow] =
            OverlayWindowTransitionRules.BeginMinimize(
                now,
                observedUnavailable: true);
        StartVisibilityEventTracking();
    }

    private void ReconcileWindowTransitions(
        long now,
        OverlayAnimationTiming timing)
    {
        foreach (var pair in _windowTransitions.ToArray())
        {
            var window = pair.Key;
            if (!NativeMethods.IsCodexWindow(window))
            {
                _windowTransitions.Remove(window);
                continue;
            }

            var isAvailable = NativeMethods.TryGetVisibleTrackedCodexWindow(
                window,
                out var candidate);
            var transition = pair.Value;
            OverlayWindowTransition updated;
            if (transition.Phase == OverlayWindowTransitionPhase.Minimizing)
            {
                updated = OverlayWindowTransitionRules.ObserveMinimizeState(
                    transition,
                    isAvailable,
                    now,
                    timing.MinimizeRecoveryGraceMilliseconds,
                    timing.RestoreRevealDelayMilliseconds);
            }
            else if (isAvailable)
            {
                updated = OverlayWindowTransitionRules.ObserveRestorePlacement(
                    transition,
                    candidate);
                if (OverlayWindowTransitionRules.IsRestoreReady(
                        updated,
                        now,
                        timing.RequiredStableSamples))
                {
                    _windowTransitions.Remove(window);
                    continue;
                }
            }
            else if (OverlayWindowTransitionRules.ShouldRestartMinimizeAfterRestore(
                         transition,
                         NativeMethods.IsWindowUnavailableForOverlay(window),
                         now,
                         VisibilityEventTrackingDuration))
            {
                updated = OverlayWindowTransitionRules.BeginMinimize(
                    now,
                    observedUnavailable: true);
            }
            else
            {
                continue;
            }

            _windowTransitions[window] = updated;
            if (updated.Phase == OverlayWindowTransitionPhase.Restoring &&
                transition.Phase != OverlayWindowTransitionPhase.Restoring)
            {
                StartVisibilityEventTracking();
            }
        }
    }

    private void UpdateTrackingTimerInterval()
    {
        if (_disposed)
            return;

        var useFastInterval = _isMoveSizing ||
                              (_visibilityTrackingUntilTick != 0 &&
                               Environment.TickCount64 < _visibilityTrackingUntilTick);
        var interval = useFastInterval ? MovingTrackingInterval : NormalTrackingInterval;
        if (_trackingTimer.Interval != interval)
            _trackingTimer.Interval = interval;
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
        if (_minimizeEventHook != 0)
            _ = NativeMethods.UnhookWinEvent(_minimizeEventHook);

        _moveSizeEventHook = 0;
        _locationChangeEventHook = 0;
        _minimizeEventHook = 0;
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
            HideOverlay(clearTrackedWindow: false, animate: false);
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
                badgeBounds.Location,
                _overlayAlpha);
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
            HideOverlay(clearTrackedWindow: false, animate: false);
        }
    }

    private void FadeOverlayTo(byte targetAlpha)
    {
        var now = Environment.TickCount64;
        var timing = CurrentAnimationTiming;
        var duration = targetAlpha == byte.MinValue
            ? timing.FadeOutMilliseconds
            : timing.FadeInMilliseconds;
        var curve = targetAlpha == byte.MinValue
            ? OverlayFadeCurve.FastOut
            : OverlayFadeCurve.SmoothStep;
        OverlayFadeTransition transition;
        if (_fadeTransition is { } currentTransition)
        {
            if (currentTransition.TargetAlpha == targetAlpha)
                return;

            // Reverse from the last alpha that was successfully drawn. Sampling
            // between frames can otherwise create a small visible jump.
            transition = OverlayFadeRules.Retarget(
                currentTransition,
                _overlayAlpha,
                targetAlpha,
                now,
                duration,
                curve);
        }
        else
        {
            transition = OverlayFadeRules.Start(
                _overlayAlpha,
                targetAlpha,
                now,
                duration,
                curve);
        }

        if (transition.DurationMilliseconds <= 0)
        {
            StopFadeAnimation();
            _overlayAlpha = targetAlpha;
            if (targetAlpha == byte.MinValue)
            {
                CompleteOverlayHide();
            }
            else if (_hasLayeredContent &&
                     !_lastBadgeBounds.IsEmpty &&
                     !DrawLayeredBadge(_lastBadgeBounds))
            {
                HideOverlay(clearTrackedWindow: false, animate: false);
            }
            return;
        }

        _fadeTransition = transition;
        _fadeTimer.Start();
    }

    private void HandleFadeTimerTick()
    {
        if (_fadeTransition is not { } transition)
        {
            _fadeTimer.Stop();
            return;
        }

        var sample = OverlayFadeRules.Sample(transition, Environment.TickCount64);
        _overlayAlpha = sample.Alpha;
        if (!_hasLayeredContent ||
            _lastBadgeBounds.IsEmpty ||
            !DrawLayeredBadge(_lastBadgeBounds))
        {
            HideOverlay(clearTrackedWindow: false, animate: false);
            return;
        }

        if (!sample.IsComplete)
            return;

        _fadeTimer.Stop();
        _fadeTransition = null;
        if (transition.TargetAlpha == byte.MinValue)
            CompleteOverlayHide();
    }

    private void StopFadeAnimation()
    {
        _fadeTimer.Stop();
        _fadeTransition = null;
    }

    private void HideOverlay(bool clearTrackedWindow, bool animate = true)
    {
        StopFastTracking();
        if (clearTrackedWindow)
        {
            _trackedCodexWindow = 0;
            StopWindowEventHooks();
        }

        if (animate &&
            IsHandleCreated &&
            NativeMethods.IsWindowVisible(Handle) &&
            _hasLayeredContent &&
            !_lastBadgeBounds.IsEmpty)
        {
            FadeOverlayTo(byte.MinValue);
            return;
        }

        CompleteOverlayHide();
    }

    private void CompleteOverlayHide()
    {
        StopFadeAnimation();
        _overlayAlpha = byte.MinValue;
        _lastBadgeBounds = Rectangle.Empty;
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
            _fadeTimer.Stop();
            _windowTransitions.Clear();
            StopWindowEventHooks();
            if (_winEventCallbackHandle.IsAllocated)
                _winEventCallbackHandle.Free();
            _trackingTimer.Dispose();
            _fadeTimer.Dispose();
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
