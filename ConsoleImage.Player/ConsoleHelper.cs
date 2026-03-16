// Console helper for enabling ANSI escape sequences on Windows
// Lives in Player project as the base layer for all console output.

using System.Runtime.InteropServices;
using System.Text;

namespace ConsoleImage.Player;

/// <summary>
///     Helper class to enable ANSI escape sequence processing on Windows consoles.
///     Modern terminals like Windows Terminal have this enabled by default,
///     but older consoles or certain configurations may need explicit enabling.
///     Also enables UTF-8 encoding for Unicode characters (Braille, block chars, etc.)
/// </summary>
public static class ConsoleHelper
{
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

    private const uint CP_UTF8 = 65001;

    private static bool _initialized;
    private static bool _ansiEnabled;
    private static bool _cellAspectDetected;
    private static float? _detectedCellAspect;
    private static bool _terminalBgDetected;
    private static (byte R, byte G, byte B)? _detectedTerminalBg;

    /// <summary>
    ///     Check if ANSI escape sequences are supported
    /// </summary>
    public static bool IsAnsiSupported
    {
        get
        {
            EnableAnsiSupport();
            return _ansiEnabled;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetCurrentConsoleFontEx(
        IntPtr hConsoleOutput,
        bool bMaximumWindow,
        ref CONSOLE_FONT_INFOEX lpConsoleCurrentFontEx);

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CONSOLE_FONT_INFOEX
    {
        public uint cbSize;
        public uint nFont;
        public COORD dwFontSize;
        public int FontFamily;
        public int FontWeight;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }

    /// <summary>
    ///     Enable ANSI escape sequence processing and UTF-8 encoding on the console.
    ///     Safe to call multiple times - will only initialize once.
    ///     Returns true if ANSI is enabled (or already was).
    /// </summary>
    public static bool EnableAnsiSupport()
    {
        if (_initialized)
            return _ansiEnabled;

        _initialized = true;

        // Set UTF-8 encoding for Unicode character support (Braille, block chars, etc.)
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // May fail in some environments, but continue anyway
        }

        // Only relevant on Windows for ANSI/VT processing
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _ansiEnabled = true;
            return true;
        }

        try
        {
            // Set console output code page to UTF-8
            SetConsoleOutputCP(CP_UTF8);

            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                _ansiEnabled = false;
                return false;
            }

            if (!GetConsoleMode(handle, out var mode))
            {
                _ansiEnabled = false;
                return false;
            }

            // Enable virtual terminal processing
            mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;

            if (!SetConsoleMode(handle, mode))
            {
                _ansiEnabled = false;
                return false;
            }

            _ansiEnabled = true;
            return true;
        }
        catch
        {
            // P/Invoke might fail in restricted environments
            _ansiEnabled = false;
            return false;
        }
    }

    /// <summary>
    ///     Detect the terminal's character cell aspect ratio (width/height).
    ///     On Windows, queries the console font metrics via GetCurrentConsoleFontEx.
    ///     Falls back to ANSI CSI 16 t query for terminals that support it.
    ///     Returns null if detection fails (caller should use 0.5 default).
    ///     Result is cached for the session.
    /// </summary>
    public static float? DetectCellAspectRatio()
    {
        if (_cellAspectDetected)
            return _detectedCellAspect;

        _cellAspectDetected = true;

        // Skip detection in non-interactive mode
        try
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
                return null;
        }
        catch
        {
            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _detectedCellAspect = TryDetectCellAspectWindows();

        _detectedCellAspect ??= TryDetectCellAspectAnsi();

        return _detectedCellAspect;
    }

    /// <summary>
    ///     Try to detect cell aspect ratio using Windows GetCurrentConsoleFontEx API.
    /// </summary>
    private static float? TryDetectCellAspectWindows()
    {
        try
        {
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return null;

            var fontInfo = new CONSOLE_FONT_INFOEX();
            fontInfo.cbSize = (uint)Marshal.SizeOf<CONSOLE_FONT_INFOEX>();

            if (!GetCurrentConsoleFontEx(handle, false, ref fontInfo))
                return null;

            var w = fontInfo.dwFontSize.X;
            var h = fontInfo.dwFontSize.Y;

            // Both must be positive
            if (w <= 0 || h <= 0)
                return null;

            // Some terminals return suspicious values (e.g. 8x8 raster font placeholder)
            if (w == h && w <= 8)
                return null;

            var ratio = (float)w / h;

            // Sanity check: ratio should be between 0.2 and 1.0 for any reasonable font
            if (ratio < 0.2f || ratio > 1.0f)
                return null;

            return ratio;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Detect the terminal's background color using OSC 11 query.
    ///     Supported by Windows Terminal, iTerm2, kitty, WezTerm, xterm, foot, Alacritty, etc.
    ///     Returns null if detection fails or times out (caller should assume black).
    ///     Result is cached for the session.
    /// </summary>
    public static (byte R, byte G, byte B)? DetectTerminalBackground()
    {
        if (_terminalBgDetected)
            return _detectedTerminalBg;

        _terminalBgDetected = true;

        try
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
                return null;
        }
        catch
        {
            return null;
        }

        _detectedTerminalBg = TryDetectTerminalBackgroundAnsi();
        return _detectedTerminalBg;
    }

    /// <summary>
    ///     Query terminal background color via OSC 11.
    ///     Send: ESC ] 11 ; ? ESC \
    ///     Response: ESC ] 11 ; rgb:RRRR/GGGG/BBBB ESC \
    ///     Each channel is 4 hex digits (16-bit); take the high byte for 8-bit color.
    /// </summary>
    private static (byte R, byte G, byte B)? TryDetectTerminalBackgroundAnsi()
    {
        try
        {
            // Drain any pending input
            while (Console.KeyAvailable)
                Console.ReadKey(true);

            // OSC 11 background color query; ST = ESC \
            Console.Write("\x1b]11;?\x1b\\");
            Console.Out.Flush();

            // Read response with 300ms timeout
            var sb = new StringBuilder();
            var deadline = Environment.TickCount64 + 300;
            var gotEsc = false;
            var inOsc = false;
            var complete = false;

            while (Environment.TickCount64 < deadline)
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(5);
                    continue;
                }

                var key = Console.ReadKey(true);
                var ch = key.KeyChar;

                if (ch == '\x1b')
                {
                    if (inOsc)
                    {
                        // ESC inside OSC marks the start of ST (ESC \)
                        gotEsc = true;
                    }
                    else
                    {
                        // Start of a new escape sequence
                        gotEsc = true;
                        sb.Clear();
                    }
                    continue;
                }

                if (gotEsc && ch == ']')
                {
                    // ESC ] → start of OSC
                    inOsc = true;
                    gotEsc = false;
                    sb.Clear();
                    continue;
                }

                if (inOsc)
                {
                    if (gotEsc && ch == '\\')
                    {
                        // ST received — OSC complete
                        complete = true;
                        break;
                    }

                    if (ch == '\a')
                    {
                        // BEL terminator (older style)
                        complete = true;
                        break;
                    }

                    gotEsc = false;
                    sb.Append(ch);
                }
            }

            // Drain any remaining response characters
            Thread.Sleep(10);
            while (Console.KeyAvailable)
                Console.ReadKey(true);

            if (!complete)
                return null;

            // Parse: 11;rgb:RRRR/GGGG/BBBB
            var text = sb.ToString();
            var rgbIdx = text.IndexOf("rgb:", StringComparison.OrdinalIgnoreCase);
            if (rgbIdx < 0)
                return null;

            var rgbPart = text.Substring(rgbIdx + 4); // after "rgb:"
            var channels = rgbPart.Split('/');
            if (channels.Length != 3)
                return null;

            // Each channel is 4 hex digits (16-bit); use the high byte
            if (!int.TryParse(channels[0][..Math.Min(2, channels[0].Length)], System.Globalization.NumberStyles.HexNumber, null, out var r) ||
                !int.TryParse(channels[1][..Math.Min(2, channels[1].Length)], System.Globalization.NumberStyles.HexNumber, null, out var g) ||
                !int.TryParse(channels[2][..Math.Min(2, channels[2].Length)], System.Globalization.NumberStyles.HexNumber, null, out var b))
                return null;

            return ((byte)r, (byte)g, (byte)b);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Try to detect cell aspect ratio using ANSI CSI 16 t query.
    ///     Many modern terminals (Windows Terminal, iTerm2, kitty, xterm, foot) support this.
    ///     Response format: ESC [ 6 ; cellHeight ; cellWidth t
    /// </summary>
    private static float? TryDetectCellAspectAnsi()
    {
        try
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
                return null;

            // Drain any pending input
            while (Console.KeyAvailable)
                Console.ReadKey(true);

            // Send CSI 16 t (report cell size in pixels)
            Console.Write("\x1b[16t");
            Console.Out.Flush();

            // Read response with 200ms timeout
            var sb = new StringBuilder();
            var deadline = Environment.TickCount64 + 200;
            var gotEsc = false;
            var complete = false;

            while (Environment.TickCount64 < deadline)
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(5);
                    continue;
                }

                var key = Console.ReadKey(true);

                if (key.KeyChar == '\x1b')
                {
                    gotEsc = true;
                    sb.Clear();
                    continue;
                }

                if (gotEsc)
                {
                    sb.Append(key.KeyChar);
                    if (key.KeyChar == 't')
                    {
                        complete = true;
                        break;
                    }
                }
            }

            // Drain any remaining response characters
            Thread.Sleep(10);
            while (Console.KeyAvailable)
                Console.ReadKey(true);

            if (!complete)
                return null;

            // Parse: [6;height;widtht
            var text = sb.ToString();
            if (text.Length < 5 || text[0] != '[' || !text.EndsWith("t"))
                return null;

            var inner = text.Substring(1, text.Length - 2);
            var parts = inner.Split(';');
            if (parts.Length != 3 || parts[0] != "6")
                return null;

            if (!int.TryParse(parts[1], out var cellHeight) ||
                !int.TryParse(parts[2], out var cellWidth))
                return null;

            if (cellWidth <= 0 || cellHeight <= 0)
                return null;

            var ratio = (float)cellWidth / cellHeight;
            if (ratio < 0.2f || ratio > 1.0f)
                return null;

            return ratio;
        }
        catch
        {
            return null;
        }
    }
}
