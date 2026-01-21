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
        var (pixelWidth, pixelHeight) = CalculateBrailleDimensions(image.Width, image.Height);
        int charWidth = pixelWidth / 2;
        int charHeight = pixelHeight / 4;

        // Resize image
        var resized = image.Clone(ctx => ctx.Resize(pixelWidth, pixelHeight));

        // Pre-compute brightness values and colors for all pixels
        var (brightness, colors) = PrecomputePixelData(resized);

        // Calculate threshold using autocontrast
        // Use 0.5 (midpoint) for balanced dot distribution instead of 0.20 which creates too many solid blocks
        var (minBrightness, maxBrightness) = GetBrightnessRangeFromBuffer(brightness);
        float threshold = minBrightness + ((maxBrightness - minBrightness) * 0.5f);
        bool invertMode = _options.Invert;

        // Apply Floyd-Steinberg dithering if enabled for better gradients
        if (_options.EnableDithering)
        {
            brightness = ApplyFloydSteinbergDithering(brightness, pixelWidth, pixelHeight, threshold);
            // After dithering, values are 0 or 1, so use 0.5 threshold
            threshold = 0.5f;
        }

        // Pre-size StringBuilder: each char cell needs ~20 bytes for ANSI codes + 1 char
        // Plus newlines and resets
        int estimatedSize = charWidth * charHeight * (_options.UseColor ? 25 : 1) + charHeight * 10;
        var sb = new System.Text.StringBuilder(estimatedSize);

        if (_options.UseColor && _options.UseParallelProcessing && charHeight > 4)
        {
            // Parallel processing: compute each row independently, then combine
            var rowStrings = new string[charHeight];
            var rowColors = new Rgba32?[charHeight]; // Track last color for each row

            System.Threading.Tasks.Parallel.For(0, charHeight, cy =>
            {
                var rowSb = new System.Text.StringBuilder(charWidth * 25);
                Rgba32? lastColor = null;

                for (int cx = 0; cx < charWidth; cx++)
                {
                    int px = cx * 2;
                    int py = cy * 4;

                    int brailleCode = 0;
                    int totalR = 0, totalG = 0, totalB = 0;
                    int colorCount = 0;

                    // Sample 8 dots
                    for (int dy = 0; dy < 4; dy++)
                    {
                        int imgY = py + dy;
                        if (imgY >= pixelHeight) continue;
                        int rowOffset = imgY * pixelWidth;

                        for (int dx = 0; dx < 2; dx++)
                        {
                            int imgX = px + dx;
                            if (imgX >= pixelWidth) continue;

                            int idx = rowOffset + imgX;
                            // In invert mode (dark terminal): show dots where brightness is HIGH
                            // In non-invert mode (light terminal): show dots where brightness is LOW
                            bool isDot = invertMode
                                ? brightness[idx] > threshold
                                : brightness[idx] < threshold;

                            if (isDot)
                            {
                                brailleCode |= DotBits[dy * 2 + dx];
                                // Only average colors from visible dots to avoid solarization
                                var c = colors[idx];
                                totalR += c.R;
                                totalG += c.G;
                                totalB += c.B;
                                colorCount++;
                            }
                        }
                    }

                    char brailleChar = (char)(BrailleBase + brailleCode);

                    if (colorCount > 0)
                    {
                        byte r = (byte)(totalR / colorCount);
                        byte g = (byte)(totalG / colorCount);
                        byte b = (byte)(totalB / colorCount);

                        // Apply gamma correction to brighten colors
                        if (_options.Gamma != 1.0f)
                        {
                            r = (byte)Math.Clamp(MathF.Pow(r / 255f, _options.Gamma) * 255f, 0, 255);
                            g = (byte)Math.Clamp(MathF.Pow(g / 255f, _options.Gamma) * 255f, 0, 255);
                            b = (byte)Math.Clamp(MathF.Pow(b / 255f, _options.Gamma) * 255f, 0, 255);
                        }

                        var avgColor = new Rgba32(r, g, b, 255);

                        if (lastColor == null || !AnsiCodes.ColorsEqual(lastColor.Value, avgColor))
                        {
                            rowSb.Append("\x1b[38;2;");
                            rowSb.Append(avgColor.R);
                            rowSb.Append(';');
                            rowSb.Append(avgColor.G);
                            rowSb.Append(';');
                            rowSb.Append(avgColor.B);
                            rowSb.Append('m');
                            lastColor = avgColor;
                        }
                    }
                    rowSb.Append(brailleChar);
                }

                rowStrings[cy] = rowSb.ToString();
            });

            // Combine rows
            for (int cy = 0; cy < charHeight; cy++)
            {
                sb.Append(rowStrings[cy]);
                if (cy < charHeight - 1)
                {
                    sb.Append("\x1b[0m");
                    sb.AppendLine();
                }
            }
            sb.Append("\x1b[0m");
        }
        else
        {
            // Sequential processing (non-color or small images)
            Rgba32? lastColor = null;

            for (int cy = 0; cy < charHeight; cy++)
            {
                for (int cx = 0; cx < charWidth; cx++)
                {
                    int px = cx * 2;
                    int py = cy * 4;

                    int brailleCode = 0;
                    int totalR = 0, totalG = 0, totalB = 0;
                    int colorCount = 0;

                    for (int dy = 0; dy < 4; dy++)
                    {
                        int imgY = py + dy;
                        if (imgY >= pixelHeight) continue;
                        int rowOffset = imgY * pixelWidth;

                        for (int dx = 0; dx < 2; dx++)
                        {
                            int imgX = px + dx;
                            if (imgX >= pixelWidth) continue;

                            int idx = rowOffset + imgX;
                            // In invert mode (dark terminal): show dots where brightness is HIGH
                            // In non-invert mode (light terminal): show dots where brightness is LOW
                            bool isDot = invertMode
                                ? brightness[idx] > threshold
                                : brightness[idx] < threshold;

                            if (isDot)
                            {
                                brailleCode |= DotBits[dy * 2 + dx];
                                // Only average colors from visible dots to avoid solarization
                                var c = colors[idx];
                                totalR += c.R;
                                totalG += c.G;
                                totalB += c.B;
                                colorCount++;
                            }
                        }
                    }

                    char brailleChar = (char)(BrailleBase + brailleCode);

                    if (_options.UseColor && colorCount > 0)
                    {
                        byte r = (byte)(totalR / colorCount);
                        byte g = (byte)(totalG / colorCount);
                        byte b = (byte)(totalB / colorCount);

                        // Apply gamma correction to brighten colors
                        if (_options.Gamma != 1.0f)
                        {
                            r = (byte)Math.Clamp(MathF.Pow(r / 255f, _options.Gamma) * 255f, 0, 255);
                            g = (byte)Math.Clamp(MathF.Pow(g / 255f, _options.Gamma) * 255f, 0, 255);
                            b = (byte)Math.Clamp(MathF.Pow(b / 255f, _options.Gamma) * 255f, 0, 255);
                        }

                        var avgColor = new Rgba32(r, g, b, 255);

                        if (lastColor == null || !AnsiCodes.ColorsEqual(lastColor.Value, avgColor))
                        {
                            sb.Append("\x1b[38;2;");
                            sb.Append(avgColor.R);
                            sb.Append(';');
                            sb.Append(avgColor.G);
                            sb.Append(';');
                            sb.Append(avgColor.B);
                            sb.Append('m');
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
        }

        resized.Dispose();
        return sb.ToString();
    }

    /// <summary>
    /// Pre-compute brightness and color data for all pixels.
    /// This is faster than individual pixel access during rendering.
    /// </summary>
    private (float[] brightness, Rgba32[] colors) PrecomputePixelData(Image<Rgba32> image)
    {
        int width = image.Width;
        int height = image.Height;
        int totalPixels = width * height;
        var brightness = new float[totalPixels];
        var colors = new Rgba32[totalPixels];

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                int rowOffset = y * width;
                for (int x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    brightness[rowOffset + x] = BrightnessHelper.GetBrightness(pixel);
                    colors[rowOffset + x] = pixel;
                }
            }
        });

        return (brightness, colors);
    }

    /// <summary>
    /// Get min/max brightness from pre-computed buffer.
    /// </summary>
    private (float min, float max) GetBrightnessRangeFromBuffer(float[] brightness)
    {
        float min = 1f;
        float max = 0f;
        for (int i = 0; i < brightness.Length; i++)
        {
            if (brightness[i] < min) min = brightness[i];
            if (brightness[i] > max) max = brightness[i];
        }
        return min >= max ? (0f, 1f) : (min, max);
    }

    /// <summary>
    /// Apply Floyd-Steinberg dithering to brightness values for smoother gradients.
    /// Returns a new array with dithered values (0 or 1).
    /// </summary>
    private float[] ApplyFloydSteinbergDithering(float[] brightness, int width, int height, float threshold)
    {
        // Normalize brightness values to 0-1 range based on min/max
        var (min, max) = GetBrightnessRangeFromBuffer(brightness);
        float range = max - min;
        if (range < 0.01f) range = 1f; // Avoid division issues

        // Work with a copy to avoid modifying original
        var result = new float[brightness.Length];
        for (int i = 0; i < brightness.Length; i++)
        {
            result[i] = (brightness[i] - min) / range;
        }

        // Floyd-Steinberg error diffusion
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float oldVal = result[idx];
                float newVal = oldVal > 0.5f ? 1f : 0f;
                result[idx] = newVal;
                float error = oldVal - newVal;

                // Distribute error to neighboring pixels
                // Right:       7/16
                // Below-left:  3/16
                // Below:       5/16
                // Below-right: 1/16
                if (x + 1 < width)
                    result[idx + 1] += error * (7f / 16f);
                if (y + 1 < height)
                {
                    if (x > 0)
                        result[(y + 1) * width + (x - 1)] += error * (3f / 16f);
                    result[(y + 1) * width + x] += error * (5f / 16f);
                    if (x + 1 < width)
                        result[(y + 1) * width + (x + 1)] += error * (1f / 16f);
                }
            }
        }

        return result;
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
                float brightness = BrightnessHelper.GetBrightness(image[x, y]);
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
