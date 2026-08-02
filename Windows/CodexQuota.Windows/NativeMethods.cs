using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodexQuota.Windows;

internal static partial class NativeMethods
{
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;
    private const uint GaRootOwner = 3;
    private const byte AcSrcOver = 0;
    private const byte AcSrcAlpha = 1;
    private const uint UlwAlpha = 0x00000002;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;
    internal const int SwHide = 0;
    internal const int SwShowNoActivate = 4;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpShowWindow = 0x0040;
    internal static readonly nint HwndTopmost = new(-1);

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void WinEventCallback(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint window, out NativeRect rectangle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial nint GetAncestor(nint window, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint module,
        [MarshalAs(UnmanagedType.FunctionPtr)] WinEventCallback callback,
        uint processId,
        uint threadId,
        uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWinEvent(nint hook);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(
        [MarshalAs(UnmanagedType.FunctionPtr)] EnumWindowsCallback callback,
        nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint window, nint deviceContext);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint deviceContext);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint deviceContext);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint graphicObject);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint deviceContext, nint graphicObject);

    [LibraryImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateLayeredWindowCore(
        nint window,
        nint destinationDeviceContext,
        ref NativePoint destination,
        ref NativeSize size,
        nint sourceDeviceContext,
        ref NativePoint source,
        uint colorKey,
        ref NativeBlendFunction blend,
        uint flags);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(
        nint window,
        int attribute,
        out NativeRect value,
        int valueSize);

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static partial int DwmGetWindowAttributeInt32(
        nint window,
        int attribute,
        out int value,
        int valueSize);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(nint icon);

    internal static bool TryUpdateLayeredWindow(
        nint window,
        Bitmap bitmap,
        Point destination)
    {
        if (window == 0 || bitmap.Width <= 0 || bitmap.Height <= 0)
            return false;

        var screenDeviceContext = GetDC(0);
        if (screenDeviceContext == 0)
            return false;

        nint memoryDeviceContext = 0;
        nint bitmapHandle = 0;
        nint previousObject = 0;
        try
        {
            memoryDeviceContext = CreateCompatibleDC(screenDeviceContext);
            if (memoryDeviceContext == 0)
                return false;

            bitmapHandle = CreateLayeredBitmapHandle(bitmap);
            if (bitmapHandle == 0)
                return false;

            previousObject = SelectObject(memoryDeviceContext, bitmapHandle);
            if (previousObject == 0 || previousObject == new nint(-1))
                return false;

            var nativeDestination = new NativePoint(destination.X, destination.Y);
            var nativeSize = new NativeSize(bitmap.Width, bitmap.Height);
            var nativeSource = new NativePoint(0, 0);
            var blend = new NativeBlendFunction
            {
                BlendOp = AcSrcOver,
                SourceConstantAlpha = byte.MaxValue,
                AlphaFormat = AcSrcAlpha
            };
            return UpdateLayeredWindowCore(
                window,
                screenDeviceContext,
                ref nativeDestination,
                ref nativeSize,
                memoryDeviceContext,
                ref nativeSource,
                0,
                ref blend,
                UlwAlpha);
        }
        finally
        {
            if (previousObject != 0 && previousObject != new nint(-1))
                _ = SelectObject(memoryDeviceContext, previousObject);
            if (bitmapHandle != 0)
                _ = DeleteObject(bitmapHandle);
            if (memoryDeviceContext != 0)
                _ = DeleteDC(memoryDeviceContext);
            _ = ReleaseDC(0, screenDeviceContext);
        }
    }

    internal static nint CreateLayeredBitmapHandle(Bitmap bitmap)
    {
        // Named Color.Transparent carries white RGB channels. GDI+ would blend
        // translucent pixels against that white before creating the HBITMAP,
        // which violates UpdateLayeredWindow's premultiplied-alpha contract
        // and produces a bright fringe. An all-zero background preserves PArgb.
        return bitmap.GetHbitmap(Color.FromArgb(0, 0, 0, 0));
    }

    internal static bool TryGetCodexForegroundWindow(out TrackedWindow mainWindow)
    {
        mainWindow = default;
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == 0 || !IsWindowVisible(foregroundWindow))
            return false;

        GetWindowThreadProcessId(foregroundWindow, out var processId);
        if (!IsSupportedCodexProcess(processId))
            return false;

        // Prefer the foreground window's root owner. When a small owned popup is
        // focused this keeps the badge on its main Codex window.
        var candidates = new List<TrackedWindow>();
        var rootOwner = GetAncestor(foregroundWindow, GaRootOwner);
        if (TryCreateCandidate(rootOwner, processId, out var ownerCandidate))
            candidates.Add(ownerCandidate);

        // EnumWindows preserves top-to-bottom Z order. Keeping that order avoids
        // selecting a larger background Codex window when several are open.
        EnumWindowsCallback callback = (window, _) =>
        {
            if (window != rootOwner && TryCreateCandidate(window, processId, out var candidate))
                candidates.Add(candidate);
            return true;
        };
        _ = EnumWindows(callback, 0);

        var selected = CodexWindowRules.SelectMainWindow(candidates);
        if (selected is not { } selectedWindow)
            return false;

        mainWindow = selectedWindow;
        return true;
    }

    internal static bool TryGetCodexForegroundBounds(out Rectangle bounds, out float scale)
    {
        if (TryGetCodexForegroundWindow(out var mainWindow))
        {
            bounds = mainWindow.Bounds;
            scale = mainWindow.Scale;
            return true;
        }

        bounds = Rectangle.Empty;
        scale = 1f;
        return false;
    }

    internal static bool TryActivateVisibleCodexWindow()
    {
        TrackedWindow? selected = null;
        EnumWindowsCallback callback = (window, _) =>
        {
            if (!TryCreateCodexCandidate(window, out var candidate) ||
                !CodexWindowRules.IsEligibleMainWindow(candidate.Bounds, candidate.Scale))
                return true;

            selected = candidate;
            return false;
        };
        _ = EnumWindows(callback, 0);

        return selected is { } candidate && TryActivateCodexWindow(candidate.Handle);
    }

    internal static bool TryActivateCodexWindow(nint window)
    {
        if (!IsWindow(window) ||
            !TryCreateCodexCandidate(window, out var candidate) ||
            !CodexWindowRules.IsEligibleMainWindow(candidate.Bounds, candidate.Scale))
            return false;

        return GetForegroundWindow() == window || SetForegroundWindow(window);
    }

    internal static bool TryGetTrackedCodexWindow(
        nint window,
        uint expectedProcessId,
        out TrackedWindow candidate) =>
        TryCreateCandidate(window, expectedProcessId, out candidate) &&
        CodexWindowRules.IsEligibleMainWindow(candidate.Bounds, candidate.Scale);

    internal static bool IsForegroundWindowOrOwnedBy(nint window)
    {
        if (window == 0)
            return false;

        var foregroundWindow = GetForegroundWindow();
        return foregroundWindow == window ||
               (foregroundWindow != 0 && GetAncestor(foregroundWindow, GaRootOwner) == window);
    }

    internal static nint HookCodexWindowMoveEvents(
        uint eventMin,
        uint eventMax,
        WinEventCallback callback,
        uint processId) =>
        SetWinEventHook(
            eventMin,
            eventMax,
            0,
            callback,
            processId,
            0,
            WineventOutOfContext | WineventSkipOwnProcess);

    private static bool TryCreateCodexCandidate(nint window, out TrackedWindow candidate)
    {
        candidate = default;
        if (window == 0)
            return false;

        GetWindowThreadProcessId(window, out var processId);
        return IsSupportedCodexProcess(processId) &&
               TryCreateCandidate(window, processId, out candidate);
    }

    private static bool IsSupportedCodexProcess(uint processId)
    {
        if (processId == 0)
            return false;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return CodexWindowRules.IsSupportedForegroundProcess(
                process.ProcessName,
                process.Id,
                Environment.ProcessId);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryCreateCandidate(
        nint window,
        uint expectedProcessId,
        out TrackedWindow candidate)
    {
        candidate = default;
        if (window == 0 || !IsWindowVisible(window) || IsIconic(window) || IsWindowCloaked(window))
            return false;

        GetWindowThreadProcessId(window, out var processId);
        if (processId != expectedProcessId || !TryGetWindowBounds(window, out var bounds))
            return false;

        candidate = new TrackedWindow(window, bounds, GetWindowScale(window));
        return true;
    }

    private static bool IsWindowCloaked(nint window) =>
        DwmGetWindowAttributeInt32(
            window,
            DwmwaCloaked,
            out var cloaked,
            sizeof(int)) == 0 &&
        cloaked != 0;

    private static bool TryGetWindowBounds(nint window, out Rectangle bounds)
    {
        NativeRect rectangle;
        if (DwmGetWindowAttribute(
                window,
                DwmwaExtendedFrameBounds,
                out rectangle,
                Marshal.SizeOf<NativeRect>()) != 0 &&
            !GetWindowRect(window, out rectangle))
        {
            bounds = Rectangle.Empty;
            return false;
        }

        bounds = Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static float GetWindowScale(nint window)
    {
        var dpi = GetDpiForWindow(window);
        return dpi > 0 ? CodexWindowRules.NormalizeScale(dpi / 96f) : 1f;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint(int x, int y)
{
    public int X = x;
    public int Y = y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeSize(int width, int height)
{
    public int Width = width;
    public int Height = height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeBlendFunction
{
    public byte BlendOp;
    public byte BlendFlags;
    public byte SourceConstantAlpha;
    public byte AlphaFormat;
}
