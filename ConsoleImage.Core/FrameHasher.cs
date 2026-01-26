// FrameHasher - Fast perceptual hashing for frame deduplication
// Uses average hash (aHash) for quick similarity detection

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Core;

/// <summary>
///     Fast perceptual hashing for detecting similar frames.
///     Uses average hash (aHash) - resizes to 8x8, computes average brightness,
///     then creates a 64-bit hash based on whether each pixel is above/below average.
/// </summary>
public static class FrameHasher
{
    /// <summary>
    ///     Compute a 64-bit perceptual hash of an image.
    ///     Similar images will have similar hashes (low Hamming distance).
    ///     Returns (hash, averageBrightness) for additional similarity checking.
    /// </summary>
    public static ulong ComputeHash(Image<Rgba32> image)
    {
        var (hash, _) = ComputeHashWithBrightness(image);
        return hash;
    }

    /// <summary>
    ///     Compute hash with brightness info for better dark frame detection.
    /// </summary>
    public static (ulong hash, int avgBrightness) ComputeHashWithBrightness(Image<Rgba32> image)
    {
        // Resize to 8x8 for hash computation
        using var small = image.Clone(ctx => ctx.Resize(8, 8));

        // Compute average brightness
        long totalBrightness = 0;
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var pixel = small[x, y];
            // Use perceived brightness formula
            var brightness = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
            totalBrightness += brightness;
        }

        var avgBrightness = (int)(totalBrightness / 64);

        // Build hash: 1 bit per pixel, set if above average
        ulong hash = 0;
        var bit = 0;
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var pixel = small[x, y];
            var brightness = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
            if (brightness >= avgBrightness) hash |= 1UL << bit;
            bit++;
        }

        return (hash, avgBrightness);
    }

    /// <summary>
    ///     Compute Hamming distance between two hashes.
    ///     Lower distance = more similar images.
    /// </summary>
    public static int HammingDistance(ulong hash1, ulong hash2)
    {
        var xor = hash1 ^ hash2;
        var distance = 0;
        while (xor != 0)
        {
            distance += (int)(xor & 1);
            xor >>= 1;
        }

        return distance;
    }

    /// <summary>
    ///     Check if two frames are perceptually similar.
    /// </summary>
    /// <param name="hash1">First frame hash</param>
    /// <param name="hash2">Second frame hash</param>
    /// <param name="threshold">Max Hamming distance to consider similar (default: 5 out of 64 bits)</param>
    public static bool AreSimilar(ulong hash1, ulong hash2, int threshold = 5)
    {
        return HammingDistance(hash1, hash2) <= threshold;
    }

    /// <summary>
    ///     Compute a quick difference score between two images (0-100).
    ///     0 = identical, 100 = completely different.
    ///     Faster than full hash comparison for adjacent frames.
    /// </summary>
    public static int QuickDifferenceScore(Image<Rgba32> current, Image<Rgba32> previous)
    {
        // Sample a grid of pixels for quick comparison
        const int samples = 16; // 4x4 grid
        var differences = 0;
        const int threshold = 30; // Per-channel difference threshold

        var stepX = Math.Max(1, current.Width / 4);
        var stepY = Math.Max(1, current.Height / 4);

        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
        {
            var px = Math.Min(x * stepX, current.Width - 1);
            var py = Math.Min(y * stepY, current.Height - 1);

            var c = current[px, py];
            var p = previous[px, py];

            var dr = Math.Abs(c.R - p.R);
            var dg = Math.Abs(c.G - p.G);
            var db = Math.Abs(c.B - p.B);

            if (dr > threshold || dg > threshold || db > threshold) differences++;
        }

        return differences * 100 / samples;
    }
}