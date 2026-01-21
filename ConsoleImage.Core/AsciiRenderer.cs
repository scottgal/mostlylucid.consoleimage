// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Main renderer class for converting images to ASCII art

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Runtime.CompilerServices;

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

    // Same staggered sampling positions as CharacterMap (3x2 grid per article)
    // Layout:  [0]  [1]  [2]   <- Top row
    //          [3]  [4]  [5]   <- Bottom row
    private static readonly (float X, float Y)[] InternalSamplingPositions =
    [
        (0.17f, 0.30f),  // Top-left (lowered)
        (0.50f, 0.25f),  // Top-center
        (0.83f, 0.20f),  // Top-right (raised)
        (0.17f, 0.80f),  // Bottom-left (lowered)
        (0.50f, 0.75f),  // Bottom-center
        (0.83f, 0.70f),  // Bottom-right (raised)
    ];

    // Pre-computed sin/cos lookup tables for circle sampling (major performance optimization)
    // Eliminates ~36 trig calls per circle × 6 circles per cell = 216 trig calls per cell
    private static readonly (float Cos, float Sin)[] InnerRingAngles = PrecomputeAngles(6, 0);
    private static readonly (float Cos, float Sin)[] MiddleRingAngles = PrecomputeAngles(12, MathF.PI / 12);
    private static readonly (float Cos, float Sin)[] OuterRingAngles = PrecomputeAngles(18, 0);
    private static readonly (float Cos, float Sin)[] OuterRing12Angles = PrecomputeAngles(12, 0); // For SampleCircleLightness

    private static (float Cos, float Sin)[] PrecomputeAngles(int count, float offset)
    {
        var angles = new (float Cos, float Sin)[count];
        for (int i = 0; i < count; i++)
        {
            float angle = i * MathF.PI * 2 / count + offset;
            angles[i] = (MathF.Cos(angle), MathF.Sin(angle));
        }
        return angles;
    }

    // 10 external sampling positions for directional contrast (per article)
    // These reach outside the cell boundaries to detect edges
    // Positions correspond to the 3x2 internal grid:
    //   [E0] [E1] [E2] [E3]     <- Above top row
    //   [E4]  0    1    2  [E5] <- Top row with left/right external
    //   [E6]  3    4    5  [E7] <- Bottom row with left/right external
    //   [E8] [E9] [E10][E11]    <- Below bottom row
    private static readonly (float X, float Y)[] ExternalSamplingPositions =
    [
        (0.17f, -0.10f),  // Above top-left
        (0.50f, -0.10f),  // Above top-center
        (0.83f, -0.10f),  // Above top-right
        (-0.15f, 0.30f),  // Left of top-left
        (1.15f, 0.20f),   // Right of top-right
        (-0.15f, 0.80f),  // Left of bottom-left
        (1.15f, 0.70f),   // Right of bottom-right
        (0.17f, 1.10f),   // Below bottom-left
        (0.50f, 1.10f),   // Below bottom-center
        (0.83f, 1.10f),   // Below bottom-right
    ];

    private const float SamplingRadius = 0.18f;

    public AsciiRenderer(RenderOptions? options = null)
    {
        _options = options ?? RenderOptions.Default;
        _characterMap = new CharacterMap(
            _options.CharacterSet,
            _options.FontFamily
        );
    }

    public AsciiFrame RenderFile(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderImage(image);
    }

    public AsciiFrame RenderStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderImage(image);
    }

    public AsciiFrame RenderImage(Image<Rgba32> image)
    {
        var (width, height) = _options.CalculateDimensions(image.Width, image.Height);

        // Calculate cell size - we want higher resolution sampling
        int cellWidth = Math.Max(4, image.Width / width);
        int cellHeight = Math.Max(4, image.Height / height);

        // Resize to exact multiple of cell size
        int targetWidth = width * cellWidth;
        int targetHeight = height * cellHeight;

        using var resized = image.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            });

            if (_options.EnableEdgeDetection)
            {
                ctx.DetectEdges();
            }
        });

        Image<Rgba32>? processed = null;

        // Determine background thresholds
        float? lightThreshold = _options.BackgroundThreshold;
        float? darkThreshold = _options.DarkBackgroundThreshold;
        bool shouldInvert = _options.Invert;

        // Auto-detect if enabled
        if (_options.AutoBackgroundSuppression && !lightThreshold.HasValue && !darkThreshold.HasValue)
        {
            var (lt, dt, inv) = DetectBackgroundType(resized);
            lightThreshold = lt;
            darkThreshold = dt;
            if (inv) shouldInvert = true;
        }

        // Apply background suppression if any threshold is set
        if (lightThreshold.HasValue || darkThreshold.HasValue)
        {
            processed = ApplyBackgroundSuppression(resized, lightThreshold, darkThreshold);
        }

        var sourceImage = processed ?? resized;

        try
        {
            return RenderFromImage(sourceImage, width, height, cellWidth, cellHeight, 0, shouldInvert);
        }
        finally
        {
            processed?.Dispose();
        }
    }

    private AsciiFrame RenderFromImage(Image<Rgba32> image, int width, int height,
                                        int cellWidth, int cellHeight, int delayMs,
                                        bool shouldInvert = false)
    {
        var characters = new char[height, width];
        var colors = _options.UseColor ? new Rgb24[height, width] : null;

        // Pre-compute all internal vectors for directional contrast
        var internalVectors = new ShapeVector[height, width];
        var externalVectors = new float[height, width, 10];

        // Pre-compute edge direction info if needed
        float[,]? edgeMagnitudes = null;
        float[,]? edgeAngles = null;
        if (_options.EnableEdgeDirectionChars)
        {
            (edgeMagnitudes, edgeAngles) = EdgeDirection.ComputeEdges(image, width, height);
        }

        if (_options.UseParallelProcessing && height > 4)
        {
            // First pass: compute all vectors
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int srcX = x * cellWidth;
                    int srcY = y * cellHeight;

                    var (vector, _) = SampleCell(image, srcX, srcY, cellWidth, cellHeight);
                    internalVectors[y, x] = vector;

                    if (_options.DirectionalContrastStrength > 0)
                    {
                        SampleExternalCircles(image, srcX, srcY, cellWidth, cellHeight,
                                              externalVectors, y, x);
                    }
                }
            });

            // Apply dithering if enabled (must be done sequentially due to error diffusion)
            if (_options.EnableDithering)
            {
                // Apply contrast first, then dither
                ApplyContrastToVectors(internalVectors, externalVectors, width, height);
                int numChars = (_options.CharacterSet ?? CharacterMap.DefaultCharacterSet).Length;
                internalVectors = Dithering.ApplyToShapeVectors(internalVectors, numChars);
            }

            // Second pass: find characters
            Parallel.For(0, height, y =>
            {
                ProcessRowWithContrast(image, characters, colors, internalVectors,
                                       externalVectors, width, height, cellWidth, cellHeight, y, shouldInvert,
                                       edgeMagnitudes, edgeAngles, skipContrast: _options.EnableDithering);
            });
        }
        else
        {
            // Sequential processing
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcX = x * cellWidth;
                    int srcY = y * cellHeight;

                    var (vector, _) = SampleCell(image, srcX, srcY, cellWidth, cellHeight);
                    internalVectors[y, x] = vector;

                    if (_options.DirectionalContrastStrength > 0)
                    {
                        SampleExternalCircles(image, srcX, srcY, cellWidth, cellHeight,
                                              externalVectors, y, x);
                    }
                }
            }

            // Apply dithering if enabled
            if (_options.EnableDithering)
            {
                ApplyContrastToVectors(internalVectors, externalVectors, width, height);
                int numChars = (_options.CharacterSet ?? CharacterMap.DefaultCharacterSet).Length;
                internalVectors = Dithering.ApplyToShapeVectors(internalVectors, numChars);
            }

            for (int y = 0; y < height; y++)
            {
                ProcessRowWithContrast(image, characters, colors, internalVectors,
                                       externalVectors, width, height, cellWidth, cellHeight, y, shouldInvert,
                                       edgeMagnitudes, edgeAngles, skipContrast: _options.EnableDithering);
            }
        }

        return new AsciiFrame(characters, colors, delayMs);
    }

    /// <summary>
    /// Apply contrast enhancement to all vectors (used when dithering is enabled)
    /// </summary>
    private void ApplyContrastToVectors(ShapeVector[,] vectors, float[,,] externalVectors, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var vector = vectors[y, x];

                if (_options.DirectionalContrastStrength > 0)
                {
                    vector = ApplyDirectionalContrastFrom10Circles(vector, externalVectors, x, y,
                                                                    width, height, _options.ContrastPower);
                }
                else if (_options.ContrastPower > 1.0f)
                {
                    vector = vector.ApplyContrast(_options.ContrastPower);
                }

                vectors[y, x] = vector;
            }
        }
    }

    private void ProcessRowWithContrast(Image<Rgba32> image, char[,] characters, Rgb24[,]? colors,
                                         ShapeVector[,] internalVectors, float[,,] externalVectors,
                                         int width, int height, int cellWidth, int cellHeight, int y,
                                         bool shouldInvert, float[,]? edgeMagnitudes = null,
                                         float[,]? edgeAngles = null, bool skipContrast = false)
    {
        for (int x = 0; x < width; x++)
        {
            var vector = internalVectors[y, x];

            // Apply contrast if not already applied (during dithering)
            if (!skipContrast)
            {
                // Per Alex Harri's article:
                // If directional contrast is enabled, apply max-then-contrast
                // Otherwise, apply global contrast enhancement only
                if (_options.DirectionalContrastStrength > 0)
                {
                    // Directional contrast: max(internal, external) then apply contrast
                    vector = ApplyDirectionalContrastFrom10Circles(vector, externalVectors, x, y,
                                                                    width, height, _options.ContrastPower);
                }
                else if (_options.ContrastPower > 1.0f)
                {
                    // Global contrast enhancement only (no directional)
                    vector = vector.ApplyContrast(_options.ContrastPower);
                }
            }

            // Find best matching character
            char c = _characterMap.FindBestMatch(vector);

            // Apply edge direction override if enabled and edge is strong enough
            if (edgeMagnitudes != null && edgeAngles != null)
            {
                c = EdgeDirection.BlendCharacter(c, edgeAngles[y, x], edgeMagnitudes[y, x]);
            }

            if (shouldInvert)
            {
                c = InvertCharacter(c);
            }

            characters[y, x] = c;

            if (colors != null)
            {
                int srcX = x * cellWidth;
                int srcY = y * cellHeight;
                var color = SampleCellColor(image, srcX, srcY, cellWidth, cellHeight);

                // Apply gamma correction to brighten colors
                if (_options.Gamma != 1.0f)
                {
                    color = new SixLabors.ImageSharp.PixelFormats.Rgb24(
                        (byte)Math.Clamp(MathF.Pow(color.R / 255f, _options.Gamma) * 255f, 0, 255),
                        (byte)Math.Clamp(MathF.Pow(color.G / 255f, _options.Gamma) * 255f, 0, 255),
                        (byte)Math.Clamp(MathF.Pow(color.B / 255f, _options.Gamma) * 255f, 0, 255));
                }

                colors[y, x] = color;
            }
        }
    }

    private static ShapeVector ApplyDirectionalContrastFrom10Circles(
        ShapeVector vector, float[,,] externalVectors, int x, int y,
        int width, int height, float contrastPower)
    {
        // Per Alex Harri's article: take MAX of internal and corresponding external values,
        // then apply contrast enhancement. This enhances edges by boosting values
        // where either the internal or external region has content.

        Span<float> maxed = stackalloc float[6];

        // Get the 10 external values for this cell
        Span<float> ext = stackalloc float[10];
        for (int i = 0; i < 10; i++)
        {
            ext[i] = externalVectors[y, x, i];
        }

        // Internal layout (3x2):
        //   [0]  [1]  [2]   <- Top row
        //   [3]  [4]  [5]   <- Bottom row
        // External layout:
        //   [0]  [1]  [2]      <- Above
        //   [3]  INT  INT [4]  <- Left/Right of top
        //   [5]  INT  INT [6]  <- Left/Right of bottom
        //   [7]  [8]  [9]      <- Below

        // Position 0 (top-left): affected by above-left (0) and left-of-top (3)
        maxed[0] = MathF.Max(vector[0], MathF.Max(ext[0], ext[3]));

        // Position 1 (top-center): affected by above-center (1)
        maxed[1] = MathF.Max(vector[1], ext[1]);

        // Position 2 (top-right): affected by above-right (2) and right-of-top (4)
        maxed[2] = MathF.Max(vector[2], MathF.Max(ext[2], ext[4]));

        // Position 3 (bottom-left): affected by left-of-bottom (5) and below-left (7)
        maxed[3] = MathF.Max(vector[3], MathF.Max(ext[5], ext[7]));

        // Position 4 (bottom-center): affected by below-center (8)
        maxed[4] = MathF.Max(vector[4], ext[8]);

        // Position 5 (bottom-right): affected by right-of-bottom (6) and below-right (9)
        maxed[5] = MathF.Max(vector[5], MathF.Max(ext[6], ext[9]));

        // Apply contrast enhancement to the maxed vector (per article formula)
        var maxedVector = new ShapeVector(maxed);
        return maxedVector.ApplyContrast(contrastPower);
    }

    private void SampleExternalCircles(Image<Rgba32> image, int cellX, int cellY,
                                        int cellWidth, int cellHeight,
                                        float[,,] externalVectors, int y, int x)
    {
        float radius = SamplingRadius * MathF.Min(cellWidth, cellHeight);

        for (int i = 0; i < 10; i++)
        {
            float sampleX = cellX + ExternalSamplingPositions[i].X * cellWidth;
            float sampleY = cellY + ExternalSamplingPositions[i].Y * cellHeight;

            // Sample coverage (darkness) not lightness - to match internal sampling
            externalVectors[y, x, i] = 1f - SampleCircleLightness(image, sampleX, sampleY, radius);
        }
    }

    private static Image<Rgba32> ApplyBackgroundSuppression(Image<Rgba32> source, float? lightThreshold, float? darkThreshold)
    {
        var result = source.Clone();

        result.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    float brightness = (pixel.R * 0.299f + pixel.G * 0.587f + pixel.B * 0.114f) / 255f;

                    // Light background suppression: bright pixels → white
                    if (lightThreshold.HasValue && brightness > lightThreshold.Value)
                    {
                        pixel = new Rgba32(255, 255, 255, pixel.A);
                    }
                    // Dark background suppression: dark pixels → white (so they become spaces)
                    else if (darkThreshold.HasValue && brightness < darkThreshold.Value)
                    {
                        pixel = new Rgba32(255, 255, 255, pixel.A);
                    }
                }
            }
        });

        return result;
    }

    private static (float? lightThreshold, float? darkThreshold, bool shouldInvert) DetectBackgroundType(Image<Rgba32> image)
    {
        // Sample edge pixels to determine background type
        int sampleCount = 0;
        float totalBrightness = 0;
        int brightCount = 0;
        int darkCount = 0;

        void SamplePixel(int x, int y)
        {
            if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
            {
                var pixel = image[x, y];
                float brightness = (pixel.R * 0.299f + pixel.G * 0.587f + pixel.B * 0.114f) / 255f;
                totalBrightness += brightness;
                sampleCount++;

                if (brightness > 0.7f) brightCount++;
                else if (brightness < 0.3f) darkCount++;
            }
        }

        // Sample top and bottom edges
        for (int x = 0; x < image.Width; x += Math.Max(1, image.Width / 50))
        {
            SamplePixel(x, 0);
            SamplePixel(x, image.Height - 1);
        }

        // Sample left and right edges
        for (int y = 0; y < image.Height; y += Math.Max(1, image.Height / 50))
        {
            SamplePixel(0, y);
            SamplePixel(image.Width - 1, y);
        }

        // Sample corners more heavily
        int cornerSize = Math.Min(10, Math.Min(image.Width, image.Height) / 10);
        for (int i = 0; i < cornerSize; i++)
        {
            for (int j = 0; j < cornerSize; j++)
            {
                SamplePixel(i, j);  // Top-left
                SamplePixel(image.Width - 1 - i, j);  // Top-right
                SamplePixel(i, image.Height - 1 - j);  // Bottom-left
                SamplePixel(image.Width - 1 - i, image.Height - 1 - j);  // Bottom-right
            }
        }

        if (sampleCount == 0) return (null, null, false);

        float avgBrightness = totalBrightness / sampleCount;
        float darkRatio = (float)darkCount / sampleCount;
        float brightRatio = (float)brightCount / sampleCount;

        // Strong dark background (like space images) - INVERT output so black becomes space
        if (darkRatio > 0.5f && avgBrightness < 0.3f)
        {
            return (null, null, true); // Invert instead of threshold
        }
        // Strong light background
        else if (brightRatio > 0.6f && avgBrightness > 0.75f)
        {
            return (0.85f, null, false);
        }

        return (null, null, false);
    }

    public IReadOnlyList<AsciiFrame> RenderGif(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderGifFrames(image);
    }

    public IReadOnlyList<AsciiFrame> RenderGifStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderGifFrames(image);
    }

    private List<AsciiFrame> RenderGifFrames(Image<Rgba32> image)
    {
        var (width, height) = _options.CalculateDimensions(image.Width, image.Height);

        int cellWidth = Math.Max(4, image.Width / width);
        int cellHeight = Math.Max(4, image.Height / height);

        // Determine frame step for sampling (skip frames for efficiency)
        int frameStep = Math.Max(1, _options.FrameSampleRate);

        // Calculate how many frames we'll actually render
        int frameCount = (image.Frames.Count + frameStep - 1) / frameStep;
        var frames = new AsciiFrame[frameCount];
        var frameIndices = new int[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            frameIndices[i] = i * frameStep;
        }

        if (_options.UseParallelProcessing && frameCount > 2)
        {
            Parallel.For(0, frameCount, i =>
            {
                frames[i] = RenderGifFrame(image, frameIndices[i], width, height, cellWidth, cellHeight, frameStep);
            });
        }
        else
        {
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = RenderGifFrame(image, frameIndices[i], width, height, cellWidth, cellHeight, frameStep);
            }
        }

        return frames.ToList();
    }

    private AsciiFrame RenderGifFrame(Image<Rgba32> image, int frameIndex, int width, int height,
                                       int cellWidth, int cellHeight, int frameStep = 1)
    {
        // Get frame metadata first
        var metadata = image.Frames[frameIndex].Metadata.GetGifMetadata();

        int delayMs = 100;
        if (metadata.FrameDelay > 0)
        {
            delayMs = metadata.FrameDelay * 10;
        }
        // Adjust delay to account for skipped frames
        delayMs = (int)((delayMs * frameStep) / _options.AnimationSpeedMultiplier);

        using var frameImage = image.Frames.CloneFrame(frameIndex);

        int targetWidth = width * cellWidth;
        int targetHeight = height * cellHeight;

        using var resized = frameImage.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            });

            if (_options.EnableEdgeDetection)
            {
                ctx.DetectEdges();
            }
        });

        Image<Rgba32>? processed = null;

        // Determine background thresholds
        float? lightThreshold = _options.BackgroundThreshold;
        float? darkThreshold = _options.DarkBackgroundThreshold;
        bool shouldInvert = _options.Invert;

        // Auto-detect if enabled
        if (_options.AutoBackgroundSuppression && !lightThreshold.HasValue && !darkThreshold.HasValue)
        {
            var (lt, dt, inv) = DetectBackgroundType(resized);
            lightThreshold = lt;
            darkThreshold = dt;
            if (inv) shouldInvert = true;
        }

        // Apply background suppression if any threshold is set
        if (lightThreshold.HasValue || darkThreshold.HasValue)
        {
            processed = ApplyBackgroundSuppression(resized, lightThreshold, darkThreshold);
        }

        var sourceImage = processed ?? resized;

        try
        {
            return RenderFromImage(sourceImage, width, height, cellWidth, cellHeight, delayMs, shouldInvert);
        }
        finally
        {
            processed?.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (ShapeVector vector, Rgb24 color) SampleCell(Image<Rgba32> image,
                                                          int cellX, int cellY,
                                                          int cellWidth, int cellHeight)
    {
        Span<float> values = stackalloc float[6];
        float radius = SamplingRadius * MathF.Min(cellWidth, cellHeight);

        int totalR = 0, totalG = 0, totalB = 0, colorSamples = 0;

        for (int i = 0; i < 6; i++)
        {
            float centerX = cellX + InternalSamplingPositions[i].X * cellWidth;
            float centerY = cellY + InternalSamplingPositions[i].Y * cellHeight;

            var (coverage, r, g, b, samples) = SampleCircleWithColor(image, centerX, centerY, radius);
            values[i] = coverage;
            totalR += r;
            totalG += g;
            totalB += b;
            colorSamples += samples;
        }

        var avgColor = colorSamples > 0
            ? new Rgb24((byte)(totalR / colorSamples), (byte)(totalG / colorSamples),
                       (byte)(totalB / colorSamples))
            : new Rgb24(128, 128, 128);

        return (new ShapeVector(values), avgColor);
    }

    private static Rgb24 SampleCellColor(Image<Rgba32> image, int cellX, int cellY,
                                          int cellWidth, int cellHeight)
    {
        int totalR = 0, totalG = 0, totalB = 0, samples = 0;

        // Sample a few points for average color
        int stepX = Math.Max(1, cellWidth / 3);
        int stepY = Math.Max(1, cellHeight / 3);

        for (int dy = stepY / 2; dy < cellHeight; dy += stepY)
        {
            for (int dx = stepX / 2; dx < cellWidth; dx += stepX)
            {
                int x = cellX + dx;
                int y = cellY + dy;

                if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                {
                    var pixel = image[x, y];
                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                    samples++;
                }
            }
        }

        return samples > 0
            ? new Rgb24((byte)(totalR / samples), (byte)(totalG / samples), (byte)(totalB / samples))
            : new Rgb24(128, 128, 128);
    }

    /// <summary>
    /// Sample a circle and return coverage (darkness) value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SampleCircleLightness(Image<Rgba32> image, float centerX, float centerY, float radius)
    {
        float total = 0;
        int samples = 0;

        // Concentric ring sampling
        // Center
        if (TrySampleLightness(image, centerX, centerY, out float val))
        {
            total += val;
            samples++;
        }

        // Inner ring (6 points) - using pre-computed angles
        float innerR = radius * 0.5f;
        for (int i = 0; i < 6; i++)
        {
            var (cos, sin) = InnerRingAngles[i];
            if (TrySampleLightness(image, centerX + cos * innerR, centerY + sin * innerR, out val))
            {
                total += val;
                samples++;
            }
        }

        // Outer ring (12 points) - using pre-computed angles
        for (int i = 0; i < 12; i++)
        {
            var (cos, sin) = OuterRing12Angles[i];
            if (TrySampleLightness(image, centerX + cos * radius, centerY + sin * radius, out val))
            {
                total += val;
                samples++;
            }
        }

        return samples > 0 ? total / samples : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TrySampleLightness(Image<Rgba32> image, float x, float y, out float lightness)
    {
        int ix = (int)x;
        int iy = (int)y;

        if (ix >= 0 && ix < image.Width && iy >= 0 && iy < image.Height)
        {
            var pixel = image[ix, iy];
            // Lightness: 0 = black, 1 = white
            lightness = (pixel.R * 0.299f + pixel.G * 0.587f + pixel.B * 0.114f) / 255f;
            return true;
        }

        lightness = 1f; // Assume white outside bounds
        return false;
    }

    private static (float coverage, int r, int g, int b, int samples) SampleCircleWithColor(
        Image<Rgba32> image, float centerX, float centerY, float radius)
    {
        float totalCoverage = 0;
        int totalR = 0, totalG = 0, totalB = 0;
        int samples = 0;
        int imgWidth = image.Width;
        int imgHeight = image.Height;

        // Inline sampling for performance (avoids delegate overhead)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddSample(float x, float y)
        {
            int ix = (int)x;
            int iy = (int)y;

            if ((uint)ix < (uint)imgWidth && (uint)iy < (uint)imgHeight)
            {
                var pixel = image[ix, iy];
                float lightness = (pixel.R * 0.299f + pixel.G * 0.587f + pixel.B * 0.114f) * (1f / 255f);
                totalCoverage += 1f - lightness; // Dark = high coverage
                totalR += pixel.R;
                totalG += pixel.G;
                totalB += pixel.B;
                samples++;
            }
        }

        // Center
        AddSample(centerX, centerY);

        // Inner ring (6 points) - using pre-computed angles
        float innerR = radius * 0.4f;
        for (int i = 0; i < 6; i++)
        {
            var (cos, sin) = InnerRingAngles[i];
            AddSample(centerX + cos * innerR, centerY + sin * innerR);
        }

        // Middle ring (12 points) - using pre-computed angles
        float midR = radius * 0.7f;
        for (int i = 0; i < 12; i++)
        {
            var (cos, sin) = MiddleRingAngles[i];
            AddSample(centerX + cos * midR, centerY + sin * midR);
        }

        // Outer ring (18 points) - using pre-computed angles
        for (int i = 0; i < 18; i++)
        {
            var (cos, sin) = OuterRingAngles[i];
            AddSample(centerX + cos * radius, centerY + sin * radius);
        }

        float avgCoverage = samples > 0 ? totalCoverage / samples : 0;
        return (avgCoverage, totalR, totalG, totalB, samples);
    }

    private char InvertCharacter(char c)
    {
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
