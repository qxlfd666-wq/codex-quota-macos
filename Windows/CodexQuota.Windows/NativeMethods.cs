using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodexQuota.Windows;

internal static partial class NativeMethods
{
    private const int DwmwaExtendedFrameBounds = 9;
    internal const int SwHide = 0;
    internal const int SwShowNoActivate = 4;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpShowWindow = 0x0040;
    internal static readonly nint HwndTopmost = new(-1);

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
    internal static partial uint GetDpiForWindow(nint window);

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

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(
        nint window,
        int attribute,
        out NativeRect value,
        int valueSize);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(nint icon);

    internal static bool TryGetCodexForegroundBounds(out Rectangle bounds, out float scale)
    {
        bounds = Rectangle.Empty;
        scale = 1f;
        var window = GetForegroundWindow();
        if (window == 0 || !IsWindowVisible(window) || IsIconic(window))
            return false;

        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
            return false;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;
            var isCodex = processName.Contains("codex", StringComparison.OrdinalIgnoreCase) ||
                          processName.Contains("chatgpt", StringComparison.OrdinalIgnoreCase);
            if (!isCodex)
                return false;
        }
        catch (Exception)
        {
            return false;
        }

        NativeRect rectangle;
        if (DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, out rectangle, Marshal.SizeOf<NativeRect>()) != 0 &&
            !GetWindowRect(window, out rectangle))
            return false;

        bounds = Rectangle.FromLTRB(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
        var dpi = GetDpiForWindow(window);
        if (dpi > 0)
            scale = Math.Clamp(dpi / 96f, 1f, 3f);
        return bounds.Width >= 700 && bounds.Height >= 500;
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
