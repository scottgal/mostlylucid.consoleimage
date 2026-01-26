using System.Text;

namespace ConsoleImage.Core.Subtitles;

/// <summary>
///     Renders subtitles for console display below the main frame.
/// </summary>
public class SubtitleRenderer
{
    /// <summary>
    ///     Colors used for different speakers in diarization.
    ///     First speaker gets yellow (not white) to visually distinguish from non-diarized subtitles.
    /// </summary>
    private static readonly (byte R, byte G, byte B)[] SpeakerColors =
    [
        (255, 255, 100), // Yellow (Speaker 1)
        (100, 255, 255), // Cyan (Speaker 2)
        (255, 150, 150), // Light red/pink (Speaker 3)
        (150, 255, 150), // Light green (Speaker 4)
        (200, 150, 255), // Light purple (Speaker 5)
        (255, 200, 100), // Orange (Speaker 6)
        (150, 200, 255), // Light blue (Speaker 7)
        (255, 255, 255) // White (Speaker 8)
    ];

    private readonly string _blankLine; // Cached blank line of _maxWidth spaces
    private readonly int _maxLines;
    private readonly int _maxWidth;
    private readonly StringBuilder _positionSb = new(512);

    // Pre-allocated reusable buffers (avoids per-frame allocations)
    private readonly StringBuilder _renderSb = new(512);
    private readonly Dictionary<string, SubtitleStyle> _speakerStyles = new();
    private readonly SubtitleStyle _style;
    private readonly bool _useColor;

    /// <summary>
    ///     Create a subtitle renderer.
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
        _blankLine = new string(' ', maxWidth);
    }

    /// <summary>
    ///     Render subtitle text for console output.
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
    ///     Render subtitle text for console output.
    /// </summary>
    /// <param name="track">The subtitle track.</param>
    /// <param name="seconds">Current playback timestamp in seconds.</param>
    /// <returns>Formatted string with ANSI positioning to display below frame.</returns>
    public string Render(SubtitleTrack track, double seconds)
    {
        return Render(track, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    ///     Render a specific subtitle entry.
    /// </summary>
    /// <param name="entry">The subtitle entry to render, or null for blank lines.</param>
    /// <returns>Formatted subtitle lines.</returns>
    public string RenderEntry(SubtitleEntry? entry)
    {
        _renderSb.Clear();

        if (entry == null)
        {
            // Render blank lines to clear previous subtitle
            for (var i = 0; i < _maxLines; i++)
            {
                _renderSb.Append(_blankLine);
                if (i < _maxLines - 1)
                    _renderSb.AppendLine();
            }

            return _renderSb.ToString();
        }

        // SECURITY: Sanitize external text to prevent ANSI escape sequence injection
        // Subtitle files from external sources could contain malicious terminal codes
        var safeText = SecurityHelper.StripAnsiCodes(entry.Text);

        // Get speaker-specific style if diarization is enabled
        var style = GetStyleForSpeaker(entry.SpeakerId);
        var lines = FormatText(safeText);

        for (var i = 0; i < _maxLines; i++)
        {
            if (i < lines.Length)
            {
                var line = lines[i];

                // Add speaker prefix on first line if available
                if (i == 0 && !string.IsNullOrEmpty(entry.SpeakerId))
                {
                    // SECURITY: Sanitize speaker ID before display
                    var safeSpeakerId = SecurityHelper.StripAnsiCodes(entry.SpeakerId);
                    var speakerName = GetSpeakerDisplayName(safeSpeakerId);
                    line = $"{speakerName}: {line}";
                }

                var padding = Math.Max(0, (_maxWidth - line.Length) / 2);
                var lineLen = Math.Min(padding + line.Length, _maxWidth);

                if (_useColor)
                    _renderSb.Append(style.AnsiPrefix);

                // Center: padding spaces + line text + trailing spaces
                _renderSb.Append(_blankLine, 0, padding);
                _renderSb.Append(line);
                var remaining = _maxWidth - lineLen;
                if (remaining > 0)
                    _renderSb.Append(_blankLine, 0, remaining);

                if (_useColor)
                    _renderSb.Append("\x1b[0m");
            }
            else
            {
                // Blank line padding
                _renderSb.Append(_blankLine);
            }

            if (i < _maxLines - 1)
                _renderSb.AppendLine();
        }

        return _renderSb.ToString();
    }

    /// <summary>
    ///     Get style for a specific speaker, creating one if needed.
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
    ///     Get display name for a speaker ID.
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
    ///     Render subtitle at a specific cursor position.
    /// </summary>
    /// <param name="entry">The subtitle entry.</param>
    /// <param name="row">Starting row (1-based).</param>
    /// <param name="col">Starting column (1-based, default: 1).</param>
    /// <returns>String with cursor positioning and subtitle content.</returns>
    public string RenderAtPosition(SubtitleEntry? entry, int row, int col = 1)
    {
        var content = RenderEntry(entry);

        // Reuse position StringBuilder; parse lines without Split allocation
        _positionSb.Clear();
        var lineIdx = 0;
        var start = 0;

        for (var i = 0; i <= content.Length; i++)
            if (i == content.Length || content[i] == '\n')
            {
                var end = i;
                if (end > start && content[end - 1] == '\r') end--;

                _positionSb.Append("\x1b[");
                _positionSb.Append(row + lineIdx);
                _positionSb.Append(';');
                _positionSb.Append(col);
                _positionSb.Append('H');
                _positionSb.Append(content, start, end - start);

                lineIdx++;
                start = i + 1;
            }

        return _positionSb.ToString();
    }

    /// <summary>
    ///     Render arbitrary text (useful for status messages like "Transcribing...").
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
    ///     Get plain text representation of the subtitle (for GIF burning).
    /// </summary>
    public string GetPlainText(SubtitleEntry? entry)
    {
        if (entry == null)
            return "";

        var lines = FormatText(entry.Text);
        return string.Join("\n", lines);
    }

    /// <summary>
    ///     Get plain text lines for the subtitle (for GIF burning).
    /// </summary>
    public string[] GetPlainTextLines(SubtitleEntry? entry)
    {
        if (entry == null)
            return Array.Empty<string>();

        return FormatText(entry.Text);
    }

    /// <summary>
    ///     Format subtitle text with word wrapping and line limits.
    ///     Uses balanced wrapping to distribute text evenly across lines.
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

                if (!string.IsNullOrEmpty(currentLine) && result.Count < _maxLines) result.Add(currentLine);
            }
        }

        return result.ToArray();
    }

    /// <summary>
    ///     Balance text across 2 lines by splitting at a word boundary near the middle.
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
///     Visual style options for subtitle rendering.
/// </summary>
public class SubtitleStyle
{
    private string? _cachedAnsiPrefix;

    /// <summary>
    ///     Bold text.
    /// </summary>
    public bool Bold { get; set; } = true;

    /// <summary>
    ///     Foreground color RGB (null = white).
    /// </summary>
    public (byte R, byte G, byte B)? ForegroundColor { get; set; }

    /// <summary>
    ///     Background color RGB (null = transparent).
    /// </summary>
    public (byte R, byte G, byte B)? BackgroundColor { get; set; }

    /// <summary>
    ///     Default style: bold white text.
    /// </summary>
    public static SubtitleStyle Default => new()
    {
        Bold = true,
        ForegroundColor = (255, 255, 255)
    };

    /// <summary>
    ///     Yellow subtitle style (common in video players).
    /// </summary>
    public static SubtitleStyle Yellow => new()
    {
        Bold = true,
        ForegroundColor = (255, 255, 0)
    };

    /// <summary>
    ///     Cached ANSI prefix codes for this style. Computed once on first access.
    /// </summary>
    public string AnsiPrefix => _cachedAnsiPrefix ??= BuildAnsiPrefix();

    private string BuildAnsiPrefix()
    {
        var sb = new StringBuilder(32);
        sb.Append("\x1b[");

        var needSemicolon = false;

        if (Bold)
        {
            sb.Append('1');
            needSemicolon = true;
        }

        if (ForegroundColor.HasValue)
        {
            var (r, g, b) = ForegroundColor.Value;
            if (needSemicolon) sb.Append(';');
            sb.Append("38;2;");
            sb.Append(r);
            sb.Append(';');
            sb.Append(g);
            sb.Append(';');
            sb.Append(b);
            needSemicolon = true;
        }

        if (BackgroundColor.HasValue)
        {
            var (r, g, b) = BackgroundColor.Value;
            if (needSemicolon) sb.Append(';');
            sb.Append("48;2;");
            sb.Append(r);
            sb.Append(';');
            sb.Append(g);
            sb.Append(';');
            sb.Append(b);
        }

        sb.Append('m');
        return sb.ToString();
    }
}