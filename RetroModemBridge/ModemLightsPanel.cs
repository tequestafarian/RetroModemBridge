
using System.Drawing.Drawing2D;

namespace RetroModemBridge;

public sealed class ModemLightsPanel : Control
{
    private readonly Dictionary<string, bool> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _names = { "DTR", "RTS", "CTS", "DSR", "DCD", "RX", "TX", "ONLINE" };

    public ModemLightsPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        MinimumSize = new Size(520, 32);
        Height = 32;
        BackColor = Color.White;

        foreach (var name in _names)
            _states[name] = false;
    }

    public void SetLight(string name, bool on)
    {
        if (!_states.ContainsKey(name))
            return;

        if (_states[name] == on)
            return;

        _states[name] = on;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Parent?.BackColor ?? Color.White);

        var panel = new Rectangle(0, 0, Width - 1, Height - 1);
        using var panelPath = RoundedRect(panel, 8);
        using var panelBrush = new SolidBrush(Color.FromArgb(251, 251, 252));
        using var panelPen = new Pen(Color.FromArgb(219, 223, 228));
        g.FillPath(panelBrush, panelPath);
        g.DrawPath(panelPen, panelPath);

        var leftPad = 16f;
        var rightPad = 16f;
        var usableWidth = Width - leftPad - rightPad;
        var slotWidth = usableWidth / _names.Length;
        using var labelFont = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        using var labelBrush = new SolidBrush(Color.FromArgb(38, 38, 38));

        for (var i = 0; i < _names.Length; i++)
        {
            var name = _names[i];
            var slotLeft = leftPad + slotWidth * i;
            var isOn = _states.TryGetValue(name, out var on) && on;
            var isOnline = string.Equals(name, "ONLINE", StringComparison.OrdinalIgnoreCase);
            var dotSize = name == "ONLINE" ? 12f : 11f;
            var measured = g.MeasureString(name, labelFont);
            var totalWidth = dotSize + 10f + measured.Width;
            var dotX = Math.Max(slotLeft + 2f, slotLeft + (slotWidth - totalWidth) / 2f);
            var dotY = panel.Y + panel.Height / 2f - dotSize / 2f;
            DrawDot(g, new RectangleF(dotX, dotY, dotSize, dotSize), isOn, isOnline);
            g.DrawString(name, labelFont, labelBrush, dotX + dotSize + 10f, panel.Y + panel.Height / 2f - measured.Height / 2f + 0.5f);
        }
    }

    private static void DrawDot(Graphics g, RectangleF rect, bool on, bool greenWhenOn)
    {
        var offColor = Color.FromArgb(118, 124, 132);
        var onColor = greenWhenOn
            ? Color.FromArgb(36, 205, 74)
            : Color.FromArgb(255, 48, 48);
        var baseColor = on ? onColor : offColor;

        if (on)
        {
            var glow = RectangleF.Inflate(rect, 7, 7);
            using var glowPath = new GraphicsPath();
            glowPath.AddEllipse(glow);
            using var glowBrush = new PathGradientBrush(glowPath)
            {
                CenterColor = greenWhenOn ? Color.FromArgb(120, 36, 205, 74) : Color.FromArgb(120, 255, 48, 48),
                SurroundColors = new[] { Color.FromArgb(0, onColor) }
            };
            g.FillPath(glowBrush, glowPath);
        }

        var topColor = on ? ControlPaint.LightLight(baseColor) : ControlPaint.Light(baseColor);
        var bottomColor = on ? ControlPaint.Dark(baseColor) : ControlPaint.DarkDark(baseColor);
        using var dotBrush = new LinearGradientBrush(rect, topColor, bottomColor, LinearGradientMode.Vertical);
        using var dotPen = new Pen(on ? Color.FromArgb(40, 40, 40) : Color.FromArgb(70, 76, 82), on ? 1.2f : 1f);
        g.FillEllipse(dotBrush, rect);
        g.DrawEllipse(dotPen, rect);

        var innerRect = RectangleF.Inflate(rect, -1.6f, -1.6f);
        using var innerBrush = new SolidBrush(on ? Color.FromArgb(70, Color.White) : Color.FromArgb(28, Color.White));
        g.FillEllipse(innerBrush, innerRect);

        var highlight = new RectangleF(rect.X + rect.Width * 0.20f, rect.Y + rect.Height * 0.12f, rect.Width * 0.46f, rect.Height * 0.28f);
        using var highlightBrush = new SolidBrush(Color.FromArgb(on ? 190 : 110, Color.White));
        g.FillEllipse(highlightBrush, highlight);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var maxRadius = Math.Max(1, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
        var d = maxRadius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
