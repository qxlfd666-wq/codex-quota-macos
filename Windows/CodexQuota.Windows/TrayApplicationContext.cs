using System.Drawing.Drawing2D;
using CodexQuota.Core;
using CodexQuota.Windows.Services;

namespace CodexQuota.Windows;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly CodexAppServerClient _client = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly OverlayForm _overlay = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _quotaItem;
    private readonly ToolStripMenuItem _detailItem;
    private readonly ToolStripMenuItem _copyDiagnosticItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _initialRefreshTimer;
    private AppSettings _settings;
    private int _refreshing;

    public TrayApplicationContext()
    {
        _settings = _settingsStore.Load();
        if (!AppSettingsStore.TryParseColor(_settings.BadgeColor, out var color))
            color = Color.FromArgb(255, 59, 48);
        _overlay.SetAccentColor(color);
        _overlay.ChooseColorRequested += (_, _) => ChooseColor();

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
        menu.Items.Add("自定义颜色…", null, (_, _) => ChooseColor());
        _startupItem = new ToolStripMenuItem("开机自动启动")
        {
            Checked = StartupManager.IsEnabled,
            CheckOnClick = false
        };
        _startupItem.Click += (_, _) => ToggleStartup();
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出额度徽标", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = CreateTrayIcon(),
            Text = "Codex 剩余额度",
            Visible = true
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
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) != 0)
            return;

        _quotaItem.Text = "正在读取 Codex 额度…";
        _detailItem.Text = "请稍候";
        try
        {
            var snapshot = await _client.FetchSnapshotAsync();
            ShowSnapshot(snapshot);
        }
        catch (Exception exception)
        {
            _quotaItem.Text = "暂时无法读取额度";
            _detailItem.Text = Shorten(exception.Message, 90);
            _notifyIcon.Text = Shorten("Codex 额度：" + exception.Message, 63);
            _copyDiagnosticItem.Enabled = true;
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

    private void ChooseColor()
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

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var background = new SolidBrush(Color.FromArgb(255, 59, 48));
        graphics.FillEllipse(background, 1, 1, 30, 30);
        using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(
            graphics,
            "%",
            font,
            new Rectangle(1, 1, 30, 30),
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static string Shorten(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    protected override void ExitThreadCore()
    {
        _initialRefreshTimer.Stop();
        _refreshTimer.Stop();
        _overlay.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _initialRefreshTimer.Dispose();
        _refreshTimer.Dispose();
        base.ExitThreadCore();
    }
}
