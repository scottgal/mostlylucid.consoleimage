using System.Text;

namespace ConsoleImage.Core.Subtitles;

/// <summary>
/// Renders subtitles for console display below the main frame.
/// </summary>
public class SubtitleRenderer
{
    private readonly int _maxWidth;
    private readonly int _maxLines;
    private readonly bool _useColor;
    private readonly SubtitleStyle _style;
    private readonly Dictionary<string, SubtitleStyle> _speakerStyles = new();

    /// <summary>
    /// Colors used for different speakers in diarization.
    /// First speaker gets yellow (not white) to visually distinguish from non-diarized subtitles.
    /// </summary>
    private static readonly (byte R, byte G, byte B)[] SpeakerColors =
    [
        ((byte)255, (byte)255, (byte)100),  // Yellow (Speaker 1)
        ((byte)100, (byte)255, (byte)255),  // Cyan (Speaker 2)
        ((byte)255, (byte)150, (byte)150),  // Light red/pink (Speaker 3)
        ((byte)150, (byte)255, (byte)150),  // Light green (Speaker 4)
        ((byte)200, (byte)150, (byte)255),  // Light purple (Speaker 5)
        ((byte)255, (byte)200, (byte)100),  // Orange (Speaker 6)
        ((byte)150, (byte)200, (byte)255),  // Light blue (Speaker 7)
        ((byte)255, (byte)255, (byte)255),  // White (Speaker 8)
    ];

    /// <summary>
    /// Create a subtitle renderer.
    /// </summary>
    /// <param name="maxWidth">Maximum width in characters for subtitle display.</param>
    /// <param name="maxLines">Maximum lines of subtitle text (default: 2).</param>
    /// <param name="useColor">Whether to use ANSI color codes.</param>
    /// <param name="style">Visual style for subtitles.</param>
    public SubtitleRenderer(int maxWidth, int maxLines = 2, bool useColor = true, SubtitleStyle? style = null)
    {
        _maxWidth = maxWidth;
        _maxLines = maxLines;
        _useColor = useColor;
        _style = style ?? SubtitleStyle.Default;
    }

    /// <summary>
    /// Render subtitle text for console output.
    /// </summary>
    /// <param name="track">The subtitle track.</param>
    /// <param name="timestamp">Current playback timestamp.</param>
    /// <returns>Formatted string with ANSI positioning to display below frame.</returns>
    public string Render(SubtitleTrack track, TimeSpan timestamp)
    {
        var entry = track.GetActiveAt(timestamp);
        return RenderEntry(entry);
    }

    /// <summary>
    /// Render subtitle text for console output.
    /// </summary>
    /// <param name="track">The subtitle track.</param>
    /// <param name="seconds">Current playback timestamp in seconds.</param>
    /// <returns>Formatted string with ANSI positioning to display below frame.</returns>
    public string Render(SubtitleTrack track, double seconds)
    {
        return Render(track, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// Render a specific subtitle entry.
    /// </summary>
    /// <param name="entry">The subtitle entry to render, or null for blank lines.</param>
    /// <returns>Formatted subtitle lines.</returns>
    public string RenderEntry(SubtitleEntry? entry)
    {
        var sb = new StringBuilder();

        if (entry == null)
        {
            // Render blank lines to clear previous subtitle
            for (var i = 0; i < _maxLines; i++)
            {
                sb.Append(new string(' ', _maxWidth));
                if (i < _maxLines - 1)
                    sb.AppendLine();
            }
            return sb.ToString();
        }

        // Get speaker-specific style if diarization is enabled
        var style = GetStyleForSpeaker(entry.SpeakerId);
        var lines = FormatText(entry.Text);

        for (var i = 0; i < _maxLines; i++)
        {
            if (i < lines.Length)
            {
                var line = lines[i];

                // Add speaker prefix on first line if available
                if (i == 0 && !string.IsNullOrEmpty(entry.SpeakerId))
                {
                    var speakerName = GetSpeakerDisplayName(entry.SpeakerId);
                    line = $"{speakerName}: {line}";
                }

                var padding = (_maxWidth - line.Length) / 2;
                var centeredLine = new string(' ', Math.Max(0, padding)) + line;

                // Pad to full width
                if (centeredLine.Length < _maxWidth)
                    centeredLine = centeredLine.PadRight(_maxWidth);

                if (_useColor)
                {
                    sb.Append(style.GetAnsiPrefix());
                    sb.Append(centeredLine);
                    sb.Append("\x1b[0m");
                }
                else
                {
                    sb.Append(centeredLine);
                }
            }
            else
            {
                // Blank line padding
                sb.Append(new string(' ', _maxWidth));
            }

            if (i < _maxLines - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Get style for a specific speaker, creating one if needed.
    /// </summary>
    private SubtitleStyle GetStyleForSpeaker(string? speakerId)
    {
        if (string.IsNullOrEmpty(speakerId))
            return _style;

        if (!_speakerStyles.TryGetValue(speakerId, out var style))
        {
            // Assign color based on speaker count
            var colorIndex = _speakerStyles.Count % SpeakerColors.Length;
            var color = SpeakerColors[colorIndex];

            style = new SubtitleStyle
            {
                Bold = true,
                ForegroundColor = color
            };
            _speakerStyles[speakerId] = style;
        }

        return style;
    }

    /// <summary>
    /// Get display name for a speaker ID.
    /// </summary>
    private static string GetSpeakerDisplayName(string speakerId)
    {
        // Convert "SPEAKER_00" to "Speaker 1", etc.
        if (speakerId.StartsWith("SPEAKER_", StringComparison.OrdinalIgnoreCase))
        {
            var numPart = speakerId[8..];
            if (int.TryParse(numPart, out var num))
                return $"Speaker {num + 1}";
        }
        return speakerId;
    }

    /// <summary>
    /// Render subtitle at a specific cursor position.
    /// </summary>
    /// <param name="entry">The subtitle entry.</param>
    /// <param name="row">Starting row (1-based).</param>
    /// <param name="col">Starting column (1-based, default: 1).</param>
    /// <returns>String with cursor positioning and subtitle content.</returns>
    public string RenderAtPosition(SubtitleEntry? entry, int row, int col = 1)
    {
        var sb = new StringBuilder();
        var content = RenderEntry(entry);
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            sb.Append($"\x1b[{row + i};{col}H"); // Move cursor to position
            sb.Append(lines[i].TrimEnd('\r'));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Render arbitrary text (useful for status messages like "Transcribing...").
    /// </summary>
    /// <param name="text">The text to render.</param>
    /// <returns>Formatted subtitle-style output.</returns>
    public string RenderText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return RenderEntry(null);

        // Create a temporary entry for rendering
        var entry = new SubtitleEntry
        {
            Text = text,
            StartTime = TimeSpan.Zero,
            EndTime = TimeSpan.FromSeconds(1)
        };
        return RenderEntry(entry);
    }

    /// <summary>
    /// Get plain text representation of the subtitle (for GIF burning).
    /// </summary>
    public string GetPlainText(SubtitleEntry? entry)
    {
        if (entry == null)
            return "";

        var lines = FormatText(entry.Text);
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Get plain text lines for the subtitle (for GIF burning).
    /// </summary>
    public string[] GetPlainTextLines(SubtitleEntry? entry)
    {
        if (entry == null)
            return Array.Empty<string>();

        return FormatText(entry.Text);
    }

    /// <summary>
    /// Format subtitle text with word wrapping and line limits.
    /// Uses balanced wrapping to distribute text evenly across lines.
    /// </summary>
    private string[] FormatText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var sourceLines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        // If source already has multiple lines, use them directly (don't re-balance)
        if (sourceLines.Length >= 2)
        {
            foreach (var sourceLine in sourceLines)
            {
                if (result.Count >= _maxLines)
                    break;

                var trimmed = sourceLine.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // Truncate if too long
                if (trimmed.Length > _maxWidth)
                    trimmed = trimmed[.._maxWidth];

                result.Add(trimmed);
            }
            return result.ToArray();
        }

        // Single source line - apply balancing/wrapping
        foreach (var sourceLine in sourceLines)
        {
            if (result.Count >= _maxLines)
                break;

            var trimmed = sourceLine.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // If it fits on one line and we only have room for one more line, use it
            if (trimmed.Length <= _maxWidth && result.Count == _maxLines - 1)
            {
                result.Add(trimmed);
            }
            // If text is short enough for one line but we have 2 lines available,
            // and it's longer than half the width, try to balance across 2 lines
            else if (trimmed.Length <= _maxWidth && trimmed.Length > _maxWidth / 2 && result.Count < _maxLines - 1)
            {
                var balanced = BalanceText(trimmed, _maxWidth);
                foreach (var line in balanced)
                {
                    if (result.Count >= _maxLines) break;
                    result.Add(line);
                }
            }
            else if (trimmed.Length <= _maxWidth)
            {
                result.Add(trimmed);
            }
            else
            {
                // Word wrap
                var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var currentLine = "";

                foreach (var word in words)
                {
                    if (result.Count >= _maxLines)
                        break;

                    if (string.IsNullOrEmpty(currentLine))
                    {
                        currentLine = word.Length <= _maxWidth ? word : word[.._maxWidth];
                    }
                    else if (currentLine.Length + 1 + word.Length <= _maxWidth)
                    {
                        currentLine += " " + word;
                    }
                    else
                    {
                        result.Add(currentLine);
                        currentLine = word.Length <= _maxWidth ? word : word[.._maxWidth];
                    }
                }

                if (!string.IsNullOrEmpty(currentLine) && result.Count < _maxLines)
                {
                    result.Add(currentLine);
                }
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Balance text across 2 lines by splitting at a word boundary near the middle.
    /// </summary>
    private static string[] BalanceText(string text, int maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1)
            return new[] { text };

        // Find the split point that gives the most balanced lines
        var targetLength = text.Length / 2;
        var bestSplit = 0;
        var bestDiff = int.MaxValue;
        var currentLength = 0;

        for (var i = 0; i < words.Length - 1; i++)
        {
            currentLength += words[i].Length + (i > 0 ? 1 : 0);
            var diff = Math.Abs(currentLength - targetLength);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestSplit = i + 1;
            }
        }

        var line1 = string.Join(" ", words.Take(bestSplit));
        var line2 = string.Join(" ", words.Skip(bestSplit));

        // Only use 2 lines if both fit within maxWidth
        if (line1.Length <= maxWidth && line2.Length <= maxWidth)
            return new[] { line1, line2 };

        // Fall back to single line if balancing doesn't work
        return new[] { text };
    }
}

/// <summary>
/// Visual style options for subtitle rendering.
/// </summary>
public class SubtitleStyle
{
    /// <summary>
    /// Bold text.
    /// </summary>
    public bool Bold { get; set; } = true;

    /// <summary>
    /// Foreground color RGB (null = white).
    /// </summary>
    public (byte R, byte G, byte B)? ForegroundColor { get; set; }

    /// <summary>
    /// Background color RGB (null = transparent).
    /// </summary>
    public (byte R, byte G, byte B)? BackgroundColor { get; set; }

    /// <summary>
    /// Default style: bold white text.
    /// </summary>
    public static SubtitleStyle Default => new()
    {
        Bold = true,
        ForegroundColor = (255, 255, 255)
    };

    /// <summary>
    /// Yellow subtitle style (common in video players).
    /// </summary>
    public static SubtitleStyle Yellow => new()
    {
        Bold = true,
        ForegroundColor = (255, 255, 0)
    };

    /// <summary>
    /// Get ANSI prefix codes for this style.
    /// </summary>
    public string GetAnsiPrefix()
    {
        var sb = new StringBuilder();
        sb.Append("\x1b[");

        var codes = new List<string>();

        if (Bold)
            codes.Add("1");

        if (ForegroundColor.HasValue)
        {
            var (r, g, b) = ForegroundColor.Value;
            codes.Add($"38;2;{r};{g};{b}");
        }

        if (BackgroundColor.HasValue)
        {
            var (r, g, b) = BackgroundColor.Value;
            codes.Add($"48;2;{r};{g};{b}");
        }

        sb.Append(string.Join(";", codes));
        sb.Append('m');

        return sb.ToString();
    }
}
