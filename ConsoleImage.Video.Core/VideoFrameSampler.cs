namespace ConsoleImage.Video.Core;

/// <summary>
///     Intelligent frame sampler that selects representative frames based on various strategies.
///     Used for scene-aware and keyframe-based sampling.
/// </summary>
public class VideoFrameSampler
{
    private readonly FFmpegService _ffmpeg;

    public VideoFrameSampler(FFmpegService ffmpeg)
    {
        _ffmpeg = ffmpeg;
    }

    /// <summary>
    ///     Get optimal frame timestamps based on the sampling strategy.
    /// </summary>
    public async Task<List<double>> GetSampleTimestampsAsync(
        string videoPath,
        VideoRenderOptions options,
        CancellationToken ct = default)
    {
        var videoInfo = await _ffmpeg.GetVideoInfoAsync(videoPath, ct);
        if (videoInfo == null)
            throw new InvalidOperationException("Could not read video info");

        var startTime = options.StartTime ?? 0;
        var endTime = options.EndTime ?? videoInfo.Duration;
        var duration = endTime - startTime;

        return options.SamplingStrategy switch
        {
            FrameSamplingStrategy.Keyframe => await GetKeyframeSamplesAsync(
                videoPath, startTime, endTime, videoInfo, options, ct),

            FrameSamplingStrategy.SceneAware => await GetSceneAwareSamplesAsync(
                videoPath, startTime, endTime, videoInfo, options, ct),

            FrameSamplingStrategy.Adaptive => await GetAdaptiveSamplesAsync(
                videoPath, startTime, endTime, videoInfo, options, ct),

            _ => GetUniformSamples(startTime, duration, videoInfo, options)
        };
    }

    /// <summary>
    ///     Uniform sampling at fixed intervals.
    /// </summary>
    private List<double> GetUniformSamples(
        double startTime,
        double duration,
        VideoInfo videoInfo,
        VideoRenderOptions options)
    {
        var timestamps = new List<double>();

        // Calculate effective FPS considering frame step
        var effectiveFps = (options.TargetFps ?? videoInfo.FrameRate) / options.FrameStep;
        if (effectiveFps <= 0) effectiveFps = 24;

        var interval = 1.0 / effectiveFps;
        var frameCount = (int)(duration / interval);

        for (var i = 0; i < frameCount; i++) timestamps.Add(startTime + i * interval);

        return timestamps;
    }

    /// <summary>
    ///     Sample at codec keyframes (I-frames).
    ///     Provides better quality frames at natural breakpoints.
    /// </summary>
    private async Task<List<double>> GetKeyframeSamplesAsync(
        string videoPath,
        double startTime,
        double endTime,
        VideoInfo videoInfo,
        VideoRenderOptions options,
        CancellationToken ct)
    {
        var keyframes = await _ffmpeg.GetKeyframesAsync(videoPath, ct);

        // Filter to time range
        var inRangeKeyframes = keyframes
            .Where(k => k.Timestamp >= startTime && k.Timestamp <= endTime)
            .OrderBy(k => k.Timestamp)
            .ToList();

        if (inRangeKeyframes.Count == 0)
            // Fall back to uniform sampling
            return GetUniformSamples(startTime, endTime - startTime, videoInfo, options);

        // Apply frame step to keyframes
        var sampledKeyframes = new List<double>();
        for (var i = 0; i < inRangeKeyframes.Count; i += options.FrameStep)
            sampledKeyframes.Add(inRangeKeyframes[i].Timestamp);

        // Fill gaps with uniform samples if keyframes are too sparse
        var effectiveFps = (options.TargetFps ?? videoInfo.FrameRate) / options.FrameStep;
        var maxGap = 1.0 / Math.Max(1, effectiveFps / 4); // Allow 4x the normal interval

        var result = new List<double>();
        for (var i = 0; i < sampledKeyframes.Count; i++)
        {
            result.Add(sampledKeyframes[i]);

            if (i < sampledKeyframes.Count - 1)
            {
                var gap = sampledKeyframes[i + 1] - sampledKeyframes[i];
                if (gap > maxGap)
                {
                    // Fill gap with uniform samples
                    var fillCount = (int)(gap / (1.0 / effectiveFps)) - 1;
                    var fillInterval = gap / (fillCount + 1);
                    for (var j = 1; j <= fillCount; j++) result.Add(sampledKeyframes[i] + j * fillInterval);
                }
            }
        }

        return result.OrderBy(t => t).ToList();
    }

    /// <summary>
    ///     Scene-aware sampling - prioritize frames at scene changes.
    ///     Ensures visual continuity and important moments are captured.
    /// </summary>
    private async Task<List<double>> GetSceneAwareSamplesAsync(
        string videoPath,
        double startTime,
        double endTime,
        VideoInfo videoInfo,
        VideoRenderOptions options,
        CancellationToken ct)
    {
        // Detect scene changes
        var sceneChanges = await _ffmpeg.DetectSceneChangesAsync(
            videoPath,
            options.SceneThreshold,
            startTime,
            endTime,
            ct);

        // Start with uniform base sampling
        var baseSamples = GetUniformSamples(startTime, endTime - startTime, videoInfo, options);

        if (sceneChanges.Count == 0)
            return baseSamples;

        // Create a set of all timestamps
        var allTimestamps = new HashSet<double>(baseSamples);

        // Add scene change timestamps
        foreach (var sceneTime in sceneChanges)
        {
            allTimestamps.Add(sceneTime);

            // Also add a frame just before the scene change for context
            var beforeScene = sceneTime - 0.1;
            if (beforeScene >= startTime)
                allTimestamps.Add(beforeScene);
        }

        return allTimestamps.OrderBy(t => t).ToList();
    }

    /// <summary>
    ///     Adaptive sampling based on scene complexity.
    ///     More frames during scene changes, fewer during static scenes.
    /// </summary>
    private async Task<List<double>> GetAdaptiveSamplesAsync(
        string videoPath,
        double startTime,
        double endTime,
        VideoInfo videoInfo,
        VideoRenderOptions options,
        CancellationToken ct)
    {
        // Detect scene changes to identify high-activity regions
        var sceneChanges = await _ffmpeg.DetectSceneChangesAsync(
            videoPath,
            options.SceneThreshold,
            startTime,
            endTime,
            ct);

        var effectiveFps = (options.TargetFps ?? videoInfo.FrameRate) / options.FrameStep;
        if (effectiveFps <= 0) effectiveFps = 24;

        var result = new List<double>();
        var duration = endTime - startTime;

        // Divide into segments between scene changes
        var boundaries = new List<double> { startTime };
        boundaries.AddRange(sceneChanges);
        boundaries.Add(endTime);
        boundaries = boundaries.Distinct().OrderBy(t => t).ToList();

        for (var i = 0; i < boundaries.Count - 1; i++)
        {
            var segmentStart = boundaries[i];
            var segmentEnd = boundaries[i + 1];
            var segmentDuration = segmentEnd - segmentStart;

            // Determine sampling rate based on whether this is near a scene change
            var isNearSceneChange = sceneChanges.Any(sc =>
                Math.Abs(sc - segmentStart) < 1.0 || Math.Abs(sc - segmentEnd) < 1.0);

            double segmentFps;
            if (isNearSceneChange)
                // Higher sampling near scene changes
                segmentFps = effectiveFps;
            else if (segmentDuration > 5.0)
                // Lower sampling for long static segments
                segmentFps = effectiveFps / 2;
            else
                segmentFps = effectiveFps * 0.75;

            var interval = 1.0 / segmentFps;
            var frameCount = Math.Max(1, (int)(segmentDuration / interval));

            for (var j = 0; j < frameCount; j++)
            {
                var timestamp = segmentStart + j * interval;
                if (timestamp < segmentEnd)
                    result.Add(timestamp);
            }
        }

        return result.Distinct().OrderBy(t => t).ToList();
    }

    /// <summary>
    ///     Estimate memory usage for buffering frames.
    /// </summary>
    public static long EstimateBufferMemory(int width, int height, int bufferFrames)
    {
        // RGB24 = 3 bytes per pixel, plus some overhead for structures
        var frameBytes = width * height * 3;
        var overhead = 1024; // Per-frame overhead
        return (frameBytes + overhead) * bufferFrames;
    }
}