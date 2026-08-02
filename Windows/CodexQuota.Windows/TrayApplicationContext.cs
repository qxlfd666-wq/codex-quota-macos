using CodexQuota.Core;
using CodexQuota.Windows.Services;
using Microsoft.Win32;

namespace CodexQuota.Windows;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly CodexAppServerClient _client = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly OverlayForm _overlay = new();
    private readonly NotifyIcon _notifyIcon;
    private Icon? _trayIcon;
    private readonly ToolStripMenuItem _quotaItem;
    private readonly ToolStripMenuItem _detailItem;
    private readonly ToolStripMenuItem _copyDiagnosticItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _initialRefreshTimer;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly bool _activateRunningCodex;
    private AppSettings _settings;
    private nint _codexWindowBeforeTrayInteraction;
    private int _refreshing;

    public TrayApplicationContext(bool activateRunningCodex)
    {
        _activateRunningCodex = activateRunningCodex;
        try
        {
            StartupManager.MigrateLegacyEntry();
        }
        catch (Exception)
        {
            // Startup registration is optional and must not prevent the tray app
            // from running if policy blocks registry writes.
        }
        _settings = _settingsStore.Load();
        if (!AppSettingsStore.TryParseColor(_settings.BadgeColor, out var color))
            color = Color.FromArgb(255, 59, 48);
        _overlay.SetAccentColor(color);
        _overlay.ChooseColorRequested += (_, _) => ChooseColor(_overlay.TrackedCodexWindow);

        var menu = new ContextMenuStrip();
        _quotaItem = new ToolStripMenuItem("正在读取 Codex 额度…") { Enabled = false };
        _detailItem = new ToolStripMenuItem("请稍候") { Enabled = false };
        _copyDiagnosticItem = new ToolStripMenuItem("复制诊断信息") { Enabled = false };
        _copyDiagnosticItem.Click += (_, _) => CopyDiagnostic();
        menu.Items.Add(_quotaItem);
        menu.Items.Add(_detailItem);
        menu.Items.Add(_copyDiagnosticItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("刷新额度", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("自定义颜色…", null, (_, _) =>
            ChooseColor(_codexWindowBeforeTrayInteraction));
        _startupItem = new ToolStripMenuItem("开机自动启动")
        {
            Checked = StartupManager.IsEnabled,
            CheckOnClick = false
        };
        _startupItem.Click += (_, _) => ToggleStartup();
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出额度徽标", null, (_, _) => ExitThread());

        _trayIcon = TrayIconRenderer.CreateForSystemTray();
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _trayIcon,
            Text = "Codex 剩余额度",
            Visible = true
        };
        SystemEvents.DisplaySettingsChanged += SystemDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += SystemUserPreferenceChanged;
        _notifyIcon.MouseDown += (_, _) =>
        {
            _codexWindowBeforeTrayInteraction =
                NativeMethods.TryGetCodexForegroundWindow(out var codexWindow)
                    ? codexWindow.Handle
                    : 0;
        };
        _notifyIcon.DoubleClick += async (_, _) => await RefreshAsync();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refreshTimer.Start();

        _initialRefreshTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _initialRefreshTimer.Tick += InitialRefresh;
        _initialRefreshTimer.Start();
        _overlay.StartTracking();
    }

    private async void InitialRefresh(object? sender, EventArgs eventArgs)
    {
        _initialRefreshTimer.Stop();
        if (_activateRunningCodex && NativeMethods.TryActivateVisibleCodexWindow())
            _overlay.RefreshPlacement();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) != 0)
            return;

        _quotaItem.Text = "正在读取 Codex 额度…";
        _detailItem.Text = "请稍候";
        _overlay.SetLoading();
        try
        {
            var snapshot = await _client.FetchSnapshotAsync(_lifetimeCancellation.Token);
            ShowSnapshot(snapshot);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _quotaItem.Text = "暂时无法读取额度";
            _detailItem.Text = Shorten(exception.Message, 90);
            _notifyIcon.Text = Shorten("Codex 额度：" + exception.Message, 63);
            _copyDiagnosticItem.Enabled = true;
            _overlay.SetError(exception.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private void ShowSnapshot(QuotaSnapshot snapshot)
    {
        _overlay.SetQuota(snapshot.RemainingPercent);
        _quotaItem.Text = $"Codex 剩余 {snapshot.RemainingPercent}%";
        _detailItem.Text = $"{snapshot.PlanName} · {snapshot.FetchedAt:HH:mm} 更新";
        _notifyIcon.Text = $"Codex 剩余 {snapshot.RemainingPercent}%";
        _copyDiagnosticItem.Enabled = true;
    }

    private void CopyDiagnostic()
    {
        try
        {
            Clipboard.SetText(_client.LastDiagnostic);
            _notifyIcon.ShowBalloonTip(1_500, "Codex Quota", "诊断信息已复制；发送前请检查内容。", ToolTipIcon.Info);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"无法复制诊断信息：{exception.Message}",
                "Codex Quota",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void ChooseColor(nint codexWindow)
    {
        try
        {
            AppSettingsStore.TryParseColor(_settings.BadgeColor, out var currentColor);
            using var dialog = new ColorDialog
            {
                AllowFullOpen = true,
                AnyColor = true,
                FullOpen = true,
                Color = currentColor.IsEmpty ? Color.Red : currentColor
            };
            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            _settings = _settings with { BadgeColor = AppSettingsStore.SerializeColor(dialog.Color) };
            _overlay.SetAccentColor(dialog.Color);
            try
            {
                _settingsStore.Save(_settings);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"无法保存颜色：{exception.Message}",
                    "Codex Quota",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            if (codexWindow != 0 && NativeMethods.TryActivateCodexWindow(codexWindow))
                _overlay.RefreshPlacement();
            _codexWindowBeforeTrayInteraction = 0;
        }
    }

    private void ToggleStartup()
    {
        try
        {
            StartupManager.SetEnabled(!StartupManager.IsEnabled);
            _startupItem.Checked = StartupManager.IsEnabled;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"无法更改开机启动设置：{exception.Message}",
                "Codex Quota",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SystemDisplaySettingsChanged(object? sender, EventArgs eventArgs) =>
        QueueTrayIconRefresh();

    private void SystemUserPreferenceChanged(
        object sender,
        UserPreferenceChangedEventArgs eventArgs) =>
        QueueTrayIconRefresh();

    private void QueueTrayIconRefresh()
    {
        if (_lifetimeCancellation.IsCancellationRequested ||
            !_overlay.IsHandleCreated ||
            _overlay.IsDisposed)
            return;

        try
        {
            _overlay.BeginInvoke(RefreshTrayIcon);
        }
        catch (InvalidOperationException)
        {
            // The application is already shutting down.
        }
    }

    private void RefreshTrayIcon()
    {
        if (_lifetimeCancellation.IsCancellationRequested)
            return;

        try
        {
            var nextIcon = TrayIconRenderer.CreateForSystemTray();
            var previousIcon = _trayIcon;
            _trayIcon = nextIcon;
            _notifyIcon.Icon = nextIcon;
            previousIcon?.Dispose();
        }
        catch (Exception)
        {
            // Keep the current icon if Windows cannot recreate it while display
            // or accessibility settings are in transition.
        }
    }

    private static string Shorten(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    protected override void ExitThreadCore()
    {
        _lifetimeCancellation.Cancel();
        _initialRefreshTimer.Stop();
        _refreshTimer.Stop();
        SystemEvents.DisplaySettingsChanged -= SystemDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= SystemUserPreferenceChanged;
        _overlay.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Icon = null;
        _notifyIcon.Dispose();
        _trayIcon?.Dispose();
        _initialRefreshTimer.Dispose();
        _refreshTimer.Dispose();
        base.ExitThreadCore();
    }
}
