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

    // Full braille block (all 8 dots on) - ⣿
    private const char BrailleFull = '\u28FF';

    // Braille bit positions (standard Unicode encoding):
    // Pos:  1  4    Bits: 0x01  0x08
    //       2  5          0x02  0x10
    //       3  6          0x04  0x20
    //       7  8          0x40  0x80
    // Full block (⣿) = 0xFF

    // Row/column masks
    private const int MaskTopRow = 0x09; // positions 1,4
    private const int MaskRow2 = 0x12; // positions 2,5
    private const int MaskRow3 = 0x24; // positions 3,6
    private const int MaskBottomRow = 0xC0; // positions 7,8
    private const int MaskLeftCol = 0x47; // positions 1,2,3,7
    private const int MaskRightCol = 0xB8; // positions 4,5,6,8
    private const int MaskTopHalf = 0x1B; // rows 1-2
    private const int MaskBottomHalf = 0xE4; // rows 3-4

    // 6-dot patterns (missing one row or column - 2 holes)
    private const int PatternTopFilled = 0xFF ^ MaskBottomRow; // ⠿ 0x3F - bottom row empty
    private const int PatternBottomFilled = 0xFF ^ MaskTopRow; // ⣶ 0xF6 - top row empty
    private const int PatternLeftFilled = 0xFF ^ MaskRightCol; // ⡇ 0x47 - right col empty
    private const int PatternRightFilled = 0xFF ^ MaskLeftCol; // ⢸ 0xB8 - left col empty

    // 7-dot patterns (single corner missing - 1 hole) - minimal black
    private const int PatternNoTL = 0xFE; // ⣾ missing top-left (pos 1)
    private const int PatternNoTR = 0xF7; // ⣷ missing top-right (pos 4)
    private const int PatternNoBL = 0xBF; // ⢿ missing bottom-left (pos 7)
    private const int PatternNoBR = 0x7F; // ⡿ missing bottom-right (pos 8)

    // 6-dot diagonal patterns (two opposite corners missing)
    private const int PatternDiagTLBR = 0xFF ^ 0x01 ^ 0x80; // ⢾ missing TL and BR
    private const int PatternDiagTRBL = 0xFF ^ 0x08 ^ 0x40; // ⡷ missing TR and BL

    // Dot bit positions in braille character
    // Pattern:  1 4
    //           2 5
    //           3 6
    //           7 8
    // Index = dy * 2 + dx, so: [0,0]=0, [0,1]=1, [1,0]=2, [1,1]=3, [2,0]=4, [2,1]=5, [3,0]=6, [3,1]=7
    private static readonly int[] DotBits = { 0x01, 0x08, 0x02, 0x10, 0x04, 0x20, 0x40, 0x80 };

    // Pre-computed ANSI escape sequences for common greyscale values (0-255)
    // Saves string allocations for repeated colors
    private static readonly string[] GreyscaleEscapes = InitGreyscaleEscapes();

    // Braille patterns for different edge directions
    // Each pattern shows the "visible" side of an edge
    private static readonly int[] EdgePatterns =
    {
        0xFF, // No edge - full block
        0x1B, // Horizontal edge, top half: ⠛
        0xE4, // Horizontal edge, bottom half: ⣤
        0x49, // Vertical edge, left half: ⡉
        0xB6, // Vertical edge, right half: ⢶
        0x09, // Diagonal /, top-left: ⠉
        0xC0, // Diagonal /, bottom-right: ⣀
        0x06, // Diagonal \, top-right: ⠆
        0x90 // Diagonal \, bottom-left: ⢐
    };

    // Pre-computed braille density patterns for smooth gradients
    // Each pattern has a specific number of dots arranged aesthetically
    // Pattern[0] = 0 dots (empty), Pattern[8] = 8 dots (full)
    // Designed to create visually smooth transitions
    private static readonly int[] DensityPatterns =
    {
        0x00, // 0 dots: ⠀ (empty)
        0x40, // 1 dot:  ⡀ (bottom-left, least intrusive)
        0x44, // 2 dots: ⡄ (left column bottom half)
        0x64, // 3 dots: ⡤ (bottom half minus one)
        0xE4, // 4 dots: ⣤ (bottom half - symmetric)
        0xE5, // 5 dots: ⣥ (bottom half + top-left)
        0xF5, // 6 dots: ⣵ (missing top-right and one)
        0xFD, // 7 dots: ⣽ (missing one corner)
        0xFF // 8 dots: ⣿ (full block)
    };

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
        // Dot center positions in pixel coordinates within the 2x4 cell
        // Row 0: y = py + 0.5, Row 1: y = py + 1.5, Row 2: y = py + 2.5, Row 3: y = py + 3.5
        // Col 0: x = px + 0.5, Col 1: x = px + 1.5
        // Sampling radius relative to half-pixel spacing
        const float radius = 0.35f;

        for (var row = 0; row < 4; row++)
        {
            var centerY = py + row + 0.5f;
            for (var col = 0; col < 2; col++)
            {
                var centerX = px + col + 0.5f;
                var dotIdx = row * 2 + col;

                // Sample with concentric rings for accuracy
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

        var cells = new CellData[charHeight, charWidth];
        var invertMode = _options.Invert;

        // Parallel render to cell array
        Parallel.For(0, charHeight, cy =>
        {
            Span<float> target8 = stackalloc float[8];

            for (var cx = 0; cx < charWidth; cx++)
            {
                var px = cx * 2;
                var py = cy * 4;

                // Shape vector matching for braille character selection
                SampleBrailleCell(brightness, pixelWidth, pixelHeight, px, py, target8);

                if (invertMode)
                    for (var i = 0; i < 8; i++)
                        target8[i] = 1f - target8[i];

                var brailleChar = _brailleMap.FindBestMatch(target8);

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
                    var runStart = x;
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

        // For COLOR/GREYSCALE mode: show most dots, only hide truly dark pixels
        // For MONOCHROME mode: use Otsu's method for optimal separation
        var threshold = (_options.UseColor || _options.UseGreyscaleAnsi)
            ? (invertMode ? 0.15f : 0.85f)
            : CalculateOtsuThreshold(brightness);

        // Apply Atkinson dithering for smooth gradients
        brightness = ApplyAtkinsonDithering(brightness, pixelWidth, pixelHeight, threshold);
        // After dithering, values are 0 or 1, so use 0.5 threshold
        var result = RenderBrailleContent(brightness, colors, pixelWidth, pixelHeight, 0.5f);

        resized.Dispose();
        return result;
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
        var baseThreshold = (_options.UseColor || _options.UseGreyscaleAnsi)
            ? (invertMode ? 0.15f : 0.85f)
            : CalculateOtsuThreshold(baseBrightness);

        var frames = new List<BrailleFrame>(frameCount);

        for (var f = 0; f < frameCount; f++)
        {
            // Spread thresholds evenly around the base.
            // For 4 frames: biases are [-0.5, -0.167, +0.167, +0.5] * spread
            var bias = spread * ((float)f / Math.Max(1, frameCount - 1) - 0.5f);
            var threshold = Math.Clamp(baseThreshold + bias, 0.01f, 0.99f);

            // Apply Atkinson dithering with biased threshold (creates a new array)
            var dithered = ApplyAtkinsonDithering(baseBrightness, pixelWidth, pixelHeight, threshold);

            // Render to braille string using post-dithering threshold of 0.5
            var content = RenderBrailleContent(dithered, colors, pixelWidth, pixelHeight, 0.5f);
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
        int pixelWidth, int pixelHeight, float threshold)
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

                    // Sample 8 braille dot positions using concentric rings
                    SampleBrailleCell(brightness, pixelWidth, pixelHeight, px, py, target8);

                    // Invert coverage if needed (swap dot on/off semantics)
                    if (invertMode)
                        for (var i = 0; i < 8; i++)
                            target8[i] = 1f - target8[i];

                    // Find best matching braille character via shape vector matching
                    var brailleChar = _brailleMap.FindBestMatch(target8);

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

                    // Sample 8 braille dot positions using concentric rings
                    SampleBrailleCell(brightness, pixelWidth, pixelHeight, px, py, target8);

                    // Invert coverage if needed
                    if (invertMode)
                        for (var i = 0; i < 8; i++)
                            target8[i] = 1f - target8[i];

                    // Find best matching braille character via shape vector matching
                    var brailleChar = _brailleMap.FindBestMatch(target8);

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
            // Near 0 or PI - vertical edge
            return showTopOrLeft ? EdgePatterns[3] : EdgePatterns[4];

        if (absAngle > 3 * MathF.PI / 8 && absAngle < 5 * MathF.PI / 8)
            // Near PI/2 - horizontal edge
            return showTopOrLeft ? EdgePatterns[1] : EdgePatterns[2];

        if (angle > 0)
            // Positive angles - diagonal /
            return showTopOrLeft ? EdgePatterns[5] : EdgePatterns[6];

        // Negative angles - diagonal \
        return showTopOrLeft ? EdgePatterns[7] : EdgePatterns[8];
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
    ///     Get min/max brightness from pre-computed buffer.
    ///     Uses Span for bounds-check elimination.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (float min, float max) GetBrightnessRangeFromBuffer(float[] brightness)
    {
        return GetBrightnessRangeFromSpan(brightness.AsSpan());
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

        // Normalize brightness values to 0-1 range based on min/max
        var (min, max) = GetBrightnessRangeFromSpan(brightness.AsSpan());
        var range = max - min;
        if (range < 0.01f) range = 1f;
        var invRange = 1f / range; // Multiply is faster than divide in inner loop

        // Reuse dithering buffer from ArrayPool
        if (_ditheringBuffer == null || _ditheringBuffer.Length < bufferLength)
        {
            if (_ditheringBuffer != null)
                ArrayPool<float>.Shared.Return(_ditheringBuffer);
            _ditheringBuffer = ArrayPool<float>.Shared.Rent(bufferLength);
        }

        var result = _ditheringBuffer;

        // Copy and normalize in one pass
        for (var i = 0; i < bufferLength; i++)
            result[i] = (brightness[i] - min) * invRange;

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
    ///     Apply Floyd-Steinberg dithering - classic algorithm, good quality.
    /// </summary>
    private float[] ApplyFloydSteinbergDithering(float[] brightness, int width, int height, float threshold)
    {
        var bufferLength = brightness.Length;
        var (min, max) = GetBrightnessRangeFromBuffer(brightness);
        var range = max - min;
        if (range < 0.01f) range = 1f;
        var invRange = 1f / range; // Multiply is faster than divide in inner loop

        // Reuse dithering buffer from ArrayPool (shared with Atkinson)
        if (_ditheringBuffer == null || _ditheringBuffer.Length < bufferLength)
        {
            if (_ditheringBuffer != null)
                ArrayPool<float>.Shared.Return(_ditheringBuffer);
            _ditheringBuffer = ArrayPool<float>.Shared.Rent(bufferLength);
        }

        var result = _ditheringBuffer;
        for (var i = 0; i < bufferLength; i++) result[i] = (brightness[i] - min) * invRange;

        // Floyd-Steinberg:
        //       X   7
        //   3   5   1
        // Divisor: 16
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var idx = y * width + x;
            var oldVal = result[idx];
            var newVal = oldVal > threshold ? 1f : 0f;
            result[idx] = newVal;
            var error = oldVal - newVal;

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
    ///     Uses ProcessPixelRows for optimal performance.
    /// </summary>
    private static (float min, float max) CalculateBrightnessRange(Image<Rgba32> image)
    {
        var min = 1f;
        var max = 0f;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var brightness = BrightnessHelper.GetBrightness(row[x]);
                    if (brightness < min) min = brightness;
                    if (brightness > max) max = brightness;
                }
            }
        });

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