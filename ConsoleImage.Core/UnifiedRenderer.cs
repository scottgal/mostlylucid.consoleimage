using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ConsoleImage.Core.ProtocolRenderers;

namespace ConsoleImage.Core;

/// <summary>
/// Unified image renderer that can automatically select the best protocol
/// for the current terminal, or use a specific protocol if requested.
/// </summary>
public class UnifiedRenderer : IDisposable
{
    private readonly RenderOptions _options;
    private readonly TerminalProtocol _protocol;
    private bool _disposed;

    /// <summary>
    /// Create a renderer that automatically detects the best protocol.
    /// </summary>
    public UnifiedRenderer(RenderOptions? options = null)
        : this(TerminalCapabilities.DetectBestProtocol(), options)
    {
    }

    /// <summary>
    /// Create a renderer using a specific protocol.
    /// </summary>
    public UnifiedRenderer(TerminalProtocol protocol, RenderOptions? options = null)
    {
        _protocol = protocol;
        _options = options ?? RenderOptions.Default;
    }

    /// <summary>
    /// The protocol being used for rendering.
    /// </summary>
    public TerminalProtocol Protocol => _protocol;

    /// <summary>
    /// Render an image file.
    /// </summary>
    public string RenderFile(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image from a stream.
    /// </summary>
    public string RenderStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderImage(image);
    }

    /// <summary>
    /// Render an image using the configured protocol.
    /// </summary>
    public string RenderImage(Image<Rgba32> image)
    {
        return _protocol switch
        {
            TerminalProtocol.Kitty => RenderWithKitty(image),
            TerminalProtocol.ITerm2 => RenderWithITerm2(image),
            TerminalProtocol.Sixel => RenderWithSixel(image),
            TerminalProtocol.ColorBlocks => RenderWithColorBlocks(image),
            TerminalProtocol.Braille => RenderWithBraille(image),
            _ => RenderWithAscii(image)
        };
    }

    /// <summary>
    /// Render a local file or remote URL.
    /// </summary>
    public string RenderAny(string pathOrUrl)
    {
        if (UrlHelper.IsUrl(pathOrUrl))
        {
            using var stream = UrlHelper.Download(pathOrUrl);
            return RenderStream(stream);
        }
        return RenderFile(pathOrUrl);
    }

    /// <summary>
    /// Render a local file or remote URL asynchronously.
    /// </summary>
    public async Task<string> RenderAnyAsync(
        string pathOrUrl,
        Action<long, long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (UrlHelper.IsUrl(pathOrUrl))
        {
            using var stream = await UrlHelper.DownloadAsync(pathOrUrl, progress, cancellationToken);
            return RenderStream(stream);
        }
        return RenderFile(pathOrUrl);
    }

    private string RenderWithKitty(Image<Rgba32> image)
    {
        using var renderer = new KittyRenderer(_options);
        return renderer.RenderImage(image);
    }

    private string RenderWithITerm2(Image<Rgba32> image)
    {
        using var renderer = new ITerm2Renderer(_options);
        return renderer.RenderImage(image);
    }

    private string RenderWithSixel(Image<Rgba32> image)
    {
        using var renderer = new SixelRenderer(_options);
        return renderer.RenderImage(image);
    }

    private string RenderWithColorBlocks(Image<Rgba32> image)
    {
        using var renderer = new ColorBlockRenderer(_options);
        return renderer.RenderImage(image);
    }

    private string RenderWithBraille(Image<Rgba32> image)
    {
        using var renderer = new BrailleRenderer(_options);
        return renderer.RenderImage(image);
    }

    private string RenderWithAscii(Image<Rgba32> image)
    {
        using var renderer = new AsciiRenderer(_options);
        var frame = renderer.RenderImage(image);
        return _options.UseColor ? frame.ToAnsiString() : frame.ToString();
    }

    /// <summary>
    /// Get a list of all available protocols with their support status.
    /// </summary>
    public static IReadOnlyList<ProtocolInfo> ListProtocols()
    {
        return Enum.GetValues<TerminalProtocol>()
            .Select(p => new ProtocolInfo(
                p,
                TerminalCapabilities.SupportsProtocol(p),
                GetProtocolDescription(p)
            ))
            .ToList();
    }

    /// <summary>
    /// Get a formatted string listing all protocols and their status.
    /// </summary>
    public static string GetProtocolList()
    {
        var protocols = ListProtocols();
        var best = TerminalCapabilities.DetectBestProtocol();

        var lines = new List<string>
        {
            "Available Rendering Modes:",
            ""
        };

        foreach (var p in protocols)
        {
            var marker = p.Protocol == best ? " (auto)" : "";
            var status = p.IsSupported ? "Supported" : "Not supported";
            lines.Add($"  {p.Protocol,-12} {status,-14}{marker}");
            lines.Add($"               {p.Description}");
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetProtocolDescription(TerminalProtocol protocol)
    {
        return protocol switch
        {
            TerminalProtocol.Ascii =>
                "Classic ASCII art using characters. Works everywhere.",
            TerminalProtocol.ColorBlocks =>
                "Unicode half-blocks with 24-bit ANSI colors. 2x vertical resolution.",
            TerminalProtocol.Braille =>
                "Unicode braille characters. 2x4 dots per cell, highest text-based resolution.",
            TerminalProtocol.Sixel =>
                "Sixel graphics protocol. Pixel-perfect, supported by xterm and others.",
            TerminalProtocol.ITerm2 =>
                "iTerm2 inline images. Full-color images in iTerm2, WezTerm, Mintty.",
            TerminalProtocol.Kitty =>
                "Kitty graphics protocol. Modern protocol with animation support.",
            _ => "Unknown protocol."
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Information about a rendering protocol.
/// </summary>
public record ProtocolInfo(
    TerminalProtocol Protocol,
    bool IsSupported,
    string Description
);
