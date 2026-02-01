// UnifiedRenderer - Single entry point for all rendering operations
// Eliminates duplicate code paths for different modes and output formats

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
///     Unified renderer that handles all render modes with a consistent interface.
/// </summary>
public class UnifiedRenderer : IDisposable
{
    private readonly RenderOptions _options;
    private bool _disposed;

    public UnifiedRenderer(RenderOptions options, RenderMode mode)
    {
        ConsoleHelper.EnableAnsiSupport();
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Mode = mode;
    }

    public RenderMode Mode { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Render a single image file to a frame.
    /// </summary>
    public IAnimationFrame RenderFile(string path)
    {
        return Mode switch
        {
            RenderMode.Braille => new BrailleRenderer(_options).RenderFileToFrame(path),
            RenderMode.ColorBlocks => new ColorBlockRenderer(_options).RenderFileToFrame(path),
            _ => RenderAsciiFile(path)
        };
    }

    /// <summary>
    ///     Render a single image to a frame.
    /// </summary>
    public IAnimationFrame RenderImage(Image<Rgba32> image)
    {
        return Mode switch
        {
            RenderMode.Braille => new BrailleFrame(new BrailleRenderer(_options).RenderImage(image), 0),
            RenderMode.ColorBlocks => new ColorBlockFrame(new ColorBlockRenderer(_options).RenderImage(image), 0),
            _ => RenderAsciiImage(image)
        };
    }

    /// <summary>
    ///     Render an animated GIF file to a list of frames.
    /// </summary>
    public List<IAnimationFrame> RenderGif(string path)
    {
        return Mode switch
        {
            RenderMode.Braille => RenderBrailleGif(path),
            RenderMode.ColorBlocks => RenderBlocksGif(path),
            _ => RenderAsciiGif(path)
        };
    }

    private IAnimationFrame RenderAsciiFile(string path)
    {
        using var renderer = new AsciiRenderer(_options);
        var frame = renderer.RenderFile(path);
        var darkThreshold = _options.Invert ? _options.DarkTerminalBrightnessThreshold : null;
        var lightThreshold = !_options.Invert ? _options.LightTerminalBrightnessThreshold : null;
        return new GenericFrame(
            _options.UseColor ? frame.ToAnsiString(darkThreshold, lightThreshold) : frame.ToString(),
            0);
    }

    private IAnimationFrame RenderAsciiImage(Image<Rgba32> image)
    {
        using var renderer = new AsciiRenderer(_options);
        var frame = renderer.RenderImage(image);
        var darkThreshold = _options.Invert ? _options.DarkTerminalBrightnessThreshold : null;
        var lightThreshold = !_options.Invert ? _options.LightTerminalBrightnessThreshold : null;
        return new GenericFrame(
            _options.UseColor ? frame.ToAnsiString(darkThreshold, lightThreshold) : frame.ToString(),
            0);
    }

    private List<IAnimationFrame> RenderBrailleGif(string path)
    {
        using var renderer = new BrailleRenderer(_options);
        return renderer.RenderGifFrames(path).Cast<IAnimationFrame>().ToList();
    }

    private List<IAnimationFrame> RenderBlocksGif(string path)
    {
        using var renderer = new ColorBlockRenderer(_options);
        return renderer.RenderGifFrames(path).Cast<IAnimationFrame>().ToList();
    }

    private List<IAnimationFrame> RenderAsciiGif(string path)
    {
        using var renderer = new AsciiRenderer(_options);
        var frames = renderer.RenderGif(path);
        var darkThreshold = _options.Invert ? _options.DarkTerminalBrightnessThreshold : null;
        var lightThreshold = !_options.Invert ? _options.LightTerminalBrightnessThreshold : null;

        return frames.Select(f => (IAnimationFrame)new GenericFrame(
            _options.UseColor ? f.ToAnsiString(darkThreshold, lightThreshold) : f.ToString(),
            f.DelayMs)).ToList();
    }

    /// <summary>
    ///     Save frames to a document (cidz or json).
    /// </summary>
    public static async Task SaveToDocumentAsync(
        IReadOnlyList<IAnimationFrame> frames,
        RenderOptions options,
        RenderMode mode,
        string outputPath,
        string? sourceFile = null,
        CancellationToken ct = default)
    {
        var doc = new ConsoleImageDocument
        {
            RenderMode = mode.ToString(),
            SourceFile = sourceFile,
            Settings = DocumentRenderSettings.FromRenderOptions(options)
        };

        foreach (var frame in frames)
        {
            var lines = frame.Content.Split('\n');
            doc.Frames.Add(new DocumentFrame
            {
                Content = frame.Content,
                DelayMs = frame.DelayMs,
                Width = lines.Length > 0 ? lines[0].Length : 0,
                Height = lines.Length
            });
        }

        await doc.SaveAsync(outputPath, ct);
    }

    /// <summary>
    ///     Save frames to an animated GIF.
    /// </summary>
    public static async Task SaveToGifAsync(
        IReadOnlyList<IAnimationFrame> frames,
        RenderMode mode,
        string outputPath,
        GifWriterOptions? options = null,
        CancellationToken ct = default)
    {
        using var writer = new GifWriter(options ?? new GifWriterOptions());

        foreach (var frame in frames)
            switch (mode)
            {
                case RenderMode.Braille when frame is BrailleFrame bf:
                    writer.AddBrailleFrame(bf, bf.DelayMs);
                    break;
                case RenderMode.ColorBlocks when frame is ColorBlockFrame cbf:
                    writer.AddColorBlockFrame(cbf, cbf.DelayMs);
                    break;
                default:
                    writer.AddFrame(frame.Content, frame.DelayMs);
                    break;
            }

        await writer.SaveAsync(outputPath, ct);
    }
}

/// <summary>
///     Generic frame implementation for unified rendering.
/// </summary>
public class GenericFrame : IAnimationFrame
{
    public GenericFrame(string content, int delayMs)
    {
        Content = content;
        DelayMs = delayMs;
    }

    public string Content { get; }
    public int DelayMs { get; }
}