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
        MinimumSize = new Size(520, 84);
        Height = 84;
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

        var leftPad = 12f;
        var rightPad = 12f;
        var usableWidth = Width - leftPad - rightPad;
        var slotWidth = usableWidth / _names.Length;

        using var labelFont = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        using var labelBrush = new SolidBrush(Color.FromArgb(38, 38, 38));

        for (var i = 0; i < _names.Length; i++)
        {
            var name = _names[i];
            var slotLeft = leftPad + slotWidth * i;
            var slotCenter = slotLeft + slotWidth / 2f;
            var isOn = _states.TryGetValue(name, out var on) && on;
            var isOnline = string.Equals(name, "ONLINE", StringComparison.OrdinalIgnoreCase);

            var dotSize = isOnline ? 24f : 22f;
            var dotX = slotCenter - dotSize / 2f;
            var dotY = panel.Y + 20f;

            DrawDot(g, new RectangleF(dotX, dotY, dotSize, dotSize), isOn, isOnline);

            var measured = g.MeasureString(name, labelFont);
            var labelX = slotCenter - measured.Width / 2f;
            var labelY = dotY + dotSize + 5f;
            g.DrawString(name, labelFont, labelBrush, labelX, labelY);
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
            var glow = RectangleF.Inflate(rect, 13, 13);
            using var glowPath = new GraphicsPath();
            glowPath.AddEllipse(glow);
            using var glowBrush = new PathGradientBrush(glowPath)
            {
                CenterColor = greenWhenOn ? Color.FromArgb(210, 70, 245, 110) : Color.FromArgb(210, 255, 84, 84),
                SurroundColors = new[] { Color.FromArgb(0, onColor) }
            };
            g.FillPath(glowBrush, glowPath);
        }

        var topColor = on ? Color.FromArgb(255, Math.Min(255, baseColor.R + 60), Math.Min(255, baseColor.G + 60), Math.Min(255, baseColor.B + 60)) : ControlPaint.Light(baseColor);
        var bottomColor = on ? Color.FromArgb(Math.Max(0, baseColor.R - 10), Math.Max(0, baseColor.G - 10), Math.Max(0, baseColor.B - 10)) : ControlPaint.DarkDark(baseColor);
        using var dotBrush = new LinearGradientBrush(rect, topColor, bottomColor, LinearGradientMode.Vertical);
        using var dotPen = new Pen(on ? Color.FromArgb(35, 35, 35) : Color.FromArgb(70, 76, 82), on ? 1.4f : 1f);
        g.FillEllipse(dotBrush, rect);
        g.DrawEllipse(dotPen, rect);

        var innerRect = RectangleF.Inflate(rect, -2.6f, -2.6f);
        using var innerBrush = new SolidBrush(on ? Color.FromArgb(125, Color.White) : Color.FromArgb(28, Color.White));
        g.FillEllipse(innerBrush, innerRect);

        var highlight = new RectangleF(rect.X + rect.Width * 0.18f, rect.Y + rect.Height * 0.12f, rect.Width * 0.48f, rect.Height * 0.30f);
        using var highlightBrush = new SolidBrush(Color.FromArgb(on ? 235 : 120, Color.White));
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
