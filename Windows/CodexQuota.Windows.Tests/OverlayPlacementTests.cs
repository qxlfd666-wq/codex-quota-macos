using System.Drawing;
using System.Runtime.InteropServices;
using Xunit;

namespace CodexQuota.Windows.Tests;

public sealed class OverlayPlacementTests
{
    [Theory]
    [InlineData(0x000A, 101, 0, 0, 101, 900, (int)CodexWindowEventAction.BeginMove)]
    [InlineData(0x000B, 101, 0, 0, 101, 900, (int)CodexWindowEventAction.EndMove)]
    [InlineData(0x0016, 101, 0, 0, 101, 900, (int)CodexWindowEventAction.BeginMinimize)]
    [InlineData(0x0017, 101, 0, 0, 101, 900, (int)CodexWindowEventAction.EndMinimize)]
    [InlineData(0x800B, 101, 0, 0, 101, 900, (int)CodexWindowEventAction.Move)]
    [InlineData(0x800B, 101, -4, 0, 101, 900, (int)CodexWindowEventAction.Ignore)]
    [InlineData(0x800B, 101, 0, 1, 101, 900, (int)CodexWindowEventAction.Ignore)]
    [InlineData(0x800B, 202, 0, 0, 101, 900, (int)CodexWindowEventAction.Ignore)]
    [InlineData(0x800B, 900, 0, 0, 900, 900, (int)CodexWindowEventAction.Ignore)]
    [InlineData(0x800B, 101, 0, 0, 0, 900, (int)CodexWindowEventAction.Ignore)]
    public void WindowEventsOnlyAffectTheCurrentTopLevelCodexWindow(
        uint eventType,
        long eventWindow,
        int objectId,
        int childId,
        long trackedWindow,
        long overlayWindow,
        int expected)
    {
        var action = CodexWindowEventRules.Classify(
            eventType,
            (nint)eventWindow,
            objectId,
            childId,
            (nint)trackedWindow,
            (nint)overlayWindow);

        Assert.Equal((CodexWindowEventAction)expected, action);
    }

    [Fact]
    public void MinimizeEndCanCompleteAPendingNonTrackedWindow()
    {
        var action = CodexWindowEventRules.Classify(
            CodexWindowEventRules.EventSystemMinimizeEnd,
            eventWindow: (nint)202,
            objectId: 0,
            childId: 0,
            trackedWindow: (nint)101,
            overlayWindow: (nint)900,
            isKnownVisibilityWindow: true);

        Assert.Equal(CodexWindowEventAction.EndMinimize, action);
    }

    [Fact]
    public void MinimizeStartCanTrackAnotherKnownCodexWindow()
    {
        var action = CodexWindowEventRules.Classify(
            CodexWindowEventRules.EventSystemMinimizeStart,
            eventWindow: (nint)202,
            objectId: 0,
            childId: 0,
            trackedWindow: (nint)101,
            overlayWindow: (nint)900,
            isKnownVisibilityWindow: true);

        Assert.Equal(CodexWindowEventAction.BeginMinimize, action);
    }

    [Fact]
    public void PendingWindowDoesNotReceiveUnrelatedMovementEvents()
    {
        var action = CodexWindowEventRules.Classify(
            CodexWindowEventRules.EventObjectLocationChange,
            eventWindow: (nint)202,
            objectId: 0,
            childId: 0,
            trackedWindow: (nint)101,
            overlayWindow: (nint)900,
            isKnownVisibilityWindow: true);

        Assert.Equal(CodexWindowEventAction.Ignore, action);
    }

    [Theory]
    [InlineData("Codex")]
    [InlineData("codex")]
    [InlineData("ChatGPT")]
    [InlineData("chatgpt")]
    public void RecognizesOnlyExactOfficialDesktopProcessNames(string processName)
    {
        Assert.True(CodexWindowRules.IsSupportedForegroundProcess(processName, 42, 7));
        Assert.False(CodexWindowRules.IsSupportedForegroundProcess("CodexQuota", 42, 7));
        Assert.False(CodexWindowRules.IsSupportedForegroundProcess("codex-code-mode-host", 42, 7));
        Assert.False(CodexWindowRules.IsSupportedForegroundProcess(processName, 7, 7));
    }

    [Fact]
    public void SelectsFirstEligibleWindowInZOrder()
    {
        var candidates = new[]
        {
            new TrackedWindow((nint)1, new Rectangle(0, 0, 900, 105), 1.25f),
            new TrackedWindow((nint)2, new Rectangle(20, 20, 1000, 700), 1.25f),
            new TrackedWindow((nint)3, new Rectangle(100, 100, 1600, 1000), 1.25f)
        };

        var selected = CodexWindowRules.SelectMainWindow(candidates);

        Assert.NotNull(selected);
        Assert.Equal((nint)2, selected.Value.Handle);
    }

    [Fact]
    public void WindowThresholdUsesLogicalPixelsAtHighDpi()
    {
        Assert.True(CodexWindowRules.IsEligibleMainWindow(new Rectangle(0, 0, 1400, 1000), 2f));
        Assert.False(CodexWindowRules.IsEligibleMainWindow(new Rectangle(0, 0, 1398, 998), 2f));
    }

    [Fact]
    public void BadgeBoundsScaleWithoutArtificialThreeHundredPercentLimit()
    {
        var codexBounds = new Rectangle(10, 20, 4000, 3000);

        var badge = CodexWindowRules.BadgeBounds(codexBounds, 4f);

        Assert.Equal(new Rectangle(282, 2872, 256, 112), badge);
    }

    [Fact]
    public void ContinuousMovementKeepsBadgeLockedToTheSameWindowOffset()
    {
        const float scale = 1.25f;
        var expectedSize = new Size(80, 35);

        for (var step = 0; step < 100; step++)
        {
            var codexBounds = new Rectangle(-1300 + step * 7, 40 + step * 3, 1280, 840);
            var badge = CodexWindowRules.BadgeBounds(codexBounds, scale);

            Assert.Equal(expectedSize, badge.Size);
            Assert.Equal((int)Math.Round(68 * scale), badge.Left - codexBounds.Left);
            Assert.InRange(
                Math.Abs((codexBounds.Bottom - badge.Top - badge.Height / 2f) - 23 * scale),
                0,
                0.5f);
        }
    }

    [Theory]
    [InlineData(1.5f)]
    [InlineData(2.5f)]
    public void HalfPixelVerticalOffsetsDoNotJitterWhileWindowMoves(float scale)
    {
        int? previousTop = null;
        for (var bottom = -1005; bottom <= -995; bottom++)
        {
            var codexBounds = Rectangle.FromLTRB(-1800, bottom - 900, -600, bottom);
            var badge = CodexWindowRules.BadgeBounds(codexBounds, scale);

            if (previousTop.HasValue)
                Assert.Equal(previousTop.Value + 1, badge.Top);
            previousTop = badge.Top;
        }
    }

    [Fact]
    public void LoadingMovementUsesMoveOnlyPathUntilRenderingIsActuallyRequired()
    {
        var previous = new Rectangle(100, 200, 80, 35);

        for (var step = 1; step <= 100; step++)
        {
            var next = new Rectangle(100 + step * 4, 200 + step * 2, 80, 35);
            Assert.Equal(
                BadgePlacementAction.MoveOnly,
                BadgePlacementRules.Decide(
                    previous,
                    next,
                    hasLayeredContent: true,
                    animateLoading: false,
                    isLoading: true));
            previous = next;
        }

        Assert.Equal(
            BadgePlacementAction.None,
            BadgePlacementRules.Decide(
                previous,
                previous,
                hasLayeredContent: true,
                animateLoading: false,
                isLoading: true));
        Assert.Equal(
            BadgePlacementAction.RenderAndMove,
            BadgePlacementRules.Decide(
                previous,
                previous,
                hasLayeredContent: true,
                animateLoading: true,
                isLoading: true));
        Assert.Equal(
            BadgePlacementAction.RenderAndMove,
            BadgePlacementRules.Decide(
                previous,
                new Rectangle(previous.X, previous.Y, 96, 42),
                hasLayeredContent: true,
                animateLoading: false,
                isLoading: true));
    }

    [Fact]
    public void RenderedBadgeUsesSmoothPerPixelAlphaWithoutOpaqueBackdrop()
    {
        using var bitmap = OverlayRenderer.Render(
            new Size(CodexWindowRules.BadgeWidth, CodexWindowRules.BadgeHeight),
            Color.FromArgb(255, 59, 48),
            21,
            OverlayDisplayState.Quota,
            0);

        Assert.Equal(0, bitmap.GetPixel(0, 0).A);
        Assert.InRange(bitmap.GetPixel(10, 14).A, 18, 30);

        var alphaValues = Enumerable.Range(0, bitmap.Height)
            .SelectMany(y => Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, y).A))
            .ToArray();
        Assert.Contains(alphaValues, alpha => alpha is > 0 and < 23);
        Assert.DoesNotContain(byte.MaxValue, alphaValues);
    }

    [Fact]
    public void NativeLayeredBitmapPixelsArePremultipliedWithoutTransparentRgbFringe()
    {
        using var bitmap = OverlayRenderer.Render(
            new Size(CodexWindowRules.BadgeWidth, CodexWindowRules.BadgeHeight),
            Color.FromArgb(255, 59, 48),
            21,
            OverlayDisplayState.Quota,
            0);
        var bitmapHandle = NativeMethods.CreateLayeredBitmapHandle(bitmap);
        Assert.NotEqual(nint.Zero, bitmapHandle);

        var deviceContext = CreateCompatibleDC(0);
        try
        {
            Assert.NotEqual(nint.Zero, deviceContext);
            var header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = bitmap.Width,
                Height = -bitmap.Height,
                Planes = 1,
                BitCount = 32,
                SizeImage = checked((uint)(bitmap.Width * bitmap.Height * 4))
            };
            var pixels = new byte[header.SizeImage];

            Assert.Equal(
                bitmap.Height,
                GetDIBits(
                    deviceContext,
                    bitmapHandle,
                    0,
                    (uint)bitmap.Height,
                    pixels,
                    ref header,
                    0));

            for (var index = 0; index < pixels.Length; index += 4)
            {
                var blue = pixels[index];
                var green = pixels[index + 1];
                var red = pixels[index + 2];
                var alpha = pixels[index + 3];
                Assert.True(
                    blue <= alpha && green <= alpha && red <= alpha,
                    $"Pixel {index / 4} is not premultiplied: BGRA={blue},{green},{red},{alpha}");
                if (alpha == 0)
                    Assert.Equal((0, 0, 0), (blue, green, red));
            }
        }
        finally
        {
            if (deviceContext != 0)
                _ = DeleteDC(deviceContext);
            _ = DeleteObject(bitmapHandle);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint deviceContext);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint deviceContext);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint graphicObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        nint deviceContext,
        nint bitmap,
        uint startScan,
        uint scanLineCount,
        [Out] byte[] pixels,
        ref BitmapInfoHeader bitmapInfo,
        uint usage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

}
