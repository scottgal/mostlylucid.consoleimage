namespace ConsoleImage.Core.Subtitles;

/// <summary>
/// Represents a single subtitle entry with timing and text.
/// </summary>
public class SubtitleEntry
{
    /// <summary>
    /// Sequential index of this subtitle (1-based for SRT compatibility).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Start time when the subtitle should appear.
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// End time when the subtitle should disappear.
    /// </summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// The subtitle text (may contain newlines for multi-line subtitles).
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Speaker identifier for diarization (e.g., "SPEAKER_00", "SPEAKER_01").
    /// Null if diarization is not enabled.
    /// </summary>
    public string? SpeakerId { get; set; }

    /// <summary>
    /// Duration of the subtitle display.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Split text into individual lines.
    /// </summary>
    public string[] Lines => Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Check if this subtitle is active at the given timestamp.
    /// </summary>
    /// <param name="timestamp">The timestamp to check.</param>
    /// <returns>True if the subtitle should be displayed at this time.</returns>
    public bool IsActiveAt(TimeSpan timestamp) => timestamp >= StartTime && timestamp < EndTime;

    /// <summary>
    /// Check if this subtitle is active at the given timestamp in seconds.
    /// </summary>
    /// <param name="seconds">The timestamp in seconds.</param>
    /// <returns>True if the subtitle should be displayed at this time.</returns>
    public bool IsActiveAt(double seconds) => IsActiveAt(TimeSpan.FromSeconds(seconds));

    public override string ToString() => $"[{Index}] {StartTime:hh\\:mm\\:ss\\.fff} --> {EndTime:hh\\:mm\\:ss\\.fff}: {Text}";
}
