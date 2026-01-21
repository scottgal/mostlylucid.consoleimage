using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;

namespace ConsoleImage.Core.ProtocolRenderers;

/// <summary>
/// Renders images using the Sixel graphics protocol.
/// Sixel is an older protocol but still supported by many terminals.
///
/// Protocol: DCS P1;P2;P3 q [sixel data] ST
/// Where DCS is \x1bP and ST is \x1b\\
/// </summary>
public class SixelRenderer : IDisposable
{
    private readonly RenderOptions _options;
    private bool _disposed;

    // Sixel uses 6 vertical pixels per row (hence "sixel")
    private const int SixelHeight = 6;

    // Default palette size (256 colors for VT340+ compatibility)
    private const int PaletteSize = 256;

    public SixelRenderer(RenderOptions? options = null)
    {
        _options = options ?? RenderOptions.Default;
    }

    /// <summary>
    /// Render an image file using Sixel protocol.
    /// </summary>
    public string RenderFile(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image stream using Sixel protocol.
    /// </summary>
    public string RenderStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image using Sixel protocol.
    /// </summary>
    public string RenderImage(Image<Rgba32> image)
    {
        // Resize and quantize the image
        var processed = ProcessImage(image);
        var shouldDispose = processed.image != image;

        try
        {
            return BuildSixelSequence(processed.image, processed.palette);
        }
        finally
        {
            if (shouldDispose)
                processed.image.Dispose();
        }
    }

    /// <summary>
    /// Render image and write directly to console.
    /// </summary>
    public void RenderToConsole(Image<Rgba32> image)
    {
        var output = RenderImage(image);
        Console.Write(output);
    }

    private (Image<Rgba32> image, Rgba32[] palette) ProcessImage(Image<Rgba32> image)
    {
        int maxWidth = _options.Width ?? _options.MaxWidth;
        int maxHeight = _options.Height ?? _options.MaxHeight;

        // Sixel pixels are roughly 2x the size of character cells
        int targetWidth = maxWidth * 2;
        int targetHeight = maxHeight * 4;

        // Ensure height is multiple of 6 for sixel rows
        targetHeight = (targetHeight / SixelHeight) * SixelHeight;
        if (targetHeight == 0) targetHeight = SixelHeight;

        // Scale image to fit
        float scale = Math.Min(
            (float)targetWidth / image.Width,
            (float)targetHeight / image.Height
        );

        int newWidth = Math.Max(1, (int)(image.Width * scale));
        int newHeight = Math.Max(1, (int)(image.Height * scale));

        // Round height to multiple of 6
        newHeight = ((newHeight + SixelHeight - 1) / SixelHeight) * SixelHeight;

        var resized = image.Clone(ctx => ctx.Resize(newWidth, newHeight));

        // Quantize to palette (simple median-cut-ish algorithm)
        var palette = ExtractPalette(resized, PaletteSize);

        return (resized, palette);
    }

    private static Rgba32[] ExtractPalette(Image<Rgba32> image, int maxColors)
    {
        // Simple color extraction - collect unique colors up to limit
        var colors = new HashSet<int>();

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (var pixel in row)
                {
                    if (colors.Count >= maxColors * 2) break;

                    // Reduce color precision for better quantization
                    int r = (pixel.R >> 4) << 4;
                    int g = (pixel.G >> 4) << 4;
                    int b = (pixel.B >> 4) << 4;
                    colors.Add((r << 16) | (g << 8) | b);
                }
            }
        });

        // Convert to palette array (take first maxColors)
        return colors.Take(maxColors)
            .Select(c => new Rgba32((byte)(c >> 16), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF), 255))
            .ToArray();
    }

    private int FindNearestPaletteIndex(Rgba32 pixel, Rgba32[] palette)
    {
        int bestIndex = 0;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < palette.Length; i++)
        {
            int dr = pixel.R - palette[i].R;
            int dg = pixel.G - palette[i].G;
            int db = pixel.B - palette[i].B;
            int distance = dr * dr + dg * dg + db * db;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private string BuildSixelSequence(Image<Rgba32> image, Rgba32[] palette)
    {
        var sb = new StringBuilder();

        // Start sixel sequence
        // DCS parameters: P1=aspect ratio numerator, P2=0, P3=0
        // We use 1;1 for square pixels
        sb.Append("\x1bPq");

        // Define palette
        // Format: #Pc;Pu;Px;Py;Pz where Pc=color number, Pu=2 for RGB, Px/Py/Pz are 0-100
        for (int i = 0; i < palette.Length; i++)
        {
            int r = (palette[i].R * 100) / 255;
            int g = (palette[i].G * 100) / 255;
            int b = (palette[i].B * 100) / 255;
            sb.Append($"#{i};2;{r};{g};{b}");
        }

        // Create indexed image
        int[,] indexed = new int[image.Height, image.Width];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    indexed[y, x] = FindNearestPaletteIndex(row[x], palette);
                }
            }
        });

        // Generate sixel data
        // Each sixel row represents 6 vertical pixels
        for (int sixelRow = 0; sixelRow < image.Height / SixelHeight; sixelRow++)
        {
            int y0 = sixelRow * SixelHeight;

            // For each color in the palette that appears in this row
            for (int colorIndex = 0; colorIndex < palette.Length; colorIndex++)
            {
                var rowData = new StringBuilder();
                bool hasColor = false;
                int repeatCount = 0;
                int lastSixelValue = -1;

                for (int x = 0; x < image.Width; x++)
                {
                    // Build sixel value (6 bits, one per vertical pixel)
                    int sixelValue = 0;
                    for (int dy = 0; dy < SixelHeight; dy++)
                    {
                        int y = y0 + dy;
                        if (y < image.Height && indexed[y, x] == colorIndex)
                        {
                            sixelValue |= (1 << dy);
                        }
                    }

                    if (sixelValue > 0)
                        hasColor = true;

                    // Run-length encoding
                    if (sixelValue == lastSixelValue)
                    {
                        repeatCount++;
                    }
                    else
                    {
                        if (lastSixelValue >= 0)
                        {
                            AppendSixelData(rowData, lastSixelValue, repeatCount);
                        }
                        lastSixelValue = sixelValue;
                        repeatCount = 1;
                    }
                }

                // Flush last run
                if (lastSixelValue >= 0)
                {
                    AppendSixelData(rowData, lastSixelValue, repeatCount);
                }

                // Only output if this color appears in the row
                if (hasColor)
                {
                    sb.Append($"#{colorIndex}");
                    sb.Append(rowData);
                    sb.Append('$'); // Carriage return (stay on same sixel row)
                }
            }

            sb.Append('-'); // Move to next sixel row
        }

        // End sixel sequence
        sb.Append("\x1b\\");

        return sb.ToString();
    }

    private static void AppendSixelData(StringBuilder sb, int sixelValue, int count)
    {
        char sixelChar = (char)(sixelValue + 63); // Sixel data is ASCII 63-126

        if (count <= 3)
        {
            // Just repeat the character
            sb.Append(new string(sixelChar, count));
        }
        else
        {
            // Use repeat introducer: !count;char
            sb.Append($"!{count}{sixelChar}");
        }
    }

    /// <summary>
    /// Check if Sixel protocol is supported in the current terminal.
    /// </summary>
    public static bool IsSupported() => TerminalCapabilities.SupportsSixel();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
