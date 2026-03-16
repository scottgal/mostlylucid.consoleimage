// Cell data structure for delta rendering and frame comparison

namespace ConsoleImage.Core;

/// <summary>
///     Stores cell data for delta comparison between frames.
///     Used by renderers to track what changed between frames for efficient updates.
///     BgR/BgG/BgB hold the background fill color for braille cells (0,0,0 = terminal default).
/// </summary>
public readonly struct CellData : IEquatable<CellData>
{
    public readonly char Character;
    public readonly byte R, G, B;       // Foreground (dot) color
    public readonly byte BgR, BgG, BgB; // Background fill color (braille gap fill; 0,0,0 = none)

    public CellData(char character, byte r, byte g, byte b)
    {
        Character = character;
        R = r; G = g; B = b;
        BgR = BgG = BgB = 0;
    }

    public CellData(char character, byte r, byte g, byte b, byte bgR, byte bgG, byte bgB)
    {
        Character = character;
        R = r; G = g; B = b;
        BgR = bgR; BgG = bgG; BgB = bgB;
    }

    public bool HasBackground => BgR != 0 || BgG != 0 || BgB != 0;

    public bool Equals(CellData other)
    {
        return Character == other.Character &&
               R == other.R && G == other.G && B == other.B &&
               BgR == other.BgR && BgG == other.BgG && BgB == other.BgB;
    }

    public override bool Equals(object? obj)
    {
        return obj is CellData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Character, R, G, B, BgR, BgG, BgB);
    }

    /// <summary>
    ///     Check if colors are similar enough to skip update (temporal stability).
    /// </summary>
    public bool IsSimilar(CellData other, int threshold)
    {
        if (Character != other.Character) return false;
        return Math.Abs(R - other.R) <= threshold &&
               Math.Abs(G - other.G) <= threshold &&
               Math.Abs(B - other.B) <= threshold &&
               Math.Abs(BgR - other.BgR) <= threshold &&
               Math.Abs(BgG - other.BgG) <= threshold &&
               Math.Abs(BgB - other.BgB) <= threshold;
    }
}
