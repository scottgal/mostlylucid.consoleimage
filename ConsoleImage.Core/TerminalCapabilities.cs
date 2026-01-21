using System.Runtime.InteropServices;

namespace ConsoleImage.Core;

/// <summary>
/// Detects terminal capabilities for choosing the best image display protocol.
/// </summary>
public static class TerminalCapabilities
{
    private static TerminalProtocol? _cachedBestProtocol;
    private static bool? _cachedSupportsColor;

    /// <summary>
    /// Detect the best available protocol for the current terminal.
    /// </summary>
    /// <param name="forceRefresh">Force re-detection instead of using cached value</param>
    /// <returns>The best available terminal protocol</returns>
    public static TerminalProtocol DetectBestProtocol(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedBestProtocol.HasValue)
            return _cachedBestProtocol.Value;

        _cachedBestProtocol = DetectBestProtocolInternal();
        return _cachedBestProtocol.Value;
    }

    /// <summary>
    /// Check if a specific protocol is supported.
    /// </summary>
    public static bool SupportsProtocol(TerminalProtocol protocol)
    {
        return protocol switch
        {
            TerminalProtocol.Ascii => true, // Always supported
            TerminalProtocol.ColorBlocks => SupportsColor(),
            TerminalProtocol.Braille => SupportsUnicode(),
            TerminalProtocol.Sixel => SupportsSixel(),
            TerminalProtocol.ITerm2 => SupportsITerm2(),
            TerminalProtocol.Kitty => SupportsKitty(),
            _ => false
        };
    }

    /// <summary>
    /// Check if terminal supports ANSI color codes.
    /// </summary>
    public static bool SupportsColor()
    {
        if (_cachedSupportsColor.HasValue)
            return _cachedSupportsColor.Value;

        _cachedSupportsColor = DetectColorSupport();
        return _cachedSupportsColor.Value;
    }

    /// <summary>
    /// Check if terminal supports Unicode characters.
    /// </summary>
    public static bool SupportsUnicode()
    {
        // Check LANG/LC_ALL environment variables for UTF-8
        var lang = Environment.GetEnvironmentVariable("LANG") ?? "";
        var lcAll = Environment.GetEnvironmentVariable("LC_ALL") ?? "";

        if (lang.Contains("UTF-8", StringComparison.OrdinalIgnoreCase) ||
            lcAll.Contains("UTF-8", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // On Windows, check console output code page
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return Console.OutputEncoding.WebName.Contains("utf", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // Most modern terminals support Unicode
        return true;
    }

    /// <summary>
    /// Check if running in iTerm2 or compatible terminal.
    /// </summary>
    public static bool SupportsITerm2()
    {
        // Check for iTerm2 specific environment variable
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM") ?? "";
        if (termProgram.Equals("iTerm.app", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for LC_TERMINAL which some compatible terminals set
        var lcTerminal = Environment.GetEnvironmentVariable("LC_TERMINAL") ?? "";
        if (lcTerminal.Equals("iTerm2", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for WezTerm which supports iTerm2 protocol
        if (termProgram.Equals("WezTerm", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for Mintty (Windows) which supports iTerm2 protocol
        var mintty = Environment.GetEnvironmentVariable("MINTTY_SHORTCUT");
        if (!string.IsNullOrEmpty(mintty))
            return true;

        return false;
    }

    /// <summary>
    /// Check if running in Kitty terminal or compatible.
    /// </summary>
    public static bool SupportsKitty()
    {
        // Check TERM for kitty
        var term = Environment.GetEnvironmentVariable("TERM") ?? "";
        if (term.Contains("kitty", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check KITTY_WINDOW_ID
        var kittyWindow = Environment.GetEnvironmentVariable("KITTY_WINDOW_ID");
        if (!string.IsNullOrEmpty(kittyWindow))
            return true;

        // Some terminals advertise Kitty support via TERM_PROGRAM
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM") ?? "";
        if (termProgram.Equals("kitty", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Check if terminal supports Sixel graphics.
    /// This is a heuristic check; actual support may vary.
    /// </summary>
    public static bool SupportsSixel()
    {
        var term = Environment.GetEnvironmentVariable("TERM") ?? "";

        // Known Sixel-capable terminals
        string[] sixelTerms = ["xterm", "mlterm", "yaft", "foot", "contour"];

        if (sixelTerms.Any(t => term.Contains(t, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Check for explicit Sixel support flag
        var colorterm = Environment.GetEnvironmentVariable("COLORTERM") ?? "";
        if (colorterm.Contains("sixel", StringComparison.OrdinalIgnoreCase))
            return true;

        // XTerm with version >= 330 supports Sixel
        var xtermVersion = Environment.GetEnvironmentVariable("XTERM_VERSION");
        if (!string.IsNullOrEmpty(xtermVersion))
        {
            // Format is "XTerm(nnn)" where nnn is version
            var match = System.Text.RegularExpressions.Regex.Match(xtermVersion, @"\((\d+)\)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int version))
            {
                return version >= 330;
            }
        }

        return false;
    }

    private static TerminalProtocol DetectBestProtocolInternal()
    {
        // Prefer native image protocols in order of quality
        if (SupportsKitty())
            return TerminalProtocol.Kitty;

        if (SupportsITerm2())
            return TerminalProtocol.ITerm2;

        if (SupportsSixel())
            return TerminalProtocol.Sixel;

        // Fall back to character-based rendering
        if (SupportsColor())
            return TerminalProtocol.ColorBlocks;

        if (SupportsUnicode())
            return TerminalProtocol.Braille;

        return TerminalProtocol.Ascii;
    }

    private static bool DetectColorSupport()
    {
        // Check NO_COLOR environment variable (https://no-color.org/)
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
            return false;

        // Check TERM
        var term = Environment.GetEnvironmentVariable("TERM") ?? "";
        if (term == "dumb")
            return false;

        // Check COLORTERM for 24-bit color
        var colorterm = Environment.GetEnvironmentVariable("COLORTERM") ?? "";
        if (colorterm == "truecolor" || colorterm == "24bit")
            return true;

        // Most modern terminals support color
        if (term.Contains("256color") || term.Contains("xterm") ||
            term.Contains("screen") || term.Contains("tmux") ||
            term.Contains("vt100") || term.Contains("linux") ||
            term.Contains("ansi") || term.Contains("cygwin"))
        {
            return true;
        }

        // On Windows, check if ANSI is enabled
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ConsoleHelper.EnableAnsiSupport();
        }

        // Default to true for Unix-like systems
        return !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    /// <summary>
    /// Get a human-readable description of the terminal capabilities.
    /// </summary>
    public static string GetCapabilitiesReport()
    {
        var protocols = Enum.GetValues<TerminalProtocol>()
            .Select(p => $"  {p}: {(SupportsProtocol(p) ? "Supported" : "Not supported")}");

        return $"""
            Terminal Capabilities Report
            ============================
            TERM: {Environment.GetEnvironmentVariable("TERM") ?? "(not set)"}
            TERM_PROGRAM: {Environment.GetEnvironmentVariable("TERM_PROGRAM") ?? "(not set)"}
            COLORTERM: {Environment.GetEnvironmentVariable("COLORTERM") ?? "(not set)"}

            Protocol Support:
            {string.Join(Environment.NewLine, protocols)}

            Best Protocol: {DetectBestProtocol()}
            """;
    }
}
