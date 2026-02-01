using ConsoleImage.Core;
using ConsoleImage.Core.Subtitles;

namespace ConsoleImage.Video.Core;

/// <summary>
///     Frame sampling mode for video playback.
/// </summary>
public enum FrameSamplingMode
{
    /// <summary>
    ///     Render every frame (or use uniform FrameStep).
    /// </summary>
    Uniform,

    /// <summary>
    ///     Smart frame skipping using perceptual hashing.
    ///     Skips visually similar frames while maintaining timing.
    /// </summary>
    Smart
}

/// <summary>
///     Configuration options for video-to-ASCII rendering.
///     Extends RenderOptions with video-specific settings.
/// </summary>
public class VideoRenderOptions
{
    /// <summary>
    ///     Base render options for ASCII conversion.
    /// </summary>
    public RenderOptions RenderOptions { get; set; } = RenderOptions.Default.Clone();

    /// <summary>
    ///     Start time in seconds. Null = start of video.
    /// </summary>
    public double? StartTime { get; set; }

    /// <summary>
    ///     End time in seconds. Null = end of video.
    /// </summary>
    public double? EndTime { get; set; }

    /// <summary>
    ///     Target playback FPS. Null = use video's native framerate.
    ///     Lower values reduce CPU/memory usage.
    /// </summary>
    public double? TargetFps { get; set; }

    /// <summary>
    ///     Frame sampling rate. 1 = every frame, 2 = every 2nd frame, etc.
    ///     Applied at decode time for efficiency.
    ///     Default: 1 (no skipping)
    /// </summary>
    public int FrameStep { get; set; } = 1;

    /// <summary>
    ///     Frame sampling strategy for intelligent sampling.
    /// </summary>
    public FrameSamplingStrategy SamplingStrategy { get; set; } = FrameSamplingStrategy.Uniform;

    /// <summary>
    ///     Frame sampling mode: Uniform (fixed step) or Smart (perceptual hash skip).
    /// </summary>
    public FrameSamplingMode SamplingMode { get; set; } = FrameSamplingMode.Uniform;

    /// <summary>
    ///     Hash similarity threshold for smart frame skipping (0-64).
    ///     Lower = stricter matching (only nearly identical frames skipped).
    ///     Higher = more aggressive (may cause visible stuttering).
    ///     Default: 2 (conservative - only skip nearly identical frames)
    /// </summary>
    public int SmartSkipThreshold { get; set; } = 2;

    /// <summary>
    ///     Enable debug output for smart frame sampling.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    ///     Number of frames to buffer ahead during playback.
    ///     Higher values provide smoother playback but use more memory.
    ///     Default: 3
    /// </summary>
    public int BufferAheadFrames { get; set; } = 3;

    /// <summary>
    ///     Animation speed multiplier. 1.0 = normal speed.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1.0f;

    /// <summary>
    ///     Number of loops. 0 = infinite.
    /// </summary>
    public int LoopCount { get; set; } = 1;

    /// <summary>
    ///     Use alternate screen buffer for playback.
    ///     When true, preserves terminal scrollback.
    /// </summary>
    public bool UseAltScreen { get; set; } = true;

    /// <summary>
    ///     Enable hardware acceleration for video decoding.
    /// </summary>
    public bool UseHardwareAcceleration { get; set; } = true;

    /// <summary>
    ///     Rendering mode for output.
    /// </summary>
    public VideoRenderMode RenderMode { get; set; } = VideoRenderMode.Ascii;

    /// <summary>
    ///     Scene change detection threshold (0.0-1.0).
    ///     Used for scene-aware sampling. Lower = more sensitive.
    /// </summary>
    public double SceneThreshold { get; set; } = 0.4;

    /// <summary>
    ///     Show status line below the rendered output with progress, timing, etc.
    /// </summary>
    public bool ShowStatus { get; set; }

    /// <summary>
    ///     Width for status line (null = auto-detect from video width).
    /// </summary>
    public int? StatusWidth { get; set; }

    /// <summary>
    ///     Source filename for status display.
    /// </summary>
    public string? SourceFileName { get; set; }

    /// <summary>
    ///     Subtitle track to display during playback.
    /// </summary>
    public SubtitleTrack? Subtitles { get; set; }

    /// <summary>
    ///     Live subtitle provider for streaming transcription.
    ///     When set, subtitles are generated dynamically during playback.
    ///     Takes precedence over the static Subtitles property.
    /// </summary>
    public ILiveSubtitleProvider? LiveSubtitleProvider { get; set; }

    /// <summary>
    ///     Row offset for content rendering (1-based).
    ///     When set, video frames start at this row instead of row 1.
    ///     Used by slideshow to reserve header rows above the video.
    ///     Default: 1 (no offset, content starts at top).
    /// </summary>
    public int ContentStartRow { get; set; } = 1;

    /// <summary>
    ///     Create default options suitable for most videos.
    /// </summary>
    public static VideoRenderOptions Default => new()
    {
        RenderOptions = RenderOptions.ForAnimation(1),
        FrameStep = 1,
        SpeedMultiplier = 1.0f,
        LoopCount = 1
    };

    /// <summary>
    ///     Create options optimized for low-resource playback.
    ///     Uses frame skipping and reduced buffer.
    /// </summary>
    public static VideoRenderOptions LowResource => new()
    {
        RenderOptions = RenderOptions.ForAnimation(1).With(o =>
        {
            o.MaxWidth = 80;
            o.MaxHeight = 24;
            o.UseParallelProcessing = false;
        }),
        FrameStep = 2,
        TargetFps = 15,
        BufferAheadFrames = 2,
        SpeedMultiplier = 1.0f,
        LoopCount = 1
    };

    /// <summary>
    ///     Create options for high-quality preview.
    /// </summary>
    public static VideoRenderOptions HighQuality => new()
    {
        RenderOptions = RenderOptions.ForAnimation(1).With(o =>
        {
            o.MaxWidth = 160;
            o.MaxHeight = 60;
            o.ContrastPower = 2.5f;
        }),
        FrameStep = 1,
        BufferAheadFrames = 5,
        SpeedMultiplier = 1.0f,
        LoopCount = 1
    };

    /// <summary>
    ///     Create options with time range.
    /// </summary>
    public static VideoRenderOptions ForTimeRange(double start, double end)
    {
        return new VideoRenderOptions
        {
            RenderOptions = RenderOptions.ForAnimation(1),
            StartTime = start,
            EndTime = end,
            LoopCount = 1
        };
    }

    /// <summary>
    ///     Calculate effective duration based on start/end times and video duration.
    /// </summary>
    public double GetEffectiveDuration(double videoDuration)
    {
        var start = StartTime ?? 0;
        var end = EndTime ?? videoDuration;
        return Math.Max(0, end - start);
    }

    /// <summary>
    ///     Create a copy with modifications.
    /// </summary>
    public VideoRenderOptions With(Action<VideoRenderOptions> configure)
    {
        var copy = Clone();
        configure(copy);
        return copy;
    }

    /// <summary>
    ///     Create a deep copy.
    /// </summary>
    public VideoRenderOptions Clone()
    {
        return new VideoRenderOptions
        {
            RenderOptions = RenderOptions.Clone(),
            StartTime = StartTime,
            EndTime = EndTime,
            TargetFps = TargetFps,
            FrameStep = FrameStep,
            SamplingStrategy = SamplingStrategy,
            SamplingMode = SamplingMode,
            SmartSkipThreshold = SmartSkipThreshold,
            DebugMode = DebugMode,
            BufferAheadFrames = BufferAheadFrames,
            SpeedMultiplier = SpeedMultiplier,
            LoopCount = LoopCount,
            UseAltScreen = UseAltScreen,
            UseHardwareAcceleration = UseHardwareAcceleration,
            RenderMode = RenderMode,
            SceneThreshold = SceneThreshold,
            ShowStatus = ShowStatus,
            SourceFileName = SourceFileName,
            Subtitles = Subtitles, // Shared reference, not deep copy
            LiveSubtitleProvider = LiveSubtitleProvider, // Shared reference
            ContentStartRow = ContentStartRow
        };
    }
}

/// <summary>
///     Frame sampling strategy for video playback.
/// </summary>
public enum FrameSamplingStrategy
{
    /// <summary>
    ///     Uniform sampling at fixed intervals.
    ///     Fastest, most predictable timing.
    /// </summary>
    Uniform,

    /// <summary>
    ///     Sample at keyframes (I-frames) for better quality.
    ///     Uses codec structure for natural breakpoints.
    /// </summary>
    Keyframe,

    /// <summary>
    ///     Scene-aware sampling - prioritize frames at scene changes.
    ///     Uses FFmpeg scene detection.
    /// </summary>
    SceneAware,

    /// <summary>
    ///     Adaptive sampling based on motion.
    ///     More frames during high-motion, fewer during static scenes.
    /// </summary>
    Adaptive
}

/// <summary>
///     Rendering mode for video output.
/// </summary>
public enum VideoRenderMode
{
    /// <summary>
    ///     Standard ASCII character rendering.
    /// </summary>
    Ascii,

    /// <summary>
    ///     Colored Unicode block characters (higher fidelity).
    /// </summary>
    ColorBlocks,

    /// <summary>
    ///     Braille characters (highest resolution, 2x4 dots per cell).
    /// </summary>
    Braille
}