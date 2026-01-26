// AnimatedDocument - Spectre.Console animated IRenderable for PlayerDocument

using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ConsoleImage.Player.Spectre;

/// <summary>
///     An animated Spectre.Console IRenderable for PlayerDocument playback.
///     Use with AnsiConsole.Live() for smooth animation.
/// </summary>
public class AnimatedDocument : IRenderable
{
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();
    private float? _targetFps;

    /// <summary>
    ///     Create from a PlayerDocument.
    /// </summary>
    public AnimatedDocument(PlayerDocument document)
    {
        Document = document;
    }

    /// <summary>
    ///     The underlying document.
    /// </summary>
    public PlayerDocument Document { get; }

    /// <summary>
    ///     Current frame index.
    /// </summary>
    public int CurrentFrame { get; private set; }

    /// <summary>
    ///     Total frame count.
    /// </summary>
    public int FrameCount => Document.FrameCount;

    /// <summary>
    ///     Whether this document has animation (more than 1 frame).
    /// </summary>
    public bool IsAnimated => Document.IsAnimated;

    /// <summary>
    ///     Target FPS override. If set, ignores embedded timing.
    /// </summary>
    public float? TargetFps
    {
        get => _targetFps;
        set => _targetFps = value;
    }

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        if (Document.FrameCount == 0)
            return new Measurement(0, 0);

        var frame = Document.Frames[CurrentFrame];
        // Min = frame width (we need at least this much), Max = capped at maxWidth
        return new Measurement(frame.Width, Math.Min(frame.Width, maxWidth));
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        if (Document.FrameCount == 0)
            yield break;

        var frame = Document.Frames[CurrentFrame];
        var lines = frame.Content.Split('\n');
        foreach (var line in lines)
        {
            yield return new Segment(line.TrimEnd('\r'));
            yield return Segment.LineBreak;
        }
    }

    /// <summary>
    ///     Create by loading a document from file.
    /// </summary>
    public static async Task<AnimatedDocument> FromFileAsync(string path, CancellationToken ct = default)
    {
        var doc = await PlayerDocument.LoadAsync(path, ct);
        return new AnimatedDocument(doc);
    }

    /// <summary>
    ///     Create from a JSON string.
    /// </summary>
    public static AnimatedDocument FromJson(string json)
    {
        var doc = PlayerDocument.FromJson(json);
        return new AnimatedDocument(doc);
    }

    /// <summary>
    ///     Advance to the next frame if enough time has elapsed.
    ///     Call this in your Live display loop.
    ///     Returns true if frame changed.
    /// </summary>
    public bool TryAdvanceFrame()
    {
        if (Document.FrameCount <= 1)
            return false;

        var currentDelay = _targetFps.HasValue && _targetFps.Value > 0
            ? (int)(1000f / _targetFps.Value)
            : Document.Frames[CurrentFrame].DelayMs;

        if (_frameTimer.ElapsedMilliseconds >= currentDelay)
        {
            CurrentFrame = (CurrentFrame + 1) % Document.FrameCount;
            _frameTimer.Restart();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Reset animation to first frame.
    /// </summary>
    public void Reset()
    {
        CurrentFrame = 0;
        _frameTimer.Restart();
    }

    /// <summary>
    ///     Set current frame directly.
    /// </summary>
    public void SetFrame(int frameIndex)
    {
        if (frameIndex >= 0 && frameIndex < Document.FrameCount)
        {
            CurrentFrame = frameIndex;
            _frameTimer.Restart();
        }
    }
}

/// <summary>
///     Extension methods for AnimatedDocument playback.
/// </summary>
public static class AnimatedDocumentExtensions
{
    /// <summary>
    ///     Play an animated document using Spectre's Live display.
    /// </summary>
    /// <param name="animation">The animated document to play</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="loopCount">Number of loops (0 = infinite)</param>
    /// <param name="onFrame">Callback on each frame</param>
    public static async Task PlayAsync(
        this AnimatedDocument animation,
        CancellationToken cancellationToken = default,
        int loopCount = 0,
        Action<LiveDisplayContext, AnimatedDocument>? onFrame = null)
    {
        var loops = 0;
        var targetLoops = loopCount == 0 ? int.MaxValue : loopCount;

        await AnsiConsole.Live(animation)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (!cancellationToken.IsCancellationRequested && loops < targetLoops)
                {
                    var wasZero = animation.CurrentFrame == 0;
                    animation.TryAdvanceFrame();

                    // Track loop completion
                    if (animation.CurrentFrame == 0 && !wasZero)
                        loops++;

                    onFrame?.Invoke(ctx, animation);
                    ctx.Refresh();

                    try
                    {
                        await Task.Delay(16, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
    }

    /// <summary>
    ///     Play an animated document with a progress display.
    /// </summary>
    public static async Task PlayWithProgressAsync(
        this AnimatedDocument animation,
        CancellationToken cancellationToken = default,
        int loopCount = 0)
    {
        var loops = 0;
        var targetLoops = loopCount == 0 ? int.MaxValue : loopCount;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Animation");

        await AnsiConsole.Live(table)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (!cancellationToken.IsCancellationRequested && loops < targetLoops)
                {
                    var wasZero = animation.CurrentFrame == 0;
                    animation.TryAdvanceFrame();

                    if (animation.CurrentFrame == 0 && !wasZero)
                        loops++;

                    // Update table with current frame and status
                    table.Rows.Clear();
                    table.AddRow(animation);
                    table.AddRow(new Markup(
                        $"[dim]Frame {animation.CurrentFrame + 1}/{animation.FrameCount} | Loop {loops + 1}/{(loopCount == 0 ? "∞" : loopCount.ToString())}[/]"));

                    ctx.Refresh();

                    try
                    {
                        await Task.Delay(16, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
    }
}