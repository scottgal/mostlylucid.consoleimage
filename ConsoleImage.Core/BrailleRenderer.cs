// Braille character renderer - 2x4 dots per cell for high resolution output
// Each braille character represents an 8-dot grid (2 wide x 4 tall)

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Core;

/// <summary>
/// Renders images using Unicode braille characters for maximum resolution.
/// Each character cell displays a 2x4 pixel grid (8 dots), giving 2x horizontal
/// and 4x vertical resolution compared to regular ASCII.
/// </summary>
public class BrailleRenderer : IDisposable
{
    private readonly RenderOptions _options;
    private bool _disposed;

    // Braille character base (U+2800 = empty braille)
    private const char BrailleBase = '\u2800';

    // Dot bit positions in braille character
    // Pattern:  1 4
    //           2 5
    //           3 6
    //           7 8
    // Index = dy * 2 + dx, so: [0,0]=0, [0,1]=1, [1,0]=2, [1,1]=3, [2,0]=4, [2,1]=5, [3,0]=6, [3,1]=7
    private static readonly int[] DotBits = { 0x01, 0x08, 0x02, 0x10, 0x04, 0x20, 0x40, 0x80 };

    public BrailleRenderer(RenderOptions? options = null)
    {
        _options = options ?? new RenderOptions();
    }

    /// <summary>
    /// Render an image file to braille string
    /// </summary>
    public string RenderFile(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image to braille string
    /// </summary>
    public string RenderImage(Image<Rgba32> image)
    {
        // Calculate output dimensions (each char = 2x4 braille dots)
        // Use CharacterAspectRatio to ensure correct visual aspect ratio
        var (pixelWidth, pixelHeight) = CalculateBrailleDimensions(image.Width, image.Height);
        int charWidth = pixelWidth / 2;
        int charHeight = pixelHeight / 4;

        // Resize image
        var resized = image.Clone(ctx => ctx.Resize(pixelWidth, pixelHeight));

        // Calculate threshold using autocontrast approach (based on img2braille technique)
        // This adjusts to the image's actual brightness range for better results
        var (minBrightness, maxBrightness) = CalculateBrightnessRange(resized);
        float brightnessRange = maxBrightness - minBrightness;

        // Use lower threshold to show more dots - dark areas should still show colored dots
        // This approach is inspired by img2braille's autocontrast feature
        float threshold = minBrightness + (brightnessRange * 0.20f);

        // Note: Braille has 8 dots per character cell (2x4), giving high enough resolution
        // that dithering is not needed - it actually introduces stippling noise.
        // We use direct brightness comparison for cleaner output.

        var sb = new System.Text.StringBuilder();
        Rgba32? lastColor = null;

        for (int cy = 0; cy < charHeight; cy++)
        {
            for (int cx = 0; cx < charWidth; cx++)
            {
                int px = cx * 2;
                int py = cy * 4;

                int brailleCode = 0;
                float totalR = 0, totalG = 0, totalB = 0;
                int colorCount = 0;

                // Sample each of the 8 dots in the 2x4 grid
                for (int dy = 0; dy < 4; dy++)
                {
                    for (int dx = 0; dx < 2; dx++)
                    {
                        int imgX = px + dx;
                        int imgY = py + dy;

                        if (imgX < resized.Width && imgY < resized.Height)
                        {
                            var pixel = resized[imgX, imgY];
                            float rawBrightness = GetBrightness(pixel);

                            // For dark terminals (default): bright pixels = dots (white/colored on black)
                            // For light terminals: dark pixels = dots (dark on white)
                            //
                            // Use direct brightness comparison - braille's high resolution (8 dots/cell)
                            // means we don't need dithering and it actually causes stippling artifacts
                            bool isDot;
                            if (_options.Invert)
                            {
                                // Dark terminal mode: dots represent brightness
                                isDot = rawBrightness > threshold;
                            }
                            else
                            {
                                // Light terminal mode: dots represent darkness
                                isDot = rawBrightness < (1f - threshold);
                            }

                            if (isDot)
                            {
                                int dotIndex = dy * 2 + dx;
                                brailleCode |= DotBits[dotIndex];
                            }

                            // Accumulate color for the cell (use original pixel, not dithered brightness)
                            totalR += pixel.R;
                            totalG += pixel.G;
                            totalB += pixel.B;
                            colorCount++;
                        }
                    }
                }

                char brailleChar = (char)(BrailleBase + brailleCode);

                if (_options.UseColor && colorCount > 0)
                {
                    var avgColor = new Rgba32(
                        (byte)(totalR / colorCount),
                        (byte)(totalG / colorCount),
                        (byte)(totalB / colorCount),
                        255
                    );

                    // No black cutoff - render all colors directly
                    // This preserves dark details that would otherwise be lost
                    if (lastColor == null || !ColorsEqual(lastColor.Value, avgColor))
                    {
                        sb.Append($"\x1b[38;2;{avgColor.R};{avgColor.G};{avgColor.B}m");
                        lastColor = avgColor;
                    }
                    sb.Append(brailleChar);
                }
                else
                {
                    sb.Append(brailleChar);
                }
            }

            if (cy < charHeight - 1)
            {
                if (_options.UseColor)
                    sb.Append("\x1b[0m");
                sb.AppendLine();
                lastColor = null;
            }
        }

        if (_options.UseColor)
            sb.Append("\x1b[0m");

        resized.Dispose();
        return sb.ToString();
    }

    /// <summary>
    /// Calculate adaptive threshold using Otsu's method for optimal binarization
    /// </summary>
    private float CalculateAdaptiveThreshold(Image<Rgba32> image)
    {
        // Build histogram
        int[] histogram = new int[256];
        int totalPixels = image.Width * image.Height;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int brightness = (int)(GetBrightness(image[x, y]) * 255);
                histogram[Math.Clamp(brightness, 0, 255)]++;
            }
        }

        // Otsu's method: find threshold that minimizes intra-class variance
        float sum = 0;
        for (int i = 0; i < 256; i++)
            sum += i * histogram[i];

        float sumB = 0;
        int wB = 0;
        int wF;

        float maxVariance = 0;
        int bestThreshold = 128;

        for (int t = 0; t < 256; t++)
        {
            wB += histogram[t];
            if (wB == 0) continue;

            wF = totalPixels - wB;
            if (wF == 0) break;

            sumB += t * histogram[t];

            float mB = sumB / wB;
            float mF = (sum - sumB) / wF;

            float variance = (float)wB * wF * (mB - mF) * (mB - mF);

            if (variance > maxVariance)
            {
                maxVariance = variance;
                bestThreshold = t;
            }
        }

        float threshold = bestThreshold / 255f;

        // Clamp threshold to preserve detail in both dark and bright areas
        // Min 0.15 ensures very dark images still show dots
        // Max 0.65 ensures very bright images don't lose all detail
        return Math.Clamp(threshold, 0.15f, 0.65f);
    }

    /// <summary>
    /// Calculate the actual brightness range of an image for autocontrast.
    /// Inspired by img2braille's autocontrast approach.
    /// </summary>
    private (float min, float max) CalculateBrightnessRange(Image<Rgba32> image)
    {
        float min = 1f;
        float max = 0f;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                float brightness = GetBrightness(image[x, y]);
                if (brightness < min) min = brightness;
                if (brightness > max) max = brightness;
            }
        }

        // Ensure valid range
        if (min >= max)
        {
            min = 0f;
            max = 1f;
        }

        return (min, max);
    }

    /// <summary>
    /// Apply Floyd-Steinberg error diffusion dithering
    /// </summary>
    private void ApplyFloydSteinbergDithering(float[,] buffer, int width, int height, float threshold)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float oldValue = buffer[y, x];
                float newValue = oldValue < threshold ? 0f : 1f;
                float error = oldValue - newValue;
                buffer[y, x] = newValue;

                // Distribute error to neighbors
                if (x + 1 < width)
                    buffer[y, x + 1] += error * 7f / 16f;
                if (y + 1 < height)
                {
                    if (x > 0)
                        buffer[y + 1, x - 1] += error * 3f / 16f;
                    buffer[y + 1, x] += error * 5f / 16f;
                    if (x + 1 < width)
                        buffer[y + 1, x + 1] += error * 1f / 16f;
                }
            }
        }
    }

    /// <summary>
    /// Apply selective Floyd-Steinberg dithering - only in mid-tone regions.
    /// Very dark and very bright areas are left undithered for clean boundaries.
    /// </summary>
    private void ApplySelectiveDithering(float[,] buffer, int width, int height, float threshold)
    {
        const float darkCutoff = 0.03f;  // Below this = true black only, no dithering
        const float brightCutoff = 0.75f; // Above this = true bright, no dithering

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float oldValue = buffer[y, x];

                // Skip dithering for very dark or very bright pixels
                // This prevents noise in uniform regions
                if (oldValue < darkCutoff || oldValue > brightCutoff)
                {
                    continue; // Leave original value untouched
                }

                // Apply dithering to mid-tones
                float newValue = oldValue < threshold ? 0f : 1f;
                float error = oldValue - newValue;
                buffer[y, x] = newValue;

                // Distribute error only to mid-tone neighbors
                if (x + 1 < width && buffer[y, x + 1] >= darkCutoff && buffer[y, x + 1] <= brightCutoff)
                    buffer[y, x + 1] += error * 7f / 16f;
                if (y + 1 < height)
                {
                    if (x > 0 && buffer[y + 1, x - 1] >= darkCutoff && buffer[y + 1, x - 1] <= brightCutoff)
                        buffer[y + 1, x - 1] += error * 3f / 16f;
                    if (buffer[y + 1, x] >= darkCutoff && buffer[y + 1, x] <= brightCutoff)
                        buffer[y + 1, x] += error * 5f / 16f;
                    if (x + 1 < width && buffer[y + 1, x + 1] >= darkCutoff && buffer[y + 1, x + 1] <= brightCutoff)
                        buffer[y + 1, x + 1] += error * 1f / 16f;
                }
            }
        }
    }

    /// <summary>
    /// Render a file to a BrailleFrame.
    /// </summary>
    public BrailleFrame RenderFileToFrame(string filePath)
    {
        return new BrailleFrame(RenderFile(filePath), 0);
    }

    /// <summary>
    /// Render an image to a BrailleFrame.
    /// </summary>
    public BrailleFrame RenderImageToFrame(Image<Rgba32> image)
    {
        return new BrailleFrame(RenderImage(image), 0);
    }

    /// <summary>
    /// Render a GIF file to a list of braille frames
    /// </summary>
    public List<BrailleFrame> RenderGif(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
        return RenderGifFramesInternal(image);
    }

    /// <summary>
    /// Render a GIF file to a list of braille frames (for GIF output).
    /// </summary>
    public List<BrailleFrame> RenderGifFrames(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
        return RenderGifFramesInternal(image);
    }

    private List<BrailleFrame> RenderGifFramesInternal(Image<Rgba32> image)
    {
        var frames = new List<BrailleFrame>();

        for (int i = 0; i < image.Frames.Count; i++)
        {
            if (_options.FrameSampleRate > 1 && i % _options.FrameSampleRate != 0)
                continue;

            using var frameImage = image.Frames.CloneFrame(i);
            string content = RenderImage(frameImage);

            var metadata = image.Frames[i].Metadata.GetGifMetadata();
            int delayMs = metadata.FrameDelay * 10; // GIF delay is in centiseconds
            if (delayMs == 0) delayMs = 100;
            delayMs = (int)(delayMs / _options.AnimationSpeedMultiplier);

            frames.Add(new BrailleFrame(content, delayMs));
        }

        return frames;
    }

    private static float GetBrightness(Rgba32 pixel)
    {
        // Perceived brightness formula
        return (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f;
    }

    private static float GetSaturation(Rgba32 pixel)
    {
        // Calculate HSL saturation (0-1)
        float r = pixel.R / 255f;
        float g = pixel.G / 255f;
        float b = pixel.B / 255f;

        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;

        if (delta < 0.001f) return 0f; // Grayscale

        float lightness = (max + min) / 2f;
        return delta / (1f - MathF.Abs(2f * lightness - 1f));
    }

    /// <summary>
    /// Apply contrast enhancement using power curve.
    /// </summary>
    private static float ApplyContrast(float brightness, float power)
    {
        // S-curve contrast enhancement centered at 0.5
        // Higher power = more contrast
        if (brightness <= 0.5f)
        {
            return 0.5f * MathF.Pow(2f * brightness, power);
        }
        else
        {
            return 1f - 0.5f * MathF.Pow(2f * (1f - brightness), power);
        }
    }

    private static bool ColorsEqual(Rgba32 a, Rgba32 b)
    {
        return a.R == b.R && a.G == b.G && a.B == b.B;
    }

    /// <summary>
    /// Calculate braille pixel dimensions accounting for CharacterAspectRatio.
    /// Uses the shared CalculateVisualDimensions method from RenderOptions.
    /// </summary>
    private (int width, int height) CalculateBrailleDimensions(int imageWidth, int imageHeight)
    {
        // Braille: 2 pixels per char width, 4 pixels per char height
        var (width, height) = _options.CalculateVisualDimensions(imageWidth, imageHeight, 2, 4);

        // Ensure dimensions are multiples of 2x4 for braille
        width = (width / 2) * 2;
        height = (height / 4) * 4;

        return (Math.Max(2, width), Math.Max(4, height));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A single frame of braille-rendered content
/// </summary>
public class BrailleFrame : IAnimationFrame
{
    public string Content { get; }
    public int DelayMs { get; }

    public BrailleFrame(string content, int delayMs)
    {
        Content = content;
        DelayMs = delayMs;
    }
}
