using System.Text.Json.Serialization;

namespace ConsoleImage.Core.Subtitles;

/// <summary>
/// Serializable DTO for storing subtitles in JSON/CIDZ documents.
/// </summary>
public class SubtitleTrackData
{
    /// <summary>
    /// Language code (e.g., "en", "es").
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// Original source file path.
    /// </summary>
    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; set; }

    /// <summary>
    /// List of subtitle entries.
    /// </summary>
    [JsonPropertyName("entries")]
    public List<SubtitleEntryData> Entries { get; set; } = new();

    /// <summary>
    /// Create from a SubtitleTrack.
    /// </summary>
    public static SubtitleTrackData FromTrack(SubtitleTrack track)
    {
        return new SubtitleTrackData
        {
            Language = track.Language,
            SourceFile = track.SourceFile,
            Entries = track.Entries.Select(e => new SubtitleEntryData
            {
                Index = e.Index,
                StartMs = (long)e.StartTime.TotalMilliseconds,
                EndMs = (long)e.EndTime.TotalMilliseconds,
                Text = e.Text
            }).ToList()
        };
    }

    /// <summary>
    /// Convert to a SubtitleTrack.
    /// </summary>
    public SubtitleTrack ToTrack()
    {
        return new SubtitleTrack
        {
            Language = Language,
            SourceFile = SourceFile,
            Entries = Entries.Select(e => new SubtitleEntry
            {
                Index = e.Index,
                StartTime = TimeSpan.FromMilliseconds(e.StartMs),
                EndTime = TimeSpan.FromMilliseconds(e.EndMs),
                Text = e.Text
            }).ToList()
        };
    }
}

/// <summary>
/// Serializable DTO for a single subtitle entry.
/// </summary>
public class SubtitleEntryData
{
    /// <summary>
    /// Sequential index.
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    /// Start time in milliseconds.
    /// </summary>
    [JsonPropertyName("startMs")]
    public long StartMs { get; set; }

    /// <summary>
    /// End time in milliseconds.
    /// </summary>
    [JsonPropertyName("endMs")]
    public long EndMs { get; set; }

    /// <summary>
    /// Subtitle text.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}
