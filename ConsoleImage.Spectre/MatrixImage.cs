using ConsoleImage.Core;
using Spectre.Console;
using Spectre.Console.Rendering;
using SpectreRenderOptions = Spectre.Console.Rendering.RenderOptions;
using CoreRenderOptions = ConsoleImage.Core.RenderOptions;

namespace ConsoleImage.Spectre;

/// <summary>
/// A Spectre.Console renderable that displays an image using Matrix digital rain effect.
/// Supports both static display and animated rain.
/// </summary>
public class MatrixImage : IRenderable
{
    private readonly string _content;
    private readonly int _width;
    private readonly int _height;

    /// <summary>
    /// Create a Matrix image from a file path.
    /// </summary>
    public MatrixImage(string filePath, CoreRenderOptions? options = null, MatrixOptions? matrixOptions = null)
    {
        options ??= new CoreRenderOptions { UseColor = true };
        matrixOptions ??= new MatrixOptions();
        using var renderer = new MatrixRenderer(options, matrixOptions);
        var frame = renderer.RenderFile(filePath);
        _content = frame.Content;

        // Calculate dimensions from content
        var lines = _content.Split('\n');
        _height = lines.Length;
        _width = lines.Length > 0 ? GetVisibleWidth(lines[0]) : 0;
    }

    /// <summary>
    /// Create a Matrix image from pre-rendered content.
    /// </summary>
    public MatrixImage(string content, int width, int height)
    {
        _content = content;
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Create a Matrix image from a MatrixFrame.
    /// </summary>
    public MatrixImage(MatrixFrame frame)
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
        int width = 0;
        bool inEscape = false;
        foreach (char c in line)
        {
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
        }
        return width;
    }
}

/// <summary>
/// Animated Matrix rain effect for use with Spectre.Console's Live display.
/// </summary>
/// <remarks>
/// <para>
/// Creates a continuous "Matrix digital rain" animation overlaid on an image.
/// For static images, generates continuous rain frames. For GIFs, applies
/// rain effect to each source frame.
/// </para>
/// <para>
/// <b>Basic usage:</b>
/// <code>
/// var animation = new AnimatedMatrixImage("photo.jpg", frameCount: 200);
/// await animation.PlayAsync(cancellationToken);
/// </code>
/// </para>
/// <para>
/// <b>With custom Matrix options:</b>
/// <code>
/// var matrixOpts = MatrixOptions.RedPill; // or ClassicGreen, BluePill, etc.
/// var animation = new AnimatedMatrixImage("photo.jpg", matrixOptions: matrixOpts);
/// </code>
/// </para>
/// </remarks>
public partial class AnimatedMatrixImage : IRenderable
{
    private readonly List<FrameData> _frames;
    private readonly int _width;
    private readonly int _height;
    private int _currentFrame;
    private DateTime _lastFrameTime;

    /// <summary>
    /// Current frame index.
    /// </summary>
    public int CurrentFrame => _currentFrame;

    /// <summary>
    /// Total number of frames.
    /// </summary>
    public int FrameCount => _frames.Count;

    /// <summary>
    /// Create an animated Matrix image from a file (image or GIF).
    /// </summary>
    public AnimatedMatrixImage(string filePath, CoreRenderOptions? options = null, MatrixOptions? matrixOptions = null, int frameCount = 100)
    {
        options ??= new CoreRenderOptions { UseColor = true };
        matrixOptions ??= new MatrixOptions();
        _frames = new List<FrameData>();
        _lastFrameTime = DateTime.UtcNow;

        // Check if it's a GIF
        if (filePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            LoadGifFrames(filePath, options, matrixOptions);
        }
        else
        {
            // Static image - generate rain animation frames
            LoadStaticImageFrames(filePath, options, matrixOptions, frameCount);
        }

        if (_frames.Count > 0)
        {
            var lines = _frames[0].Content.Split('\n');
            _height = lines.Length;
            _width = lines.Length > 0 ? GetVisibleWidth(lines[0]) : 0;
        }
    }

    private void LoadGifFrames(string filePath, CoreRenderOptions options, MatrixOptions matrixOptions)
    {
        using var renderer = new MatrixRenderer(options, matrixOptions);
        var frames = renderer.RenderGif(filePath);
        foreach (var frame in frames)
        {
            _frames.Add(new FrameData(frame.Content, frame.DelayMs));
        }
    }

    private void LoadStaticImageFrames(string filePath, CoreRenderOptions options, MatrixOptions matrixOptions, int frameCount)
    {
        using var renderer = new MatrixRenderer(options, matrixOptions);

        // Generate multiple frames for animation effect
        for (int i = 0; i < frameCount; i++)
        {
            var frame = renderer.RenderFile(filePath);
            _frames.Add(new FrameData(frame.Content, frame.DelayMs));
        }
    }

    /// <summary>
    /// Advance to the next frame if enough time has elapsed.
    /// Call this in your Live display loop.
    /// Returns true if frame changed.
    /// </summary>
    public bool TryAdvanceFrame()
    {
        if (_frames.Count <= 1)
            return false;

        var now = DateTime.UtcNow;
        var currentDelay = _frames[_currentFrame].DelayMs;

        if ((now - _lastFrameTime).TotalMilliseconds >= currentDelay)
        {
            _currentFrame = (_currentFrame + 1) % _frames.Count;
            _lastFrameTime = now;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reset animation to first frame.
    /// </summary>
    public void Reset()
    {
        _currentFrame = 0;
        _lastFrameTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Set current frame directly.
    /// </summary>
    public void SetFrame(int frameIndex)
    {
        if (frameIndex >= 0 && frameIndex < _frames.Count)
        {
            _currentFrame = frameIndex;
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

        var frame = _frames[_currentFrame];
        var lines = frame.Content.Split('\n');
        foreach (var line in lines)
        {
            yield return new Segment(line.TrimEnd('\r'));
            yield return Segment.LineBreak;
        }
    }

    private static int GetVisibleWidth(string line)
    {
        int width = 0;
        bool inEscape = false;
        foreach (char c in line)
        {
            if (c == '\x1b')
                inEscape = true;
            else if (inEscape)
            {
                if (char.IsLetter(c))
                    inEscape = false;
            }
            else
                width++;
        }
        return width;
    }

    private record FrameData(string Content, int DelayMs);
}

/// <summary>
/// Extension methods for AnimatedMatrixImage with Spectre.Console.
/// </summary>
public static class AnimatedMatrixImageExtensions
{
    /// <summary>
    /// Play an animated Matrix image using Spectre's Live display.
    /// </summary>
    public static async Task PlayAsync(
        this AnimatedMatrixImage animation,
        CancellationToken cancellationToken = default,
        int loopCount = 0,
        Action<LiveDisplayContext, AnimatedMatrixImage>? onFrame = null)
    {
        int loops = 0;

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
