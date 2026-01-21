using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace ConsoleImage.Core.ProtocolRenderers;

/// <summary>
/// Renders images using the iTerm2 Inline Images Protocol.
/// Supported by iTerm2, WezTerm, Mintty, and other compatible terminals.
///
/// Protocol: OSC 1337 ; File=[args] : [base64 data] ST
/// Where ST is \a (BEL) or \x1b\\
/// </summary>
public class ITerm2Renderer : IDisposable
{
    private readonly RenderOptions _options;
    private bool _disposed;

    public ITerm2Renderer(RenderOptions? options = null)
    {
        _options = options ?? RenderOptions.Default;
    }

    /// <summary>
    /// Render an image file using iTerm2 protocol.
    /// </summary>
    public string RenderFile(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image stream using iTerm2 protocol.
    /// </summary>
    public string RenderStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image using iTerm2 protocol.
    /// </summary>
    public string RenderImage(Image<Rgba32> image)
    {
        // Optionally resize if max dimensions are set
        var resized = ResizeIfNeeded(image);
        var shouldDispose = resized != image;

        try
        {
            // Convert image to PNG bytes
            using var ms = new MemoryStream();
            resized.Save(ms, new PngEncoder());
            var imageBytes = ms.ToArray();

            // Build iTerm2 escape sequence
            return BuildITerm2Sequence(imageBytes, resized.Width, resized.Height);
        }
        finally
        {
            if (shouldDispose)
                resized.Dispose();
        }
    }

    /// <summary>
    /// Render image and write directly to console with optional progress.
    /// </summary>
    public void RenderToConsole(Image<Rgba32> image)
    {
        var output = RenderImage(image);
        Console.Write(output);
    }

    private Image<Rgba32> ResizeIfNeeded(Image<Rgba32> image)
    {
        int maxWidth = _options.Width ?? _options.MaxWidth;
        int maxHeight = _options.Height ?? _options.MaxHeight;

        // iTerm2 uses pixels, so we don't need character aspect ratio conversion
        // But we do need to respect terminal cell dimensions
        // Assume each cell is roughly 8x16 pixels for standard fonts
        int termWidthPixels = maxWidth * 8;
        int termHeightPixels = maxHeight * 16;

        if (image.Width <= termWidthPixels && image.Height <= termHeightPixels)
            return image;

        float scale = Math.Min(
            (float)termWidthPixels / image.Width,
            (float)termHeightPixels / image.Height
        );

        int newWidth = Math.Max(1, (int)(image.Width * scale));
        int newHeight = Math.Max(1, (int)(image.Height * scale));

        return image.Clone(ctx => ctx.Resize(newWidth, newHeight));
    }

    private static string BuildITerm2Sequence(byte[] imageBytes, int width, int height)
    {
        var base64 = Convert.ToBase64String(imageBytes);

        // OSC 1337 ; File=[args] : [base64] BEL
        // Args: size=N;width=Npx;height=Npx;preserveAspectRatio=1;inline=1
        var args = $"size={imageBytes.Length};width={width}px;height={height}px;preserveAspectRatio=1;inline=1";

        return $"\x1b]1337;File={args}:{base64}\a";
    }

    /// <summary>
    /// Check if iTerm2 protocol is supported in the current terminal.
    /// </summary>
    public static bool IsSupported() => TerminalCapabilities.SupportsITerm2();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
