using ConsoleImage.Core;
using Spectre.Console;
using Spectre.Console.Rendering;
using SpectreRenderOptions = Spectre.Console.Rendering.RenderOptions;
using CoreRenderOptions = ConsoleImage.Core.RenderOptions;

namespace ConsoleImage.Spectre;

/// <summary>
///     Animation mode for rendering GIFs.
/// </summary>
public enum AnimationMode
{
    /// <summary>Standard ASCII art rendering.</summary>
    Ascii,

    /// <summary>High-fidelity colored Unicode blocks.</summary>
    ColorBlock,

    /// <summary>Ultra-high resolution braille characters.</summary>
    Braille,

    /// <summary>Matrix digital rain effect.</summary>
    Matrix
}

/// <summary>
///     Animated image for use with Spectre.Console's Live display.
///     Supports ASCII, ColorBlock, Braille, and Matrix rendering modes.
/// </summary>
/// <remarks>
///     <para>
///         Use this class to display animated GIFs or create animation effects from static images.
///         Each frame is pre-rendered at construction time for smooth playback.
///     </para>
///     <para>
///         <b>Basic usage:</b>
///         <code>
/// var animation = new AnimatedImage("cat.gif", AnimationMode.Braille);
/// await animation.PlayAsync(cancellationToken);
/// </code>
///     </para>
///     <para>
///         <b>Manual control:</b>
///         <code>
/// await AnsiConsole.Live(animation).StartAsync(async ctx => {
///     while (!token.IsCancellationRequested) {
///         animation.TryAdvanceFrame();
///         ctx.Refresh();
///         await Task.Delay(16);
///     }
/// });
/// </code>
///     </para>
/// </remarks>
public partial class AnimatedImage : IRenderable
{
    private readonly List<FrameData> _frames;
    private readonly int _height;
    private readonly int _width;
    private DateTime _lastFrameTime;
    private float? _targetFps;

    /// <summary>
    ///     Create an animated image from a GIF file.
    /// </summary>
    public AnimatedImage(string filePath, AnimationMode mode = AnimationMode.Ascii, CoreRenderOptions? options = null)
    {
        options ??= new CoreRenderOptions { UseColor = true };
        _frames = new List<FrameData>();
        _lastFrameTime = DateTime.UtcNow;

        switch (mode)
        {
            case AnimationMode.Ascii:
                LoadAsciiFrames(filePath, options);
                break;
            case AnimationMode.ColorBlock:
                LoadColorBlockFrames(filePath, options);
                break;
            case AnimationMode.Braille:
                LoadBrailleFrames(filePath, options);
                break;
            case AnimationMode.Matrix:
                LoadMatrixFrames(filePath, options);
                break;
        }

        if (_frames.Count > 0)
        {
            _width = _frames[0].Width;
            _height = _frames[0].Height;
        }
    }

    /// <summary>
    ///     Target FPS override. If set, ignores embedded GIF timing.
    /// </summary>
    public float? TargetFps
    {
        get => _targetFps;
        set => _targetFps = value;
    }

    /// <summary>
    ///     Current frame index.
    /// </summary>
    public int CurrentFrame { get; private set; }

    /// <summary>
    ///     Total number of frames.
    /// </summary>
    public int FrameCount => _frames.Count;

    /// <summary>
    ///     Advance to the next frame if enough time has elapsed.
    ///     Call this in your Live display loop.
    ///     Returns true if frame changed.
    /// </summary>
    public bool TryAdvanceFrame()
    {
        if (_frames.Count <= 1)
            return false;

        var now = DateTime.UtcNow;
        var currentDelay = _targetFps.HasValue && _targetFps.Value > 0
            ? (int)(1000f / _targetFps.Value)
            : _frames[CurrentFrame].DelayMs;

        if ((now - _lastFrameTime).TotalMilliseconds >= currentDelay)
        {
            CurrentFrame = (CurrentFrame + 1) % _frames.Count;
            _lastFrameTime = now;
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
        _lastFrameTime = DateTime.UtcNow;
    }

    /// <summary>
    ///     Set current frame directly.
    /// </summary>
    public void SetFrame(int frameIndex)
    {
        if (frameIndex >= 0 && frameIndex < _frames.Count)
        {
            CurrentFrame = frameIndex;
            _lastFrameTime = DateTime.UtcNow;
        }
    }

    public Measurement Measure(SpectreRenderOptions options, int maxWidth)
    {
        return new Measurement(_width, _width);
    }

    public IEnumerable<Segment> Render(SpectreRenderOptions options, int maxWidth)
    {
        if (_frames.Count == 0)
            yield break;

        var frame = _frames[CurrentFrame];
        var lines = frame.Content.Split('\n');
        foreach (var line in lines)
        {
            yield return new Segment(line.TrimEnd('\r'));
            yield return Segment.LineBreak;
        }
    }

    private void LoadAsciiFrames(string filePath, CoreRenderOptions options)
    {
        using var renderer = new AsciiRenderer(options);
        var frames = renderer.RenderGif(filePath);
        foreach (var frame in frames)
        {
            var content = options.UseColor ? frame.ToAnsiString() : frame.ToString();
            _frames.Add(new FrameData(content, frame.Width, frame.Height, frame.DelayMs));
        }
    }

    private void LoadColorBlockFrames(string filePath, CoreRenderOptions options)
    {
        using var renderer = new ColorBlockRenderer(options);
        var frames = renderer.RenderGif(filePath);
        foreach (var frame in frames)
        {
            var (width, height) = GetDimensionsFromContent(frame.Content);
            _frames.Add(new FrameData(frame.Content, width, height, frame.DelayMs));
        }
    }

    private void LoadBrailleFrames(string filePath, CoreRenderOptions options)
    {
        using var renderer = new BrailleRenderer(options);
        var frames = renderer.RenderGif(filePath);
        foreach (var frame in frames)
        {
            var (width, height) = GetDimensionsFromContent(frame.Content);
            _frames.Add(new FrameData(frame.Content, width, height, frame.DelayMs));
        }
    }

    private void LoadMatrixFrames(string filePath, CoreRenderOptions options)
    {
        using var renderer = new MatrixRenderer(options);
        var frames = renderer.RenderGif(filePath);
        foreach (var frame in frames)
        {
            var (width, height) = GetDimensionsFromContent(frame.Content);
            _frames.Add(new FrameData(frame.Content, width, height, frame.DelayMs));
        }
    }

    private static (int width, int height) GetDimensionsFromContent(string content)
    {
        var lines = content.Split('\n');
        var height = lines.Length;
        var width = lines.Length > 0 ? GetVisibleWidth(lines[0]) : 0;
        return (width, height);
    }

    private static int GetVisibleWidth(string line)
    {
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

    private record FrameData(string Content, int Width, int Height, int DelayMs);
}

/// <summary>
///     Extension methods for AnimatedImage with Spectre.Console.
/// </summary>
public static class AnimatedImageExtensions
{
    /// <summary>
    ///     Play an animated image using Spectre's Live display.
    /// </summary>
    public static async Task PlayAsync(
        this AnimatedImage animation,
        CancellationToken cancellationToken = default,
        int loopCount = 0,
        Action<LiveDisplayContext, AnimatedImage>? onFrame = null)
    {
        var loops = 0;

        await AnsiConsole.Live(animation)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    animation.TryAdvanceFrame();
                    onFrame?.Invoke(ctx, animation);
                    ctx.Refresh();

                    // Check for loop completion
                    if (animation.CurrentFrame == 0 && loops > 0)
                    {
                        loops++;
                        if (loopCount > 0 && loops >= loopCount)
                            break;
                    }
                    else if (animation.CurrentFrame > 0 && loops == 0)
                    {
                        loops = 1;
                    }

                    try
                    {
                        // Update at ~60fps for smooth timing
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