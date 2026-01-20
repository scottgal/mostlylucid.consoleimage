using ConsoleImage.Core;
using Spectre.Console;
using Spectre.Console.Rendering;
using SpectreRenderOptions = Spectre.Console.Rendering.RenderOptions;
using CoreRenderOptions = ConsoleImage.Core.RenderOptions;

namespace ConsoleImage.Spectre;

/// <summary>
/// A Spectre.Console renderable that displays an image as ASCII art.
/// </summary>
public class AsciiImage : IRenderable
{
    private readonly string _content;
    private readonly int _width;
    private readonly int _height;

    /// <summary>
    /// Create an ASCII image from a file path.
    /// </summary>
    public AsciiImage(string filePath, CoreRenderOptions? options = null)
    {
        options ??= new CoreRenderOptions();
        using var renderer = new AsciiRenderer(options);
        var frame = renderer.RenderFile(filePath);
        _content = options.UseColor ? frame.ToAnsiString() : frame.ToString();
        _width = frame.Width;
        _height = frame.Height;
    }

    /// <summary>
    /// Create an ASCII image from a pre-rendered frame.
    /// </summary>
    public AsciiImage(AsciiFrame frame, bool useColor = true)
    {
        _content = useColor ? frame.ToAnsiString() : frame.ToString();
        _width = frame.Width;
        _height = frame.Height;
    }

    /// <summary>
    /// Create an ASCII image from raw content string.
    /// </summary>
    public AsciiImage(string content, int width, int height)
    {
        _content = content;
        _width = width;
        _height = height;
    }

    public Measurement Measure(SpectreRenderOptions options, int maxWidth)
    {
        return new Measurement(_width, _width);
    }

    public IEnumerable<Segment> Render(SpectreRenderOptions options, int maxWidth)
    {
        // Split content into lines and yield segments
        var lines = _content.Split('\n');
        foreach (var line in lines)
        {
            // Spectre will handle ANSI escape sequences in the text
            yield return new Segment(line.TrimEnd('\r'));
            yield return Segment.LineBreak;
        }
    }
}
