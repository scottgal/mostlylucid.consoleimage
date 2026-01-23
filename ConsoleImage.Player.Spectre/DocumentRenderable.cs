// DocumentRenderable - Spectre.Console IRenderable for PlayerDocument
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ConsoleImage.Player.Spectre;

/// <summary>
///     A Spectre.Console IRenderable that displays a single frame from a PlayerDocument.
/// </summary>
public class DocumentFrame : IRenderable
{
    private readonly Player.PlayerFrame _frame;

    /// <summary>
    ///     Create from a PlayerFrame.
    /// </summary>
    public DocumentFrame(Player.PlayerFrame frame)
    {
        _frame = frame;
    }

    /// <summary>
    ///     Create from raw content.
    /// </summary>
    public DocumentFrame(string content, int width, int height)
    {
        _frame = new Player.PlayerFrame
        {
            Content = content,
            Width = width,
            Height = height
        };
    }

    /// <summary>
    ///     The underlying frame.
    /// </summary>
    public Player.PlayerFrame Frame => _frame;

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        return new Measurement(_frame.Width, _frame.Width);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var lines = _frame.Content.Split('\n');
        foreach (var line in lines)
        {
            yield return new Segment(line.TrimEnd('\r'));
            yield return Segment.LineBreak;
        }
    }
}

/// <summary>
///     A Spectre.Console IRenderable that displays a static document (first frame).
/// </summary>
public class DocumentImage : IRenderable
{
    private readonly PlayerDocument _document;
    private readonly int _frameIndex;

    /// <summary>
    ///     Create from a PlayerDocument, displaying the specified frame (default: first).
    /// </summary>
    public DocumentImage(PlayerDocument document, int frameIndex = 0)
    {
        _document = document;
        _frameIndex = Math.Clamp(frameIndex, 0, Math.Max(0, document.FrameCount - 1));
    }

    /// <summary>
    ///     The underlying document.
    /// </summary>
    public PlayerDocument Document => _document;

    /// <summary>
    ///     Current frame index being displayed.
    /// </summary>
    public int FrameIndex => _frameIndex;

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        if (_document.FrameCount == 0)
            return new Measurement(0, 0);

        var frame = _document.Frames[_frameIndex];
        return new Measurement(frame.Width, frame.Width);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        if (_document.FrameCount == 0)
            yield break;

        var frame = _document.Frames[_frameIndex];
        var lines = frame.Content.Split('\n');
        foreach (var line in lines)
        {
            yield return new Segment(line.TrimEnd('\r'));
            yield return Segment.LineBreak;
        }
    }
}
