using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace ConsoleImage.Core.ProtocolRenderers;

/// <summary>
/// Renders images using the Kitty Graphics Protocol.
/// Supported by Kitty terminal and increasingly other terminals.
///
/// Protocol: APC G [key=value pairs] ; [payload] ST
/// Where APC is \x1b_G and ST is \x1b\\
/// </summary>
public class KittyRenderer : IDisposable
{
    private readonly RenderOptions _options;
    private bool _disposed;

    // Maximum chunk size for base64 data (4096 bytes of base64 = 3072 raw bytes)
    private const int MaxChunkSize = 4096;

    public KittyRenderer(RenderOptions? options = null)
    {
        _options = options ?? RenderOptions.Default;
    }

    /// <summary>
    /// Render an image file using Kitty protocol.
    /// </summary>
    public string RenderFile(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image stream using Kitty protocol.
    /// </summary>
    public string RenderStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image using Kitty protocol.
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

            // Build Kitty escape sequences (chunked for large images)
            return BuildKittySequence(imageBytes);
        }
        finally
        {
            if (shouldDispose)
                resized.Dispose();
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

    private Image<Rgba32> ResizeIfNeeded(Image<Rgba32> image)
    {
        int maxWidth = _options.Width ?? _options.MaxWidth;
        int maxHeight = _options.Height ?? _options.MaxHeight;

        // Kitty uses terminal cells, assume ~8x16 pixels per cell
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

    private static string BuildKittySequence(byte[] imageBytes)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        var sb = new System.Text.StringBuilder();

        if (base64.Length <= MaxChunkSize)
        {
            // Single chunk - simpler case
            // a=T: action=transmit and display
            // f=100: format=PNG
            // t=d: transmission=direct (base64 data follows)
            sb.Append($"\x1b_Ga=T,f=100,t=d;{base64}\x1b\\");
        }
        else
        {
            // Multiple chunks needed
            int offset = 0;
            bool first = true;

            while (offset < base64.Length)
            {
                int remaining = base64.Length - offset;
                int chunkSize = Math.Min(MaxChunkSize, remaining);
                string chunk = base64.Substring(offset, chunkSize);
                bool isLast = offset + chunkSize >= base64.Length;

                if (first)
                {
                    // First chunk: a=T (transmit+display), m=1 (more chunks follow)
                    sb.Append($"\x1b_Ga=T,f=100,t=d,m={(!isLast ? 1 : 0)};{chunk}\x1b\\");
                    first = false;
                }
                else
                {
                    // Continuation chunk: m=0 for last, m=1 for more
                    sb.Append($"\x1b_Gm={(!isLast ? 1 : 0)};{chunk}\x1b\\");
                }

                offset += chunkSize;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Check if Kitty protocol is supported in the current terminal.
    /// </summary>
    public static bool IsSupported() => TerminalCapabilities.SupportsKitty();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
