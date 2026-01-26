// DocumentRenderable - Spectre.Console IRenderable for PlayerDocument

using Spectre.Console.Rendering;

namespace ConsoleImage.Player.Spectre;

/// <summary>
///     A Spectre.Console IRenderable that displays a single frame from a PlayerDocument.
/// </summary>
public class DocumentFrame : IRenderable
{
    /// <summary>
    ///     Create from a PlayerFrame.
    /// </summary>
    public DocumentFrame(PlayerFrame frame)
    {
        Frame = frame;
    }

    /// <summary>
    ///     Create from raw content.
    /// </summary>
    public DocumentFrame(string content, int width, int height)
    {
        Frame = new PlayerFrame
        {
            Content = content,
            Width = width,
            Height = height
        };
    }

    /// <summary>
    ///     The underlying frame.
    /// </summary>
    public PlayerFrame Frame { get; }

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        return new Measurement(Frame.Width, Math.Min(Frame.Width, maxWidth));
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var lines = Frame.Content.Split('\n');
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
    /// <summary>
    ///     Create from a PlayerDocument, displaying the specified frame (default: first).
    /// </summary>
    public DocumentImage(PlayerDocument document, int frameIndex = 0)
    {
        Document = document;
        FrameIndex = Math.Clamp(frameIndex, 0, Math.Max(0, document.FrameCount - 1));
    }

    /// <summary>
    ///     The underlying document.
    /// </summary>
    public PlayerDocument Document { get; }

    /// <summary>
    ///     Current frame index being displayed.
    /// </summary>
    public int FrameIndex { get; }

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        if (Document.FrameCount == 0)
            return new Measurement(0, 0);

        var frame = Document.Frames[FrameIndex];
        return new Measurement(frame.Width, Math.Min(frame.Width, maxWidth));
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        if (Document.FrameCount == 0)
            yield break;

        var frame = Document.Frames[FrameIndex];
        var lines = frame.Content.Split('\n');
        foreach (var line in lines)
        {
            yield return new Segment(line.TrimEnd('\r'));
            yield return Segment.LineBreak;
        }
    }
}