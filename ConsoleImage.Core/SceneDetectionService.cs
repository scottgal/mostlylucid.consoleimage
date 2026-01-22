using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
/// Scene detection using histogram-based comparison.
/// Pure ImageSharp implementation - no ML dependencies.
/// Based on research: histogram methods achieve ~1.7ms per comparison with F1=0.6+ accuracy.
/// </summary>
public static class SceneDetectionService
{
    /// <summary>
    /// Detect scenes in an animated image using histogram-based motion detection.
    /// Returns frame indices at the END of each scene.
    /// </summary>
    /// <param name="image">The loaded animated image</param>
    /// <param name="maxScenes">Maximum number of scenes to return</param>
    /// <returns>Scene detection result with frame indices and metrics</returns>
    public static SceneDetectionResult DetectScenes(Image<Rgba32> image, int maxScenes = 4)
    {
        var frameCount = image.Frames.Count;

        if (frameCount <= 1)
            return new SceneDetectionResult
            {
                TotalFrames = frameCount,
                SceneCount = 1,
                SceneFrameIndices = [0],
                AverageMotion = 0,
                UsedMotionDetection = false
            };

        if (frameCount <= maxScenes)
        {
            // Few frames - just return all frame indices
            var allIndices = Enumerable.Range(0, frameCount).ToList();
            return new SceneDetectionResult
            {
                TotalFrames = frameCount,
                SceneCount = frameCount,
                SceneFrameIndices = allIndices,
                AverageMotion = 0,
                UsedMotionDetection = false
            };
        }

        // Sample frames to avoid analyzing every frame for long videos
        var sampleInterval = frameCount > 100 ? frameCount / 50 : 1;
        var sampledFrameIndices = new List<int>();
        for (var i = 0; i < frameCount; i += sampleInterval)
            sampledFrameIndices.Add(i);
        if (!sampledFrameIndices.Contains(frameCount - 1))
            sampledFrameIndices.Add(frameCount - 1);

        // Calculate histogram-based motion scores between sampled frames
        var motionScores = new List<(int frameIdx, double score)>();
        int[]? prevHist = null;
        var prevFrameIdx = -1;

        foreach (var frameIdx in sampledFrameIndices)
        {
            using var currentFrame = image.Frames.CloneFrame(frameIdx);
            var currentHist = ComputeColorHistogram(currentFrame);

            if (prevHist != null)
            {
                var motionScore = CompareHistograms(prevHist, currentHist);
                motionScores.Add((prevFrameIdx, motionScore));
            }

            prevHist = currentHist;
            prevFrameIdx = frameIdx;
        }

        if (motionScores.Count == 0)
            return new SceneDetectionResult
            {
                TotalFrames = frameCount,
                SceneCount = 2,
                SceneFrameIndices = [0, Math.Max(0, frameCount - 1)],
                AverageMotion = 0,
                UsedMotionDetection = false
            };

        // Find scene boundaries (frames with high motion = scene change)
        var avgMotion = motionScores.Average(m => m.score);
        var stdDev = Math.Sqrt(motionScores.Average(m => Math.Pow(m.score - avgMotion, 2)));
        var threshold = avgMotion + stdDev;

        var sceneFrames = new List<int> { 0 }; // Always include first frame

        foreach (var (frameIdx, score) in motionScores)
        {
            if (score > threshold && !sceneFrames.Contains(frameIdx))
                sceneFrames.Add(frameIdx);
        }

        // Always include the last frame
        var lastFrame = frameCount - 1;
        if (!sceneFrames.Contains(lastFrame))
            sceneFrames.Add(lastFrame);

        // If we have too many scenes, select the most significant ones
        if (sceneFrames.Count > maxScenes)
        {
            var sorted = sceneFrames
                .Select(idx => (idx, motionScores.FirstOrDefault(m => m.frameIdx == idx).score))
                .OrderByDescending(x => x.score)
                .Take(maxScenes - 2)
                .Select(x => x.idx)
                .ToList();

            sorted.Add(0);
            sorted.Add(lastFrame);
            sceneFrames = sorted.Distinct().OrderBy(x => x).ToList();
        }

        // Remove visually similar frames
        sceneFrames = DeduplicateSceneFrames(image, sceneFrames);

        return new SceneDetectionResult
        {
            TotalFrames = frameCount,
            SceneCount = sceneFrames.Count,
            SceneFrameIndices = sceneFrames,
            AverageMotion = avgMotion,
            UsedMotionDetection = true
        };
    }

    /// <summary>
    /// Quick check if an animated image has significant scene changes.
    /// </summary>
    public static bool HasSignificantSceneChanges(Image<Rgba32> image, double threshold = 0.15)
    {
        if (image.Frames.Count <= 2)
            return false;

        var middleIdx = image.Frames.Count / 2;
        using var first = image.Frames.CloneFrame(0);
        using var middle = image.Frames.CloneFrame(middleIdx);
        using var last = image.Frames.CloneFrame(image.Frames.Count - 1);

        var hist1 = ComputeColorHistogram(first);
        var hist2 = ComputeColorHistogram(middle);
        var hist3 = ComputeColorHistogram(last);

        var diff1 = CompareHistograms(hist1, hist2);
        var diff2 = CompareHistograms(hist2, hist3);

        return diff1 > threshold || diff2 > threshold;
    }

    /// <summary>
    /// Compute color histogram for a frame (64 bins per channel = 192 total).
    /// </summary>
    private static int[] ComputeColorHistogram(Image<Rgba32> frame)
    {
        const int binCount = 64;
        var hist = new int[binCount * 3];

        var sampleStep = Math.Max(2, Math.Min(frame.Width, frame.Height) / 64);

        for (var y = 0; y < frame.Height; y += sampleStep)
        for (var x = 0; x < frame.Width; x += sampleStep)
        {
            var p = frame[x, y];
            hist[p.R * binCount / 256]++;
            hist[binCount + p.G * binCount / 256]++;
            hist[2 * binCount + p.B * binCount / 256]++;
        }

        return hist;
    }

    /// <summary>
    /// Compare two histograms using histogram intersection.
    /// Returns difference score: 0 = identical, 1 = completely different.
    /// </summary>
    private static double CompareHistograms(int[] hist1, int[] hist2)
    {
        long intersection = 0;
        long total1 = 0;

        for (var i = 0; i < hist1.Length; i++)
        {
            intersection += Math.Min(hist1[i], hist2[i]);
            total1 += hist1[i];
        }

        if (total1 == 0) return 0;

        var similarity = (double)intersection / total1;
        return 1.0 - similarity;
    }

    /// <summary>
    /// Remove visually similar frames from the scene list.
    /// </summary>
    private static List<int> DeduplicateSceneFrames(Image<Rgba32> image, List<int> frameIndices)
    {
        if (frameIndices.Count <= 2)
            return frameIndices;

        var uniqueIndices = new List<int> { frameIndices[0] };
        int[]? lastUniqueHist = null;

        foreach (var frameIdx in frameIndices)
        {
            using var currentFrame = image.Frames.CloneFrame(frameIdx);
            var currentHist = ComputeColorHistogram(currentFrame);

            if (lastUniqueHist == null)
            {
                lastUniqueHist = currentHist;
                continue;
            }

            // >8% different = keep this frame
            var diff = CompareHistograms(lastUniqueHist, currentHist);
            if (diff > 0.08)
            {
                uniqueIndices.Add(frameIdx);
                lastUniqueHist = currentHist;
            }
        }

        return uniqueIndices;
    }
}

/// <summary>
/// Scene detection result for animated images.
/// </summary>
public record SceneDetectionResult
{
    /// <summary>Total number of frames in the animation.</summary>
    public int TotalFrames { get; init; }

    /// <summary>Number of distinct scenes detected.</summary>
    public int SceneCount { get; init; }

    /// <summary>Frame indices representing key frames from each scene.</summary>
    public List<int> SceneFrameIndices { get; init; } = [];

    /// <summary>Average motion across all frames (0-1 scale).</summary>
    public double AverageMotion { get; init; }

    /// <summary>Whether motion-based scene detection was used.</summary>
    public bool UsedMotionDetection { get; init; }
}
