using System.Text;

namespace ConsoleImage.Transcription;

/// <summary>
/// Formats transcription segments to SRT subtitle format.
/// </summary>
public static class SrtFormatter
{
    /// <summary>
    /// Format segments as SRT file content.
    /// </summary>
    public static string Format(IEnumerable<TranscriptSegment> segments, bool includeSpeakerIds = true)
    {
        var sb = new StringBuilder();
        var index = 1;

        foreach (var segment in segments)
        {
            sb.AppendLine(index.ToString());
            sb.AppendLine($"{FormatTimestamp(segment.StartSeconds)} --> {FormatTimestamp(segment.EndSeconds)}");

            var text = segment.Text;
            if (includeSpeakerIds && !string.IsNullOrEmpty(segment.SpeakerId))
            {
                text = $"[{segment.SpeakerId}] {text}";
            }
            sb.AppendLine(text);
            sb.AppendLine();

            index++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Write segments to SRT file.
    /// </summary>
    public static async Task WriteAsync(
        string path,
        IEnumerable<TranscriptSegment> segments,
        bool includeSpeakerIds = true,
        CancellationToken ct = default)
    {
        var content = Format(segments, includeSpeakerIds);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, ct);
    }

    /// <summary>
    /// Append segments to existing SRT file (for incremental writing).
    /// </summary>
    public static async Task AppendAsync(
        string path,
        TranscriptSegment segment,
        int index,
        bool includeSpeakerId = true,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(index.ToString());
        sb.AppendLine($"{FormatTimestamp(segment.StartSeconds)} --> {FormatTimestamp(segment.EndSeconds)}");

        var text = segment.Text;
        if (includeSpeakerId && !string.IsNullOrEmpty(segment.SpeakerId))
        {
            text = $"[{segment.SpeakerId}] {text}";
        }
        sb.AppendLine(text);
        sb.AppendLine();

        await File.AppendAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct);
    }

    private static string FormatTimestamp(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
    }
}
