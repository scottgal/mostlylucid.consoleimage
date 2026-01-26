// Time parsing utilities for CLI options

namespace ConsoleImage.Cli.Utilities;

/// <summary>
///     Parses time strings in various formats to seconds.
///     Supports: seconds (4.7), mm:ss (6:47), hh:mm:ss (1:30:00)
/// </summary>
public static class TimeParser
{
    /// <summary>
    ///     Parse a time string to seconds.
    /// </summary>
    /// <param name="input">Time string: "4.7", "6:47", "1:30:00"</param>
    /// <returns>Time in seconds</returns>
    /// <exception cref="FormatException">Thrown with helpful error message if format is invalid</exception>
    public static double Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Time value cannot be empty");

        input = input.Trim();

        // Check for time format with colons (mm:ss or hh:mm:ss)
        if (input.Contains(':'))
        {
            var parts = input.Split(':');

            if (parts.Length == 2)
            {
                // mm:ss format
                if (!int.TryParse(parts[0], out var minutes) || minutes < 0)
                    throw new FormatException(
                        $"Invalid minutes '{parts[0]}' in time '{input}'. " +
                        "Expected format: mm:ss (e.g., 6:47) or hh:mm:ss (e.g., 1:30:00)");

                if (!double.TryParse(parts[1], out var seconds) || seconds < 0 || seconds >= 60)
                    throw new FormatException(
                        $"Invalid seconds '{parts[1]}' in time '{input}'. " +
                        "Seconds must be 0-59. Expected format: mm:ss (e.g., 6:47)");

                return minutes * 60 + seconds;
            }

            if (parts.Length == 3)
            {
                // hh:mm:ss format
                if (!int.TryParse(parts[0], out var hours) || hours < 0)
                    throw new FormatException(
                        $"Invalid hours '{parts[0]}' in time '{input}'. " +
                        "Expected format: hh:mm:ss (e.g., 1:30:00)");

                if (!int.TryParse(parts[1], out var minutes) || minutes < 0 || minutes >= 60)
                    throw new FormatException(
                        $"Invalid minutes '{parts[1]}' in time '{input}'. " +
                        "Minutes must be 0-59. Expected format: hh:mm:ss (e.g., 1:30:00)");

                if (!double.TryParse(parts[2], out var seconds) || seconds < 0 || seconds >= 60)
                    throw new FormatException(
                        $"Invalid seconds '{parts[2]}' in time '{input}'. " +
                        "Seconds must be 0-59. Expected format: hh:mm:ss (e.g., 1:30:00)");

                return hours * 3600 + minutes * 60 + seconds;
            }

            throw new FormatException(
                $"Invalid time format '{input}'. " +
                "Expected: seconds (4.7), mm:ss (6:47), or hh:mm:ss (1:30:00)");
        }

        // Plain seconds (with optional decimal)
        if (double.TryParse(input, out var result))
        {
            if (result < 0)
                throw new FormatException($"Time cannot be negative: '{input}'");
            return result;
        }

        throw new FormatException(
            $"Invalid time format '{input}'. " +
            "Expected: seconds (4.7), mm:ss (6:47), hh:mm:ss (1:30:00), or use -sm for decimal minutes");
    }

    /// <summary>
    ///     Parse decimal minutes to seconds (e.g., 6.47 = 6m 28.2s)
    /// </summary>
    public static double ParseMinutes(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Minutes value cannot be empty");

        input = input.Trim();

        if (double.TryParse(input, out var minutes))
        {
            if (minutes < 0)
                throw new FormatException($"Minutes cannot be negative: '{input}'");
            return minutes * 60;
        }

        throw new FormatException(
            $"Invalid minutes format '{input}'. Expected decimal number (e.g., 6.47 = 6m 28.2s)");
    }

    /// <summary>
    ///     Parse nullable decimal minutes.
    /// </summary>
    public static double? ParseMinutesNullable(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;
        return ParseMinutes(input);
    }

    /// <summary>
    ///     Try to parse a time string. Returns false if invalid.
    /// </summary>
    public static bool TryParse(string? input, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        try
        {
            seconds = Parse(input);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Parse a nullable time string. Returns null if input is null/empty.
    /// </summary>
    /// <exception cref="FormatException">Thrown with helpful error if format is invalid</exception>
    public static double? ParseNullable(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;
        return Parse(input);
    }

    /// <summary>
    ///     Format seconds as a human-readable time string.
    /// </summary>
    public static string Format(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{ts:h\\:mm\\:ss}";
        if (ts.TotalMinutes >= 1)
            return $"{ts:m\\:ss}";
        return $"{seconds:F1}s";
    }
}