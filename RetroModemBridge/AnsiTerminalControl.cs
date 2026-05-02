using System.Drawing.Drawing2D;
using System.Text;

namespace RetroModemBridge;

public sealed class AnsiTerminalControl : Control
{
    private sealed class Cell
    {
        public char Ch = ' ';
        public Color Fore = Color.Silver;
        public Color Back = Color.Black;
        public bool Bold;
    }

    private const int Columns = 80;
    private const int Rows = 24;

    private readonly Cell[,] _screen = new Cell[Rows, Columns];
    private readonly StringBuilder _sequence = new();
    private readonly Encoding _encoding;
    private int _cursorRow;
    private int _cursorCol;
    private int _savedRow;
    private int _savedCol;
    private int _scrollTop;
    private int _scrollBottom = Rows - 1;
    private TerminalParserState _state = TerminalParserState.Normal;
    private Color _currentFore = Color.Silver;
    private Color _currentBack = Color.Black;
    private bool _bold;
    private bool _cursorVisible = true;
    private bool _wrap = true;
    private Font _terminalFont = new("Consolas", 13F, FontStyle.Regular, GraphicsUnit.Point);

    public event Action<byte[]>? SendBytesRequested;

    private enum TerminalParserState
    {
        Normal,
        Escape,
        Csi,
        Osc,
        OscEscape
    }

    public AnsiTerminalControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        BackColor = Color.Black;
        ForeColor = Color.Silver;
        MinimumSize = new Size(640, 400);

        try
        {
            _encoding = Encoding.GetEncoding(437);
        }
        catch
        {
            _encoding = Encoding.ASCII;
        }

        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < Columns; col++)
                _screen[row, col] = new Cell();
        }

        ResetTerminal();
    }

    public void Clear()
    {
        ResetTerminal();
        Invalidate();
    }

    public void WriteBytes(byte[] bytes)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<byte[]>(WriteBytes), bytes);
            return;
        }

        var text = _encoding.GetString(bytes);
        foreach (var ch in text)
            ProcessChar(ch);

        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _terminalFont.Dispose();

        base.Dispose(disposing);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        return keyCode is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Tab or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
            || base.IsInputKey(keyData);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);

        if (e.KeyChar == '\r')
        {
            SendBytesRequested?.Invoke(new byte[] { 13 });
            e.Handled = true;
            return;
        }

        if (e.KeyChar == '\b')
        {
            SendBytesRequested?.Invoke(new byte[] { 8 });
            e.Handled = true;
            return;
        }

        if (!char.IsControl(e.KeyChar))
        {
            SendBytesRequested?.Invoke(_encoding.GetBytes(new[] { e.KeyChar }));
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        byte[]? seq = e.KeyCode switch
        {
            Keys.Up => Encoding.ASCII.GetBytes("\x1b[A"),
            Keys.Down => Encoding.ASCII.GetBytes("\x1b[B"),
            Keys.Right => Encoding.ASCII.GetBytes("\x1b[C"),
            Keys.Left => Encoding.ASCII.GetBytes("\x1b[D"),
            Keys.Home => Encoding.ASCII.GetBytes("\x1b[H"),
            Keys.End => Encoding.ASCII.GetBytes("\x1b[F"),
            Keys.PageUp => Encoding.ASCII.GetBytes("\x1b[5~"),
            Keys.PageDown => Encoding.ASCII.GetBytes("\x1b[6~"),
            Keys.Insert => Encoding.ASCII.GetBytes("\x1b[2~"),
            Keys.Delete => Encoding.ASCII.GetBytes("\x1b[3~"),
            Keys.F1 => Encoding.ASCII.GetBytes("\x1bOP"),
            Keys.F2 => Encoding.ASCII.GetBytes("\x1bOQ"),
            Keys.F3 => Encoding.ASCII.GetBytes("\x1bOR"),
            Keys.F4 => Encoding.ASCII.GetBytes("\x1bOS"),
            _ => null
        };

        if (seq is not null)
        {
            SendBytesRequested?.Invoke(seq);
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Color.Black);

        var cellW = ClientSize.Width / (float)Columns;
        var cellH = ClientSize.Height / (float)Rows;

        // Keep the font slightly smaller than the cell so letters like m, w,
        // and CP437 block characters do not touch the right edge of the cell.
        var fontSizeByHeight = cellH * 0.70F;
        var fontSizeByWidth = cellW * 1.22F;
        var fontSize = Math.Max(7.5F, Math.Min(fontSizeByHeight, fontSizeByWidth));

        if (Math.Abs(_terminalFont.Size - fontSize) > 0.25F)
        {
            _terminalFont.Dispose();
            _terminalFont = new Font("Consolas", fontSize, FontStyle.Regular, GraphicsUnit.Point);
        }

        // Paint all backgrounds first. This preserves ANSI color blocks and
        // prevents later text draws from being erased by neighboring cells.
        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < Columns; col++)
            {
                var cell = _screen[row, col];
                var x = (int)MathF.Floor(col * cellW);
                var y = (int)MathF.Floor(row * cellH);
                var nextX = (int)MathF.Ceiling((col + 1) * cellW);
                var nextY = (int)MathF.Ceiling((row + 1) * cellH);
                var backRect = new Rectangle(x, y, Math.Max(1, nextX - x), Math.Max(1, nextY - y));

                using var back = new SolidBrush(cell.Back);
                g.FillRectangle(back, backRect);
            }
        }

        // Draw CP437 block/shade characters as cell graphics instead of font glyphs.
        // This keeps ANSI art flush with no tiny gaps between neighboring cells.
        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < Columns; col++)
            {
                var cell = _screen[row, col];
                if (!IsCellGraphic(cell.Ch))
                    continue;

                var x = (int)MathF.Floor(col * cellW);
                var y = (int)MathF.Floor(row * cellH);
                var nextX = (int)MathF.Ceiling((col + 1) * cellW);
                var nextY = (int)MathF.Ceiling((row + 1) * cellH);
                var rect = new Rectangle(x, y, Math.Max(1, nextX - x), Math.Max(1, nextY - y));
                DrawCellGraphic(g, rect, cell.Ch, cell.Bold ? Brighten(cell.Fore) : cell.Fore);
            }
        }

        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces,
            Trimming = StringTrimming.None
        };

        // Draw text one fixed cell at a time.
        // This is more important for the Session Mirror than natural Windows text
        // rendering because the goal is to match the retro terminal's 80-column grid.
        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < Columns; col++)
            {
                var cell = _screen[row, col];
                if (cell.Ch == ' ' || IsCellGraphic(cell.Ch))
                    continue;

                var fore = cell.Bold ? Brighten(cell.Fore) : cell.Fore;
                var x = col * cellW;
                var y = row * cellH;
                var drawRect = new RectangleF(
                    x,
                    y,
                    cellW,
                    cellH);

                if (IsBoxDrawing(cell.Ch))
                {
                    DrawBoxDrawingCell(g, cell.Ch, drawRect, fore);
                    continue;
                }

                drawRect = new RectangleF(
                    x - 1.5F,
                    y - 1,
                    cellW + 5,
                    cellH + 4);

                using var brush = new SolidBrush(fore);
                g.DrawString(cell.Ch.ToString(), _terminalFont, brush, drawRect, format);
            }
        }

        if (Focused && _cursorVisible)
        {
            var cursorRect = new RectangleF(_cursorCol * cellW, _cursorRow * cellH + cellH - 3, cellW, 2);
            using var brush = new SolidBrush(Color.FromArgb(220, Color.White));
            g.FillRectangle(brush, cursorRect);
        }
    }


    private static bool IsCellGraphic(char ch) => ch switch
    {
        '█' or '▀' or '▄' or '▌' or '▐' or '░' or '▒' or '▓' => true,
        _ => false
    };

    private static void DrawCellGraphic(Graphics g, Rectangle rect, char ch, Color color)
    {
        using var brush = new SolidBrush(color);

        switch (ch)
        {
            case '█':
                g.FillRectangle(brush, rect);
                break;
            case '▀':
                g.FillRectangle(brush, rect.X, rect.Y, rect.Width, Math.Max(1, rect.Height / 2));
                break;
            case '▄':
                var half = Math.Max(1, rect.Height / 2);
                g.FillRectangle(brush, rect.X, rect.Bottom - half, rect.Width, half);
                break;
            case '▌':
                g.FillRectangle(brush, rect.X, rect.Y, Math.Max(1, rect.Width / 2), rect.Height);
                break;
            case '▐':
                var halfW = Math.Max(1, rect.Width / 2);
                g.FillRectangle(brush, rect.Right - halfW, rect.Y, halfW, rect.Height);
                break;
            case '░':
                DrawShade(g, rect, color, 0.25f);
                break;
            case '▒':
                DrawShade(g, rect, color, 0.50f);
                break;
            case '▓':
                DrawShade(g, rect, color, 0.75f);
                break;
        }
    }

    private static void DrawShade(Graphics g, Rectangle rect, Color color, float density)
    {
        using var brush = new SolidBrush(color);
        var step = density < 0.35f ? 4 : density < 0.65f ? 3 : 2;
        var fillEvery = density < 0.35f ? 4 : density < 0.65f ? 2 : 1;

        for (var y = rect.Y; y < rect.Bottom; y += step)
        {
            for (var x = rect.X; x < rect.Right; x += step)
            {
                if (((x + y) / step) % fillEvery == 0 || density > 0.70f)
                    g.FillRectangle(brush, x, y, Math.Min(1, rect.Right - x), Math.Min(1, rect.Bottom - y));
            }
        }
    }

    private void ProcessChar(char ch)
    {
        switch (_state)
        {
            case TerminalParserState.Escape:
                HandleEscapeChar(ch);
                return;
            case TerminalParserState.Csi:
                _sequence.Append(ch);
                if (ch >= '@' && ch <= '~')
                {
                    HandleCsi(_sequence.ToString());
                    _sequence.Clear();
                    _state = TerminalParserState.Normal;
                }
                return;
            case TerminalParserState.Osc:
                if (ch == '\x07')
                {
                    _sequence.Clear();
                    _state = TerminalParserState.Normal;
                }
                else if (ch == '\x1b')
                {
                    _state = TerminalParserState.OscEscape;
                }
                else
                {
                    _sequence.Append(ch);
                }
                return;
            case TerminalParserState.OscEscape:
                if (ch == '\\')
                {
                    _sequence.Clear();
                    _state = TerminalParserState.Normal;
                }
                else
                {
                    _state = TerminalParserState.Osc;
                }
                return;
        }

        switch (ch)
        {
            case '\x1b':
                _sequence.Clear();
                _state = TerminalParserState.Escape;
                return;
            case '\r':
                _cursorCol = 0;
                return;
            case '\n':
                LineFeed();
                return;
            case '\b':
                if (_cursorCol > 0)
                    _cursorCol--;
                return;
            case '\t':
                var spaces = 8 - (_cursorCol % 8);
                for (var i = 0; i < spaces; i++)
                    PutChar(' ');
                return;
            case '\x07':
            case '\0':
                return;
        }

        if (!char.IsControl(ch))
            PutChar(ch);
    }

    private void HandleEscapeChar(char ch)
    {
        switch (ch)
        {
            case '[':
                _state = TerminalParserState.Csi;
                _sequence.Clear();
                return;
            case ']':
                _state = TerminalParserState.Osc;
                _sequence.Clear();
                return;
            case '7':
                SaveCursor();
                break;
            case '8':
                RestoreCursor();
                break;
            case 'D':
                LineFeed();
                break;
            case 'E':
                _cursorCol = 0;
                LineFeed();
                break;
            case 'M':
                ReverseIndex();
                break;
            case 'c':
                ResetTerminal();
                break;
        }

        _sequence.Clear();
        _state = TerminalParserState.Normal;
    }

    private void HandleCsi(string sequence)
    {
        if (sequence.Length == 0)
            return;

        var final = sequence[^1];
        var body = sequence[..^1];
        var isPrivate = body.StartsWith('?');
        if (isPrivate)
            body = body[1..];

        var args = ParseArgs(body);
        var first = Arg(args, 0, 1);

        switch (final)
        {
            case 'm':
                ApplySgr(args);
                break;
            case 'H':
            case 'f':
                MoveCursor(Arg(args, 0, 1) - 1, Arg(args, 1, 1) - 1);
                break;
            case 'A':
                MoveCursor(_cursorRow - first, _cursorCol);
                break;
            case 'B':
                MoveCursor(_cursorRow + first, _cursorCol);
                break;
            case 'C':
                MoveCursor(_cursorRow, _cursorCol + first);
                break;
            case 'D':
                MoveCursor(_cursorRow, _cursorCol - first);
                break;
            case 'E':
                MoveCursor(_cursorRow + first, 0);
                break;
            case 'F':
                MoveCursor(_cursorRow - first, 0);
                break;
            case 'G':
                MoveCursor(_cursorRow, first - 1);
                break;
            case 'd':
                MoveCursor(first - 1, _cursorCol);
                break;
            case 'J':
                ClearScreenMode(Arg(args, 0, 0));
                break;
            case 'K':
                ClearLineMode(Arg(args, 0, 0));
                break;
            case 'L':
                InsertLines(first);
                break;
            case 'M':
                DeleteLines(first);
                break;
            case 'P':
                DeleteChars(first);
                break;
            case '@':
                InsertSpaces(first);
                break;
            case 'X':
                EraseChars(first);
                break;
            case 'n':
                if (first == 6)
                {
                    // ANSI Device Status Report: report cursor position.
                    // Many BBSes use this to auto-detect that the terminal supports ANSI.
                    var report = $"[{_cursorRow + 1};{_cursorCol + 1}R";
                    SendBytesRequested?.Invoke(System.Text.Encoding.ASCII.GetBytes(report));
                }
                break;
            case 's':
                SaveCursor();
                break;
            case 'u':
                RestoreCursor();
                break;
            case 'r':
                SetScrollRegion(args);
                break;
            case 'h':
                SetMode(isPrivate, args, true);
                break;
            case 'l':
                SetMode(isPrivate, args, false);
                break;
        }
    }

    private static int[] ParseArgs(string body)
    {
        if (string.IsNullOrEmpty(body))
            return Array.Empty<int>();

        return body
            .Split(';')
            .Select(part => int.TryParse(part, out var value) ? value : 0)
            .ToArray();
    }

    private static int Arg(int[] args, int index, int fallback)
    {
        if (index >= args.Length || args[index] == 0)
            return fallback;

        return args[index];
    }

    private void ApplySgr(int[] args)
    {
        if (args.Length == 0)
            args = new[] { 0 };

        foreach (var code in args)
        {
            switch (code)
            {
                case 0:
                    _currentFore = Color.Silver;
                    _currentBack = Color.Black;
                    _bold = false;
                    break;
                case 1:
                    _bold = true;
                    break;
                case 22:
                    _bold = false;
                    break;
                case 39:
                    _currentFore = Color.Silver;
                    break;
                case 49:
                    _currentBack = Color.Black;
                    break;
                case >= 30 and <= 37:
                    _currentFore = AnsiColor(code - 30, false);
                    break;
                case >= 40 and <= 47:
                    _currentBack = AnsiColor(code - 40, false);
                    break;
                case >= 90 and <= 97:
                    _currentFore = AnsiColor(code - 90, true);
                    break;
                case >= 100 and <= 107:
                    _currentBack = AnsiColor(code - 100, true);
                    break;
            }
        }
    }

    private void SetMode(bool isPrivate, int[] args, bool enabled)
    {
        foreach (var arg in args)
        {
            if (isPrivate && arg == 25)
                _cursorVisible = enabled;
            else if (isPrivate && arg == 7)
                _wrap = enabled;
        }
    }

    private void SetScrollRegion(int[] args)
    {
        var top = Arg(args, 0, 1) - 1;
        var bottom = Arg(args, 1, Rows) - 1;

        if (top < 0 || bottom <= top || bottom >= Rows)
        {
            _scrollTop = 0;
            _scrollBottom = Rows - 1;
        }
        else
        {
            _scrollTop = top;
            _scrollBottom = bottom;
        }

        MoveCursor(0, 0);
    }

    private void PutChar(char ch)
    {
        if (_cursorCol >= Columns)
        {
            if (!_wrap)
                _cursorCol = Columns - 1;
            else
                NewLine();
        }

        var cell = _screen[_cursorRow, _cursorCol];
        cell.Ch = ch;
        cell.Fore = _currentFore;
        cell.Back = _currentBack;
        cell.Bold = _bold;

        if (_cursorCol == Columns - 1)
        {
            if (_wrap)
                NewLine();
        }
        else
        {
            _cursorCol++;
        }
    }

    private void NewLine()
    {
        _cursorCol = 0;
        LineFeed();
    }

    private void LineFeed()
    {
        if (_cursorRow == _scrollBottom)
            ScrollUp(_scrollTop, _scrollBottom);
        else
            _cursorRow = Clamp(_cursorRow + 1, 0, Rows - 1);
    }

    private void ReverseIndex()
    {
        if (_cursorRow == _scrollTop)
            ScrollDown(_scrollTop, _scrollBottom);
        else
            _cursorRow = Clamp(_cursorRow - 1, 0, Rows - 1);
    }

    private void ScrollUp(int top, int bottom)
    {
        for (var row = top + 1; row <= bottom; row++)
        {
            for (var col = 0; col < Columns; col++)
                CopyCell(_screen[row, col], _screen[row - 1, col]);
        }

        ClearRow(bottom);
    }

    private void ScrollDown(int top, int bottom)
    {
        for (var row = bottom - 1; row >= top; row--)
        {
            for (var col = 0; col < Columns; col++)
                CopyCell(_screen[row, col], _screen[row + 1, col]);
        }

        ClearRow(top);
    }

    private void InsertLines(int count)
    {
        if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom)
            return;

        count = Math.Min(count, _scrollBottom - _cursorRow + 1);
        for (var i = 0; i < count; i++)
            ScrollDown(_cursorRow, _scrollBottom);
    }

    private void DeleteLines(int count)
    {
        if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom)
            return;

        count = Math.Min(count, _scrollBottom - _cursorRow + 1);
        for (var i = 0; i < count; i++)
            ScrollUp(_cursorRow, _scrollBottom);
    }

    private void InsertSpaces(int count)
    {
        count = Math.Min(count, Columns - _cursorCol);
        for (var col = Columns - 1; col >= _cursorCol + count; col--)
            CopyCell(_screen[_cursorRow, col - count], _screen[_cursorRow, col]);

        for (var col = _cursorCol; col < _cursorCol + count; col++)
            ClearCell(_cursorRow, col);
    }

    private void DeleteChars(int count)
    {
        count = Math.Min(count, Columns - _cursorCol);
        for (var col = _cursorCol; col < Columns - count; col++)
            CopyCell(_screen[_cursorRow, col + count], _screen[_cursorRow, col]);

        for (var col = Columns - count; col < Columns; col++)
            ClearCell(_cursorRow, col);
    }

    private void EraseChars(int count)
    {
        count = Math.Min(count, Columns - _cursorCol);
        for (var col = _cursorCol; col < _cursorCol + count; col++)
            ClearCell(_cursorRow, col);
    }

    private void ClearScreenMode(int mode)
    {
        switch (mode)
        {
            case 0:
                ClearToEndOfScreen();
                break;
            case 1:
                ClearFromStartOfScreen();
                break;
            case 2:
            case 3:
                ClearScreen();
                MoveCursor(0, 0);
                break;
        }
    }

    private void ClearLineMode(int mode)
    {
        switch (mode)
        {
            case 0:
                ClearToEndOfLine();
                break;
            case 1:
                ClearFromStartOfLine();
                break;
            case 2:
                ClearEntireLine();
                break;
        }
    }

    private void ClearScreen()
    {
        for (var row = 0; row < Rows; row++)
            ClearRow(row);
    }

    private void ClearRow(int row)
    {
        for (var col = 0; col < Columns; col++)
            ClearCell(row, col);
    }

    private void ClearCell(int row, int col)
    {
        var cell = _screen[row, col];
        cell.Ch = ' ';
        cell.Fore = _currentFore;
        cell.Back = _currentBack;
        cell.Bold = false;
    }

    private void ClearToEndOfLine()
    {
        for (var col = _cursorCol; col < Columns; col++)
            ClearCell(_cursorRow, col);
    }

    private void ClearFromStartOfLine()
    {
        for (var col = 0; col <= _cursorCol; col++)
            ClearCell(_cursorRow, col);
    }

    private void ClearEntireLine()
    {
        for (var col = 0; col < Columns; col++)
            ClearCell(_cursorRow, col);
    }

    private void ClearToEndOfScreen()
    {
        ClearToEndOfLine();
        for (var row = _cursorRow + 1; row < Rows; row++)
            ClearRow(row);
    }

    private void ClearFromStartOfScreen()
    {
        for (var row = 0; row < _cursorRow; row++)
            ClearRow(row);
        ClearFromStartOfLine();
    }

    private void ResetTerminal()
    {
        _currentFore = Color.Silver;
        _currentBack = Color.Black;
        _bold = false;
        _cursorVisible = true;
        _wrap = true;
        _scrollTop = 0;
        _scrollBottom = Rows - 1;
        _cursorRow = 0;
        _cursorCol = 0;
        _savedRow = 0;
        _savedCol = 0;
        _state = TerminalParserState.Normal;
        _sequence.Clear();
        ClearScreen();
    }

    private void MoveCursor(int row, int col)
    {
        _cursorRow = Clamp(row, 0, Rows - 1);
        _cursorCol = Clamp(col, 0, Columns - 1);
    }

    private void SaveCursor()
    {
        _savedRow = _cursorRow;
        _savedCol = _cursorCol;
    }

    private void RestoreCursor()
    {
        MoveCursor(_savedRow, _savedCol);
    }

    private static void CopyCell(Cell from, Cell to)
    {
        to.Ch = from.Ch;
        to.Fore = from.Fore;
        to.Back = from.Back;
        to.Bold = from.Bold;
    }

    private static Color AnsiColor(int index, bool bright)
    {
        return index switch
        {
            0 => bright ? Color.Gray : Color.Black,
            1 => bright ? Color.Red : Color.Maroon,
            2 => bright ? Color.Lime : Color.Green,
            3 => bright ? Color.Yellow : Color.Olive,
            4 => bright ? Color.RoyalBlue : Color.Navy,
            5 => bright ? Color.Magenta : Color.Purple,
            6 => bright ? Color.Cyan : Color.Teal,
            7 => bright ? Color.White : Color.Silver,
            _ => Color.Silver
        };
    }


    private static bool IsBoxDrawing(char ch)
    {
        return ch is
            '─' or '│' or '┌' or '┐' or '└' or '┘' or '├' or '┤' or '┬' or '┴' or '┼' or
            '═' or '║' or '╔' or '╗' or '╚' or '╝' or '╠' or '╣' or '╦' or '╩' or '╬' or
            '╒' or '╕' or '╘' or '╛' or '╞' or '╡' or '╤' or '╧' or '╪' or
            '╓' or '╖' or '╙' or '╜' or '╟' or '╢' or '╥' or '╨' or '╫';
    }

    private static void DrawBoxDrawingCell(Graphics g, char ch, RectangleF rect, Color color)
    {
        using var pen = new Pen(color, Math.Max(1.35F, rect.Height / 11F))
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Square,
            EndCap = System.Drawing.Drawing2D.LineCap.Square
        };

        var left = rect.Left - 1.0F;
        var right = rect.Right + 1.0F;
        var top = rect.Top - 0.75F;
        var bottom = rect.Bottom + 0.75F;
        var midX = rect.Left + rect.Width / 2F;
        var midY = rect.Top + rect.Height / 2F;

        void H() => g.DrawLine(pen, left, midY, right, midY);
        void V() => g.DrawLine(pen, midX, top, midX, bottom);
        void L() => g.DrawLine(pen, left, midY, midX, midY);
        void R() => g.DrawLine(pen, midX, midY, right, midY);
        void U() => g.DrawLine(pen, midX, top, midX, midY);
        void D() => g.DrawLine(pen, midX, midY, midX, bottom);

        switch (ch)
        {
            case '─':
            case '═':
                H();
                break;

            case '│':
            case '║':
                V();
                break;

            case '┌':
            case '╔':
            case '╒':
            case '╓':
                R();
                D();
                break;

            case '┐':
            case '╗':
            case '╕':
            case '╖':
                L();
                D();
                break;

            case '└':
            case '╚':
            case '╘':
            case '╙':
                R();
                U();
                break;

            case '┘':
            case '╝':
            case '╛':
            case '╜':
                L();
                U();
                break;

            case '├':
            case '╠':
            case '╞':
            case '╟':
                U();
                D();
                R();
                break;

            case '┤':
            case '╣':
            case '╡':
            case '╢':
                U();
                D();
                L();
                break;

            case '┬':
            case '╦':
            case '╤':
            case '╥':
                L();
                R();
                D();
                break;

            case '┴':
            case '╩':
            case '╧':
            case '╨':
                L();
                R();
                U();
                break;

            case '┼':
            case '╬':
            case '╪':
            case '╫':
                H();
                V();
                break;

            default:
                using (var brush = new SolidBrush(color))
                    g.DrawString(ch.ToString(), SystemFonts.DefaultFont, brush, rect);
                break;
        }
    }

    private static Color Brighten(Color color)
    {
        return Color.FromArgb(
            Math.Min(255, color.R + 50),
            Math.Min(255, color.G + 50),
            Math.Min(255, color.B + 50));
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}
