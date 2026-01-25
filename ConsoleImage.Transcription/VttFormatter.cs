using System.Text;

namespace ConsoleImage.Transcription;

/// <summary>
/// Formats transcription segments to WebVTT subtitle format.
/// </summary>
public static class VttFormatter
{
    /// <summary>
    /// Format segments as VTT file content.
    /// </summary>
    public static string Format(IEnumerable<TranscriptSegment> segments, bool includeSpeakerIds = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        var index = 1;
        foreach (var segment in segments)
        {
            // VTT cue identifier (optional but useful)
            sb.AppendLine(index.ToString());
            sb.AppendLine($"{FormatTimestamp(segment.StartSeconds)} --> {FormatTimestamp(segment.EndSeconds)}");

            var text = segment.Text;
            if (includeSpeakerIds && !string.IsNullOrEmpty(segment.SpeakerId))
            {
                text = $"<v {segment.SpeakerId}>{text}";
            }
            sb.AppendLine(text);
            sb.AppendLine();

            index++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Write segments to VTT file.
    /// </summary>
    public static async Task WriteAsync(
        string path,
        IEnumerable<TranscriptSegment> segments,
        bool includeSpeakerIds = false,
        CancellationToken ct = default)
    {
        var content = Format(segments, includeSpeakerIds);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, ct);
    }

    private static string FormatTimestamp(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        // VTT uses HH:MM:SS.mmm format (dot, not comma like SRT)
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }
}
