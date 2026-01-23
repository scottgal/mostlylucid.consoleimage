using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Video.Core;

/// <summary>
/// Smart keyframe extraction service that combines multiple strategies:
/// - Codec I-frame prioritization (higher quality frames)
/// - Scene change detection (capture important transitions)
/// - Perceptual hash deduplication (remove visually similar frames)
/// - Adaptive sampling (more frames in high-activity regions)
///
/// Used by both CLI and GUI for consistent keyframe extraction.
/// </summary>
public class SmartKeyframeExtractor
{
    private readonly FFmpegService _ffmpeg;
    private readonly KeyframeDeduplicationService _deduplication;

    // Configuration constants
    private const int MinKeyframeInterval = 2; // Minimum seconds between keyframes
    private const int ThumbnailWidth = 128; // Low-res for fast deduplication
    private const int DeduplicationHammingThreshold = 10;

    public SmartKeyframeExtractor(FFmpegService ffmpeg)
    {
        _ffmpeg = ffmpeg;
        _deduplication = new KeyframeDeduplicationService();
    }

    /// <summary>
    /// Extract keyframes using the specified strategy.
    /// </summary>
    /// <param name="videoPath">Path to video file</param>
    /// <param name="settings">Extraction settings</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of extracted keyframe images with timestamps</returns>
    public async Task<List<ExtractedKeyframe>> ExtractAsync(
        string videoPath,
        KeyframeExtractionSettings settings,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        await _ffmpeg.InitializeAsync(null, ct);

        var videoInfo = await _ffmpeg.GetVideoInfoAsync(videoPath, ct);
        if (videoInfo == null)
        {
            throw new InvalidOperationException("Could not read video info");
        }

        progress?.Report(("Analyzing video...", 0.05));

        var startTime = settings.StartTime ?? 0;
        var endTime = settings.EndTime ?? videoInfo.Duration;

        // Step 1: Get candidate timestamps based on strategy
        var candidates = await GetCandidateTimestampsAsync(
            videoPath, videoInfo, startTime, endTime, settings, progress, ct);

        progress?.Report(($"Found {candidates.Count} candidates", 0.2));

        // Step 2: Smart deduplication for non-uniform strategies
        List<double> finalTimestamps;
        if (settings.Strategy != KeyframeStrategy.Uniform &&
            candidates.Count > settings.TargetCount &&
            settings.EnableDeduplication)
        {
            finalTimestamps = await DeduplicateAsync(
                videoPath, candidates, settings, progress, ct);
        }
        else if (candidates.Count > settings.TargetCount)
        {
            finalTimestamps = SelectRepresentative(candidates, settings.TargetCount);
        }
        else
        {
            finalTimestamps = candidates;
        }

        progress?.Report(($"Extracting {finalTimestamps.Count} keyframes...", 0.5));

        // Step 3: Extract full-resolution frames
        var results = new List<ExtractedKeyframe>();

        // Get scene scores for annotation (if scene-aware)
        var sceneScores = settings.Strategy is KeyframeStrategy.SceneAware or KeyframeStrategy.Adaptive
            ? await GetSceneScoresAsync(videoPath, startTime, endTime, settings.SceneThreshold, ct)
            : new Dictionary<double, double>();

        for (int i = 0; i < finalTimestamps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var timestamp = finalTimestamps[i];
            progress?.Report(($"Extracting frame {i + 1}/{finalTimestamps.Count}...",
                0.5 + (0.5 * i / finalTimestamps.Count)));

            var image = await _ffmpeg.ExtractFrameAsync(videoPath, timestamp, null, null, ct);
            if (image != null)
            {
                // Find if this timestamp is near a scene change
                double? sceneScore = null;
                if (sceneScores.Count > 0)
                {
                    var closest = sceneScores.Keys
                        .OrderBy(t => Math.Abs(t - timestamp))
                        .FirstOrDefault();

                    if (Math.Abs(closest - timestamp) < 1.0)
                    {
                        sceneScore = 1.0;
                    }
                }

                results.Add(new ExtractedKeyframe
                {
                    Index = i + 1,
                    Timestamp = timestamp,
                    Image = image,
                    IsSceneBoundary = sceneScore.HasValue
                });
            }
        }

        progress?.Report(($"Extracted {results.Count} keyframes", 1.0));
        return results;
    }

    /// <summary>
    /// Get candidate timestamps using the selected strategy.
    /// </summary>
    private async Task<List<double>> GetCandidateTimestampsAsync(
        string videoPath,
        VideoInfo videoInfo,
        double startTime,
        double endTime,
        KeyframeExtractionSettings settings,
        IProgress<(string Status, double Progress)>? progress,
        CancellationToken ct)
    {
        return settings.Strategy switch
        {
            KeyframeStrategy.Uniform =>
                GetUniformTimestamps(startTime, endTime, settings.TargetCount),

            KeyframeStrategy.Keyframe =>
                await GetIframePrioritizedAsync(videoPath, startTime, endTime, settings.TargetCount, ct),

            KeyframeStrategy.SceneAware =>
                await GetSceneAwareAsync(videoPath, startTime, endTime, settings, progress, ct),

            KeyframeStrategy.Adaptive =>
                await GetAdaptiveAsync(videoPath, startTime, endTime, settings, progress, ct),

            _ => GetUniformTimestamps(startTime, endTime, settings.TargetCount)
        };
    }

    /// <summary>
    /// Uniform sampling at fixed intervals.
    /// </summary>
    private static List<double> GetUniformTimestamps(double startTime, double endTime, int count)
    {
        var timestamps = new List<double>();
        var duration = endTime - startTime;
        var interval = duration / (count + 1);

        for (int i = 1; i <= count; i++)
        {
            timestamps.Add(startTime + (interval * i));
        }

        return timestamps;
    }

    /// <summary>
    /// Prioritize codec I-frames (higher quality) aligned with time intervals.
    /// </summary>
    private async Task<List<double>> GetIframePrioritizedAsync(
        string videoPath,
        double startTime,
        double endTime,
        int targetCount,
        CancellationToken ct)
    {
        var keyframes = await _ffmpeg.GetKeyframesAsync(videoPath, ct);

        var inRange = keyframes
            .Where(k => k.Timestamp >= startTime && k.Timestamp <= endTime)
            .OrderBy(k => k.Timestamp)
            .ToList();

        if (inRange.Count == 0)
        {
            return GetUniformTimestamps(startTime, endTime, targetCount);
        }

        // Get uniform target timestamps
        var uniformTargets = GetUniformTimestamps(startTime, endTime, targetCount);

        // For each target, find nearest I-frame
        var selected = new List<double>();
        var usedTimestamps = new HashSet<double>();

        foreach (var target in uniformTargets)
        {
            // Find nearest I-frame within 2 seconds
            var nearestIframe = inRange
                .Where(f => !usedTimestamps.Contains(f.Timestamp))
                .Where(f => Math.Abs(f.Timestamp - target) < 2.0)
                .OrderBy(f => Math.Abs(f.Timestamp - target))
                .FirstOrDefault();

            var timestamp = nearestIframe?.Timestamp ?? target;

            if (!usedTimestamps.Contains(timestamp))
            {
                selected.Add(timestamp);
                usedTimestamps.Add(timestamp);
            }
        }

        return selected.OrderBy(t => t).ToList();
    }

    /// <summary>
    /// Scene-aware: prioritize frames at scene boundaries and I-frames.
    /// </summary>
    private async Task<List<double>> GetSceneAwareAsync(
        string videoPath,
        double startTime,
        double endTime,
        KeyframeExtractionSettings settings,
        IProgress<(string Status, double Progress)>? progress,
        CancellationToken ct)
    {
        progress?.Report(("Detecting scene changes...", 0.1));

        var sceneChanges = await _ffmpeg.DetectSceneChangesAsync(
            videoPath, settings.SceneThreshold, startTime, endTime, ct);

        var keyframes = await _ffmpeg.GetKeyframesAsync(videoPath, ct);
        var iframeTimestamps = keyframes
            .Where(k => k.Timestamp >= startTime && k.Timestamp <= endTime)
            .Select(k => k.Timestamp)
            .ToList();

        var candidates = new HashSet<double>();

        // Add scene change timestamps (highest priority)
        foreach (var scene in sceneChanges)
        {
            candidates.Add(scene);

            // Also add I-frame just after scene change if available
            var nearbyIframe = iframeTimestamps
                .Where(t => t > scene && t < scene + 1.0)
                .OrderBy(t => t)
                .FirstOrDefault();

            if (nearbyIframe > 0)
            {
                candidates.Add(nearbyIframe);
            }
        }

        // Add I-frames that maintain minimum interval
        foreach (var iframe in iframeTimestamps)
        {
            var tooClose = candidates.Any(c => Math.Abs(c - iframe) < MinKeyframeInterval);
            if (!tooClose)
            {
                candidates.Add(iframe);
            }
        }

        // Fill gaps with uniform samples
        var uniformFill = GetUniformTimestamps(startTime, endTime, settings.TargetCount * 2);
        foreach (var t in uniformFill)
        {
            var tooClose = candidates.Any(c => Math.Abs(c - t) < MinKeyframeInterval / 2);
            if (!tooClose)
            {
                candidates.Add(t);
            }
        }

        return candidates.OrderBy(t => t).ToList();
    }

    /// <summary>
    /// Adaptive: more frames in high-activity regions, fewer in static regions.
    /// </summary>
    private async Task<List<double>> GetAdaptiveAsync(
        string videoPath,
        double startTime,
        double endTime,
        KeyframeExtractionSettings settings,
        IProgress<(string Status, double Progress)>? progress,
        CancellationToken ct)
    {
        progress?.Report(("Analyzing scene activity...", 0.1));

        var sceneChanges = await _ffmpeg.DetectSceneChangesAsync(
            videoPath, settings.SceneThreshold, startTime, endTime, ct);

        var keyframes = await _ffmpeg.GetKeyframesAsync(videoPath, ct);
        var iframeTimestamps = keyframes
            .Where(k => k.Timestamp >= startTime && k.Timestamp <= endTime)
            .Select(k => k.Timestamp)
            .ToHashSet();

        var candidates = new List<double>();
        var duration = endTime - startTime;

        // Create segments based on scene changes
        var boundaries = new List<double> { startTime };
        boundaries.AddRange(sceneChanges.Where(t => t > startTime && t < endTime));
        boundaries.Add(endTime);
        boundaries = boundaries.Distinct().OrderBy(t => t).ToList();

        var totalSegments = boundaries.Count - 1;
        var baseFramesPerSegment = Math.Max(1, settings.TargetCount / totalSegments);

        for (int i = 0; i < boundaries.Count - 1; i++)
        {
            var segmentStart = boundaries[i];
            var segmentEnd = boundaries[i + 1];
            var segmentDuration = segmentEnd - segmentStart;

            // More frames for shorter segments (more activity)
            var segmentFrames = segmentDuration < duration / totalSegments / 2
                ? baseFramesPerSegment * 2
                : baseFramesPerSegment;

            // Try to use I-frames in segment first
            var segmentIframes = iframeTimestamps
                .Where(t => t >= segmentStart && t < segmentEnd)
                .OrderBy(t => t)
                .ToList();

            var selectedInSegment = 0;

            foreach (var iframe in segmentIframes)
            {
                if (selectedInSegment >= segmentFrames) break;

                var tooClose = candidates.Any(c => Math.Abs(c - iframe) < MinKeyframeInterval);
                if (!tooClose)
                {
                    candidates.Add(iframe);
                    selectedInSegment++;
                }
            }

            // Fill remaining with uniform
            if (selectedInSegment < segmentFrames)
            {
                var remaining = segmentFrames - selectedInSegment;
                var interval = segmentDuration / (remaining + 1);

                for (int j = 1; j <= remaining; j++)
                {
                    var timestamp = segmentStart + (interval * j);
                    if (timestamp < segmentEnd)
                    {
                        var tooClose = candidates.Any(c => Math.Abs(c - timestamp) < MinKeyframeInterval);
                        if (!tooClose)
                        {
                            candidates.Add(timestamp);
                        }
                    }
                }
            }
        }

        return candidates.OrderBy(t => t).ToList();
    }

    /// <summary>
    /// Use perceptual hashing to remove visually similar frames.
    /// </summary>
    private async Task<List<double>> DeduplicateAsync(
        string videoPath,
        List<double> candidates,
        KeyframeExtractionSettings settings,
        IProgress<(string Status, double Progress)>? progress,
        CancellationToken ct)
    {
        progress?.Report(("Extracting thumbnails for deduplication...", 0.25));

        // Extract low-res thumbnails
        var thumbnails = new List<(double Timestamp, Image<Rgba32> Image)>();
        for (int i = 0; i < candidates.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var timestamp = candidates[i];
            var thumb = await _ffmpeg.ExtractFrameAsync(videoPath, timestamp, ThumbnailWidth, null, ct);

            if (thumb != null)
            {
                thumbnails.Add((timestamp, thumb));
            }

            if (i % 10 == 0)
            {
                progress?.Report(($"Extracting thumbnails {i + 1}/{candidates.Count}...",
                    0.25 + (0.1 * i / candidates.Count)));
            }
        }

        progress?.Report(("Deduplicating similar frames...", 0.4));

        // Filter using perceptual hashing
        var uniqueTimestamps = await _deduplication.FilterTimestampsAsync(
            thumbnails, DeduplicationHammingThreshold, ct);

        // Dispose thumbnails
        foreach (var (_, img) in thumbnails)
        {
            img.Dispose();
        }

        var removed = candidates.Count - uniqueTimestamps.Count;
        var percent = candidates.Count > 0 ? (100.0 * removed / candidates.Count) : 0;

        progress?.Report(($"Deduplication: removed {removed} similar frames ({percent:F1}%)", 0.45));

        // Select target count from unique frames
        return uniqueTimestamps.Count > settings.TargetCount
            ? SelectRepresentative(uniqueTimestamps, settings.TargetCount)
            : uniqueTimestamps;
    }

    /// <summary>
    /// Get scene change timestamps for annotation.
    /// </summary>
    private async Task<Dictionary<double, double>> GetSceneScoresAsync(
        string videoPath,
        double startTime,
        double endTime,
        double threshold,
        CancellationToken ct)
    {
        try
        {
            var sceneChanges = await _ffmpeg.DetectSceneChangesAsync(
                videoPath, threshold, startTime, endTime, ct);

            return sceneChanges.ToDictionary(t => t, _ => 1.0);
        }
        catch
        {
            return new Dictionary<double, double>();
        }
    }

    /// <summary>
    /// Select evenly distributed timestamps from a larger list.
    /// </summary>
    private static List<double> SelectRepresentative(List<double> timestamps, int targetCount)
    {
        if (timestamps.Count <= targetCount)
            return timestamps;

        var result = new List<double>();
        var step = (double)timestamps.Count / targetCount;

        for (int i = 0; i < targetCount; i++)
        {
            var index = (int)(i * step);
            result.Add(timestamps[Math.Min(index, timestamps.Count - 1)]);
        }

        return result.Distinct().ToList();
    }
}

/// <summary>
/// Settings for smart keyframe extraction.
/// </summary>
public record KeyframeExtractionSettings
{
    /// <summary>Target number of keyframes to extract.</summary>
    public int TargetCount { get; init; } = 10;

    /// <summary>Extraction strategy.</summary>
    public KeyframeStrategy Strategy { get; init; } = KeyframeStrategy.Adaptive;

    /// <summary>Start time in seconds (null = beginning).</summary>
    public double? StartTime { get; init; }

    /// <summary>End time in seconds (null = end of video).</summary>
    public double? EndTime { get; init; }

    /// <summary>Scene detection threshold (0.0-1.0, lower = more sensitive).</summary>
    public double SceneThreshold { get; init; } = 0.4;

    /// <summary>Enable perceptual hash deduplication.</summary>
    public bool EnableDeduplication { get; init; } = true;
}

/// <summary>
/// Keyframe extraction strategy.
/// </summary>
public enum KeyframeStrategy
{
    /// <summary>Extract frames at uniform intervals.</summary>
    Uniform,

    /// <summary>Prioritize codec I-frames (higher quality).</summary>
    Keyframe,

    /// <summary>Prioritize frames at scene boundaries.</summary>
    SceneAware,

    /// <summary>Adaptive: more frames in high-activity regions.</summary>
    Adaptive
}

/// <summary>
/// Extracted keyframe with metadata.
/// </summary>
public record ExtractedKeyframe
{
    /// <summary>1-based index.</summary>
    public int Index { get; init; }

    /// <summary>Timestamp in seconds.</summary>
    public double Timestamp { get; init; }

    /// <summary>Full-resolution frame image.</summary>
    public required Image<Rgba32> Image { get; init; }

    /// <summary>Whether this frame is near a scene boundary.</summary>
    public bool IsSceneBoundary { get; init; }

    /// <summary>Source/extraction method that selected this frame (e.g., "uniform", "scene", "keyframe").</summary>
    public string Source { get; init; } = "uniform";

    /// <summary>Formatted timestamp (MM:SS.f).</summary>
    public string TimestampFormatted => TimeSpan.FromSeconds(Timestamp).ToString(@"mm\:ss\.f");
}
