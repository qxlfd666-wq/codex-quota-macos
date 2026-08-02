namespace CodexQuota.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(argument =>
                string.Equals(argument, "--self-test", StringComparison.OrdinalIgnoreCase)))
            return RunSelfTest();
        if (args.Any(argument =>
                string.Equals(argument, "--check-quota", StringComparison.OrdinalIgnoreCase)))
            return RunQuotaCheck();

        var backgroundStart = args.Any(argument =>
            string.Equals(argument, "--background-start", StringComparison.OrdinalIgnoreCase));
        using var singleInstance = new Mutex(
            initiallyOwned: true,
            name: @"Local\CodexQuota.Windows",
            createdNew: out var isFirstInstance);
        if (!isFirstInstance)
        {
            if (!backgroundStart)
                _ = NativeMethods.TryActivateVisibleCodexWindow();
            return 0;
        }

        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new TrayApplicationContext(activateRunningCodex: !backgroundStart));
        }
        finally
        {
            singleInstance.ReleaseMutex();
        }

        return 0;
    }

    private static int RunSelfTest()
    {
        if (!OperatingSystem.IsWindows())
            return 10;
        if (!Services.AppSettingsStore.TryParseColor("#FF3B30", out var color) || color.R != 255)
            return 11;

        var badge = CodexWindowRules.BadgeBounds(new Rectangle(0, 0, 1600, 1000), 1.25f);
        if (badge.Width != 80 || badge.Height != 35)
            return 12;

        try
        {
            _ = NativeMethods.TryGetCodexForegroundBounds(out _, out _);

            using var preview = OverlayRenderer.Render(
                new Size(CodexWindowRules.BadgeWidth, CodexWindowRules.BadgeHeight),
                Color.FromArgb(255, 59, 48),
                21,
                OverlayDisplayState.Quota,
                0);
            if (preview.GetPixel(0, 0).A != 0 || preview.GetPixel(10, 14).A >= byte.MaxValue)
                return 14;

            using var overlay = new OverlayForm();
            if (!NativeMethods.TryUpdateLayeredWindow(
                    overlay.Handle,
                    preview,
                    new Point(-32_000, -32_000)))
                return 15;

            using var trayIcon = TrayIconRenderer.CreateForSystemTray(
                TrayIconContent.Quota(21),
                Color.FromArgb(255, 59, 48));
            if (trayIcon.Width is < 16 or > 32 || trayIcon.Height is < 16 or > 32)
                return 16;

            using var shareCard = ShareCardRenderer.Render(
                new ShareCardData(21, DateTimeOffset.UtcNow),
                Color.FromArgb(255, 59, 48));
            if (shareCard.Size != ShareCardRenderer.CardSize)
                return 17;
        }
        catch
        {
            return 13;
        }

        return 0;
    }

    private static int RunQuotaCheck()
    {
        var client = new Services.CodexAppServerClient();
        try
        {
            var snapshot = client.FetchSnapshotAsync().GetAwaiter().GetResult();
            if (snapshot.RemainingPercent is < 0 or > 100)
                return 21;

            Console.WriteLine("Codex quota check succeeded.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(client.LastDiagnostic);
            return 20;
        }
    }
}
