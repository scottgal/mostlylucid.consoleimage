// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Character shape vector generation using font rendering

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Core;

/// <summary>
/// Generates shape vectors for ASCII characters by rendering them and sampling regions.
/// Uses staggered sampling circles as described in Alex Harri's article.
/// Thread-safe for concurrent rendering operations.
/// </summary>
public class CharacterMap
{
    private readonly Dictionary<char, ShapeVector> _vectors = new();
    private readonly KdTree _kdTree;
    private readonly ConcurrentDictionary<int, char> _cache = new();
    private readonly int _cacheBits;

    // Sampling circle configuration - 3x2 staggered pattern (per Alex Harri's article)
    // 3 columns, 2 rows - left circles lowered, right circles raised to minimize gaps
    // Layout:  [0]  [1]  [2]   <- Top row
    //          [3]  [4]  [5]   <- Bottom row
    private static readonly (float X, float Y)[] SamplingPositions =
    [
        (0.17f, 0.30f),  // Top-left (lowered)
        (0.50f, 0.25f),  // Top-center
        (0.83f, 0.20f),  // Top-right (raised)
        (0.17f, 0.80f),  // Bottom-left (lowered)
        (0.50f, 0.75f),  // Bottom-center
        (0.83f, 0.70f),  // Bottom-right (raised)
    ];

    private const float SamplingRadius = 0.20f; // Radius as fraction of cell size
    private const int SamplesPerCircle = 37;    // Number of samples per circle for accuracy

    /// <summary>
    /// Default ASCII character set suitable for art rendering
    /// Ordered from lightest to darkest
    /// </summary>
    public static readonly string DefaultCharacterSet =
        " .'`^\",:;Il!i><~+_-?][}{1)(|\\/tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$";

    /// <summary>
    /// Simple character set for basic rendering
    /// </summary>
    public static readonly string SimpleCharacterSet =
        " .:-=+*#%@";

    /// <summary>
    /// Block character set for high-density output
    /// </summary>
    public static readonly string BlockCharacterSet =
        " ░▒▓█";

    /// <summary>
    /// Extended character set with more gradations
    /// </summary>
    public static readonly string ExtendedCharacterSet =
        " `.-':_,^=;><+!rc*/z?sLTv)J7(|Fi{C}fI31tlu[neoZ5Yxjya]2ESwqkP6h9d4VpOGbUAKXHm8RD#$Bg0MNWQ%&@";

    /// <summary>
    /// Gets the shape vector for a character
    /// </summary>
    public ShapeVector GetVector(char c) => _vectors.TryGetValue(c, out var v) ? v : default;

    /// <summary>
    /// Gets all available characters
    /// </summary>
    public IEnumerable<char> Characters => _vectors.Keys;

    public CharacterMap(string? characterSet = null, string? fontFamily = null,
                        int cellSize = 32, int cacheBits = 5)
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

        // Normalize vectors
        NormalizeVectors();
    }

    private static Font GetFont(string? fontFamily, int cellSize)
    {
        FontFamily family;

        if (!string.IsNullOrEmpty(fontFamily))
        {
            if (!SystemFonts.TryGet(fontFamily, out family))
            {
                family = GetDefaultMonospaceFont();
            }
        }
        else
        {
            family = GetDefaultMonospaceFont();
        }

        return family.CreateFont(cellSize * 0.9f, FontStyle.Regular);
    }

    private static FontFamily GetDefaultMonospaceFont()
    {
        string[] monospaceFonts = ["Consolas", "Courier New", "Lucida Console",
                                   "DejaVu Sans Mono", "Liberation Mono", "monospace"];

        foreach (var fontName in monospaceFonts)
        {
            if (SystemFonts.TryGet(fontName, out var family))
                return family;
        }

        var families = SystemFonts.Families.ToList();
        if (families.Count == 0)
            throw new InvalidOperationException("No fonts available on system");

        return families[0];
    }

    private static ShapeVector RenderCharacterVector(char c, Font font, int cellSize)
    {
        // Create a cell-sized image to render the character
        using var image = new Image<L8>(cellSize, cellSize, new L8(255)); // White background

        // Render the character in black, centered
        string text = c.ToString();

        // Measure text to center it
        var bounds = TextMeasurer.MeasureBounds(text, new TextOptions(font));

        float offsetX = (cellSize - bounds.Width) / 2 - bounds.X;
        float offsetY = (cellSize - bounds.Height) / 2 - bounds.Y;

        var textOptions = new RichTextOptions(font)
        {
            Origin = new PointF(offsetX, offsetY),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        image.Mutate(ctx => ctx.DrawText(textOptions, text, Color.Black));

        // Sample 6 regions using staggered circles
        return SampleCharacterRegions(image, cellSize);
    }

    private static ShapeVector SampleCharacterRegions(Image<L8> image, int cellSize)
    {
        Span<float> values = stackalloc float[6];
        float radius = SamplingRadius * cellSize;

        for (int i = 0; i < 6; i++)
        {
            float centerX = SamplingPositions[i].X * cellSize;
            float centerY = SamplingPositions[i].Y * cellSize;

            values[i] = SampleCircleCoverage(image, centerX, centerY, radius);
        }

        return new ShapeVector(values);
    }

    /// <summary>
    /// Sample coverage within a circle - returns fraction of dark pixels (0-1)
    /// Uses concentric ring sampling for accuracy
    /// </summary>
    private static float SampleCircleCoverage(Image<L8> image, float centerX, float centerY, float radius)
    {
        float total = 0;
        int sampleCount = 0;

        // Sample in concentric rings for even distribution
        // Center point
        if (TrySamplePoint(image, centerX, centerY, out float centerVal))
        {
            total += centerVal;
            sampleCount++;
        }

        // Inner ring (6 points at 0.4 * radius)
        float innerRadius = radius * 0.4f;
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathF.PI * 2 / 6;
            float x = centerX + MathF.Cos(angle) * innerRadius;
            float y = centerY + MathF.Sin(angle) * innerRadius;

            if (TrySamplePoint(image, x, y, out float val))
            {
                total += val;
                sampleCount++;
            }
        }

        // Middle ring (12 points at 0.7 * radius)
        float midRadius = radius * 0.7f;
        for (int i = 0; i < 12; i++)
        {
            float angle = i * MathF.PI * 2 / 12 + MathF.PI / 12; // Offset by half step
            float x = centerX + MathF.Cos(angle) * midRadius;
            float y = centerY + MathF.Sin(angle) * midRadius;

            if (TrySamplePoint(image, x, y, out float val))
            {
                total += val;
                sampleCount++;
            }
        }

        // Outer ring (18 points at radius)
        for (int i = 0; i < 18; i++)
        {
            float angle = i * MathF.PI * 2 / 18;
            float x = centerX + MathF.Cos(angle) * radius;
            float y = centerY + MathF.Sin(angle) * radius;

            if (TrySamplePoint(image, x, y, out float val))
            {
                total += val;
                sampleCount++;
            }
        }

        return sampleCount > 0 ? total / sampleCount : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TrySamplePoint(Image<L8> image, float x, float y, out float coverage)
    {
        int ix = (int)x;
        int iy = (int)y;

        if (ix >= 0 && ix < image.Width && iy >= 0 && iy < image.Height)
        {
            // Convert: white (255) = 0 coverage, black (0) = 1 coverage
            coverage = 1f - image[ix, iy].PackedValue / 255f;
            return true;
        }

        coverage = 0;
        return false;
    }

    private void NormalizeVectors()
    {
        if (_vectors.Count == 0) return;

        // Per Alex Harri's article: Find max value for EACH of the 6 components separately
        // "For each of the 6 components across all ASCII characters, the maximum value is identified.
        // Each character's vector components are then divided by their respective maximum values"
        Span<float> maxPerComponent = stackalloc float[6];
        for (int i = 0; i < 6; i++) maxPerComponent[i] = 0;

        foreach (var v in _vectors.Values)
        {
            for (int i = 0; i < 6; i++)
            {
                maxPerComponent[i] = MathF.Max(maxPerComponent[i], v[i]);
            }
        }

        // Check if any component has values
        bool hasValues = false;
        for (int i = 0; i < 6; i++)
        {
            if (maxPerComponent[i] > 0) hasValues = true;
        }
        if (!hasValues) return;

        // Normalize each component by its own max
        var normalized = new Dictionary<char, ShapeVector>();
        foreach (var (c, v) in _vectors)
        {
            normalized[c] = new ShapeVector(
                maxPerComponent[0] > 0 ? v[0] / maxPerComponent[0] : 0,
                maxPerComponent[1] > 0 ? v[1] / maxPerComponent[1] : 0,
                maxPerComponent[2] > 0 ? v[2] / maxPerComponent[2] : 0,
                maxPerComponent[3] > 0 ? v[3] / maxPerComponent[3] : 0,
                maxPerComponent[4] > 0 ? v[4] / maxPerComponent[4] : 0,
                maxPerComponent[5] > 0 ? v[5] / maxPerComponent[5] : 0
            );
        }

        _vectors.Clear();
        foreach (var (c, v) in normalized)
        {
            _vectors[c] = v;
        }
    }

    /// <summary>
    /// Find the best matching character for a shape vector.
    /// Thread-safe with internal caching for repeated lookups.
    /// </summary>
    public char FindBestMatch(in ShapeVector target)
    {
        int cacheKey = target.GetQuantizedKey(_cacheBits);

        // Try to get from cache first (fast path)
        if (_cache.TryGetValue(cacheKey, out char cached))
            return cached;

        // Copy to allow use in lambda (in parameters can't be captured)
        var targetCopy = target;

        // Use GetOrAdd for thread-safe caching
        return _cache.GetOrAdd(cacheKey, _ => _kdTree.FindNearest(targetCopy));
    }

    /// <summary>
    /// Clear the lookup cache
    /// </summary>
    public void ClearCache() => _cache.Clear();
}
