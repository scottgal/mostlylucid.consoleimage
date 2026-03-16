// Braille character renderer - 2x4 dots per cell for high resolution output
// Each braille character represents an 8-dot grid (2 wide x 4 tall)

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
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

    // Braille bit positions (standard Unicode encoding):
    // Pos:  1  4    Bits: 0x01  0x08
    //       2  5          0x02  0x10
    //       3  6          0x04  0x20
    //       7  8          0x40  0x80
    // Full block (⣿) = 0xFF

    // Pre-computed ANSI escape sequences for common greyscale values (0-255)
    // Saves string allocations for repeated colors
    private static readonly string[] GreyscaleEscapes = InitGreyscaleEscapes();

    // Pre-computed sin/cos for concentric ring sampling around braille dot centers
    private static readonly (float Cos, float Sin)[] InnerRingAngles = PrecomputeAngles(4, 0);
    private static readonly (float Cos, float Sin)[] OuterRingAngles = PrecomputeAngles(8, MathF.PI / 8);

    private readonly BrailleCharacterMap _brailleMap = new();
    private readonly RenderOptions _options;

    // Reusable buffers to reduce GC pressure during video playback
    private float[]? _brightnessBuffer;
    private Rgba32[]? _colorsBuffer;
    private bool _disposed;
    private float[]? _ditheringBuffer;
    private int _lastBufferSize;

    public BrailleRenderer(RenderOptions? options = null)
    {
        ConsoleHelper.EnableAnsiSupport();
        _options = options ?? new RenderOptions();
    }

    private static (float Cos, float Sin)[] PrecomputeAngles(int count, float offset)
    {
        var angles = new (float Cos, float Sin)[count];
        for (var i = 0; i < count; i++)
        {
            var angle = i * MathF.PI * 2 / count + offset;
            angles[i] = (MathF.Cos(angle), MathF.Sin(angle));
        }
        return angles;
    }

    /// <summary>
    ///     Sample 8 braille dot positions from brightness data using concentric ring sampling.
    ///     Each dot center is sampled with rings for accuracy, converting brightness to coverage.
    ///     Dot centers in normalized cell coords: col 0 at x=0.25, col 1 at x=0.75
    ///     Rows at y = 0.125, 0.375, 0.625, 0.875 (evenly spaced in 4 rows).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SampleBrailleCell(float[] brightness, int pixelWidth, int pixelHeight,
        int px, int py, Span<float> target8)
    {
        const float radius = 0.35f;

        for (var row = 0; row < 4; row++)
        {
            var centerY = py + row + 0.5f;
            for (var col = 0; col < 2; col++)
            {
                var centerX = px + col + 0.5f;
                var dotIdx = row * 2 + col;

                float total = 0;
                var count = 0;

                // Center point
                var ix = (int)centerX;
                var iy = (int)centerY;
                if ((uint)ix < (uint)pixelWidth && (uint)iy < (uint)pixelHeight)
                {
                    total += 1f - brightness[iy * pixelWidth + ix];
                    count++;
                }

                // Inner ring (4 points at 0.5 * radius)
                var innerR = radius * 0.5f;
                for (var i = 0; i < 4; i++)
                {
                    var (cos, sin) = InnerRingAngles[i];
                    ix = (int)(centerX + cos * innerR);
                    iy = (int)(centerY + sin * innerR);
                    if ((uint)ix < (uint)pixelWidth && (uint)iy < (uint)pixelHeight)
                    {
                        total += 1f - brightness[iy * pixelWidth + ix];
                        count++;
                    }
                }

                // Outer ring (8 points at radius)
                for (var i = 0; i < 8; i++)
                {
                    var (cos, sin) = OuterRingAngles[i];
                    ix = (int)(centerX + cos * radius);
                    iy = (int)(centerY + sin * radius);
                    if ((uint)ix < (uint)pixelWidth && (uint)iy < (uint)pixelHeight)
                    {
                        total += 1f - brightness[iy * pixelWidth + ix];
                        count++;
                    }
                }

                // Coverage: 0 = white/empty, 1 = black/filled
                target8[dotIdx] = count > 0 ? total / count : 0f;
            }
        }
    }

    /// <summary>
    ///     Compute braille character directly from binary (post-dithered) brightness data.
    ///     Reads exactly 8 pixels (one per dot position) instead of 104 concentric ring samples,
    ///     and computes the Unicode braille code directly instead of searching 256 patterns.
    ///     ~10x faster than SampleBrailleCell + FindBestMatch for post-dithered data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char ComputeBrailleCodeDirect(float[] brightness, int pixelWidth, int pixelHeight,
        int px, int py, bool invertMode, float threshold)
    {
        // Braille dot bits indexed by [row * 2 + col]:
        // Row 0: 0x01, 0x08   Row 1: 0x02, 0x10
        // Row 2: 0x04, 0x20   Row 3: 0x40, 0x80
        var code = 0;

        // Unrolled 4 rows × 2 cols for maximum throughput
        // Row 0
        if ((uint)py < (uint)pixelHeight)
        {
            var rowOff = py * pixelWidth;
            if ((uint)px < (uint)pixelWidth)
            {
                var b = brightness[rowOff + px];
                if (invertMode ? b > threshold : b < threshold) code |= 0x01;
            }

            if ((uint)(px + 1) < (uint)pixelWidth)
            {
                var b = brightness[rowOff + px + 1];
                if (invertMode ? b > threshold : b < threshold) code |= 0x08;
            }
        }

        // Row 1
        if ((uint)(py + 1) < (uint)pixelHeight)
        {
            var rowOff = (py + 1) * pixelWidth;
            if ((uint)px < (uint)pixelWidth)
            {
                var b = brightness[rowOff + px];
                if (invertMode ? b > threshold : b < threshold) code |= 0x02;
            }

            if ((uint)(px + 1) < (uint)pixelWidth)
            {
                var b = brightness[rowOff + px + 1];
                if (invertMode ? b > threshold : b < threshold) code |= 0x10;
            }
        }

        // Row 2
        if ((uint)(py + 2) < (uint)pixelHeight)
        {
            var rowOff = (py + 2) * pixelWidth;
            if ((uint)px < (uint)pixelWidth)
            {
                var b = brightness[rowOff + px];
                if (invertMode ? b > threshold : b < threshold) code |= 0x04;
            }

            if ((uint)(px + 1) < (uint)pixelWidth)
            {
                var b = brightness[rowOff + px + 1];
                if (invertMode ? b > threshold : b < threshold) code |= 0x20;
            }
        }

        // Row 3
        if ((uint)(py + 3) < (uint)pixelHeight)
        {
            var rowOff = (py + 3) * pixelWidth;
            if ((uint)px < (uint)pixelWidth)
            {
                var b = brightness[rowOff + px];
                if (invertMode ? b > threshold : b < threshold) code |= 0x40;
            }

            if ((uint)(px + 1) < (uint)pixelWidth)
            {
                var b = brightness[rowOff + px + 1];
                if (invertMode ? b > threshold : b < threshold) code |= 0x80;
            }
        }

        return (char)(BrailleBase + code);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Return pooled buffers
        if (_brightnessBuffer != null)
        {
            ArrayPool<float>.Shared.Return(_brightnessBuffer);
            _brightnessBuffer = null;
        }

        if (_colorsBuffer != null)
        {
            ArrayPool<Rgba32>.Shared.Return(_colorsBuffer);
            _colorsBuffer = null;
        }

        if (_ditheringBuffer != null)
        {
            ArrayPool<float>.Shared.Return(_ditheringBuffer);
            _ditheringBuffer = null;
        }

        GC.SuppressFinalize(this);
    }

    private static string[] InitGreyscaleEscapes()
    {
        var escapes = new string[256];
        for (var i = 0; i < 256; i++)
            escapes[i] = $"\x1b[38;2;{i};{i};{i}m";
        return escapes;
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

        // Apply dithering unless disabled (same logic as RenderImage)
        var invertMode = _options.Invert;
        float threshold;
        if (_options.UseColor || _options.UseGreyscaleAnsi)
            threshold = invertMode ? 0.15f : 0.85f;
        else
            threshold = CalculateOtsuThreshold(brightness);

        if (!_options.DisableBrailleDithering)
        {
            brightness = ApplyAtkinsonDithering(brightness, pixelWidth, pixelHeight, threshold);
            threshold = 0.5f;
        }

        var cells = new CellData[charHeight, charWidth];

        // Parallel render to cell array
        Parallel.For(0, charHeight, cy =>
        {
            for (var cx = 0; cx < charWidth; cx++)
            {
                var px = cx * 2;
                var py = cy * 4;

                // Direct braille code computation (same optimized path as RenderBrailleContent)
                var brailleChar = ComputeBrailleCodeDirect(
                    brightness, pixelWidth, pixelHeight, px, py, invertMode, threshold);

                // Collect average color from all pixels in cell
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

                        var c = colors[rowOffset + imgX];
                        totalR += c.R;
                        totalG += c.G;
                        totalB += c.B;
                        colorCount++;
                    }
                }

                // Calculate average color
                byte r = 0, g = 0, b = 0;
                if (colorCount > 0)
                {
                    r = (byte)(totalR / colorCount);
                    g = (byte)(totalG / colorCount);
                    b = (byte)(totalB / colorCount);

                    (r, g, b) = BoostBrailleColor(r, g, b, _options.Gamma);

                    var paletteSize = _options.ColorCount;
                    if (paletteSize.HasValue && paletteSize.Value > 0)
                    {
                        var quantStep = Math.Max(1, 256 / paletteSize.Value);
                        r = (byte)(r / quantStep * quantStep);
                        g = (byte)(g / quantStep * quantStep);
                        b = (byte)(b / quantStep * quantStep);
                    }
                    else if (_options.EnableTemporalStability)
                    {
                        var quantStep = Math.Max(1, _options.ColorStabilityThreshold / 2);
                        r = (byte)(r / quantStep * quantStep);
                        g = (byte)(g / quantStep * quantStep);
                        b = (byte)(b / quantStep * quantStep);
                    }
                }

                cells[cy, cx] = new CellData(brailleChar, r, g, b);
            }
        });

        return cells;
    }

    /// <summary>
    ///     Render an image with delta optimization - only outputs changed cells.
    ///     Much more efficient for video/animation playback where most cells don't change.
    /// </summary>
    /// <param name="image">Image to render</param>
    /// <param name="previousCells">Previous frame's cells (null for first frame)</param>
    /// <param name="colorThreshold">Color difference threshold for considering a cell unchanged (0-255)</param>
    /// <returns>Tuple of (ANSI output string, current frame's cells for next comparison)</returns>
    public (string output, CellData[,] cells) RenderWithDelta(
        Image<Rgba32> image,
        CellData[,]? previousCells,
        int colorThreshold = 8)
    {
        var cells = RenderToCells(image);
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);

        // First frame or dimension change - full redraw
        if (previousCells == null ||
            previousCells.GetLength(0) != height ||
            previousCells.GetLength(1) != width)
            return (RenderCellsToString(cells), cells);

        // Delta render - only output changed cells
        // Optimized: batch consecutive changed cells on same row with same color
        // Pre-size for ~20% cell change rate (cursor pos + color + char per changed cell)
        var sb = new StringBuilder(width * height * 5);
        byte lastR = 0, lastG = 0, lastB = 0;
        var hasColor = false;
        var changedCount = 0;

        for (var y = 0; y < height; y++)
        {
            var x = 0;
            while (x < width)
            {
                var current = cells[y, x];
                var previous = previousCells[y, x];

                // Skip if cell hasn't changed (with threshold tolerance)
                if (current.IsSimilar(previous, colorThreshold))
                {
                    x++;
                    continue;
                }

                changedCount++;

                // Position cursor at this cell (1-indexed)
                sb.Append("\x1b[");
                sb.Append(y + 1);
                sb.Append(';');
                sb.Append(x + 1);
                sb.Append('H');

                // Output color if needed
                if (_options.UseColor || _options.UseGreyscaleAnsi)
                {
                    if (!hasColor || current.R != lastR || current.G != lastG || current.B != lastB)
                    {
                        AppendColorCode(sb, current.R, current.G, current.B, _options.ColorDepth, _options.UseGreyscaleAnsi);
                        lastR = current.R;
                        lastG = current.G;
                        lastB = current.B;
                        hasColor = true;
                    }

                    // Batch consecutive changed cells with same color on this row
                    sb.Append(current.Character);
                    x++;

                    while (x < width)
                    {
                        var nextCurrent = cells[y, x];
                        var nextPrevious = previousCells[y, x];

                        // Stop if cell unchanged or different color
                        if (nextCurrent.IsSimilar(nextPrevious, colorThreshold) ||
                            nextCurrent.R != lastR || nextCurrent.G != lastG || nextCurrent.B != lastB)
                            break;

                        sb.Append(nextCurrent.Character);
                        changedCount++;
                        x++;
                    }
                }
                else
                {
                    sb.Append(current.Character);
                    x++;
                }
            }
        }

        // Reset color at end
        if (hasColor)
            sb.Append("\x1b[0m");

        return (sb.ToString(), cells);
    }

    /// <summary>
    ///     Convert cell array to full ANSI string (for first frame or full redraw).
    ///     Uses run-length encoding for consecutive same-color cells.
    /// </summary>
    private string RenderCellsToString(CellData[,] cells)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        var sb = new StringBuilder(width * height * 15); // Reduced estimate due to RLE

        sb.Append("\x1b[H"); // Home cursor

        for (var y = 0; y < height; y++)
        {
            var x = 0;
            while (x < width)
            {
                var cell = cells[y, x];

                if (_options.UseColor || _options.UseGreyscaleAnsi)
                {
                    // Append color code
                    AppendColorCode(sb, cell.R, cell.G, cell.B, _options.ColorDepth, _options.UseGreyscaleAnsi);

                    // Run-length encode: collect consecutive cells with same color
                    while (x < width &&
                           cells[y, x].R == cell.R &&
                           cells[y, x].G == cell.G &&
                           cells[y, x].B == cell.B)
                    {
                        sb.Append(cells[y, x].Character);
                        x++;
                    }
                }
                else
                {
                    sb.Append(cell.Character);
                    x++;
                }
            }

            if (y < height - 1) sb.Append("\x1b[0m\n");
        }

        sb.Append("\x1b[0m");
        return sb.ToString();
    }

    /// <summary>
    ///     Append ANSI color code efficiently, using pre-computed greyscale when possible.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendColorCode(StringBuilder sb, byte r, byte g, byte b,
        ColorDepth depth = ColorDepth.TrueColor, bool greyscale = false)
    {
        // Greyscale ANSI: convert to brightness and use 256-color grey ramp
        if (greyscale)
        {
            var brightness = (byte)(0.299f * r + 0.587f * g + 0.114f * b);
            AnsiCodes.AppendForegroundGrey256(sb, brightness);
            return;
        }

        if (depth != ColorDepth.TrueColor)
        {
            AnsiCodes.AppendForegroundAdaptive(sb, r, g, b, depth);
            return;
        }

        // Use pre-computed escape for greyscale colors
        if (r == g && g == b)
        {
            sb.Append(GreyscaleEscapes[r]);
        }
        else
        {
            // Manual append is faster than string interpolation
            sb.Append("\x1b[38;2;");
            sb.Append(r);
            sb.Append(';');
            sb.Append(g);
            sb.Append(';');
            sb.Append(b);
            sb.Append('m');
        }
    }

    /// <summary>
    ///     Render an image to braille string
    /// </summary>
    public string RenderImage(Image<Rgba32> image)
    {
        // Calculate output dimensions (each char = 2x4 braille dots)
        var (pixelWidth, pixelHeight) = CalculateBrailleDimensions(image.Width, image.Height);

        // Resize image
        var resized = image.Clone(ctx => ctx.Resize(pixelWidth, pixelHeight));

        // Pre-compute brightness values and colors for all pixels
        var (brightness, colors) = PrecomputePixelData(resized);

        // Calculate threshold using autocontrast
        var invertMode = _options.Invert;

        // For dual-color mode: always use Otsu threshold to ensure balanced ON/OFF mix
        // — with invert=true the 0.15f threshold turns everything on, hiding the BG color entirely.
        // For COLOR/GREYSCALE mode: show most dots, only hide truly dark pixels
        // For MONOCHROME mode: use Otsu's method for optimal separation
        float threshold;
        if (_options.UseDualColor)
            threshold = CalculateOtsuThreshold(brightness);
        else if (_options.UseColor || _options.UseGreyscaleAnsi)
            threshold = invertMode ? 0.15f : 0.85f;
        else
            threshold = CalculateOtsuThreshold(brightness);

        // Apply Atkinson dithering for smooth gradients (unless disabled)
        if (_options.DisableBrailleDithering)
        {
            // No dithering: threshold continuous values directly.
            // Sharper edges but gradients may show banding.
            var result = RenderBrailleContent(brightness, colors, pixelWidth, pixelHeight, threshold);
            resized.Dispose();
            return result;
        }

        // Keep original brightness for color grouping in dual-color mode.
        // ApplyAtkinsonDithering returns a separate pool buffer, so originalBrightness
        // still points to the unmodified pre-dithered values.
        var originalBrightness = brightness;
        brightness = ApplyAtkinsonDithering(brightness, pixelWidth, pixelHeight, threshold);
        // After dithering, values are 0 or 1 — use direct pixel read to avoid ring-sampling smear
        string result2;
        if (_options.UseDualColor)
            // Pass original brightness + Otsu threshold for color grouping (avoids dithering-pattern banding).
            // The dithered brightness is used only for the character dot pattern.
            result2 = RenderBrailleContentDual(brightness, originalBrightness, colors, pixelWidth, pixelHeight, threshold, _options.DualColorStrategy ?? "value");
        else
            result2 = RenderBrailleContent(brightness, colors, pixelWidth, pixelHeight, 0.5f, binaryData: true);

        resized.Dispose();
        return result2;
    }

    /// <summary>
    ///     EXPERIMENTAL: Generate multiple braille frames with different brightness thresholds
    ///     for temporal super-resolution (perceptual interlacing).
    ///     Each frame uses a slightly different dithering threshold; when played
    ///     rapidly, the human visual system integrates the frames and perceives
    ///     more tonal detail than any single frame can display.
    ///     Known issues: playback produces black bars due to clearing bugs in BrailleInterlacePlayer.
    /// </summary>
    public List<BrailleFrame> RenderInterlaceFrames(Image<Rgba32> image)
    {
        var frameCount = Math.Clamp(_options.InterlaceFrameCount, 2, 8);
        var spread = Math.Clamp(_options.InterlaceSpread, 0.01f, 0.2f);
        // Delay per subframe: distribute one visible frame period across N subframes
        var delayMs = Math.Max(1, (int)(1000f / (_options.InterlaceFps * frameCount)));

        // Shared computation: resize and pixel data (expensive, done once)
        var (pixelWidth, pixelHeight) = CalculateBrailleDimensions(image.Width, image.Height);
        using var resized = image.Clone(ctx => ctx.Resize(pixelWidth, pixelHeight));
        var (baseBrightness, colors) = PrecomputePixelData(resized);

        // Calculate base threshold
        var invertMode = _options.Invert;
        float baseThreshold;
        if (_options.UseColor || _options.UseGreyscaleAnsi)
            baseThreshold = invertMode ? 0.15f : 0.85f;
        else
            baseThreshold = CalculateOtsuThreshold(baseBrightness);

        var frames = new List<BrailleFrame>(frameCount);

        for (var f = 0; f < frameCount; f++)
        {
            // Spread thresholds evenly around the base.
            // For 4 frames: biases are [-0.5, -0.167, +0.167, +0.5] * spread
            var bias = spread * ((float)f / Math.Max(1, frameCount - 1) - 0.5f);
            var threshold = Math.Clamp(baseThreshold + bias, 0.01f, 0.99f);

            // Apply Atkinson dithering with biased threshold (creates a new array)
            var dithered = ApplyAtkinsonDithering(baseBrightness, pixelWidth, pixelHeight, threshold);

            // Render to braille string using post-dithered binary data
            var content = RenderBrailleContent(dithered, colors, pixelWidth, pixelHeight, 0.5f, binaryData: true);
            frames.Add(new BrailleFrame(content, delayMs));
        }

        return frames;
    }

    /// <summary>
    ///     Render pre-computed brightness and color data to a braille ANSI string.
    ///     Uses shape vector matching against all 256 braille patterns for optimal character selection.
    ///     This is the core rendering step shared by RenderImage and RenderInterlaceFrames.
    /// </summary>
    private string RenderBrailleContent(float[] brightness, Rgba32[] colors,
        int pixelWidth, int pixelHeight, float threshold, bool binaryData = false)
    {
        var charWidth = pixelWidth / 2;
        var charHeight = pixelHeight / 4;
        var invertMode = _options.Invert;

        // useAnsiOutput covers both full-color and greyscale ANSI modes
        var useAnsiOutput = _options.UseColor || _options.UseGreyscaleAnsi;

        // Pre-size StringBuilder: each char cell needs ~20 bytes for ANSI codes + 1 char
        var estimatedSize = charWidth * charHeight * (useAnsiOutput ? 25 : 1) + charHeight * 10;
        var sb = new StringBuilder(estimatedSize);

        var colorDepth = _options.ColorDepth;
        var greyscaleAnsi = _options.UseGreyscaleAnsi;

        if (useAnsiOutput && _options.UseParallelProcessing && charHeight > 4)
        {
            // Parallel processing: compute each row independently, then combine
            var rowStrings = new string[charHeight];

            Parallel.For(0, charHeight, cy =>
            {
                var rowSb = new StringBuilder(charWidth * 25);
                Rgba32? lastColor = null;
                Span<float> target8 = stackalloc float[8];

                for (var cx = 0; cx < charWidth; cx++)
                {
                    var px = cx * 2;
                    var py = cy * 4;

                    char brailleChar;
                    if (binaryData)
                    {
                        brailleChar = ComputeBrailleCodeDirect(
                            brightness, pixelWidth, pixelHeight, px, py, invertMode, threshold);
                    }
                    else
                    {
                        SampleBrailleCell(brightness, pixelWidth, pixelHeight, px, py, target8);
                        if (invertMode)
                            for (var i = 0; i < 8; i++)
                                target8[i] = 1f - target8[i];
                        brailleChar = _brailleMap.FindBestMatch(target8);
                    }

                    // Collect average color from all pixels in cell
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

                            var c = colors[rowOffset + imgX];
                            totalR += c.R;
                            totalG += c.G;
                            totalB += c.B;
                            colorCount++;
                        }
                    }

                    if (colorCount > 0)
                    {
                        var r = (byte)(totalR / colorCount);
                        var g = (byte)(totalG / colorCount);
                        var b = (byte)(totalB / colorCount);

                        (r, g, b) = BoostBrailleColor(r, g, b, _options.Gamma);

                        // Skip absolute black characters (invisible on dark terminal)
                        if (r <= 2 && g <= 2 && b <= 2 && brailleChar == BrailleBase)
                        {
                            rowSb.Append(' ');
                            continue;
                        }

                        var avgColor = new Rgba32(r, g, b, 255);

                        if (lastColor == null || !AnsiCodes.ColorsEqual(lastColor.Value, avgColor))
                        {
                            if (greyscaleAnsi)
                                AnsiCodes.AppendForegroundGrey256(rowSb,
                                    BrightnessHelper.ToGrayscale(avgColor));
                            else
                                AnsiCodes.AppendForegroundAdaptive(rowSb, avgColor.R, avgColor.G, avgColor.B,
                                    colorDepth);
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
            Span<float> target8 = stackalloc float[8];

            for (var cy = 0; cy < charHeight; cy++)
            {
                for (var cx = 0; cx < charWidth; cx++)
                {
                    var px = cx * 2;
                    var py = cy * 4;

                    char brailleChar;
                    if (binaryData)
                    {
                        brailleChar = ComputeBrailleCodeDirect(
                            brightness, pixelWidth, pixelHeight, px, py, invertMode, threshold);
                    }
                    else
                    {
                        SampleBrailleCell(brightness, pixelWidth, pixelHeight, px, py, target8);
                        if (invertMode)
                            for (var i = 0; i < 8; i++)
                                target8[i] = 1f - target8[i];
                        brailleChar = _brailleMap.FindBestMatch(target8);
                    }

                    if (useAnsiOutput)
                    {
                        // Collect average color from all pixels in cell
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

                                var c = colors[rowOffset + imgX];
                                totalR += c.R;
                                totalG += c.G;
                                totalB += c.B;
                                colorCount++;
                            }
                        }

                        if (colorCount > 0)
                        {
                            var r = (byte)(totalR / colorCount);
                            var g = (byte)(totalG / colorCount);
                            var b = (byte)(totalB / colorCount);

                            (r, g, b) = BoostBrailleColor(r, g, b, _options.Gamma);

                            if (r <= 2 && g <= 2 && b <= 2 && brailleChar == BrailleBase)
                            {
                                sb.Append(' ');
                                continue;
                            }

                            var avgColor = new Rgba32(r, g, b, 255);

                            if (lastColor == null || !AnsiCodes.ColorsEqual(lastColor.Value, avgColor))
                            {
                                if (greyscaleAnsi)
                                    AnsiCodes.AppendForegroundGrey256(sb,
                                        BrightnessHelper.ToGrayscale(avgColor));
                                else
                                    AnsiCodes.AppendForegroundAdaptive(sb, avgColor.R, avgColor.G, avgColor.B,
                                        colorDepth);
                                lastColor = avgColor;
                            }
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
                    if (useAnsiOutput)
                        sb.Append("\x1b[0m");
                    sb.AppendLine();
                    lastColor = null;
                }
            }

            if (useAnsiOutput)
                sb.Append("\x1b[0m");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Render braille with dual-color mode: FG from ON pixels, BG from OFF pixels.
    ///
    ///     Two-pass anti-aliased pipeline:
    ///       Pass 1  — compute raw (char, fg, rawBg) for every cell from pixel data.
    ///       AA pass — apply a 3×3 Gaussian blur to rawBg across cells.
    ///                 This is the terminal equivalent of edge anti-aliasing: instead of each
    ///                 cell's background jumping hard to black at object edges, the blurred BG
    ///                 carries neighboring cells' ambient color into the transition zone, producing
    ///                 smooth gradients rather than harsh cutoffs.
    ///       Pass 2  — apply color strategy, gamma, and luminance correction on smoothed BG,
    ///                 then emit ANSI.
    ///
    ///     FG is deliberately kept local (2×4 cell only) for accuracy.
    ///     BG is smoothed across cells because it is the "background fill" — the ambient
    ///     colour between and around the dots — and smooth inter-cell transitions look far
    ///     better than independent per-cell averages at object boundaries.
    /// </summary>
    private string RenderBrailleContentDual(float[] brightness, float[] originalBrightness, Rgba32[] colors,
        int pixelWidth, int pixelHeight, float threshold, string strategy)
    {
        var charWidth = pixelWidth / 2;
        var charHeight = pixelHeight / 4;
        var cells = charWidth * charHeight;
        var invertMode = _options.Invert;
        var strategyLower = strategy.ToLowerInvariant();

        // --- Pass 1: compute raw cell data ---
        // Use ArrayPool to avoid per-frame heap pressure during video playback.
        var chars   = ArrayPool<char>.Shared.Rent(cells);
        var cellFgR = ArrayPool<byte>.Shared.Rent(cells);
        var cellFgG = ArrayPool<byte>.Shared.Rent(cells);
        var cellFgB = ArrayPool<byte>.Shared.Rent(cells);
        var rawBgR  = ArrayPool<byte>.Shared.Rent(cells);
        var rawBgG  = ArrayPool<byte>.Shared.Rent(cells);
        var rawBgB  = ArrayPool<byte>.Shared.Rent(cells);
        var smBgR   = ArrayPool<byte>.Shared.Rent(cells);
        var smBgG   = ArrayPool<byte>.Shared.Rent(cells);
        var smBgB   = ArrayPool<byte>.Shared.Rent(cells);

        try
        {
            for (var cy = 0; cy < charHeight; cy++)
            {
                for (var cx = 0; cx < charWidth; cx++)
                {
                    var idx = cy * charWidth + cx;
                    var px  = cx * 2;
                    var py  = cy * 4;

                    chars[idx] = ComputeBrailleCodeDirect(brightness, pixelWidth, pixelHeight, px, py, invertMode, threshold);

                    int onR = 0, onG = 0, onB = 0, onCount  = 0;
                    int offR = 0, offG = 0, offB = 0, offCount = 0;

                    for (var dy = 0; dy < 4; dy++)
                    {
                        var imgY = py + dy;
                        if (imgY >= pixelHeight) continue;
                        var rowOffset = imgY * pixelWidth;

                        for (var dx = 0; dx < 2; dx++)
                        {
                            var imgX = px + dx;
                            if (imgX >= pixelWidth) continue;

                            var c     = colors[rowOffset + imgX];
                            var origB = originalBrightness[rowOffset + imgX];
                            // Use original (pre-dithered) brightness with the Otsu threshold.
                            // Dithered binary values cause row-level banding (Atkinson error
                            // diffusion creates different ON/OFF distributions per row).
                            if (invertMode ? origB > threshold : origB <= threshold)
                            { onR  += c.R; onG  += c.G; onB  += c.B; onCount++;  }
                            else
                            { offR += c.R; offG += c.G; offB += c.B; offCount++; }
                        }
                    }

                    // Compute raw FG (accurate, local) and raw BG (to be smoothed later).
                    if (onCount == 0)
                    {
                        // All-background cell: both colours track the OFF pixel average.
                        cellFgR[idx] = rawBgR[idx] = offCount > 0 ? (byte)(offR / offCount) : (byte)0;
                        cellFgG[idx] = rawBgG[idx] = offCount > 0 ? (byte)(offG / offCount) : (byte)0;
                        cellFgB[idx] = rawBgB[idx] = offCount > 0 ? (byte)(offB / offCount) : (byte)0;
                    }
                    else if (offCount == 0)
                    {
                        // Fully-covered cell: both colours track the ON pixel average.
                        // BG is invisible here but we still store it for the blur — the
                        // smoothed BG will bleed into adjacent edge-cells and improve AA there.
                        cellFgR[idx] = rawBgR[idx] = (byte)(onR / onCount);
                        cellFgG[idx] = rawBgG[idx] = (byte)(onG / onCount);
                        cellFgB[idx] = rawBgB[idx] = (byte)(onB / onCount);
                    }
                    else
                    {
                        cellFgR[idx] = (byte)(onR  / onCount);
                        cellFgG[idx] = (byte)(onG  / onCount);
                        cellFgB[idx] = (byte)(onB  / onCount);
                        rawBgR[idx]  = (byte)(offR / offCount);
                        rawBgG[idx]  = (byte)(offG / offCount);
                        rawBgB[idx]  = (byte)(offB / offCount);
                    }
                }
            }

            // --- AA pass: Gaussian blur on raw BG channel ---
            // 3×3 kernel (unnormalised weights: corners=1, edges=2, centre=4; sum=16).
            // This smooths inter-cell BG transitions: at an object edge, the hard jump from
            // the object's shadow colour to pure black is replaced by a gradual gradient —
            // the terminal equivalent of anti-aliased edge rendering.
            // FG is deliberately NOT blurred: dot colours must be accurate to the source.
            SmoothenBgChannel(rawBgR, rawBgG, rawBgB, smBgR, smBgG, smBgB, charWidth, charHeight);

            // --- Pass 2: apply strategy + corrections, build ANSI output ---
            var sb = new StringBuilder(cells * 30 + charHeight * 10);

            byte lastFgR = 0, lastFgG = 0, lastFgB = 0;
            byte lastBgR = 0, lastBgG = 0, lastBgB = 0;
            var hasLastColor = false;

            // Pre-compute quantisation step once (temporal stability / palette reduction).
            var quantStep = 0;
            if (_options.EnableTemporalStability || (_options.ColorCount.HasValue && _options.ColorCount.Value > 0))
            {
                quantStep = (_options.ColorCount.HasValue && _options.ColorCount.Value > 0)
                    ? Math.Max(1, 256 / _options.ColorCount.Value)
                    : Math.Max(1, _options.ColorStabilityThreshold / 2);
                if (quantStep <= 1) quantStep = 0; // 0 = disabled
            }

            for (var cy = 0; cy < charHeight; cy++)
            {
                for (var cx = 0; cx < charWidth; cx++)
                {
                    var idx = cy * charWidth + cx;

                    var brailleChar = chars[idx];
                    byte fgR = cellFgR[idx], fgG = cellFgG[idx], fgB = cellFgB[idx];
                    byte bgR = smBgR[idx],   bgG = smBgG[idx],   bgB = smBgB[idx];

                    // Apply colour strategy to the mixed-cell case.
                    // For fully-ON or fully-OFF cells both colours are the same so strategy is a no-op.
                    if (fgR != bgR || fgG != bgG || fgB != bgB)
                    {
                        (fgR, fgG, fgB, bgR, bgG, bgB) = strategyLower switch
                        {
                            "complement" => ApplyComplementStrategy(fgR, fgG, fgB, bgR, bgG, bgB),
                            "warmcool"   => ApplyWarmCoolStrategy  (fgR, fgG, fgB, bgR, bgG, bgB),
                            "saturate"   => ApplySaturateStrategy  (fgR, fgG, fgB, bgR, bgG, bgB),
                            _            => (fgR, fgG, fgB, bgR, bgG, bgB)
                        };
                    }

                    // Temporal stability: snap both colours to a coarse grid to reduce flicker.
                    if (quantStep > 0)
                    {
                        fgR = (byte)(fgR / quantStep * quantStep);
                        fgG = (byte)(fgG / quantStep * quantStep);
                        fgB = (byte)(fgB / quantStep * quantStep);
                        bgR = (byte)(bgR / quantStep * quantStep);
                        bgG = (byte)(bgG / quantStep * quantStep);
                        bgB = (byte)(bgB / quantStep * quantStep);
                    }

                    // FG: mild brighten so sparse dots pop without colour distortion.
                    // BG: mild darken so the fill clearly recedes behind the dots.
                    (fgR, fgG, fgB) = BoostBrailleColor(fgR, fgG, fgB, 0.85f);
                    (bgR, bgG, bgB) = BoostBrailleColor(bgR, bgG, bgB, 1.2f);

                    // Correct inverted luminance: dots must always be the dominant colour.
                    var fgLum = 0.299f * fgR / 255f + 0.587f * fgG / 255f + 0.114f * fgB / 255f;
                    var bgLum = 0.299f * bgR / 255f + 0.587f * bgG / 255f + 0.114f * bgB / 255f;
                    if (bgLum > fgLum)
                    {
                        bgR = (byte)(bgR * 0.65f);
                        bgG = (byte)(bgG * 0.65f);
                        bgB = (byte)(bgB * 0.65f);
                    }

                    // Always emit an explicit ANSI cell — never a bare space — so the
                    // terminal's own background never shows through in dual-color mode.
                    var colorChanged = !hasLastColor ||
                        fgR != lastFgR || fgG != lastFgG || fgB != lastFgB ||
                        bgR != lastBgR || bgG != lastBgG || bgB != lastBgB;

                    if (colorChanged)
                    {
                        sb.Append("\x1b[38;2;");
                        sb.Append(fgR); sb.Append(';'); sb.Append(fgG); sb.Append(';'); sb.Append(fgB);
                        sb.Append(";48;2;");
                        sb.Append(bgR); sb.Append(';'); sb.Append(bgG); sb.Append(';'); sb.Append(bgB);
                        sb.Append('m');
                        lastFgR = fgR; lastFgG = fgG; lastFgB = fgB;
                        lastBgR = bgR; lastBgG = bgG; lastBgB = bgB;
                        hasLastColor = true;
                    }

                    sb.Append(brailleChar);
                }

                if (cy < charHeight - 1)
                {
                    sb.Append("\x1b[0m");
                    sb.AppendLine();
                    hasLastColor = false;
                }
            }

            sb.Append("\x1b[0m");
            return sb.ToString();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(chars);
            ArrayPool<byte>.Shared.Return(cellFgR);
            ArrayPool<byte>.Shared.Return(cellFgG);
            ArrayPool<byte>.Shared.Return(cellFgB);
            ArrayPool<byte>.Shared.Return(rawBgR);
            ArrayPool<byte>.Shared.Return(rawBgG);
            ArrayPool<byte>.Shared.Return(rawBgB);
            ArrayPool<byte>.Shared.Return(smBgR);
            ArrayPool<byte>.Shared.Return(smBgG);
            ArrayPool<byte>.Shared.Return(smBgB);
        }
    }

    /// <summary>
    ///     Apply a 3×3 Gaussian blur to the BG colour channel across the cell grid.
    ///     This is the anti-aliasing step for dual-color braille:
    ///     each cell's background colour becomes a weighted average of itself and its
    ///     8 neighbours, smoothing the hard transitions at object edges.
    ///
    ///     Unnormalised kernel weights:
    ///       1  2  1
    ///       2  4  2     (sum = 16; border cells normalise over available neighbours)
    ///       1  2  1
    /// </summary>
    private static void SmoothenBgChannel(
        byte[] srcR, byte[] srcG, byte[] srcB,
        byte[] dstR, byte[] dstG, byte[] dstB,
        int width, int height)
    {
        for (var cy = 0; cy < height; cy++)
        {
            for (var cx = 0; cx < width; cx++)
            {
                float totalR = 0, totalG = 0, totalB = 0, totalW = 0;

                for (var ky = -1; ky <= 1; ky++)
                {
                    var ny = cy + ky;
                    if (ny < 0 || ny >= height) continue;
                    var wy = ky == 0 ? 2f : 1f;

                    for (var kx = -1; kx <= 1; kx++)
                    {
                        var nx = cx + kx;
                        if (nx < 0 || nx >= width) continue;
                        var wx = kx == 0 ? 2f : 1f;
                        var w  = wx * wy;

                        var ni = ny * width + nx;
                        totalR += srcR[ni] * w;
                        totalG += srcG[ni] * w;
                        totalB += srcB[ni] * w;
                        totalW += w;
                    }
                }

                var i = cy * width + cx;
                dstR[i] = (byte)(totalR / totalW);
                dstG[i] = (byte)(totalG / totalW);
                dstB[i] = (byte)(totalB / totalW);
            }
        }
    }

    private static (byte fgR, byte fgG, byte fgB, byte bgR, byte bgG, byte bgB) ApplyComplementStrategy(
        byte fgR, byte fgG, byte fgB, byte _, byte __, byte ___)
    {
        RgbToHsl(fgR, fgG, fgB, out var h, out var s, out var l);
        h = (h + 0.5f) % 1.0f;
        var (bgR, bgG, bgB) = HslToRgb(h, s, l * 0.3f);
        return (fgR, fgG, fgB, bgR, bgG, bgB);
    }

    private static (byte fgR, byte fgG, byte fgB, byte bgR, byte bgG, byte bgB) ApplyWarmCoolStrategy(
        byte fgR, byte fgG, byte fgB, byte bgR, byte bgG, byte bgB)
    {
        var fgWarmth = (fgR / 255f + fgG / 255f * 0.5f - fgB / 255f) * 0.5f;
        var bgWarmth = (bgR / 255f + bgG / 255f * 0.5f - bgB / 255f) * 0.5f;
        if (fgWarmth < bgWarmth)
            return (bgR, bgG, bgB, fgR, fgG, fgB);
        return (fgR, fgG, fgB, bgR, bgG, bgB);
    }

    private static (byte fgR, byte fgG, byte fgB, byte bgR, byte bgG, byte bgB) ApplySaturateStrategy(
        byte fgR, byte fgG, byte fgB, byte bgR, byte bgG, byte bgB)
    {
        RgbToHsl(fgR, fgG, fgB, out var fgH, out var fgS, out var fgL);
        fgS = MathF.Min(1.0f, fgS * 1.3f);
        var (satFgR, satFgG, satFgB) = HslToRgb(fgH, fgS, fgL);

        RgbToHsl(bgR, bgG, bgB, out var bgH, out var bgS, out var bgL);
        bgS *= 0.4f;
        var (desatBgR, desatBgG, desatBgB) = HslToRgb(bgH, bgS, bgL);

        return (satFgR, satFgG, satFgB, desatBgR, desatBgG, desatBgB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RgbToHsl(byte r, byte g, byte b, out float h, out float s, out float l)
    {
        var rf = r / 255f;
        var gf = g / 255f;
        var bf = b / 255f;

        var max = MathF.Max(rf, MathF.Max(gf, bf));
        var min = MathF.Min(rf, MathF.Min(gf, bf));
        l = (max + min) / 2f;

        if (max == min) { h = s = 0f; return; }

        var d = max - min;
        s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

        if (max == rf) h = ((gf - bf) / d + (gf < bf ? 6f : 0f)) / 6f;
        else if (max == gf) h = ((bf - rf) / d + 2f) / 6f;
        else h = ((rf - gf) / d + 4f) / 6f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (byte r, byte g, byte b) HslToRgb(float h, float s, float l)
    {
        if (s == 0f) { var g = (byte)(l * 255f); return (g, g, g); }

        float HueToRgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        var q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        var p = 2f * l - q;
        return ((byte)(HueToRgb(p, q, h + 1f / 3f) * 255f),
                (byte)(HueToRgb(p, q, h) * 255f),
                (byte)(HueToRgb(p, q, h - 1f / 3f) * 255f));
    }

    /// <summary>
    ///     Pre-compute brightness and color data for all pixels.
    ///     This is faster than individual pixel access during rendering.
    ///     Applies color quantization if ColorCount is set.
    ///     Uses ArrayPool for buffer reuse to reduce GC pressure during video playback.
    /// </summary>
    private (float[] brightness, Rgba32[] colors) PrecomputePixelData(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;
        var totalPixels = width * height;

        // Reuse or allocate buffers from ArrayPool
        if (_brightnessBuffer == null || _lastBufferSize < totalPixels)
        {
            // Return old buffers if they exist
            if (_brightnessBuffer != null)
                ArrayPool<float>.Shared.Return(_brightnessBuffer);
            if (_colorsBuffer != null)
                ArrayPool<Rgba32>.Shared.Return(_colorsBuffer);

            _brightnessBuffer = ArrayPool<float>.Shared.Rent(totalPixels);
            _colorsBuffer = ArrayPool<Rgba32>.Shared.Rent(totalPixels);
            _lastBufferSize = totalPixels;
        }

        // These are guaranteed non-null after the if block above
        var brightness = _brightnessBuffer!;
        var colors = _colorsBuffer!;

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

        // Terminal background for alpha compositing — avoids black edge artifacts on light terminals
        var termBg = _options.TerminalBackground;
        var termBgR = termBg?.R ?? 0;
        var termBgG = termBg?.G ?? 0;
        var termBgB = termBg?.B ?? 0;
        var hasTermBg = termBg.HasValue && (termBgR > 0 || termBgG > 0 || termBgB > 0);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var rowOffset = y * width;
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];

                    // Composite semi-transparent pixels against terminal background.
                    // Without this, alpha < 255 pixels bleed black on light terminals.
                    if (hasTermBg && pixel.A < 255)
                    {
                        var a = pixel.A / 255f;
                        var ia = 1f - a;
                        pixel = new Rgba32(
                            (byte)(pixel.R * a + termBgR * ia),
                            (byte)(pixel.G * a + termBgG * ia),
                            (byte)(pixel.B * a + termBgB * ia),
                            255);
                    }

                    // Apply color quantization for palette reduction
                    if (quantize && quantStep > 1)
                        pixel = new Rgba32(
                            (byte)(pixel.R / quantStep * quantStep),
                            (byte)(pixel.G / quantStep * quantStep),
                            (byte)(pixel.B / quantStep * quantStep),
                            pixel.A);

                    brightness[rowOffset + x] = BrightnessHelper.GetBrightness(pixel);
                    colors[rowOffset + x] = pixel;
                }
            }
        });

        return (brightness, colors);
    }

    /// <summary>
    ///     Get min/max brightness from span - SIMD optimized when available.
    /// </summary>
    private static (float min, float max) GetBrightnessRangeFromSpan(ReadOnlySpan<float> brightness)
    {
        if (brightness.IsEmpty)
            return (0f, 1f);

        var len = brightness.Length;

        // Use SIMD if available and buffer is large enough
        if (Vector.IsHardwareAccelerated && len >= Vector<float>.Count * 2) return GetBrightnessRangeSimd(brightness);

        // Fallback: unrolled scalar loop
        var min = brightness[0];
        var max = brightness[0];
        var i = 1;

        // Unrolled loop - process 4 elements at a time
        for (; i + 3 < len; i += 4)
        {
            var v0 = brightness[i];
            var v1 = brightness[i + 1];
            var v2 = brightness[i + 2];
            var v3 = brightness[i + 3];

            var localMin = MathF.Min(MathF.Min(v0, v1), MathF.Min(v2, v3));
            var localMax = MathF.Max(MathF.Max(v0, v1), MathF.Max(v2, v3));

            if (localMin < min) min = localMin;
            if (localMax > max) max = localMax;
        }

        for (; i < len; i++)
        {
            var v = brightness[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        return min >= max ? (0f, 1f) : (min, max);
    }

    /// <summary>
    ///     SIMD-accelerated min/max calculation using Vector&lt;float&gt;.
    /// </summary>
    private static (float min, float max) GetBrightnessRangeSimd(ReadOnlySpan<float> brightness)
    {
        var vectorSize = Vector<float>.Count;
        var len = brightness.Length;
        var vectorizedLength = len - len % vectorSize;

        // Initialize with first vector
        var minVec = new Vector<float>(brightness);
        var maxVec = minVec;

        // Process vectors
        for (var i = vectorSize; i < vectorizedLength; i += vectorSize)
        {
            var vec = new Vector<float>(brightness.Slice(i));
            minVec = Vector.Min(minVec, vec);
            maxVec = Vector.Max(maxVec, vec);
        }

        // Reduce vectors to scalars
        var min = float.MaxValue;
        var max = float.MinValue;

        for (var i = 0; i < vectorSize; i++)
        {
            if (minVec[i] < min) min = minVec[i];
            if (maxVec[i] > max) max = maxVec[i];
        }

        // Handle remaining elements
        for (var i = vectorizedLength; i < len; i++)
        {
            var v = brightness[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        return min >= max ? (0f, 1f) : (min, max);
    }

    /// <summary>
    ///     Apply gamma correction for braille colors.
    ///     Minimal boost to keep colors true to source while compensating
    ///     slightly for braille dot sparsity.
    /// </summary>
    private static (byte r, byte g, byte b) BoostBrailleColor(byte r, byte g, byte b, float gamma)
    {
        // Convert to float for processing
        var rf = r / 255f;
        var gf = g / 255f;
        var bf = b / 255f;

        // Apply gamma correction
        if (gamma != 1.0f)
        {
            rf = MathF.Pow(rf, gamma);
            gf = MathF.Pow(gf, gamma);
            bf = MathF.Pow(bf, gamma);
        }

        // Gentle saturation boost to compensate for sparse dot coverage
        var maxC = MathF.Max(rf, MathF.Max(gf, bf));
        var minC = MathF.Min(rf, MathF.Min(gf, bf));
        var delta = maxC - minC;

        if (delta > 0.01f)
        {
            var mid = (maxC + minC) / 2f;
            const float satBoost = 1.08f; // 8% saturation boost (gentle)
            rf = mid + (rf - mid) * satBoost;
            gf = mid + (gf - mid) * satBoost;
            bf = mid + (bf - mid) * satBoost;
        }

        // Clamp and convert back
        return (
            (byte)Math.Clamp(rf * 255f, 0, 255),
            (byte)Math.Clamp(gf * 255f, 0, 255),
            (byte)Math.Clamp(bf * 255f, 0, 255)
        );
    }

    /// <summary>
    ///     Calculate optimal threshold using Otsu's method.
    ///     Maximizes between-class variance for best foreground/background separation.
    ///     Uses stackalloc for zero-allocation histogram.
    /// </summary>
    private static float CalculateOtsuThreshold(ReadOnlySpan<float> brightness)
    {
        const int histogramSize = 256;
        Span<int> histogram = stackalloc int[histogramSize];
        histogram.Clear(); // stackalloc doesn't zero-initialize
        var totalPixels = brightness.Length;

        // Build histogram
        for (var i = 0; i < totalPixels; i++)
        {
            var bin = (int)(brightness[i] * 255);
            bin = Math.Clamp(bin, 0, 255);
            histogram[bin]++;
        }

        // Calculate total mean
        var totalSum = 0f;
        for (var i = 0; i < histogramSize; i++)
            totalSum += i * histogram[i];

        var sumB = 0f;
        var wB = 0;
        var maxVariance = 0f;
        var optimalThreshold = 0;

        // Find threshold that maximizes between-class variance
        for (var t = 0; t < histogramSize; t++)
        {
            wB += histogram[t];
            if (wB == 0) continue;

            var wF = totalPixels - wB;
            if (wF == 0) break;

            sumB += t * histogram[t];

            var mB = sumB / wB;
            var mF = (totalSum - sumB) / wF;

            var variance = (float)wB * wF * (mB - mF) * (mB - mF);

            if (variance > maxVariance)
            {
                maxVariance = variance;
                optimalThreshold = t;
            }
        }

        return optimalThreshold / 255f;
    }

    /// <summary>
    ///     Apply Atkinson dithering - best for braille due to high contrast and reduced speckling.
    ///     Only propagates 6/8 of error (not full), producing cleaner binary output.
    ///     Developed by Bill Atkinson at Apple for MacPaint.
    ///     Uses ArrayPool for buffer reuse during video playback.
    /// </summary>
    private float[] ApplyAtkinsonDithering(float[] brightness, int width, int height, float threshold)
    {
        var bufferLength = brightness.Length;

        // Reuse dithering buffer from ArrayPool
        if (_ditheringBuffer == null || _ditheringBuffer.Length < bufferLength)
        {
            if (_ditheringBuffer != null)
                ArrayPool<float>.Shared.Return(_ditheringBuffer);
            _ditheringBuffer = ArrayPool<float>.Shared.Rent(bufferLength);
        }

        var result = _ditheringBuffer;

        // Copy directly — perceptual brightness is already in [0,1].
        // Avoid per-frame min/max normalization which causes brightness to jump between frames.
        for (var i = 0; i < bufferLength; i++)
            result[i] = brightness[i];

        // Atkinson error diffusion pattern:
        //       X   1   1
        //   1   1   1
        //       1
        // Divisor: 8 (but only distributes 6/8, rest is discarded)
        // This creates higher contrast by not fully propagating error
        var nextRowOffset = width;
        var nextNextRowOffset = width * 2;

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            var hasNextRow = y + 1 < height;
            var hasNextNextRow = y + 2 < height;

            for (var x = 0; x < width; x++)
            {
                var idx = rowOffset + x;
                var oldVal = result[idx];
                var newVal = oldVal > threshold ? 1f : 0f;
                result[idx] = newVal;
                var error = (oldVal - newVal) * 0.125f; // 1/8

                // Distribute to 6 neighbors (only 6/8 of error propagated)
                if (x + 1 < width)
                    result[idx + 1] += error;
                if (x + 2 < width)
                    result[idx + 2] += error;
                if (hasNextRow)
                {
                    var nextRowIdx = idx + nextRowOffset;
                    if (x > 0)
                        result[nextRowIdx - 1] += error;
                    result[nextRowIdx] += error;
                    if (x + 1 < width)
                        result[nextRowIdx + 1] += error;
                }

                if (hasNextNextRow)
                    result[idx + nextNextRowOffset] += error;
            }
        }

        return result;
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

            var delayMs = FrameTiming.GetFrameDelayMs(image.Frames[i]);
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