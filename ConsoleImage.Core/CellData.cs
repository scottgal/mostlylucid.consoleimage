// Cell data structure for delta rendering and frame comparison

namespace ConsoleImage.Core;

/// <summary>
///     Stores cell data for delta comparison between frames.
///     Used by renderers to track what changed between frames for efficient updates.
/// </summary>
public readonly struct CellData : IEquatable<CellData>
{
    public readonly char Character;
    public readonly byte R, G, B;

    public CellData(char character, byte r, byte g, byte b)
    {
        Character = character;
        R = r;
        G = g;
        B = b;
    }

    public bool Equals(CellData other)
    {
        return Character == other.Character &&
               R == other.R && G == other.G && B == other.B;
    }

    public override bool Equals(object? obj)
    {
        return obj is CellData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Character, R, G, B);
    }

    /// <summary>
    ///     Check if colors are similar enough to skip update (temporal stability).
    /// </summary>
    public bool IsSimilar(CellData other, int threshold)
    {
        if (Character != other.Character) return false;
        return Math.Abs(R - other.R) <= threshold &&
               Math.Abs(G - other.G) <= threshold &&
               Math.Abs(B - other.B) <= threshold;
    }
}