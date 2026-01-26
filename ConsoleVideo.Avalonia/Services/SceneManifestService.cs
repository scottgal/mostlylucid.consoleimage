using System.Text.Json;
using ConsoleImage.Video.Core;
using ConsoleVideo.Avalonia.Models;
using SixLabors.ImageSharp;
using CoreKeyframe = ConsoleImage.Video.Core.ExtractedKeyframe;

namespace ConsoleVideo.Avalonia.Services;

/// <summary>
///     Service for creating and managing scene manifests.
/// </summary>
public class SceneManifestService
{
    /// <summary>
    ///     Create a scene manifest from extracted keyframes.
    /// </summary>
    public SceneManifest CreateManifest(
        string videoPath,
        VideoInfo videoInfo,
        IReadOnlyList<CoreKeyframe> keyframes,
        ExtractionSettings settings,
        string outputFolder)
    {
        var manifest = new SceneManifest
        {
            Created = DateTime.UtcNow,
            Video = new VideoMetadata
            {
                FileName = Path.GetFileName(videoPath),
                FullPath = videoPath,
                Duration = videoInfo.Duration,
                Width = videoInfo.Width,
                Height = videoInfo.Height,
                FrameRate = videoInfo.FrameRate,
                Codec = videoInfo.VideoCodec
            },
            Extraction = new ExtractionMetadata
            {
                Strategy = settings.Strategy.ToString().ToLowerInvariant(),
                RequestedCount = settings.TargetKeyframeCount,
                ActualCount = keyframes.Count,
                StartTime = settings.StartTime,
                EndTime = settings.EndTime,
                SceneThreshold = settings.SceneThreshold
            }
        };

        foreach (var kf in keyframes)
            manifest.Keyframes.Add(new KeyframeEntry
            {
                Index = kf.Index,
                Timestamp = kf.Timestamp,
                TimestampFormatted = FormatTimestamp(kf.Timestamp),
                PositionPercent = videoInfo.Duration > 0 ? kf.Timestamp / videoInfo.Duration * 100 : 0,
                Source = kf.Source,
                IsSceneBoundary = kf.IsSceneBoundary ? true : null,
                Path = $"frames/keyframe_{kf.Index:D3}_{kf.Timestamp:F2}s.png",
                SceneChangeScore = kf.IsSceneBoundary ? 1.0 : null
            });

        return manifest;
    }

    /// <summary>
    ///     Save manifest to JSON file.
    /// </summary>
    public async Task SaveManifestAsync(SceneManifest manifest, string outputPath, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(manifest, SceneManifestJsonContext.Default.SceneManifest);
        await File.WriteAllTextAsync(outputPath, json, ct);
    }

    /// <summary>
    ///     Load manifest from JSON file.
    /// </summary>
    public async Task<SceneManifest?> LoadManifestAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize(json, SceneManifestJsonContext.Default.SceneManifest);
    }

    /// <summary>
    ///     Export complete manifest with keyframe images.
    /// </summary>
    public async Task ExportAsync(
        string videoPath,
        VideoInfo videoInfo,
        IReadOnlyList<CoreKeyframe> keyframes,
        ExtractionSettings settings,
        string outputFolder,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        // Create output folder structure
        Directory.CreateDirectory(outputFolder);
        var framesFolder = Path.Combine(outputFolder, "frames");
        Directory.CreateDirectory(framesFolder);

        progress?.Report(("Saving keyframe images...", 0.1));

        // Save keyframe images
        for (var i = 0; i < keyframes.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var kf = keyframes[i];
            var filename = $"keyframe_{kf.Index:D3}_{kf.Timestamp:F2}s.png";
            var imagePath = Path.Combine(framesFolder, filename);

            await kf.Image.SaveAsPngAsync(imagePath, ct);

            progress?.Report(($"Saved {i + 1}/{keyframes.Count} images...", 0.1 + 0.7 * (i + 1) / keyframes.Count));
        }

        progress?.Report(("Creating manifest...", 0.9));

        // Create and save manifest
        var manifest = CreateManifest(videoPath, videoInfo, keyframes, settings, outputFolder);
        var manifestPath = Path.Combine(outputFolder, "manifest.json");
        await SaveManifestAsync(manifest, manifestPath, ct);

        progress?.Report(("Export complete", 1.0));
    }

    private static string FormatTimestamp(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? ts.ToString(@"h\:mm\:ss\.f")
            : ts.ToString(@"mm\:ss\.f");
    }
}