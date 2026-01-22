// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// This implementation uses shape-matching with 6D vectors for high-quality ASCII art

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace ConsoleImage.Core;

/// <summary>
///     Represents a 6-dimensional shape vector used for character matching.
///     The six dimensions correspond to sampling regions in a 3x2 grid:
///     [0] top-left, [1] top-center, [2] top-right
///     [3] bottom-left, [4] bottom-center, [5] bottom-right
///     Uses SIMD operations where available for fast distance calculations.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct ShapeVector : IEquatable<ShapeVector>
{
    // Store as 8 floats to align with Vector256 (2 padding elements)
    // This enables efficient SIMD operations
    private readonly float _v6, _v7;

    public float TopLeft { get; }

    public float TopRight { get; }

    public float MiddleLeft { get; }

    public float MiddleRight { get; }

    public float BottomLeft { get; }

    public float BottomRight { get; }

    public ShapeVector(float topLeft, float topRight, float middleLeft,
        float middleRight, float bottomLeft, float bottomRight)
    {
        TopLeft = topLeft;
        TopRight = topRight;
        MiddleLeft = middleLeft;
        MiddleRight = middleRight;
        BottomLeft = bottomLeft;
        BottomRight = bottomRight;
        _v6 = 0;
        _v7 = 0;
    }

    public ShapeVector(ReadOnlySpan<float> values)
    {
        if (values.Length < 6)
            throw new ArgumentException("Shape vector requires at least 6 values", nameof(values));

        TopLeft = values[0];
        TopRight = values[1];
        MiddleLeft = values[2];
        MiddleRight = values[3];
        BottomLeft = values[4];
        BottomRight = values[5];
        _v6 = 0;
        _v7 = 0;
    }

    /// <summary>
    ///     Gets the component at the specified index (0-5)
    /// </summary>
    public float this[int index] => index switch
    {
        0 => TopLeft,
        1 => TopRight,
        2 => MiddleLeft,
        3 => MiddleRight,
        4 => BottomLeft,
        5 => BottomRight,
        _ => throw new IndexOutOfRangeException($"Index {index} out of range for ShapeVector")
    };

    /// <summary>
    ///     Calculate squared Euclidean distance to another vector.
    ///     Uses SIMD instructions when available for ~4x speedup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceSquaredTo(in ShapeVector other)
    {
        if (Vector256.IsHardwareAccelerated) return DistanceSquaredAvx(in this, in other);

        if (Vector128.IsHardwareAccelerated) return DistanceSquaredSse(in this, in other);

        return DistanceSquaredScalar(in other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DistanceSquaredAvx(in ShapeVector a, in ShapeVector b)
    {
        // Load both vectors as Vector256<float>
        var va = Vector256.Create(a.TopLeft, a.TopRight, a.MiddleLeft, a.MiddleRight, a.BottomLeft, a.BottomRight, 0f,
            0f);
        var vb = Vector256.Create(b.TopLeft, b.TopRight, b.MiddleLeft, b.MiddleRight, b.BottomLeft, b.BottomRight, 0f,
            0f);

        // Compute difference
        var diff = va - vb;

        // Square the differences
        var squared = diff * diff;

        // Sum all elements (only first 6 matter, last 2 are 0)
        return Vector256.Sum(squared);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DistanceSquaredSse(in ShapeVector a, in ShapeVector b)
    {
        // Process first 4 elements
        var va1 = Vector128.Create(a.TopLeft, a.TopRight, a.MiddleLeft, a.MiddleRight);
        var vb1 = Vector128.Create(b.TopLeft, b.TopRight, b.MiddleLeft, b.MiddleRight);
        var diff1 = va1 - vb1;
        var sq1 = diff1 * diff1;

        // Process remaining 2 elements (with padding)
        var va2 = Vector128.Create(a.BottomLeft, a.BottomRight, 0f, 0f);
        var vb2 = Vector128.Create(b.BottomLeft, b.BottomRight, 0f, 0f);
        var diff2 = va2 - vb2;
        var sq2 = diff2 * diff2;

        // Sum all
        return Vector128.Sum(sq1) + Vector128.Sum(sq2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float DistanceSquaredScalar(in ShapeVector other)
    {
        var d0 = TopLeft - other.TopLeft;
        var d1 = TopRight - other.TopRight;
        var d2 = MiddleLeft - other.MiddleLeft;
        var d3 = MiddleRight - other.MiddleRight;
        var d4 = BottomLeft - other.BottomLeft;
        var d5 = BottomRight - other.BottomRight;

        return d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3 + d4 * d4 + d5 * d5;
    }

    /// <summary>
    ///     Calculate Euclidean distance to another vector
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceTo(in ShapeVector other)
    {
        return MathF.Sqrt(DistanceSquaredTo(other));
    }

    /// <summary>
    ///     Get the maximum component value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Max()
    {
        if (Vector128.IsHardwareAccelerated)
        {
            var v1 = Vector128.Create(TopLeft, TopRight, MiddleLeft, MiddleRight);
            var v2 = Vector128.Create(BottomLeft, BottomRight, float.MinValue, float.MinValue);
            var max1 = Vector128.Max(v1, v2);
            // Horizontal max
            var max2 = Vector128.Max(max1, Vector128.Shuffle(max1, Vector128.Create(2, 3, 0, 1)));
            var max3 = Vector128.Max(max2, Vector128.Shuffle(max2, Vector128.Create(1, 0, 3, 2)));
            return max3.GetElement(0);
        }

        return MathF.Max(MathF.Max(MathF.Max(TopLeft, TopRight), MathF.Max(MiddleLeft, MiddleRight)),
            MathF.Max(BottomLeft, BottomRight));
    }

    /// <summary>
    ///     Minimum coverage threshold below which a cell is considered uniform/empty.
    ///     Prevents noise amplification on near-white or near-black frames.
    /// </summary>
    private const float MinCoverageThreshold = 0.03f;

    /// <summary>
    ///     Apply contrast enhancement by normalizing, applying power, and denormalizing.
    ///     Returns zero vector if max coverage is below threshold to prevent noise amplification.
    /// </summary>
    public ShapeVector ApplyContrast(float power)
    {
        var max = Max();

        // If max coverage is below threshold, the cell is essentially uniform
        // (near-white or near-black). Return zero vector to prevent noise amplification
        // that could cause random character matching on uniform frames.
        if (max <= MinCoverageThreshold) return default;

        var invMax = 1f / max;
        return new ShapeVector(
            MathF.Pow(TopLeft * invMax, power) * max,
            MathF.Pow(TopRight * invMax, power) * max,
            MathF.Pow(MiddleLeft * invMax, power) * max,
            MathF.Pow(MiddleRight * invMax, power) * max,
            MathF.Pow(BottomLeft * invMax, power) * max,
            MathF.Pow(BottomRight * invMax, power) * max
        );
    }

    /// <summary>
    ///     Apply directional contrast using external sampling values.
    ///     Uses SIMD for the multiplication operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ShapeVector ApplyDirectionalContrast(in ShapeVector external, float strength)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            var strengthVec = Vector128.Create(strength);
            var oneVec = Vector128.Create(1f);

            // First 4 elements
            var v1 = Vector128.Create(TopLeft, TopRight, MiddleLeft, MiddleRight);
            var e1 = Vector128.Create(external.TopLeft, external.TopRight, external.MiddleLeft, external.MiddleRight);
            var r1 = v1 * (oneVec - e1 * strengthVec);

            // Last 2 elements
            var v2 = Vector128.Create(BottomLeft, BottomRight, 0f, 0f);
            var e2 = Vector128.Create(external.BottomLeft, external.BottomRight, 0f, 0f);
            var r2 = v2 * (oneVec - e2 * strengthVec);

            return new ShapeVector(
                r1.GetElement(0), r1.GetElement(1), r1.GetElement(2), r1.GetElement(3),
                r2.GetElement(0), r2.GetElement(1)
            );
        }

        return new ShapeVector(
            TopLeft * (1 - external.TopLeft * strength),
            TopRight * (1 - external.TopRight * strength),
            MiddleLeft * (1 - external.MiddleLeft * strength),
            MiddleRight * (1 - external.MiddleRight * strength),
            BottomLeft * (1 - external.BottomLeft * strength),
            BottomRight * (1 - external.BottomRight * strength)
        );
    }

    /// <summary>
    ///     Quantize the vector components to create a cache key
    /// </summary>
    public int GetQuantizedKey(int bitsPerComponent = 5)
    {
        var levels = 1 << bitsPerComponent;
        var mask = levels - 1;
        var scale = levels - 1;

        var q0 = Math.Clamp((int)(TopLeft * scale), 0, mask);
        var q1 = Math.Clamp((int)(TopRight * scale), 0, mask);
        var q2 = Math.Clamp((int)(MiddleLeft * scale), 0, mask);
        var q3 = Math.Clamp((int)(MiddleRight * scale), 0, mask);
        var q4 = Math.Clamp((int)(BottomLeft * scale), 0, mask);
        var q5 = Math.Clamp((int)(BottomRight * scale), 0, mask);

        return q0 | (q1 << bitsPerComponent) | (q2 << (2 * bitsPerComponent)) |
               (q3 << (3 * bitsPerComponent)) | (q4 << (4 * bitsPerComponent)) |
               (q5 << (5 * bitsPerComponent));
    }

    public void CopyTo(Span<float> destination)
    {
        if (destination.Length < 6)
            throw new ArgumentException("Destination must have at least 6 elements");

        destination[0] = TopLeft;
        destination[1] = TopRight;
        destination[2] = MiddleLeft;
        destination[3] = MiddleRight;
        destination[4] = BottomLeft;
        destination[5] = BottomRight;
    }

    public bool Equals(ShapeVector other)
    {
        return TopLeft == other.TopLeft && TopRight == other.TopRight &&
               MiddleLeft == other.MiddleLeft && MiddleRight == other.MiddleRight &&
               BottomLeft == other.BottomLeft && BottomRight == other.BottomRight;
    }

    public override bool Equals(object? obj)
    {
        return obj is ShapeVector other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TopLeft, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomRight);
    }

    public override string ToString()
    {
        return $"[{TopLeft:F2}, {TopRight:F2}, {MiddleLeft:F2}, {MiddleRight:F2}, {BottomLeft:F2}, {BottomRight:F2}]";
    }

    public static bool operator ==(ShapeVector left, ShapeVector right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ShapeVector left, ShapeVector right)
    {
        return !left.Equals(right);
    }
}