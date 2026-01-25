namespace ConsoleImage.Core.Subtitles;

/// <summary>
/// Interface for providers that generate subtitles dynamically during playback.
/// Used for live transcription where subtitles are generated ahead of playback.
/// </summary>
public interface ILiveSubtitleProvider
{
    /// <summary>
    /// The subtitle track being populated with transcribed segments.
    /// Query with GetActiveAt() to get subtitles at a given time.
    /// </summary>
    SubtitleTrack Track { get; }

    /// <summary>
    /// Whether all subtitles have been generated.
    /// </summary>
    bool IsComplete { get; }

    /// <summary>
    /// The furthest timestamp that has been transcribed.
    /// </summary>
    double LastTranscribedTime { get; }

    /// <summary>
    /// Check if subtitles are ready for the given playback time without blocking.
    /// </summary>
    /// <param name="playbackTime">The playback timestamp in seconds.</param>
    /// <returns>True if subtitles for this time are ready.</returns>
    bool HasSubtitlesReadyFor(double playbackTime);

    /// <summary>
    /// Wait until transcription has caught up to the given playback time.
    /// Use this to prevent subtitles from getting out of sync - playback pauses if needed.
    /// </summary>
    /// <param name="playbackTime">The playback timestamp we need subtitles for.</param>
    /// <param name="timeoutMs">Maximum time to wait before giving up (0 = wait forever).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if subtitles are ready, false if timed out or complete but not at target.</returns>
    Task<bool> WaitForTranscriptionAsync(double playbackTime, int timeoutMs = 30000, CancellationToken ct = default);

    /// <summary>
    /// Ensure transcription has processed up to the specified time.
    /// Call this periodically during playback to keep subtitles buffered ahead.
    /// </summary>
    /// <param name="currentPlaybackTime">Current playback position in seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureTranscribedUpToAsync(double currentPlaybackTime, CancellationToken ct = default);
}
