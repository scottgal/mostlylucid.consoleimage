using ConsoleImage.Video.Core;
using ConsoleVideo.Avalonia.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleVideo.Avalonia.Services;

/// <summary>
///     Wrapper service for keyframe extraction in the Avalonia app.
///     Uses SmartKeyframeExtractor from Video.Core for the actual extraction.
/// </summary>
public class KeyframeExtractionService
{
    /// <summary>
    ///     Extract keyframes based on the specified settings.
    /// </summary>
    public async Task<List<ExtractedKeyframeViewModel>> ExtractKeyframesAsync(
        string videoPath,
        ExtractionSettings settings,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        using var ffmpeg = new FFmpegService();
        var extractor = new SmartKeyframeExtractor(ffmpeg);

        // Convert UI settings to core settings
        var coreSettings = new KeyframeExtractionSettings
        {
            TargetCount = settings.TargetKeyframeCount,
            Strategy = settings.Strategy switch
            {
                ExtractionStrategy.Uniform => KeyframeStrategy.Uniform,
                ExtractionStrategy.Keyframe => KeyframeStrategy.Keyframe,
                ExtractionStrategy.SceneAware => KeyframeStrategy.SceneAware,
                ExtractionStrategy.Adaptive => KeyframeStrategy.Adaptive,
                _ => KeyframeStrategy.Adaptive
            },
            StartTime = settings.StartTime,
            EndTime = settings.EndTime,
            SceneThreshold = settings.SceneThreshold,
            EnableDeduplication = true
        };

        var extracted = await extractor.ExtractAsync(videoPath, coreSettings, progress, ct);

        // Convert to view models
        return extracted.Select(kf => new ExtractedKeyframeViewModel
        {
            Index = kf.Index,
            Timestamp = kf.Timestamp,
            Image = kf.Image,
            IsSceneBoundary = kf.IsSceneBoundary,
            Source = kf.IsSceneBoundary ? "Scene Boundary" : "I-Frame/Uniform"
        }).ToList();
    }
}

/// <summary>
///     Extended keyframe model with source indication.
/// </summary>
public record ExtractedKeyframeViewModel
{
    public required int Index { get; init; }
    public required double Timestamp { get; init; }
    public required Image<Rgba32> Image { get; init; }
    public bool IsSceneBoundary { get; init; }
    public string Source { get; init; } = "";
    public string TimestampFormatted => TimeSpan.FromSeconds(Timestamp).ToString(@"mm\:ss\.f");
}