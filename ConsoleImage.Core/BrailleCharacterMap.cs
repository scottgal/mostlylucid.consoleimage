// Braille character shape vector matching
// Mathematical generation of 8D vectors for all 256 braille patterns
// Uses SIMD brute force matching (256 vectors = faster than any graph traversal)

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace ConsoleImage.Core;

/// <summary>
///     Generates 8D shape vectors for all 256 braille patterns mathematically.
///     Each braille code (0x00-0xFF) has 8 dots in a 2x4 grid.
///     Vector component = 1.0 if dot ON, 0.0 if dot OFF.
///     No font rendering needed - braille patterns are defined by Unicode standard.
/// </summary>
public class BrailleCharacterMap
{
    /// <summary>
    ///     Braille dot bit positions in standard Unicode encoding:
    ///     Pos:  1  4    Bits: 0x01  0x08
    ///           2  5          0x02  0x10
    ///           3  6          0x04  0x20
    ///           7  8          0x40  0x80
    ///     Index order matches 2x4 grid: [row0col0, row0col1, row1col0, row1col1, ...]
    /// </summary>
    private static readonly int[] DotBits = [0x01, 0x08, 0x02, 0x10, 0x04, 0x20, 0x40, 0x80];

    private const char BrailleBase = '\u2800';
    private const int BrailleCount = 256;

    private readonly ConcurrentDictionary<int, char> _cache = new();
    private readonly char[] _characters;     // 256 braille chars
    private readonly float[] _vectorData;    // 256 * 8 floats (SIMD-aligned)

    // Cache statistics
    private long _cacheHits;
    private long _cacheMisses;

    public BrailleCharacterMap()
    {
        _characters = new char[BrailleCount];
        _vectorData = new float[BrailleCount * 8];

        // Generate all 256 braille patterns mathematically
        for (var code = 0; code < BrailleCount; code++)
        {
            _characters[code] = (char)(BrailleBase + code);

            var offset = code * 8;
            for (var dot = 0; dot < 8; dot++)
            {
                // Check if this dot is ON in the braille code
                _vectorData[offset + dot] = (code & DotBits[dot]) != 0 ? 1.0f : 0.0f;
            }
        }
    }

    /// <summary>
    ///     Find the best matching braille character for an 8D target vector.
    ///     Uses quantized caching for repeated lookups.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public char FindBestMatch(ReadOnlySpan<float> target8)
    {
        // Quantize to 4 bits per component: 8 components × 4 bits = 32 bits (fits in int)
        var cacheKey = GetQuantizedKey(target8);

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);

        var result = FindBestMatchBruteForce(target8);
        return _cache.GetOrAdd(cacheKey, result);
    }

    /// <summary>
    ///     Find best match using SIMD brute force.
    ///     With 256 vectors, this is faster than any tree structure.
    /// </summary>
    public char FindBestMatchBruteForce(ReadOnlySpan<float> target8)
    {
        var bestDist = float.MaxValue;
        var bestIdx = 0;

        if (Vector256.IsHardwareAccelerated)
        {
            // Load target vector once (all 8 floats fit in Vector256)
            var targetVec = Vector256.Create(
                target8[0], target8[1], target8[2], target8[3],
                target8[4], target8[5], target8[6], target8[7]);

            for (var i = 0; i < BrailleCount; i++)
            {
                var offset = i * 8;
                var charVec = Vector256.Create(
                    _vectorData[offset], _vectorData[offset + 1],
                    _vectorData[offset + 2], _vectorData[offset + 3],
                    _vectorData[offset + 4], _vectorData[offset + 5],
                    _vectorData[offset + 6], _vectorData[offset + 7]);

                var diff = targetVec - charVec;
                var squared = diff * diff;
                var dist = Vector256.Sum(squared);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
        }
        else
        {
            // Scalar fallback
            for (var i = 0; i < BrailleCount; i++)
            {
                var offset = i * 8;
                var dist = 0f;
                for (var d = 0; d < 8; d++)
                {
                    var diff = target8[d] - _vectorData[offset + d];
                    dist += diff * diff;
                }

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
        }

        return _characters[bestIdx];
    }

    /// <summary>
    ///     Quantize 8 float components to 4 bits each = 32-bit cache key.
    ///     Values are expected in [0, 1] range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetQuantizedKey(ReadOnlySpan<float> target8)
    {
        const int bitsPerComponent = 4;
        const int levels = 1 << bitsPerComponent; // 16
        const int mask = levels - 1; // 0xF
        const float scale = levels - 1; // 15.0

        var key = 0;
        for (var i = 0; i < 8; i++)
        {
            var q = Math.Clamp((int)(target8[i] * scale), 0, mask);
            key |= q << (i * bitsPerComponent);
        }

        return key;
    }

    /// <summary>
    ///     Clear the lookup cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);
    }

    /// <summary>
    ///     Get cache statistics for performance analysis.
    /// </summary>
    public (long Hits, long Misses, int CacheSize, double HitRate) GetCacheStats()
    {
        var hits = Interlocked.Read(ref _cacheHits);
        var misses = Interlocked.Read(ref _cacheMisses);
        var total = hits + misses;
        var hitRate = total > 0 ? (double)hits / total : 0.0;
        return (hits, misses, _cache.Count, hitRate);
    }
}
