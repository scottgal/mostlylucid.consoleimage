// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Color block renderer for high-fidelity colored terminal output

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;

namespace ConsoleImage.Core;

/// <summary>
/// Renders images as colored blocks using Unicode half-block characters.
/// Each character cell displays two pixels vertically using upper/lower half-blocks
/// with separate foreground and background colors, effectively doubling resolution.
/// </summary>
public class ColorBlockRenderer : IDisposable
{
    private readonly RenderOptions _options;
    private bool _disposed;

    // Unicode half-block characters
    private const char UpperHalfBlock = '▀';  // Upper half solid
    private const char LowerHalfBlock = '▄';  // Lower half solid
    private const char FullBlock = '█';       // Full block
    private const char Space = ' ';           // Empty

    public ColorBlockRenderer(RenderOptions? options = null)
    {
        _options = options ?? RenderOptions.Default;
    }

    /// <summary>
    /// Render image file to colored block output
    /// </summary>
    public string RenderFile(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderImage(image);
    }

    /// <summary>
    /// Render image stream to colored block output
    /// </summary>
    public string RenderStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderImage(image);
    }

    /// <summary>
    /// Render image to colored block output
    /// </summary>
    public string RenderImage(Image<Rgba32> image)
    {
        // For half-block rendering, each character represents 2 pixels vertically
        // Calculate output dimensions in PIXELS (not characters)
        var (pixelWidth, pixelHeight) = CalculatePixelDimensions(image.Width, image.Height);

        // Ensure even height for half-block pairing
        if (pixelHeight % 2 != 0) pixelHeight++;

        // Resize image to target pixel dimensions
        using var resized = image.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(pixelWidth, pixelHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            });
        });

        return RenderPixels(resized);
    }

    /// <summary>
    /// Calculate output dimensions in PIXELS for half-block rendering.
    /// Half-blocks give 2x vertical resolution, so we need to account for this differently
    /// than regular ASCII rendering.
    /// </summary>
    private (int width, int height) CalculatePixelDimensions(int imageWidth, int imageHeight)
    {
        float imageAspect = (float)imageWidth / imageHeight;

        // For half-blocks: each character cell displays 2 vertical pixels
        // So if we want N character rows, we need N*2 pixel rows
        // Character cells are typically 2:1 (height:width), but half-blocks make them effectively 1:1
        // So we use aspect ratio of 1.0 for half-block rendering

        int maxPixelWidth = _options.Width ?? _options.MaxWidth;
        int maxPixelHeight = (_options.Height ?? _options.MaxHeight) * 2; // *2 for half-blocks

        int outputWidth, outputHeight;

        if (_options.Width.HasValue)
        {
            // User specified width - calculate height to maintain aspect ratio
            outputWidth = _options.Width.Value;
            outputHeight = (int)(outputWidth / imageAspect);
        }
        else if (_options.Height.HasValue)
        {
            // User specified height in character rows - convert to pixels
            outputHeight = _options.Height.Value * 2;
            outputWidth = (int)(outputHeight * imageAspect);
        }
        else
        {
            // Auto-calculate from max dimensions
            if (imageAspect > (float)maxPixelWidth / maxPixelHeight)
            {
                outputWidth = maxPixelWidth;
                outputHeight = (int)(outputWidth / imageAspect);
            }
            else
            {
                outputHeight = maxPixelHeight;
                outputWidth = (int)(outputHeight * imageAspect);
            }
        }

        // Clamp to max dimensions
        if (outputWidth > maxPixelWidth)
        {
            outputWidth = maxPixelWidth;
            outputHeight = (int)(outputWidth / imageAspect);
        }
        if (outputHeight > maxPixelHeight)
        {
            outputHeight = maxPixelHeight;
            outputWidth = (int)(outputHeight * imageAspect);
        }

        return (Math.Max(1, outputWidth), Math.Max(2, outputHeight));
    }

    private string RenderPixels(Image<Rgba32> image)
    {
        var sb = new StringBuilder();
        int charRows = image.Height / 2;

        for (int row = 0; row < charRows; row++)
        {
            int y1 = row * 2;       // Upper pixel row
            int y2 = row * 2 + 1;   // Lower pixel row

            for (int x = 0; x < image.Width; x++)
            {
                var upper = image[x, y1];
                var lower = image[x, y2];

                AppendColoredBlock(sb, upper, lower);
            }

            sb.Append("\x1b[0m"); // Reset at end of line
            if (row < charRows - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendColoredBlock(StringBuilder sb, Rgba32 upper, Rgba32 lower)
    {
        // Determine transparency
        bool upperTransparent = upper.A < 128;
        bool lowerTransparent = lower.A < 128;

        if (upperTransparent && lowerTransparent)
        {
            // Both transparent - just space
            sb.Append(' ');
            return;
        }

        if (upperTransparent)
        {
            // Only lower visible - use lower half block with foreground color
            sb.Append($"\x1b[38;2;{lower.R};{lower.G};{lower.B}m{LowerHalfBlock}");
            return;
        }

        if (lowerTransparent)
        {
            // Only upper visible - use upper half block with foreground color
            sb.Append($"\x1b[38;2;{upper.R};{upper.G};{upper.B}m{UpperHalfBlock}");
            return;
        }

        // Both visible - use upper half block with upper as foreground, lower as background
        // The upper half block (▀) shows the foreground color in top half, background in bottom half
        sb.Append($"\x1b[38;2;{upper.R};{upper.G};{upper.B};48;2;{lower.R};{lower.G};{lower.B}m{UpperHalfBlock}");
    }

    /// <summary>
    /// Render animated GIF to list of colored block frames
    /// </summary>
    public IReadOnlyList<ColorBlockFrame> RenderGif(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderGifFrames(image);
    }

    /// <summary>
    /// Render animated GIF stream to list of colored block frames
    /// </summary>
    public IReadOnlyList<ColorBlockFrame> RenderGifStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderGifFrames(image);
    }

    private List<ColorBlockFrame> RenderGifFrames(Image<Rgba32> image)
    {
        var frames = new List<ColorBlockFrame>();

        // Determine frame step for sampling (skip frames for efficiency)
        int frameStep = Math.Max(1, _options.FrameSampleRate);

        for (int i = 0; i < image.Frames.Count; i += frameStep)
        {
            using var frameImage = image.Frames.CloneFrame(i);

            var metadata = image.Frames[i].Metadata.GetGifMetadata();

            int delayMs = 100;
            if (metadata.FrameDelay > 0)
            {
                delayMs = metadata.FrameDelay * 10;
            }
            // Adjust delay to account for skipped frames
            delayMs = (int)((delayMs * frameStep) / _options.AnimationSpeedMultiplier);

            string content = RenderImage(frameImage);
            frames.Add(new ColorBlockFrame(content, delayMs));
        }

        return frames;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A single frame of colored block output
/// </summary>
public class ColorBlockFrame
{
    public string Content { get; }
    public int DelayMs { get; }

    public ColorBlockFrame(string content, int delayMs)
    {
        Content = content;
        DelayMs = delayMs;
    }

    public override string ToString() => Content;
}
