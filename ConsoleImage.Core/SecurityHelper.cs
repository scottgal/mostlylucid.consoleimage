// SecurityHelper - Input validation utilities to prevent command injection

using System.Text;
using System.Text.RegularExpressions;

namespace ConsoleImage.Core;

/// <summary>
///     Security utilities for input validation and command injection prevention.
/// </summary>
public static class SecurityHelper
{
    /// <summary>
    ///     Shell metacharacters that could be used for command injection.
    /// </summary>
    private static readonly char[] ShellMetacharacters =
    {
        '|', '&', ';', '$', '`', '(', ')', '{', '}', '<', '>', '\n', '\r', '\0'
    };

    /// <summary>
    ///     Check if a string contains shell metacharacters that could be used for command injection.
    /// </summary>
    /// <param name="input">String to check.</param>
    /// <returns>True if dangerous characters found.</returns>
    public static bool ContainsShellMetacharacters(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        return input.IndexOfAny(ShellMetacharacters) >= 0;
    }

    /// <summary>
    ///     Validate that a file path is safe to use in command arguments.
    /// </summary>
    /// <param name="path">File path to validate.</param>
    /// <param name="requireExists">If true, also verifies the file exists.</param>
    /// <returns>True if path is safe to use.</returns>
    public static bool IsValidFilePath(string? path, bool requireExists = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Check for shell metacharacters
        if (ContainsShellMetacharacters(path))
            return false;

        // Check if path contains directory traversal attempts
        if (path.Contains(".."))
            // Allow if it resolves to a valid absolute path
            try
            {
                var fullPath = Path.GetFullPath(path);
                // If the resolved path still contains ".." something is wrong
                if (fullPath.Contains(".."))
                    return false;
            }
            catch (PathTooLongException)
            {
                // Long paths are valid on modern Windows/.NET - don't reject them
                // Path.GetFullPath may throw for very long relative paths with ".."
                // but absolute long paths are fine
                if (Path.IsPathRooted(path))
                    return true; // Absolute long path - allow it
                return false;
            }
            catch
            {
                return false;
            }

        // Verify file exists if required
        if (requireExists && !File.Exists(path) && !Directory.Exists(path))
            return false;

        return true;
    }

    /// <summary>
    ///     Validate that a URL is safe to use (only http/https protocols).
    /// </summary>
    /// <param name="url">URL to validate.</param>
    /// <returns>True if URL is safe.</returns>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Only allow http and https protocols
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == "http" || uri.Scheme == "https";
    }

    /// <summary>
    ///     Escape a string for safe use in shell arguments.
    ///     Wraps in quotes and escapes internal quotes.
    /// </summary>
    /// <param name="argument">Argument to escape.</param>
    /// <returns>Safely escaped argument string.</returns>
    public static string EscapeShellArgument(string? argument)
    {
        if (string.IsNullOrEmpty(argument))
            return "\"\"";

        // For Windows, escape double quotes by doubling them
        if (OperatingSystem.IsWindows()) return "\"" + argument.Replace("\"", "\"\"") + "\"";

        // For Unix-like systems, use single quotes and escape single quotes
        return "'" + argument.Replace("'", "'\\''") + "'";
    }

    /// <summary>
    ///     Sanitize a filename to remove potentially dangerous characters.
    /// </summary>
    /// <param name="filename">Filename to sanitize.</param>
    /// <param name="replacement">Replacement character for invalid chars.</param>
    /// <returns>Sanitized filename.</returns>
    public static string SanitizeFilename(string? filename, char replacement = '_')
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "unnamed";

        var invalid = Path.GetInvalidFileNameChars()
            .Concat(ShellMetacharacters)
            .Distinct()
            .ToArray();

        var sanitized = string.Concat(filename.Select(c => invalid.Contains(c) ? replacement : c));

        // Also remove leading/trailing dots and spaces
        sanitized = sanitized.Trim('.', ' ');

        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
    }

    /// <summary>
    ///     Remove ANSI escape sequences from text to prevent terminal injection attacks.
    ///     External text (subtitles, speaker names) should always be sanitized before display.
    /// </summary>
    /// <param name="text">Text that may contain ANSI escape sequences.</param>
    /// <returns>Text with all ANSI sequences removed.</returns>
    public static string StripAnsiCodes(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Remove ANSI CSI sequences: ESC [ ... letter
        // Also removes OSC sequences: ESC ] ... BEL/ST
        var result = Regex.Replace(
            text,
            @"\x1b[\[\]()#;?]?(?:[0-9]{1,4}(?:;[0-9]{0,4})*)?[A-Za-z@^`\x7f]?",
            string.Empty);

        // Also strip bare ESC and other control characters (except common whitespace)
        var sb = new StringBuilder(result.Length);
        foreach (var c in result)
            // Allow printable characters, space, tab, newline
            if (c >= ' ' || c == '\t' || c == '\n' || c == '\r')
                sb.Append(c);

        return sb.ToString();
    }
}