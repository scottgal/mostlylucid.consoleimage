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
        // Calculate output dimensions (each char = 2x4 pixels)
        // Braille dots are effectively square because the 2x4 grid within a ~2:1 terminal character
        // compensates for terminal aspect ratio. Don't apply CharacterAspectRatio again.
        int charWidth = _options.Width ?? Math.Min(image.Width / 2, _options.MaxWidth);
        int charHeight = _options.Height ?? (int)((float)charWidth * image.Height / image.Width / 2);
        charHeight = Math.Min(charHeight, _options.MaxHeight);

        // Pixel dimensions (2x width, 4x height for braille)
        int pixelWidth = charWidth * 2;
        int pixelHeight = charHeight * 4;

        // Resize image
        var resized = image.Clone(ctx => ctx.Resize(pixelWidth, pixelHeight));

        // Calculate adaptive threshold using Otsu's method or use fixed threshold
        float threshold = CalculateAdaptiveThreshold(resized);

        // Create brightness buffer for dithering
        float[,] brightnessBuffer = new float[pixelHeight, pixelWidth];
        for (int y = 0; y < pixelHeight; y++)
        {
            for (int x = 0; x < pixelWidth; x++)
            {
                brightnessBuffer[y, x] = GetBrightness(resized[x, y]);
            }
        }

        // Apply Floyd-Steinberg dithering if enabled
        if (_options.EnableDithering)
        {
            ApplyFloydSteinbergDithering(brightnessBuffer, pixelWidth, pixelHeight, threshold);
        }

        var sb = new System.Text.StringBuilder();
        Rgba32? lastColor = null;
        bool lastWasSkipped = false;

        // Get brightness thresholds based on terminal mode
        float? darkThreshold = _options.Invert ? _options.DarkTerminalBrightnessThreshold : null;
        float? lightThreshold = !_options.Invert ? _options.LightTerminalBrightnessThreshold : null;

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
                            float brightness = brightnessBuffer[imgY, imgX];

                            // For dark terminals (default): dark pixels = dots (ink)
                            // For light terminals (Invert=false): bright pixels = dots
                            // This matches intuitive expectations
                            bool isDot = _options.Invert
                                ? brightness < threshold  // Dark terminal: dark pixels show as dots
                                : brightness > threshold; // Light terminal: bright pixels show as dots

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

                    float avgBrightness = GetBrightness(avgColor);

                    // Check if this cell should be skipped (blend with terminal background)
                    bool skipColor = (darkThreshold.HasValue && avgBrightness < darkThreshold.Value) ||
                                     (lightThreshold.HasValue && avgBrightness > lightThreshold.Value);

                    if (skipColor)
                    {
                        // Output empty braille or space without color code
                        if (!lastWasSkipped && lastColor != null)
                        {
                            sb.Append("\x1b[0m");
                        }
                        sb.Append(brailleCode == 0 ? ' ' : brailleChar);
                        lastWasSkipped = true;
                        lastColor = null;
                    }
                    else
                    {
                        if (lastColor == null || !ColorsEqual(lastColor.Value, avgColor))
                        {
                            sb.Append($"\x1b[38;2;{avgColor.R};{avgColor.G};{avgColor.B}m");
                            lastColor = avgColor;
                        }
                        sb.Append(brailleChar);
                        lastWasSkipped = false;
                    }
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
                lastWasSkipped = false;
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

        return bestThreshold / 255f;
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
    /// Render a GIF file to a list of braille frames
    /// </summary>
    public List<BrailleFrame> RenderGif(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
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

    private static bool ColorsEqual(Rgba32 a, Rgba32 b)
    {
        return a.R == b.R && a.G == b.G && a.B == b.B;
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
