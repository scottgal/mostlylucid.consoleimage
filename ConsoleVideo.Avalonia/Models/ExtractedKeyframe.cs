namespace ConsoleVideo.Avalonia.Models;

// NOTE: ExtractedKeyframe is now defined in ConsoleImage.Video.Core.
// Use ConsoleImage.Video.Core.ExtractedKeyframe for extracted frames.

/// <summary>
///     Settings for keyframe extraction.
/// </summary>
public record ExtractionSettings
{
    public int TargetKeyframeCount { get; init; } = 10;
    public double? StartTime { get; init; }
    public double? EndTime { get; init; }
    public ExtractionStrategy Strategy { get; init; } = ExtractionStrategy.Uniform;
    public double SceneThreshold { get; init; } = 0.4;
}

/// <summary>
///     Extraction strategy options.
/// </summary>
public enum ExtractionStrategy
{
    Uniform,
    Keyframe,
    SceneAware,
    Adaptive
}

/// <summary>
///     Render mode for ASCII preview.
/// </summary>
public enum RenderMode
{
    Ascii,
    Blocks,
    Braille
}