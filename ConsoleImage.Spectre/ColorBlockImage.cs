using ConsoleImage.Core;
using Spectre.Console.Rendering;
using SpectreRenderOptions = Spectre.Console.Rendering.RenderOptions;
using CoreRenderOptions = ConsoleImage.Core.RenderOptions;

namespace ConsoleImage.Spectre;

/// <summary>
///     A Spectre.Console renderable that displays an image using colored Unicode blocks.
///     Provides higher fidelity than ASCII by using half-block characters with separate
///     foreground and background colors (2 pixels per character cell).
/// </summary>
public class ColorBlockImage : IRenderable
{
    private readonly string _content;
    private readonly int _height;
    private readonly int _width;

    /// <summary>
    ///     Create a color block image from a file path.
    /// </summary>
    public ColorBlockImage(string filePath, CoreRenderOptions? options = null)
    {
        options ??= new CoreRenderOptions { UseColor = true };
        using var renderer = new ColorBlockRenderer(options);
        _content = renderer.RenderFile(filePath);

        // Calculate dimensions from content
        var lines = _content.Split('\n');
        _height = lines.Length;
        _width = lines.Length > 0 ? GetVisibleWidth(lines[0]) : 0;
    }

    /// <summary>
    ///     Create a color block image from pre-rendered content.
    /// </summary>
    public ColorBlockImage(string content, int width, int height)
    {
        _content = content;
        _width = width;
        _height = height;
    }

    /// <summary>
    ///     Create a color block image from a ColorBlockFrame.
    /// </summary>
    public ColorBlockImage(ColorBlockFrame frame)
    {
        _content = frame.Content;
        var lines = _content.Split('\n');
        _height = lines.Length;
        _width = lines.Length > 0 ? GetVisibleWidth(lines[0]) : 0;
    }

    public Measurement Measure(SpectreRenderOptions options, int maxWidth)
    {
        return new Measurement(_width, _width);
    }

    public IEnumerable<Segment> Render(SpectreRenderOptions options, int maxWidth)
    {
        var lines = _content.Split('\n');
        foreach (var line in lines)
        {
            yield return new Segment(line.TrimEnd('\r'));
            yield return Segment.LineBreak;
        }
    }

    private static int GetVisibleWidth(string line)
    {
        // Strip ANSI escape sequences to get visible character count
        var width = 0;
        var inEscape = false;
        foreach (var c in line)
            if (c == '\x1b')
            {
                inEscape = true;
            }
            else if (inEscape)
            {
                if (char.IsLetter(c))
                    inEscape = false;
            }
            else
            {
                width++;
            }

        return width;
    }
}