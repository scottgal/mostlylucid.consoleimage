namespace ConsoleImage.Transcription;

/// <summary>
/// A single transcribed segment with timing information.
/// </summary>
public record TranscriptSegment
{
    /// <summary>Start time in seconds.</summary>
    public double StartSeconds { get; init; }

    /// <summary>End time in seconds.</summary>
    public double EndSeconds { get; init; }

    /// <summary>Transcribed text.</summary>
    public string Text { get; init; } = "";

    /// <summary>Confidence score (0-1).</summary>
    public float Confidence { get; init; }

    /// <summary>Speaker ID if diarization is enabled.</summary>
    public string? SpeakerId { get; init; }

    /// <summary>Duration in seconds.</summary>
    public double DurationSeconds => EndSeconds - StartSeconds;

    /// <summary>Start time as TimeSpan.</summary>
    public TimeSpan StartTime => TimeSpan.FromSeconds(StartSeconds);

    /// <summary>End time as TimeSpan.</summary>
    public TimeSpan EndTime => TimeSpan.FromSeconds(EndSeconds);
}

/// <summary>
/// Full transcription result.
/// </summary>
public record TranscriptionResult
{
    /// <summary>All transcribed segments.</summary>
    public List<TranscriptSegment> Segments { get; init; } = new();

    /// <summary>Full text (all segments concatenated).</summary>
    public string FullText => string.Join(" ", Segments.Select(s => s.Text));

    /// <summary>Detected or specified language.</summary>
    public string? Language { get; init; }

    /// <summary>Number of unique speakers (if diarization was used).</summary>
    public int SpeakerCount { get; init; }

    /// <summary>Processing time in milliseconds.</summary>
    public long ProcessingTimeMs { get; init; }

    /// <summary>Total audio duration in seconds.</summary>
    public double AudioDurationSeconds { get; init; }
}
