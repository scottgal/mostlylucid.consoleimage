// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// This implementation uses shape-matching with 6D vectors for high-quality ASCII art

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace ConsoleImage.Core;

/// <summary>
/// Represents a 6-dimensional shape vector used for character matching.
/// The six dimensions correspond to sampling regions:
/// top-left, top-right, middle-left, middle-right, bottom-left, bottom-right
/// Uses SIMD operations where available for fast distance calculations.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct ShapeVector : IEquatable<ShapeVector>
{
    // Store as 8 floats to align with Vector256 (2 padding elements)
    // This enables efficient SIMD operations
    private readonly float _v0, _v1, _v2, _v3, _v4, _v5, _v6, _v7;

    public float TopLeft => _v0;
    public float TopRight => _v1;
    public float MiddleLeft => _v2;
    public float MiddleRight => _v3;
    public float BottomLeft => _v4;
    public float BottomRight => _v5;

    public ShapeVector(float topLeft, float topRight, float middleLeft,
                       float middleRight, float bottomLeft, float bottomRight)
    {
        _v0 = topLeft;
        _v1 = topRight;
        _v2 = middleLeft;
        _v3 = middleRight;
        _v4 = bottomLeft;
        _v5 = bottomRight;
        _v6 = 0;
        _v7 = 0;
    }

    public ShapeVector(ReadOnlySpan<float> values)
    {
        if (values.Length < 6)
            throw new ArgumentException("Shape vector requires at least 6 values", nameof(values));

        _v0 = values[0];
        _v1 = values[1];
        _v2 = values[2];
        _v3 = values[3];
        _v4 = values[4];
        _v5 = values[5];
        _v6 = 0;
        _v7 = 0;
    }

    /// <summary>
    /// Gets the component at the specified index (0-5)
    /// </summary>
    public float this[int index] => index switch
    {
        0 => _v0,
        1 => _v1,
        2 => _v2,
        3 => _v3,
        4 => _v4,
        5 => _v5,
        _ => throw new IndexOutOfRangeException($"Index {index} out of range for ShapeVector")
    };

    /// <summary>
    /// Calculate squared Euclidean distance to another vector.
    /// Uses SIMD instructions when available for ~4x speedup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceSquaredTo(in ShapeVector other)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            return DistanceSquaredAvx(in this, in other);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            return DistanceSquaredSse(in this, in other);
        }
        else
        {
            return DistanceSquaredScalar(in other);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DistanceSquaredAvx(in ShapeVector a, in ShapeVector b)
    {
        // Load both vectors as Vector256<float>
        var va = Vector256.Create(a._v0, a._v1, a._v2, a._v3, a._v4, a._v5, 0f, 0f);
        var vb = Vector256.Create(b._v0, b._v1, b._v2, b._v3, b._v4, b._v5, 0f, 0f);

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
        var va1 = Vector128.Create(a._v0, a._v1, a._v2, a._v3);
        var vb1 = Vector128.Create(b._v0, b._v1, b._v2, b._v3);
        var diff1 = va1 - vb1;
        var sq1 = diff1 * diff1;

        // Process remaining 2 elements (with padding)
        var va2 = Vector128.Create(a._v4, a._v5, 0f, 0f);
        var vb2 = Vector128.Create(b._v4, b._v5, 0f, 0f);
        var diff2 = va2 - vb2;
        var sq2 = diff2 * diff2;

        // Sum all
        return Vector128.Sum(sq1) + Vector128.Sum(sq2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float DistanceSquaredScalar(in ShapeVector other)
    {
        float d0 = _v0 - other._v0;
        float d1 = _v1 - other._v1;
        float d2 = _v2 - other._v2;
        float d3 = _v3 - other._v3;
        float d4 = _v4 - other._v4;
        float d5 = _v5 - other._v5;

        return d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3 + d4 * d4 + d5 * d5;
    }

    /// <summary>
    /// Calculate Euclidean distance to another vector
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceTo(in ShapeVector other) => MathF.Sqrt(DistanceSquaredTo(other));

    /// <summary>
    /// Get the maximum component value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Max()
    {
        if (Vector128.IsHardwareAccelerated)
        {
            var v1 = Vector128.Create(_v0, _v1, _v2, _v3);
            var v2 = Vector128.Create(_v4, _v5, float.MinValue, float.MinValue);
            var max1 = Vector128.Max(v1, v2);
            // Horizontal max
            var max2 = Vector128.Max(max1, Vector128.Shuffle(max1, Vector128.Create(2, 3, 0, 1)));
            var max3 = Vector128.Max(max2, Vector128.Shuffle(max2, Vector128.Create(1, 0, 3, 2)));
            return max3.GetElement(0);
        }

        return MathF.Max(MathF.Max(MathF.Max(_v0, _v1), MathF.Max(_v2, _v3)),
                         MathF.Max(_v4, _v5));
    }

    /// <summary>
    /// Apply contrast enhancement by normalizing, applying power, and denormalizing
    /// </summary>
    public ShapeVector ApplyContrast(float power)
    {
        float max = Max();
        if (max <= 0) return this;

        float invMax = 1f / max;
        return new ShapeVector(
            MathF.Pow(_v0 * invMax, power) * max,
            MathF.Pow(_v1 * invMax, power) * max,
            MathF.Pow(_v2 * invMax, power) * max,
            MathF.Pow(_v3 * invMax, power) * max,
            MathF.Pow(_v4 * invMax, power) * max,
            MathF.Pow(_v5 * invMax, power) * max
        );
    }

    /// <summary>
    /// Apply directional contrast using external sampling values.
    /// Uses SIMD for the multiplication operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ShapeVector ApplyDirectionalContrast(in ShapeVector external, float strength)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            var strengthVec = Vector128.Create(strength);
            var oneVec = Vector128.Create(1f);

            // First 4 elements
            var v1 = Vector128.Create(_v0, _v1, _v2, _v3);
            var e1 = Vector128.Create(external._v0, external._v1, external._v2, external._v3);
            var r1 = v1 * (oneVec - e1 * strengthVec);

            // Last 2 elements
            var v2 = Vector128.Create(_v4, _v5, 0f, 0f);
            var e2 = Vector128.Create(external._v4, external._v5, 0f, 0f);
            var r2 = v2 * (oneVec - e2 * strengthVec);

            return new ShapeVector(
                r1.GetElement(0), r1.GetElement(1), r1.GetElement(2), r1.GetElement(3),
                r2.GetElement(0), r2.GetElement(1)
            );
        }

        return new ShapeVector(
            _v0 * (1 - external._v0 * strength),
            _v1 * (1 - external._v1 * strength),
            _v2 * (1 - external._v2 * strength),
            _v3 * (1 - external._v3 * strength),
            _v4 * (1 - external._v4 * strength),
            _v5 * (1 - external._v5 * strength)
        );
    }

    /// <summary>
    /// Quantize the vector components to create a cache key
    /// </summary>
    public int GetQuantizedKey(int bitsPerComponent = 5)
    {
        int levels = 1 << bitsPerComponent;
        int mask = levels - 1;
        int scale = levels - 1;

        int q0 = Math.Clamp((int)(_v0 * scale), 0, mask);
        int q1 = Math.Clamp((int)(_v1 * scale), 0, mask);
        int q2 = Math.Clamp((int)(_v2 * scale), 0, mask);
        int q3 = Math.Clamp((int)(_v3 * scale), 0, mask);
        int q4 = Math.Clamp((int)(_v4 * scale), 0, mask);
        int q5 = Math.Clamp((int)(_v5 * scale), 0, mask);

        return q0 | (q1 << bitsPerComponent) | (q2 << (2 * bitsPerComponent)) |
               (q3 << (3 * bitsPerComponent)) | (q4 << (4 * bitsPerComponent)) |
               (q5 << (5 * bitsPerComponent));
    }

    public void CopyTo(Span<float> destination)
    {
        if (destination.Length < 6)
            throw new ArgumentException("Destination must have at least 6 elements");

        destination[0] = _v0;
        destination[1] = _v1;
        destination[2] = _v2;
        destination[3] = _v3;
        destination[4] = _v4;
        destination[5] = _v5;
    }

    public bool Equals(ShapeVector other) =>
        _v0 == other._v0 && _v1 == other._v1 &&
        _v2 == other._v2 && _v3 == other._v3 &&
        _v4 == other._v4 && _v5 == other._v5;

    public override bool Equals(object? obj) => obj is ShapeVector other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_v0, _v1, _v2, _v3, _v4, _v5);

    public override string ToString() =>
        $"[{_v0:F2}, {_v1:F2}, {_v2:F2}, {_v3:F2}, {_v4:F2}, {_v5:F2}]";

    public static bool operator ==(ShapeVector left, ShapeVector right) => left.Equals(right);
    public static bool operator !=(ShapeVector left, ShapeVector right) => !left.Equals(right);
}
