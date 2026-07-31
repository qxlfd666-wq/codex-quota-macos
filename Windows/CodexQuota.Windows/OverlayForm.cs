using System.Drawing.Drawing2D;

namespace CodexQuota.Windows;

internal sealed class OverlayForm : Form
{
    private const int BadgeWidth = 64;
    private const int BadgeHeight = 28;
    private readonly System.Windows.Forms.Timer _trackingTimer;
    private int? _remainingPercent;
    private Color _accentColor = Color.FromArgb(255, 59, 48);

    public OverlayForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        ClientSize = new Size(BadgeWidth, BadgeHeight);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Cursor = Cursors.Hand;
        AccessibleName = "Codex 剩余额度";
        DoubleBuffered = true;

        Click += (_, _) => ChooseColorRequested?.Invoke(this, EventArgs.Empty);
        _trackingTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _trackingTimer.Tick += (_, _) => UpdatePlacement();
    }

    public event EventHandler? ChooseColorRequested;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            const int wsExNoActivate = 0x08000000;
            var parameters = base.CreateParams;
            parameters.ExStyle |= wsExToolWindow | wsExNoActivate;
            return parameters;
        }
    }

    public void StartTracking()
    {
        _ = Handle;
        _trackingTimer.Start();
        UpdatePlacement();
    }

    public void SetQuota(int remainingPercent)
    {
        _remainingPercent = Math.Clamp(remainingPercent, 0, 100);
        AccessibleDescription = $"Codex 剩余 {_remainingPercent}%";
        Invalidate();
    }

    public void SetAccentColor(Color color)
    {
        _accentColor = color;
        Invalidate();
    }

    private void UpdatePlacement()
    {
        if (!NativeMethods.TryGetCodexForegroundBounds(out var codexBounds, out var scale))
        {
            if (Visible)
                NativeMethods.ShowWindow(Handle, NativeMethods.SwHide);
            return;
        }

        // Codex currently has no public API for its account-row coordinates.
        // These offsets follow the stable lower-left sidebar geometry.
        var width = (int)Math.Round(BadgeWidth * scale);
        var height = (int)Math.Round(BadgeHeight * scale);
        var x = codexBounds.Left + (int)Math.Round(68 * scale);
        var y = codexBounds.Bottom - height - (int)Math.Round(12 * scale);
        NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HwndTopmost,
            x,
            y,
            width,
            height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var scale = Math.Max(1f, Height / (float)BadgeHeight);

        using var backgroundPath = RoundedRectangle(
            new Rectangle(0, 0, Width - 1, Height - 1),
            (int)Math.Round(8 * scale));
        using var backgroundBrush = new SolidBrush(Color.FromArgb(37, 37, 39));
        graphics.FillPath(backgroundBrush, backgroundPath);

        using var textBrush = new SolidBrush(_accentColor);
        using var font = new Font("Segoe UI", 9, FontStyle.Bold, GraphicsUnit.Point);
        var text = _remainingPercent is { } remaining ? $"{remaining}%" : "--";
        var textSize = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, textBrush, (Width - textSize.Width) / 2f, 3f * scale);

        var track = new RectangleF(
            7 * scale,
            Height - 6 * scale,
            Width - 14 * scale,
            3 * scale);
        using var trackBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
        graphics.FillRoundedRectangle(trackBrush, track, 1.5f * scale);
        if (_remainingPercent.HasValue && _remainingPercent.Value > 0)
        {
            var percent = _remainingPercent.Value;
            var fill = new RectangleF(track.X, track.Y, track.Width * percent / 100f, track.Height);
            graphics.FillRoundedRectangle(textBrush, fill, 1.5f * scale);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _trackingTimer.Dispose();
        base.Dispose(disposing);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF rectangle, float radius)
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
