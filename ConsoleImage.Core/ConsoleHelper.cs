// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Console helper for enabling ANSI escape sequences on Windows

using System.Runtime.InteropServices;

namespace ConsoleImage.Core;

/// <summary>
/// Helper class to enable ANSI escape sequence processing on Windows consoles.
/// Modern terminals like Windows Terminal have this enabled by default,
/// but older consoles or certain configurations may need explicit enabling.
/// </summary>
public static class ConsoleHelper
{
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    private static bool _initialized;
    private static bool _ansiEnabled;

    /// <summary>
    /// Enable ANSI escape sequence processing on the console.
    /// Safe to call multiple times - will only initialize once.
    /// Returns true if ANSI is enabled (or already was).
    /// </summary>
    public static bool EnableAnsiSupport()
    {
        if (_initialized)
            return _ansiEnabled;

        _initialized = true;

        // Only relevant on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _ansiEnabled = true;
            return true;
        }

        try
        {
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                _ansiEnabled = false;
                return false;
            }

            if (!GetConsoleMode(handle, out uint mode))
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
    /// Check if ANSI escape sequences are supported
    /// </summary>
    public static bool IsAnsiSupported
    {
        get
        {
            EnableAnsiSupport();
            return _ansiEnabled;
        }
    }
}
