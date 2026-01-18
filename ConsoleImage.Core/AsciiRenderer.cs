// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Main renderer class for converting images to ASCII art

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Core;

/// <summary>
/// High-quality ASCII art renderer using shape-matching algorithm.
/// Based on Alex Harri's approach: https://alexharri.com/blog/ascii-rendering
/// </summary>
public class AsciiRenderer : IDisposable
{
    private readonly CharacterMap _characterMap;
    private readonly RenderOptions _options;
    private bool _disposed;

    /// <summary>
    /// Create a new ASCII renderer with specified options
    /// </summary>
    public AsciiRenderer(RenderOptions? options = null)
    {
        _options = options ?? RenderOptions.Default;
        _characterMap = new CharacterMap(
            _options.CharacterSet,
            _options.FontFamily
        );
    }

    /// <summary>
    /// Render an image file to ASCII art
    /// </summary>
    public AsciiFrame RenderFile(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image from a stream to ASCII art
    /// </summary>
    public AsciiFrame RenderStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image to ASCII art
    /// </summary>
    public AsciiFrame RenderImage(Image<Rgba32> image)
    {
        var (width, height) = _options.CalculateDimensions(image.Width, image.Height);

        // Resize image to target dimensions
        using var resized = image.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(width * 2, height * 2), // Oversample for better quality
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3
        }));

        var characters = new char[height, width];
        var colors = _options.UseColor ? new Rgb24[height, width] : null;

        // Process each cell
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Sample the cell region
                int srcX = x * 2;
                int srcY = y * 2;

                var (vector, color) = SampleCell(resized, srcX, srcY, 2, 2);

                // Apply contrast enhancement
                if (_options.ContrastPower > 1.0f)
                {
                    vector = vector.ApplyContrast(_options.ContrastPower);
                }

                // Apply directional contrast if enabled
                if (_options.DirectionalContrastStrength > 0 && x > 0 && y > 0 &&
                    x < width - 1 && y < height - 1)
                {
                    var external = SampleExternalRegion(resized, srcX, srcY, 2, 2);
                    vector = vector.ApplyDirectionalContrast(external, _options.DirectionalContrastStrength);
                }

                // Find best matching character
                char c = _characterMap.FindBestMatch(vector);

                if (_options.Invert)
                {
                    // Invert by using the inverse character
                    c = InvertCharacter(c);
                }

                characters[y, x] = c;

                if (colors != null)
                {
                    colors[y, x] = color;
                }
            }
        }

        return new AsciiFrame(characters, colors);
    }

    /// <summary>
    /// Render an animated GIF to a sequence of ASCII frames
    /// </summary>
    public IReadOnlyList<AsciiFrame> RenderGif(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderGifFrames(image);
    }

    /// <summary>
    /// Render an animated GIF from a stream
    /// </summary>
    public IReadOnlyList<AsciiFrame> RenderGifStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderGifFrames(image);
    }

    private List<AsciiFrame> RenderGifFrames(Image<Rgba32> image)
    {
        var frames = new List<AsciiFrame>();
        var (width, height) = _options.CalculateDimensions(image.Width, image.Height);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            // Extract frame
            using var frameImage = image.Frames.CloneFrame(i);

            // Get frame delay from metadata
            int delayMs = 100; // Default
            var metadata = image.Frames[i].Metadata.GetGifMetadata();
            if (metadata.FrameDelay > 0)
            {
                delayMs = metadata.FrameDelay * 10; // GIF delay is in centiseconds
            }

            // Apply speed multiplier
            delayMs = (int)(delayMs / _options.AnimationSpeedMultiplier);

            // Render the frame
            var asciiFrame = RenderSingleFrame(frameImage, width, height, delayMs);
            frames.Add(asciiFrame);
        }

        return frames;
    }

    private AsciiFrame RenderSingleFrame(Image<Rgba32> image, int width, int height, int delayMs)
    {
        // Resize image to target dimensions
        using var resized = image.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(width * 2, height * 2),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3
        }));

        var characters = new char[height, width];
        var colors = _options.UseColor ? new Rgb24[height, width] : null;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcX = x * 2;
                int srcY = y * 2;

                var (vector, color) = SampleCell(resized, srcX, srcY, 2, 2);

                if (_options.ContrastPower > 1.0f)
                {
                    vector = vector.ApplyContrast(_options.ContrastPower);
                }

                if (_options.DirectionalContrastStrength > 0 && x > 0 && y > 0 &&
                    x < width - 1 && y < height - 1)
                {
                    var external = SampleExternalRegion(resized, srcX, srcY, 2, 2);
                    vector = vector.ApplyDirectionalContrast(external, _options.DirectionalContrastStrength);
                }

                char c = _characterMap.FindBestMatch(vector);

                if (_options.Invert)
                {
                    c = InvertCharacter(c);
                }

                characters[y, x] = c;

                if (colors != null)
                {
                    colors[y, x] = color;
                }
            }
        }

        return new AsciiFrame(characters, colors, delayMs);
    }

    private (ShapeVector vector, Rgb24 color) SampleCell(Image<Rgba32> image,
                                                          int x, int y, int cellWidth, int cellHeight)
    {
        // Sample 6 regions (2 columns x 3 rows)
        float regionWidth = cellWidth / 2f;
        float regionHeight = cellHeight / 3f;

        float[] values = new float[6];
        int totalR = 0, totalG = 0, totalB = 0, colorSamples = 0;

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                int index = row * 2 + col;
                float centerX = x + (col + 0.5f) * regionWidth;
                float centerY = y + (row + 0.5f) * regionHeight;

                var (lightness, r, g, b, samples) = SampleRegion(image, centerX, centerY,
                                                                  regionWidth * 0.4f);
                values[index] = lightness;
                totalR += r;
                totalG += g;
                totalB += b;
                colorSamples += samples;
            }
        }

        var avgColor = colorSamples > 0
            ? new Rgb24((byte)(totalR / colorSamples), (byte)(totalG / colorSamples),
                       (byte)(totalB / colorSamples))
            : new Rgb24(128, 128, 128);

        return (new ShapeVector(values), avgColor);
    }

    private (float lightness, int r, int g, int b, int samples) SampleRegion(
        Image<Rgba32> image, float centerX, float centerY, float radius)
    {
        const int sampleCount = 9;
        float totalLightness = 0;
        int totalR = 0, totalG = 0, totalB = 0;
        int validSamples = 0;

        // Center sample
        AddSample(image, (int)centerX, (int)centerY,
                  ref totalLightness, ref totalR, ref totalG, ref totalB, ref validSamples);

        // Samples around center
        for (int i = 0; i < sampleCount - 1; i++)
        {
            float angle = i * MathF.PI * 2 / (sampleCount - 1);
            int sx = (int)(centerX + MathF.Cos(angle) * radius);
            int sy = (int)(centerY + MathF.Sin(angle) * radius);
            AddSample(image, sx, sy,
                      ref totalLightness, ref totalR, ref totalG, ref totalB, ref validSamples);
        }

        float avgLightness = validSamples > 0 ? totalLightness / validSamples : 0;
        return (avgLightness, totalR, totalG, totalB, validSamples);
    }

    private void AddSample(Image<Rgba32> image, int x, int y,
                           ref float totalLightness, ref int totalR, ref int totalG, ref int totalB,
                           ref int validSamples)
    {
        if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
            return;

        var pixel = image[x, y];

        // Calculate lightness (0 = black, 1 = white)
        float lightness = (pixel.R * 0.299f + pixel.G * 0.587f + pixel.B * 0.114f) / 255f;

        // Invert so dark = high value (more ink/coverage)
        totalLightness += 1f - lightness;
        totalR += pixel.R;
        totalG += pixel.G;
        totalB += pixel.B;
        validSamples++;
    }

    private ShapeVector SampleExternalRegion(Image<Rgba32> image, int x, int y,
                                              int cellWidth, int cellHeight)
    {
        // Sample regions just outside the cell boundaries
        float[] values = new float[6];

        // Offsets for external sampling (slightly outside cell)
        float halfWidth = cellWidth / 2f;
        float thirdHeight = cellHeight / 3f;

        // Sample external regions corresponding to each internal region
        (float dx, float dy)[] offsets =
        [
            (-halfWidth, -thirdHeight),     // Top-left external
            (cellWidth + halfWidth, -thirdHeight),  // Top-right external
            (-halfWidth, thirdHeight),       // Middle-left external
            (cellWidth + halfWidth, thirdHeight),  // Middle-right external
            (-halfWidth, cellHeight + thirdHeight),  // Bottom-left external
            (cellWidth + halfWidth, cellHeight + thirdHeight)  // Bottom-right external
        ];

        for (int i = 0; i < 6; i++)
        {
            int sx = (int)(x + offsets[i].dx);
            int sy = (int)(y + offsets[i].dy);

            if (sx >= 0 && sx < image.Width && sy >= 0 && sy < image.Height)
            {
                var pixel = image[sx, sy];
                float lightness = (pixel.R * 0.299f + pixel.G * 0.587f + pixel.B * 0.114f) / 255f;
                values[i] = lightness; // Keep as lightness (not inverted) for contrast
            }
        }

        return new ShapeVector(values);
    }

    private char InvertCharacter(char c)
    {
        // Simple inversion: use opposite density character
        string charSet = _options.CharacterSet ?? CharacterMap.DefaultCharacterSet;
        int index = charSet.IndexOf(c);
        if (index < 0) return c;

        int invertedIndex = charSet.Length - 1 - index;
        return charSet[invertedIndex];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
