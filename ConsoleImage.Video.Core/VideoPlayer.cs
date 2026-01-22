namespace ConsoleImage.Video.Core;

/// <summary>
/// Simple static API for playing videos in the terminal.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a streamlined API for common video playback scenarios.
/// For advanced control, use <see cref="VideoAnimationPlayer"/> directly.
/// </para>
/// <para>
/// <b>Basic usage:</b>
/// <code>
/// // Play a video file
/// await VideoPlayer.PlayAsync("video.mp4");
///
/// // Play with options
/// await VideoPlayer.PlayAsync("video.mp4", new VideoRenderOptions {
///     RenderMode = VideoRenderMode.Braille
/// });
/// </code>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Simple playback
/// await VideoPlayer.PlayAsync("movie.mp4");
///
/// // With ColorBlocks mode
/// await VideoPlayer.PlayAsync("movie.mp4", new VideoRenderOptions {
///     RenderMode = VideoRenderMode.ColorBlocks
/// });
///
/// // With cancellation support
/// using var cts = new CancellationTokenSource();
/// Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
/// await VideoPlayer.PlayAsync("movie.mp4", cancellationToken: cts.Token);
///
/// // Stream from URL
/// await VideoPlayer.PlayAsync("https://example.com/video.mp4");
///
/// // Play specific time range
/// await VideoPlayer.PlayAsync("movie.mp4", new VideoRenderOptions {
///     StartTime = 30,  // Start at 30 seconds
///     EndTime = 60     // End at 60 seconds
/// });
/// </code>
/// </example>
public static class VideoPlayer
{
    /// <summary>
    /// Play a video file in ASCII mode with default options.
    /// </summary>
    /// <param name="path">Path to the video file, or a URL</param>
    /// <param name="cancellationToken">Token to cancel playback</param>
    /// <returns>Task that completes when playback ends</returns>
    public static Task PlayAsync(string path, CancellationToken cancellationToken = default)
    {
        return PlayAsync(path, null, cancellationToken);
    }

    /// <summary>
    /// Play a video file with custom options.
    /// </summary>
    /// <param name="path">Path to the video file, or a URL</param>
    /// <param name="options">Playback and rendering options</param>
    /// <param name="cancellationToken">Token to cancel playback</param>
    /// <returns>Task that completes when playback ends</returns>
    /// <example>
    /// <code>
    /// var options = new VideoRenderOptions
    /// {
    ///     RenderMode = VideoRenderMode.Braille,
    ///     SpeedMultiplier = 1.5f,
    ///     LoopCount = 3
    /// };
    /// await VideoPlayer.PlayAsync("video.mp4", options);
    /// </code>
    /// </example>
    public static async Task PlayAsync(string path, VideoRenderOptions? options, CancellationToken cancellationToken = default)
    {
        using var player = new VideoAnimationPlayer(path, options);
        await player.PlayAsync(cancellationToken);
    }

    /// <summary>
    /// Get information about a video file.
    /// </summary>
    /// <param name="path">Path to the video file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Video metadata including duration, resolution, codec, etc.</returns>
    /// <example>
    /// <code>
    /// var info = await VideoPlayer.GetInfoAsync("video.mp4");
    /// Console.WriteLine($"Duration: {info.Duration}s");
    /// Console.WriteLine($"Resolution: {info.Width}x{info.Height}");
    /// Console.WriteLine($"FPS: {info.FrameRate}");
    /// </code>
    /// </example>
    public static async Task<VideoInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        using var ffmpeg = new FFmpegService();
        return await ffmpeg.GetVideoInfoAsync(path, cancellationToken);
    }
}
