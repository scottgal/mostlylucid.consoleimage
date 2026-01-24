using System.Text.RegularExpressions;

namespace ConsoleImage.Core.Subtitles;

/// <summary>
/// Parser for SRT and WebVTT subtitle formats.
/// </summary>
public static partial class SubtitleParser
{
    // SRT timecode: 00:00:01,000 --> 00:00:04,000
    [GeneratedRegex(@"(\d{2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})[,.](\d{3})", RegexOptions.Compiled)]
    private static partial Regex SrtTimecodeRegex();

    // VTT timecode: 00:00:01.000 --> 00:00:04.000 (or without hours: 00:01.000)
    [GeneratedRegex(@"(?:(\d{2}):)?(\d{2}):(\d{2})\.(\d{3})\s*-->\s*(?:(\d{2}):)?(\d{2}):(\d{2})\.(\d{3})", RegexOptions.Compiled)]
    private static partial Regex VttTimecodeRegex();

    // HTML tags to strip
    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    // VTT cue settings (position, align, etc.)
    [GeneratedRegex(@"\s+(position|line|align|size|vertical):[^\s]+", RegexOptions.Compiled)]
    private static partial Regex VttCueSettingsRegex();

    /// <summary>
    /// Parse a subtitle file, auto-detecting format from content.
    /// </summary>
    /// <param name="filePath">Path to the subtitle file.</param>
    /// <returns>Parsed subtitle track.</returns>
    public static async Task<SubtitleTrack> ParseAsync(string filePath, CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        return Parse(content, filePath);
    }

    /// <summary>
    /// Parse subtitle content, auto-detecting format.
    /// </summary>
    /// <param name="content">Subtitle file content.</param>
    /// <param name="sourcePath">Optional source path for metadata.</param>
    /// <returns>Parsed subtitle track.</returns>
    public static SubtitleTrack Parse(string content, string? sourcePath = null)
    {
        // Detect format by content
        var isVtt = content.TrimStart().StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase);

        var track = isVtt ? ParseVtt(content) : ParseSrt(content);
        track.SourceFile = sourcePath;

        // Try to extract language from filename (e.g., "video.en.srt")
        if (!string.IsNullOrEmpty(sourcePath))
        {
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            var parts = fileName.Split('.');
            if (parts.Length >= 2)
            {
                var langPart = parts[^1];
                if (langPart.Length is 2 or 3) // ISO language codes
                    track.Language = langPart.ToLowerInvariant();
            }
        }

        return track;
    }

    /// <summary>
    /// Parse SRT (SubRip) format subtitles.
    /// </summary>
    /// <param name="content">SRT file content.</param>
    /// <returns>Parsed subtitle track.</returns>
    public static SubtitleTrack ParseSrt(string content)
    {
        var track = new SubtitleTrack();
        var lines = content.Split('\n');
        var i = 0;

        while (i < lines.Length)
        {
            // Skip empty lines
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                i++;

            if (i >= lines.Length)
                break;

            // Try to parse index number
            var indexLine = lines[i].Trim();
            if (!int.TryParse(indexLine, out var index))
            {
                i++;
                continue;
            }
            i++;

            if (i >= lines.Length)
                break;

            // Parse timecode line
            var timeLine = lines[i].Trim();
            var match = SrtTimecodeRegex().Match(timeLine);
            if (!match.Success)
            {
                i++;
                continue;
            }

            var startTime = new TimeSpan(
                0,
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
                int.Parse(match.Groups[4].Value));

            var endTime = new TimeSpan(
                0,
                int.Parse(match.Groups[5].Value),
                int.Parse(match.Groups[6].Value),
                int.Parse(match.Groups[7].Value),
                int.Parse(match.Groups[8].Value));

            i++;

            // Collect text lines until empty line or end
            var textLines = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                textLines.Add(lines[i].TrimEnd('\r'));
                i++;
            }

            if (textLines.Count > 0)
            {
                var text = StripHtmlTags(string.Join("\n", textLines));
                track.Entries.Add(new SubtitleEntry
                {
                    Index = index,
                    StartTime = startTime,
                    EndTime = endTime,
                    Text = text
                });
            }
        }

        return track;
    }

    /// <summary>
    /// Parse WebVTT format subtitles.
    /// </summary>
    /// <param name="content">VTT file content.</param>
    /// <returns>Parsed subtitle track.</returns>
    public static SubtitleTrack ParseVtt(string content)
    {
        var track = new SubtitleTrack();
        var lines = content.Split('\n');
        var i = 0;
        var index = 0;

        // Skip WEBVTT header and any metadata
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                break;
            }
            // Skip header, NOTE, STYLE, REGION blocks
            if (line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("STYLE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("REGION", StringComparison.OrdinalIgnoreCase))
            {
                // Skip until empty line
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                    i++;
            }
            i++;
        }

        while (i < lines.Length)
        {
            // Skip empty lines
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                i++;

            if (i >= lines.Length)
                break;

            // Check if this line is a cue identifier (optional in VTT)
            var currentLine = lines[i].Trim();

            // Check if it's a timecode line
            var match = VttTimecodeRegex().Match(currentLine);
            if (!match.Success)
            {
                // Might be a cue identifier, skip and try next line
                i++;
                if (i >= lines.Length)
                    break;
                currentLine = lines[i].Trim();
                match = VttTimecodeRegex().Match(currentLine);
                if (!match.Success)
                {
                    continue;
                }
            }

            // Parse timecode
            var startHours = string.IsNullOrEmpty(match.Groups[1].Value) ? 0 : int.Parse(match.Groups[1].Value);
            var startTime = new TimeSpan(
                0,
                startHours,
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
                int.Parse(match.Groups[4].Value));

            var endHours = string.IsNullOrEmpty(match.Groups[5].Value) ? 0 : int.Parse(match.Groups[5].Value);
            var endTime = new TimeSpan(
                0,
                endHours,
                int.Parse(match.Groups[6].Value),
                int.Parse(match.Groups[7].Value),
                int.Parse(match.Groups[8].Value));

            i++;

            // Collect text lines until empty line or end
            var textLines = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                var textLine = lines[i].TrimEnd('\r');
                // Remove VTT cue settings if they appear in the text
                textLine = VttCueSettingsRegex().Replace(textLine, "");
                textLines.Add(textLine);
                i++;
            }

            if (textLines.Count > 0)
            {
                index++;
                var text = StripHtmlTags(string.Join("\n", textLines));
                track.Entries.Add(new SubtitleEntry
                {
                    Index = index,
                    StartTime = startTime,
                    EndTime = endTime,
                    Text = text
                });
            }
        }

        return track;
    }

    /// <summary>
    /// Strip HTML tags from subtitle text.
    /// </summary>
    private static string StripHtmlTags(string text)
    {
        // Remove common VTT/SRT styling tags
        text = HtmlTagRegex().Replace(text, "");

        // Handle HTML entities
        text = text.Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&apos;", "'")
                   .Replace("&nbsp;", " ");

        return text.Trim();
    }

    /// <summary>
    /// Detect if a file is a supported subtitle format.
    /// </summary>
    public static bool IsSupportedFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".srt" or ".vtt" or ".webvtt";
    }
}
