// Braille character renderer - 2x4 dots per cell for high resolution output
// Each braille character represents an 8-dot grid (2 wide x 4 tall)

using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Core;

/// <summary>
///     Renders images using Unicode braille characters for maximum resolution.
///     Each character cell displays a 2x4 pixel grid (8 dots), giving 2x horizontal
///     and 4x vertical resolution compared to regular ASCII.
/// </summary>
public class BrailleRenderer : IDisposable
{
    // Braille character base (U+2800 = empty braille)
    private const char BrailleBase = '\u2800';

    // Full braille block (all 8 dots on) - ⣿
    private const char BrailleFull = '\u28FF';

    // Dot bit positions in braille character
    // Pattern:  1 4
    //           2 5
    //           3 6
    //           7 8
    // Index = dy * 2 + dx, so: [0,0]=0, [0,1]=1, [1,0]=2, [1,1]=3, [2,0]=4, [2,1]=5, [3,0]=6, [3,1]=7
    private static readonly int[] DotBits = { 0x01, 0x08, 0x02, 0x10, 0x04, 0x20, 0x40, 0x80 };
    private readonly RenderOptions _options;
    private bool _disposed;

    public BrailleRenderer(RenderOptions? options = null)
    {
        _options = options ?? new RenderOptions();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Render an image file to braille string
    /// </summary>
    public string RenderFile(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
        return RenderImage(image);
    }

    /// <summary>
    ///     Render an image to cell data array for delta rendering.
    ///     This is more efficient for video playback as it enables
    ///     only updating changed cells.
    /// </summary>
    public CellData[,] RenderToCells(Image<Rgba32> image)
    {
        var (pixelWidth, pixelHeight) = CalculateBrailleDimensions(image.Width, image.Height);
        var charWidth = pixelWidth / 2;
        var charHeight = pixelHeight / 4;

        using var resized = image.Clone(ctx => ctx.Resize(pixelWidth, pixelHeight));
        var (brightness, colors) = PrecomputePixelData(resized);
        var (minBrightness, maxBrightness) = GetBrightnessRangeFromBuffer(brightness);

        var cells = new CellData[charHeight, charWidth];

        // Parallel render to cell array
        Parallel.For(0, charHeight, cy =>
        {
            Span<float> cellBrightness = stackalloc float[8];
            Span<int> cellIndices = stackalloc int[8];

            for (var cx = 0; cx < charWidth; cx++)
            {
                var px = cx * 2;
                var py = cy * 4;

                int totalR = 0, totalG = 0, totalB = 0;
                var colorCount = 0;

                for (var dy = 0; dy < 4; dy++)
                {
                    var imgY = py + dy;
                    if (imgY >= pixelHeight) continue;
                    var rowOffset = imgY * pixelWidth;

                    for (var dx = 0; dx < 2; dx++)
                    {
                        var imgX = px + dx;
                        if (imgX >= pixelWidth) continue;

                        var idx = rowOffset + imgX;
                        var c = colors[idx];
                        totalR += c.R;
                        totalG += c.G;
                        totalB += c.B;

                        cellBrightness[colorCount] = brightness[idx];
                        cellIndices[colorCount] = dy * 2 + dx;
                        colorCount++;
                    }
                }

                // Get braille character (always full block in color mode)
                var brailleCode = _options.UseColor
                    ? CalculateHybridBrailleCode(cellBrightness, cellIndices, colorCount,
                        minBrightness, maxBrightness, _options.Invert)
                    : 0xFF;
                var brailleChar = (char)(BrailleBase + brailleCode);

                // Calculate average color
                byte r = 0, g = 0, b = 0;
                if (colorCount > 0)
                {
                    r = (byte)(totalR / colorCount);
                    g = (byte)(totalG / colorCount);
                    b = (byte)(totalB / colorCount);

                    if (_options.Gamma != 1.0f)
                    {
                        r = (byte)Math.Clamp(MathF.Pow(r / 255f, _options.Gamma) * 255f, 0, 255);
                        g = (byte)Math.Clamp(MathF.Pow(g / 255f, _options.Gamma) * 255f, 0, 255);
                        b = (byte)Math.Clamp(MathF.Pow(b / 255f, _options.Gamma) * 255f, 0, 255);
                    }
                }

                cells[cy, cx] = new CellData(brailleChar, r, g, b);
            }
        });

        return cells;
    }

    /// <summary>
    ///     Render an image to braille string
    /// </summary>
    public string RenderImage(Image<Rgba32> image)
    {
        // Calculate output dimensions (each char = 2x4 braille dots)
        var (pixelWidth, pixelHeight) = CalculateBrailleDimensions(image.Width, image.Height);
        var charWidth = pixelWidth / 2;
        var charHeight = pixelHeight / 4;

        // Resize image
        var resized = image.Clone(ctx => ctx.Resize(pixelWidth, pixelHeight));

        // Pre-compute brightness values and colors for all pixels
        var (brightness, colors) = PrecomputePixelData(resized);

        // Calculate threshold using autocontrast
        var (minBrightness, maxBrightness) = GetBrightnessRangeFromBuffer(brightness);
        var invertMode = _options.Invert;

        // For colored braille: use HYBRID mode
        // This gives density proportional to brightness for more pleasing output
        var useHybridMode = _options.UseColor;

        // Calculate threshold for dot pattern mode
        float threshold;
        if (_options.UseColor)
        {
            // Colored mode with dots: be VERY generous with dots (dense output)
            // Only suppress truly dark pixels in invert mode, or truly bright in non-invert
            threshold = invertMode
                ? minBrightness + (maxBrightness - minBrightness) * 0.15f  // Show 85% of brightness range as dots
                : minBrightness + (maxBrightness - minBrightness) * 0.85f; // Show 85% as dots
        }
        else
        {
            // Monochrome mode: use standard mid-point threshold
            var thresholdBias = invertMode ? 0.35f : 0.65f;
            threshold = minBrightness + (maxBrightness - minBrightness) * thresholdBias;
        }

        // Apply Floyd-Steinberg dithering only when explicitly enabled
        var useDithering = _options.EnableDithering;
        if (useDithering && !useHybridMode)
        {
            brightness = ApplyFloydSteinbergDithering(brightness, pixelWidth, pixelHeight, threshold);
            // After dithering, values are 0 or 1, so use 0.5 threshold
            threshold = 0.5f;
        }

        // Pre-size StringBuilder: each char cell needs ~20 bytes for ANSI codes + 1 char
        // Plus newlines and resets
        var estimatedSize = charWidth * charHeight * (_options.UseColor ? 25 : 1) + charHeight * 10;
        var sb = new StringBuilder(estimatedSize);

        // Key insight: separate color and brightness concerns
        // - COLOR: average ALL 8 pixels in cell (prevents solarization)
        // - DOTS: show brightness detail via threshold

        if (_options.UseColor && _options.UseParallelProcessing && charHeight > 4)
        {
            // Parallel processing: compute each row independently, then combine
            var rowStrings = new string[charHeight];

            Parallel.For(0, charHeight, cy =>
            {
                var rowSb = new StringBuilder(charWidth * 25);
                Rgba32? lastColor = null;

                // Pre-allocate cell buffers outside inner loop to avoid stack overflow
                Span<float> cellBrightness = stackalloc float[8];
                Span<int> cellIndices = stackalloc int[8];

                for (var cx = 0; cx < charWidth; cx++)
                {
                    var px = cx * 2;
                    var py = cy * 4;

                    // Collect colors and brightness for the cell
                    int totalR = 0, totalG = 0, totalB = 0;
                    var colorCount = 0;
                    var cellMinBright = 1f;
                    var cellMaxBright = 0f;

                    for (var dy = 0; dy < 4; dy++)
                    {
                        var imgY = py + dy;
                        if (imgY >= pixelHeight) continue;
                        var rowOffset = imgY * pixelWidth;

                        for (var dx = 0; dx < 2; dx++)
                        {
                            var imgX = px + dx;
                            if (imgX >= pixelWidth) continue;

                            var idx = rowOffset + imgX;
                            var c = colors[idx];
                            totalR += c.R;
                            totalG += c.G;
                            totalB += c.B;

                            var b = brightness[idx];
                            cellBrightness[colorCount] = b;
                            cellIndices[colorCount] = dy * 2 + dx;
                            if (b < cellMinBright) cellMinBright = b;
                            if (b > cellMaxBright) cellMaxBright = b;
                            colorCount++;
                        }
                    }

                    // Determine braille pattern
                    int brailleCode;

                    if (useHybridMode)
                    {
                        // Hybrid mode: uses local variance for edge detection (fast)
                        brailleCode = CalculateHybridBrailleCode(
                            cellBrightness, cellIndices, colorCount,
                            minBrightness, maxBrightness, invertMode);
                    }
                    else
                    {
                        // Standard mode: threshold-based dot pattern
                        brailleCode = 0;
                        for (var i = 0; i < colorCount; i++)
                        {
                            var isDot = invertMode
                                ? cellBrightness[i] > threshold
                                : cellBrightness[i] < threshold;

                            if (isDot)
                                brailleCode |= DotBits[cellIndices[i]];
                        }
                    }

                    var brailleChar = (char)(BrailleBase + brailleCode);

                    if (colorCount > 0)
                    {
                        var r = (byte)(totalR / colorCount);
                        var g = (byte)(totalG / colorCount);
                        var b = (byte)(totalB / colorCount);

                        // Apply gamma correction
                        if (_options.Gamma != 1.0f)
                        {
                            r = (byte)Math.Clamp(MathF.Pow(r / 255f, _options.Gamma) * 255f, 0, 255);
                            g = (byte)Math.Clamp(MathF.Pow(g / 255f, _options.Gamma) * 255f, 0, 255);
                            b = (byte)Math.Clamp(MathF.Pow(b / 255f, _options.Gamma) * 255f, 0, 255);
                        }

                        // Skip absolute black characters (invisible on dark terminal)
                        // This reduces file size and improves rendering
                        if (r <= 2 && g <= 2 && b <= 2 && brailleCode == 0)
                        {
                            rowSb.Append(' ');
                            continue;
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
            for (var cy = 0; cy < charHeight; cy++)
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

            // Pre-allocate cell buffers outside loops to avoid stack overflow
            Span<float> cellBrightness = stackalloc float[8];
            Span<int> cellIndices = stackalloc int[8];

            for (var cy = 0; cy < charHeight; cy++)
            {
                for (var cx = 0; cx < charWidth; cx++)
                {
                    var px = cx * 2;
                    var py = cy * 4;

                    // Collect colors and brightness for the cell
                    int totalR = 0, totalG = 0, totalB = 0;
                    var colorCount = 0;
                    var cellMinBright = 1f;
                    var cellMaxBright = 0f;

                    for (var dy = 0; dy < 4; dy++)
                    {
                        var imgY = py + dy;
                        if (imgY >= pixelHeight) continue;
                        var rowOffset = imgY * pixelWidth;

                        for (var dx = 0; dx < 2; dx++)
                        {
                            var imgX = px + dx;
                            if (imgX >= pixelWidth) continue;

                            var idx = rowOffset + imgX;
                            var c = colors[idx];
                            totalR += c.R;
                            totalG += c.G;
                            totalB += c.B;

                            var b = brightness[idx];
                            cellBrightness[colorCount] = b;
                            cellIndices[colorCount] = dy * 2 + dx;
                            if (b < cellMinBright) cellMinBright = b;
                            if (b > cellMaxBright) cellMaxBright = b;
                            colorCount++;
                        }
                    }

                    // Determine braille pattern
                    int brailleCode;

                    if (useHybridMode)
                    {
                        // Hybrid mode: uses local variance for edge detection (fast)
                        brailleCode = CalculateHybridBrailleCode(
                            cellBrightness, cellIndices, colorCount,
                            minBrightness, maxBrightness, invertMode);
                    }
                    else
                    {
                        // Standard mode: threshold-based dot pattern
                        brailleCode = 0;
                        for (var i = 0; i < colorCount; i++)
                        {
                            var isDot = invertMode
                                ? cellBrightness[i] > threshold
                                : cellBrightness[i] < threshold;

                            if (isDot)
                                brailleCode |= DotBits[cellIndices[i]];
                        }
                    }

                    var brailleChar = (char)(BrailleBase + brailleCode);

                    if (_options.UseColor && colorCount > 0)
                    {
                        var r = (byte)(totalR / colorCount);
                        var g = (byte)(totalG / colorCount);
                        var b = (byte)(totalB / colorCount);

                        // Apply gamma correction
                        if (_options.Gamma != 1.0f)
                        {
                            r = (byte)Math.Clamp(MathF.Pow(r / 255f, _options.Gamma) * 255f, 0, 255);
                            g = (byte)Math.Clamp(MathF.Pow(g / 255f, _options.Gamma) * 255f, 0, 255);
                            b = (byte)Math.Clamp(MathF.Pow(b / 255f, _options.Gamma) * 255f, 0, 255);
                        }

                        // Skip absolute black characters (invisible on dark terminal)
                        // This reduces file size and improves rendering
                        if (r <= 2 && g <= 2 && b <= 2 && brailleCode == 0)
                        {
                            sb.Append(' ');
                            continue;
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

    // Braille patterns for different edge directions
    // Each pattern shows the "visible" side of an edge
    private static readonly int[] EdgePatterns =
    {
        0xFF,  // No edge - full block
        0x1B,  // Horizontal edge, top half: ⠛
        0xE4,  // Horizontal edge, bottom half: ⣤
        0x49,  // Vertical edge, left half: ⡉
        0xB6,  // Vertical edge, right half: ⢶
        0x09,  // Diagonal /, top-left: ⠉
        0xC0,  // Diagonal /, bottom-right: ⣀
        0x06,  // Diagonal \, top-right: ⠆
        0x90   // Diagonal \, bottom-left: ⢐
    };

    /// <summary>
    ///     Get braille pattern for an edge based on angle.
    ///     Maps edge angle to appropriate half-block pattern.
    /// </summary>
    private static int GetEdgeBraillePattern(float angle, float brightness, bool invertMode)
    {
        // Determine which side of the edge to show based on brightness and mode
        // In dark terminal (invert=true): show bright side
        // In light terminal (invert=false): show dark side
        var showTopOrLeft = invertMode ? brightness > 0.5f : brightness < 0.5f;

        // Map angle to edge type
        // angle is perpendicular to gradient, so:
        // angle ~0 or ~PI = vertical edge (left/right split)
        // angle ~PI/2 or ~-PI/2 = horizontal edge (top/bottom split)
        // angle ~PI/4 = diagonal /
        // angle ~-PI/4 or ~3PI/4 = diagonal \

        var absAngle = MathF.Abs(angle);

        if (absAngle < MathF.PI / 8 || absAngle > 7 * MathF.PI / 8)
        {
            // Near 0 or PI - vertical edge
            return showTopOrLeft ? EdgePatterns[3] : EdgePatterns[4];
        }
        else if (absAngle > 3 * MathF.PI / 8 && absAngle < 5 * MathF.PI / 8)
        {
            // Near PI/2 - horizontal edge
            return showTopOrLeft ? EdgePatterns[1] : EdgePatterns[2];
        }
        else if (angle > 0)
        {
            // Positive angles - diagonal /
            return showTopOrLeft ? EdgePatterns[5] : EdgePatterns[6];
        }
        else
        {
            // Negative angles - diagonal \
            return showTopOrLeft ? EdgePatterns[7] : EdgePatterns[8];
        }
    }

    /// <summary>
    ///     Pre-compute brightness and color data for all pixels.
    ///     This is faster than individual pixel access during rendering.
    ///     Applies color quantization if ColorCount is set.
    /// </summary>
    private (float[] brightness, Rgba32[] colors) PrecomputePixelData(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var totalPixels = width * height;
        var brightness = new float[totalPixels];
        var colors = new Rgba32[totalPixels];

        // Color quantization settings
        var colorCount = _options.ColorCount;
        var quantStep = 1;
        var quantize = false;

        if (colorCount.HasValue && colorCount.Value > 0)
        {
            // Calculate step size for color reduction
            // e.g., 4 colors = 64 step (256/4), 16 colors = 16 step
            quantStep = Math.Max(1, 256 / colorCount.Value);
            quantize = true;
        }
        else if (_options.EnableTemporalStability)
        {
            // Fallback to temporal stability quantization
            quantStep = Math.Max(1, _options.ColorStabilityThreshold / 2);
            quantize = true;
        }

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var rowOffset = y * width;
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];

                    // Apply color quantization for palette reduction
                    if (quantize && quantStep > 1)
                    {
                        pixel = new Rgba32(
                            (byte)(pixel.R / quantStep * quantStep),
                            (byte)(pixel.G / quantStep * quantStep),
                            (byte)(pixel.B / quantStep * quantStep),
                            pixel.A);
                    }

                    brightness[rowOffset + x] = BrightnessHelper.GetBrightness(pixel);
                    colors[rowOffset + x] = pixel;
                }
            }
        });

        return (brightness, colors);
    }

    /// <summary>
    ///     Get min/max brightness from pre-computed buffer.
    /// </summary>
    private (float min, float max) GetBrightnessRangeFromBuffer(float[] brightness)
    {
        var min = 1f;
        var max = 0f;
        for (var i = 0; i < brightness.Length; i++)
        {
            if (brightness[i] < min) min = brightness[i];
            if (brightness[i] > max) max = brightness[i];
        }

        return min >= max ? (0f, 1f) : (min, max);
    }

    /// <summary>
    ///     Apply Floyd-Steinberg dithering to brightness values for smoother gradients.
    ///     Returns a new array with dithered values (0 or 1).
    /// </summary>
    private float[] ApplyFloydSteinbergDithering(float[] brightness, int width, int height, float threshold)
    {
        // Normalize brightness values to 0-1 range based on min/max
        var (min, max) = GetBrightnessRangeFromBuffer(brightness);
        var range = max - min;
        if (range < 0.01f) range = 1f; // Avoid division issues

        // Work with a copy to avoid modifying original
        var result = new float[brightness.Length];
        for (var i = 0; i < brightness.Length; i++) result[i] = (brightness[i] - min) / range;

        // Floyd-Steinberg error diffusion
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var idx = y * width + x;
            var oldVal = result[idx];
            var newVal = oldVal > 0.5f ? 1f : 0f;
            result[idx] = newVal;
            var error = oldVal - newVal;

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
                    result[(y + 1) * width + x + 1] += error * (1f / 16f);
            }
        }

        return result;
    }

    /// <summary>
    ///     Calculate the actual brightness range of an image for autocontrast.
    ///     Inspired by img2braille's autocontrast approach.
    /// </summary>
    private (float min, float max) CalculateBrightnessRange(Image<Rgba32> image)
    {
        var min = 1f;
        var max = 0f;

        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var brightness = BrightnessHelper.GetBrightness(image[x, y]);
            if (brightness < min) min = brightness;
            if (brightness > max) max = brightness;
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
    ///     Render a file to a BrailleFrame.
    /// </summary>
    public BrailleFrame RenderFileToFrame(string filePath)
    {
        return new BrailleFrame(RenderFile(filePath), 0);
    }

    /// <summary>
    ///     Render an image to a BrailleFrame.
    /// </summary>
    public BrailleFrame RenderImageToFrame(Image<Rgba32> image)
    {
        return new BrailleFrame(RenderImage(image), 0);
    }

    /// <summary>
    ///     Render a GIF file to a list of braille frames
    /// </summary>
    public List<BrailleFrame> RenderGif(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
        return RenderGifFramesInternal(image);
    }

    /// <summary>
    ///     Render a GIF file to a list of braille frames (for GIF output).
    /// </summary>
    public List<BrailleFrame> RenderGifFrames(string filePath)
    {
        using var image = Image.Load<Rgba32>(filePath);
        return RenderGifFramesInternal(image);
    }

    private List<BrailleFrame> RenderGifFramesInternal(Image<Rgba32> image)
    {
        var frames = new List<BrailleFrame>();

        for (var i = 0; i < image.Frames.Count; i++)
        {
            if (_options.FrameSampleRate > 1 && i % _options.FrameSampleRate != 0)
                continue;

            using var frameImage = image.Frames.CloneFrame(i);
            var content = RenderImage(frameImage);

            var metadata = image.Frames[i].Metadata.GetGifMetadata();
            var delayMs = metadata.FrameDelay * 10; // GIF delay is in centiseconds
            if (delayMs == 0) delayMs = 100;
            delayMs = (int)(delayMs / _options.AnimationSpeedMultiplier);

            frames.Add(new BrailleFrame(content, delayMs));
        }

        return frames;
    }

    /// <summary>
    ///     Calculate braille pixel dimensions accounting for CharacterAspectRatio.
    ///     Uses the shared CalculateVisualDimensions method from RenderOptions.
    /// </summary>
    private (int width, int height) CalculateBrailleDimensions(int imageWidth, int imageHeight)
    {
        // Braille: 2 pixels per char width, 4 pixels per char height
        var (width, height) = _options.CalculateVisualDimensions(imageWidth, imageHeight, 2, 4);

        // Ensure dimensions are multiples of 2x4 for braille
        width = width / 2 * 2;
        height = height / 4 * 4;

        return (Math.Max(2, width), Math.Max(4, height));
    }

    // Pre-computed braille density patterns for smooth gradients
    // Each pattern has a specific number of dots arranged aesthetically
    // Pattern[0] = 0 dots (empty), Pattern[8] = 8 dots (full)
    // Designed to create visually smooth transitions
    private static readonly int[] DensityPatterns =
    {
        0x00,  // 0 dots: ⠀ (empty)
        0x40,  // 1 dot:  ⡀ (bottom-left, least intrusive)
        0x44,  // 2 dots: ⡄ (left column bottom half)
        0x64,  // 3 dots: ⡤ (bottom half minus one)
        0xE4,  // 4 dots: ⣤ (bottom half - symmetric)
        0xE5,  // 5 dots: ⣥ (bottom half + top-left)
        0xF5,  // 6 dots: ⣵ (missing top-right and one)
        0xFD,  // 7 dots: ⣽ (missing one corner)
        0xFF   // 8 dots: ⣿ (full block)
    };

    // Braille bit positions (standard Unicode encoding):
    // Pos:  1  4    Bits: 0x01  0x08
    //       2  5          0x02  0x10
    //       3  6          0x04  0x20
    //       7  8          0x40  0x80
    // Full block (⣿) = 0xFF

    // Row/column masks
    private const int MaskTopRow = 0x09;      // positions 1,4
    private const int MaskRow2 = 0x12;        // positions 2,5
    private const int MaskRow3 = 0x24;        // positions 3,6
    private const int MaskBottomRow = 0xC0;   // positions 7,8
    private const int MaskLeftCol = 0x47;     // positions 1,2,3,7
    private const int MaskRightCol = 0xB8;    // positions 4,5,6,8
    private const int MaskTopHalf = 0x1B;     // rows 1-2
    private const int MaskBottomHalf = 0xE4;  // rows 3-4

    // 6-dot patterns (missing one row or column - 2 holes)
    private const int PatternTopFilled = 0xFF ^ MaskBottomRow;     // ⠿ 0x3F - bottom row empty
    private const int PatternBottomFilled = 0xFF ^ MaskTopRow;     // ⣶ 0xF6 - top row empty
    private const int PatternLeftFilled = 0xFF ^ MaskRightCol;     // ⡇ 0x47 - right col empty
    private const int PatternRightFilled = 0xFF ^ MaskLeftCol;     // ⢸ 0xB8 - left col empty

    // 7-dot patterns (single corner missing - 1 hole) - minimal black
    private const int PatternNoTL = 0xFE;     // ⣾ missing top-left (pos 1)
    private const int PatternNoTR = 0xF7;     // ⣷ missing top-right (pos 4)
    private const int PatternNoBL = 0xBF;     // ⢿ missing bottom-left (pos 7)
    private const int PatternNoBR = 0x7F;     // ⡿ missing bottom-right (pos 8)

    // 6-dot diagonal patterns (two opposite corners missing)
    private const int PatternDiagTLBR = 0xFF ^ 0x01 ^ 0x80;  // ⢾ missing TL and BR
    private const int PatternDiagTRBL = 0xFF ^ 0x08 ^ 0x40;  // ⡷ missing TR and BL

    // Additional subtle patterns for fine detail
    // Half-block patterns (4 dots each)
    private const int PatternTopHalf = 0x1B;      // ⠛ top 2 rows only
    private const int PatternBottomHalf = 0xE4;   // ⣤ bottom 2 rows only
    private const int PatternLeftHalf = 0x47;     // ⡇ left column
    private const int PatternRightHalf = 0xB8;    // ⢸ right column

    /// <summary>
    ///     Calculate braille code for colored mode with refined edge detection.
    ///     Balances detail (edge patterns) with solid color aesthetics (full blocks).
    ///     Uses graduated patterns based on edge strength.
    /// </summary>
    private static int CalculateHybridBrailleCode(
        ReadOnlySpan<float> cellBrightness,
        ReadOnlySpan<int> cellIndices,
        int colorCount,
        float minBrightness,
        float maxBrightness,
        bool invertMode)
    {
        if (colorCount == 0) return 0xFF;

        // Calculate cell brightness statistics
        var minCell = 1f;
        var maxCell = 0f;
        for (var i = 0; i < colorCount; i++)
        {
            var b = cellBrightness[i];
            if (b < minCell) minCell = b;
            if (b > maxCell) maxCell = b;
        }
        var cellRange = maxCell - minCell;

        // Very uniform cells get full blocks - preserves solid color look
        if (cellRange < 0.12f)
        {
            return 0xFF;
        }

        // Calculate brightness for each quadrant of the 2x4 cell
        // Top half (rows 0-1) and bottom half (rows 2-3)
        float topLeft = 0, topRight = 0, bottomLeft = 0, bottomRight = 0;
        int tlCount = 0, trCount = 0, blCount = 0, brCount = 0;

        for (var i = 0; i < colorCount; i++)
        {
            var idx = cellIndices[i];
            var b = cellBrightness[i];
            var dy = idx / 2;  // 0-3 (row)
            var dx = idx % 2;  // 0-1 (column)

            if (dy < 2)
            {
                if (dx == 0) { topLeft += b; tlCount++; }
                else { topRight += b; trCount++; }
            }
            else
            {
                if (dx == 0) { bottomLeft += b; blCount++; }
                else { bottomRight += b; brCount++; }
            }
        }

        // Average each quadrant
        topLeft = tlCount > 0 ? topLeft / tlCount : 0.5f;
        topRight = trCount > 0 ? topRight / trCount : 0.5f;
        bottomLeft = blCount > 0 ? bottomLeft / blCount : 0.5f;
        bottomRight = brCount > 0 ? bottomRight / brCount : 0.5f;

        // Calculate edge directions
        var top = (topLeft + topRight) / 2;
        var bottom = (bottomLeft + bottomRight) / 2;
        var left = (topLeft + bottomLeft) / 2;
        var right = (topRight + bottomRight) / 2;

        var vertDiff = MathF.Abs(top - bottom);
        var horizDiff = MathF.Abs(left - right);

        // Check for diagonal edges (corner-to-corner contrast)
        var diagTLBR = (topLeft + bottomRight) / 2;
        var diagTRBL = (topRight + bottomLeft) / 2;
        var diagDiff = MathF.Abs(diagTLBR - diagTRBL);

        // Find darkest corner for subtle edge removal
        var corners = new[] { topLeft, topRight, bottomLeft, bottomRight };
        var darkestCorner = 0;
        var darkestVal = corners[0];
        var brightestCorner = 0;
        var brightestVal = corners[0];
        for (var i = 1; i < 4; i++)
        {
            if (corners[i] < darkestVal) { darkestVal = corners[i]; darkestCorner = i; }
            if (corners[i] > brightestVal) { brightestVal = corners[i]; brightestCorner = i; }
        }

        // Determine which corner to remove based on terminal mode
        // Dark terminal (invert=true): remove darkest corner (show bright areas)
        // Light terminal (invert=false): remove brightest corner (show dark areas)
        var cornerToRemove = invertMode ? darkestCorner : brightestCorner;

        // Graduated edge detection thresholds
        const float subtleThreshold = 0.15f;   // Subtle edges - single corner removed
        const float moderateThreshold = 0.25f; // Moderate edges - 2 corners removed
        const float strongThreshold = 0.40f;   // Strong edges - half block

        // Strong diagonal edges
        if (diagDiff > vertDiff && diagDiff > horizDiff && diagDiff > moderateThreshold)
        {
            var tlbrBrighter = diagTLBR > diagTRBL;
            if (diagDiff > strongThreshold)
            {
                // Very strong diagonal - use half patterns
                if (tlbrBrighter == invertMode)
                    return PatternDiagTRBL;  // Remove TR+BL corners
                else
                    return PatternDiagTLBR;  // Remove TL+BR corners
            }
            else
            {
                // Moderate diagonal - remove single corner
                if (tlbrBrighter == invertMode)
                    return PatternNoTR;  // Darker diagonal is TR-BL
                else
                    return PatternNoTL;  // Darker diagonal is TL-BR
            }
        }

        // Strong horizontal edges (top vs bottom)
        if (vertDiff > horizDiff && vertDiff > subtleThreshold)
        {
            var topBrighter = top > bottom;
            if (vertDiff > strongThreshold)
            {
                // Very strong horizontal edge - half block
                return (topBrighter == invertMode) ? PatternTopHalf : PatternBottomHalf;
            }
            else if (vertDiff > moderateThreshold)
            {
                // Moderate horizontal - row removed
                return (topBrighter == invertMode) ? PatternTopFilled : PatternBottomFilled;
            }
            else
            {
                // Subtle horizontal - single corner
                return (topBrighter == invertMode) ? PatternNoBL : PatternNoTR;
            }
        }

        // Strong vertical edges (left vs right)
        if (horizDiff > subtleThreshold)
        {
            var leftBrighter = left > right;
            if (horizDiff > strongThreshold)
            {
                // Very strong vertical edge - half block
                return (leftBrighter == invertMode) ? PatternLeftHalf : PatternRightHalf;
            }
            else if (horizDiff > moderateThreshold)
            {
                // Moderate vertical - column removed
                return (leftBrighter == invertMode) ? PatternLeftFilled : PatternRightFilled;
            }
            else
            {
                // Subtle vertical - single corner
                return (leftBrighter == invertMode) ? PatternNoTR : PatternNoBL;
            }
        }

        // Cell has contrast but no clear edge direction
        // Use subtle single-corner removal based on darkest/brightest corner
        if (cellRange > subtleThreshold)
        {
            return cornerToRemove switch
            {
                0 => PatternNoTL,  // Remove top-left
                1 => PatternNoTR,  // Remove top-right
                2 => PatternNoBL,  // Remove bottom-left
                3 => PatternNoBR,  // Remove bottom-right
                _ => 0xFF
            };
        }

        // Default to full block for moderate contrast without clear edges
        return 0xFF;
    }
}

/// <summary>
///     A single frame of braille-rendered content
/// </summary>
public class BrailleFrame : IAnimationFrame
{
    public BrailleFrame(string content, int delayMs)
    {
        Content = content;
        DelayMs = delayMs;
    }

    public string Content { get; }
    public int DelayMs { get; }
}