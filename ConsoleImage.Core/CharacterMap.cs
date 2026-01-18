// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Character shape vector generation using font rendering

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Core;

/// <summary>
/// Generates shape vectors for ASCII characters by rendering them and sampling regions.
/// </summary>
public class CharacterMap
{
    private readonly Dictionary<char, ShapeVector> _vectors = new();
    private readonly KdTree _kdTree;
    private readonly Dictionary<int, char> _cache = new();
    private readonly int _cacheBits;

    /// <summary>
    /// Default ASCII character set suitable for art rendering
    /// </summary>
    public static readonly string DefaultCharacterSet =
        " .`'^\",:;!i><~+_-?][}{1)(|/\\tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$";

    /// <summary>
    /// Simple character set for basic rendering
    /// </summary>
    public static readonly string SimpleCharacterSet =
        " .'`^\",:;Il!i><~+_-?][}{1)(|\\/tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$";

    /// <summary>
    /// Block character set for high-density output
    /// </summary>
    public static readonly string BlockCharacterSet =
        " ░▒▓█";

    /// <summary>
    /// Gets the shape vector for a character
    /// </summary>
    public ShapeVector GetVector(char c) => _vectors.TryGetValue(c, out var v) ? v : default;

    /// <summary>
    /// Gets all available characters
    /// </summary>
    public IEnumerable<char> Characters => _vectors.Keys;

    public CharacterMap(string? characterSet = null, string? fontFamily = null,
                        int cellSize = 16, int cacheBits = 5)
    {
        _cacheBits = cacheBits;
        characterSet ??= DefaultCharacterSet;

        // Generate shape vectors for each character
        GenerateVectors(characterSet, fontFamily, cellSize);

        // Build k-d tree for fast lookup
        var entries = _vectors.Select(kvp =>
            new KdTree.CharacterEntry(kvp.Key, kvp.Value));
        _kdTree = new KdTree(entries);
    }

    private void GenerateVectors(string characterSet, string? fontFamily, int cellSize)
    {
        Font font = GetFont(fontFamily, cellSize);

        foreach (char c in characterSet.Distinct())
        {
            var vector = RenderCharacterVector(c, font, cellSize);
            _vectors[c] = vector;
        }

        // Normalize vectors so the space character maps to (0,0,0,0,0,0)
        // and the densest character maps closer to (1,1,1,1,1,1)
        NormalizeVectors();
    }

    private static Font GetFont(string? fontFamily, int cellSize)
    {
        FontFamily family;

        if (!string.IsNullOrEmpty(fontFamily))
        {
            if (!SystemFonts.TryGet(fontFamily, out family))
            {
                // Fall back to a monospace font
                family = GetDefaultMonospaceFont();
            }
        }
        else
        {
            family = GetDefaultMonospaceFont();
        }

        return family.CreateFont(cellSize, FontStyle.Regular);
    }

    private static FontFamily GetDefaultMonospaceFont()
    {
        // Try common monospace fonts in order of preference
        string[] monospaceFonts = ["Consolas", "Courier New", "Lucida Console",
                                   "DejaVu Sans Mono", "Liberation Mono", "monospace"];

        foreach (var fontName in monospaceFonts)
        {
            if (SystemFonts.TryGet(fontName, out var family))
                return family;
        }

        // Fall back to first available font
        var families = SystemFonts.Families.ToList();
        if (families.Count == 0)
            throw new InvalidOperationException("No fonts available on system");

        return families[0];
    }

    private static ShapeVector RenderCharacterVector(char c, Font font, int cellSize)
    {
        // Create a cell-sized image to render the character
        using var image = new Image<L8>(cellSize, cellSize, new L8(255)); // White background

        // Render the character in black
        string text = c.ToString();
        var textOptions = new RichTextOptions(font)
        {
            Origin = new PointF(0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        image.Mutate(ctx => ctx.DrawText(textOptions, text, Color.Black));

        // Sample 6 regions using circles
        return SampleRegions(image, cellSize);
    }

    private static ShapeVector SampleRegions(Image<L8> image, int cellSize)
    {
        // Define 6 sampling regions (2x3 grid)
        // Each region is sampled with multiple points for anti-aliasing
        float regionWidth = cellSize / 2f;
        float regionHeight = cellSize / 3f;

        float[] values = new float[6];

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                int index = row * 2 + col;
                float centerX = (col + 0.5f) * regionWidth;
                float centerY = (row + 0.5f) * regionHeight;
                float radius = MathF.Min(regionWidth, regionHeight) * 0.4f;

                values[index] = SampleCircle(image, centerX, centerY, radius);
            }
        }

        return new ShapeVector(values);
    }

    private static float SampleCircle(Image<L8> image, float centerX, float centerY, float radius)
    {
        // Supersample with multiple points in a circle pattern
        const int samples = 12;
        float total = 0;
        int validSamples = 0;

        // Center point
        int cx = (int)centerX;
        int cy = (int)centerY;
        if (cx >= 0 && cx < image.Width && cy >= 0 && cy < image.Height)
        {
            total += 1f - image[cx, cy].PackedValue / 255f; // Invert: black = 1, white = 0
            validSamples++;
        }

        // Points around the circle
        for (int i = 0; i < samples; i++)
        {
            float angle = i * MathF.PI * 2 / samples;
            int x = (int)(centerX + MathF.Cos(angle) * radius);
            int y = (int)(centerY + MathF.Sin(angle) * radius);

            if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
            {
                total += 1f - image[x, y].PackedValue / 255f;
                validSamples++;
            }
        }

        return validSamples > 0 ? total / validSamples : 0;
    }

    private void NormalizeVectors()
    {
        if (_vectors.Count == 0) return;

        // Find max value across all vectors
        float maxValue = _vectors.Values
            .SelectMany(v => new[] { v.TopLeft, v.TopRight, v.MiddleLeft,
                                     v.MiddleRight, v.BottomLeft, v.BottomRight })
            .Max();

        if (maxValue <= 0) return;

        // Normalize all vectors
        var normalized = new Dictionary<char, ShapeVector>();
        foreach (var (c, v) in _vectors)
        {
            normalized[c] = new ShapeVector(
                v.TopLeft / maxValue,
                v.TopRight / maxValue,
                v.MiddleLeft / maxValue,
                v.MiddleRight / maxValue,
                v.BottomLeft / maxValue,
                v.BottomRight / maxValue
            );
        }

        _vectors.Clear();
        foreach (var (c, v) in normalized)
        {
            _vectors[c] = v;
        }
    }

    /// <summary>
    /// Find the best matching character for a shape vector
    /// </summary>
    public char FindBestMatch(in ShapeVector target)
    {
        // Check cache first
        int cacheKey = target.GetQuantizedKey(_cacheBits);
        if (_cache.TryGetValue(cacheKey, out char cached))
            return cached;

        // Use k-d tree for fast lookup
        char result = _kdTree.FindNearest(target);

        // Cache the result
        _cache[cacheKey] = result;

        return result;
    }

    /// <summary>
    /// Clear the lookup cache
    /// </summary>
    public void ClearCache() => _cache.Clear();
}
