using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Video.Core;

/// <summary>
///     Fast perceptual hash-based keyframe deduplication.
///     Computes difference hashes (dHash) to identify visually similar frames
///     before expensive processing. Typically achieves 20-40% frame reduction.
/// </summary>
public class KeyframeDeduplicationService
{
    // dHash parameters: 9x8 grayscale = 64 bits
    private const int HashWidth = 9;
    private const int HashHeight = 8;

    /// <summary>
    ///     Default Hamming distance threshold.
    ///     Lower = stricter (fewer duplicates), Higher = looser (more duplicates)
    ///     10 is a good balance for video keyframes.
    /// </summary>
    public const int DefaultHammingThreshold = 10;

    /// <summary>
    ///     Filter out visually similar frames from a list.
    ///     Uses perceptual hashing with temporal locality (checks last 20 frames).
    /// </summary>
    /// <param name="frames">Enumerable of timestamp + image pairs</param>
    /// <param name="hammingThreshold">Max Hamming distance to consider as duplicate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of unique frame candidates with dHash values</returns>
    public async Task<List<KeyframeCandidate>> FilterSimilarFramesAsync(
        IEnumerable<(double Timestamp, Image<Rgba32> Image)> frames,
        int hammingThreshold = DefaultHammingThreshold,
        CancellationToken ct = default)
    {
        var candidates = new List<KeyframeCandidate>();
        var seenHashes = new List<(double Timestamp, ulong DHash)>();

        // Sort by timestamp for temporal ordering
        var sortedFrames = frames.OrderBy(f => f.Timestamp).ToList();

        foreach (var (timestamp, image) in sortedFrames)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Compute fast dHash
                var dHash = ComputeDHash(image);

                // Check against recent hashes (temporal locality - only check last 20)
                var recentHashes = seenHashes.TakeLast(20).ToList();
                var isDuplicate = recentHashes.Any(seen =>
                    HammingDistance(dHash, seen.DHash) <= hammingThreshold);

                if (!isDuplicate)
                {
                    candidates.Add(new KeyframeCandidate
                    {
                        Timestamp = timestamp,
                        DHash = dHash
                    });
                    seenHashes.Add((timestamp, dHash));
                }
            }
            catch
            {
                // Include frame if hashing fails
                candidates.Add(new KeyframeCandidate
                {
                    Timestamp = timestamp,
                    DHash = 0
                });
            }
        }

        return await Task.FromResult(candidates);
    }

    /// <summary>
    ///     Filter similar timestamps using dHash. Returns unique timestamps only.
    /// </summary>
    public async Task<List<double>> FilterTimestampsAsync(
        IEnumerable<(double Timestamp, Image<Rgba32> Image)> frames,
        int hammingThreshold = DefaultHammingThreshold,
        CancellationToken ct = default)
    {
        var candidates = await FilterSimilarFramesAsync(frames, hammingThreshold, ct);
        return candidates.Select(c => c.Timestamp).ToList();
    }

    /// <summary>
    ///     Compute difference hash (dHash) for an image.
    ///     Uses 9x8 grayscale comparison producing 64-bit hash.
    ///     Very fast (~1ms) and effective for near-duplicate detection.
    /// </summary>
    public ulong ComputeDHash(Image<Rgba32> image)
    {
        // Clone and resize to 9x8 (one extra column for gradient comparison)
        using var resized = image.Clone(x => x
            .Resize(HashWidth, HashHeight)
            .Grayscale());

        ulong hash = 0;
        var bit = 0;

        // Compare adjacent pixels horizontally
        for (var y = 0; y < HashHeight; y++)
        for (var x = 0; x < HashWidth - 1; x++)
        {
            var left = resized[x, y].R;
            var right = resized[x + 1, y].R;

            // Set bit if left pixel is brighter than right
            if (left > right) hash |= 1UL << bit;
            bit++;
        }

        return hash;
    }

    /// <summary>
    ///     Calculate Hamming distance between two hashes.
    ///     Returns the number of differing bits (0-64).
    /// </summary>
    public static int HammingDistance(ulong a, ulong b)
    {
        return BitOperations.PopCount(a ^ b);
    }

    /// <summary>
    ///     Calculate similarity percentage between two hashes.
    ///     1.0 = identical, 0.0 = completely different
    /// </summary>
    public static double HashSimilarity(ulong a, ulong b)
    {
        var distance = HammingDistance(a, b);
        return 1.0 - distance / 64.0;
    }
}

/// <summary>
///     Candidate keyframe after deduplication filtering.
/// </summary>
public record KeyframeCandidate
{
    /// <summary>Timestamp in seconds</summary>
    public double Timestamp { get; init; }

    /// <summary>Perceptual hash (dHash) for similarity comparison</summary>
    public ulong DHash { get; init; }
}