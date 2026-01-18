// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Animation player for displaying animated GIFs in the console

namespace ConsoleImage.Core;

/// <summary>
/// Plays ASCII art animations in the console
/// </summary>
public class AsciiAnimationPlayer : IDisposable
{
    private readonly IReadOnlyList<AsciiFrame> _frames;
    private readonly bool _useColor;
    private readonly int _loopCount;
    private CancellationTokenSource? _cts;
    private Task? _playTask;
    private bool _disposed;

    /// <summary>
    /// Event raised when a frame is rendered
    /// </summary>
    public event EventHandler<FrameRenderedEventArgs>? FrameRendered;

    /// <summary>
    /// Event raised when animation completes
    /// </summary>
    public event EventHandler? AnimationCompleted;

    /// <summary>
    /// Current frame index
    /// </summary>
    public int CurrentFrame { get; private set; }

    /// <summary>
    /// Total number of frames
    /// </summary>
    public int FrameCount => _frames.Count;

    /// <summary>
    /// Whether the animation is currently playing
    /// </summary>
    public bool IsPlaying => _playTask != null && !_playTask.IsCompleted;

    public AsciiAnimationPlayer(IReadOnlyList<AsciiFrame> frames, bool useColor = false, int loopCount = 0)
    {
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));
        _useColor = useColor;
        _loopCount = loopCount;
    }

    /// <summary>
    /// Play the animation in the console
    /// </summary>
    public async Task PlayAsync(CancellationToken cancellationToken = default)
    {
        if (_frames.Count == 0)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        int loops = 0;
        int startRow = Console.CursorTop;

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                for (int i = 0; i < _frames.Count && !_cts.Token.IsCancellationRequested; i++)
                {
                    CurrentFrame = i;
                    var frame = _frames[i];

                    // Move cursor to start position
                    Console.SetCursorPosition(0, startRow);

                    // Render frame
                    string output = _useColor ? frame.ToAnsiString() : frame.ToString();
                    Console.Write(output);

                    // Clear any remaining content from previous frame
                    Console.Write(new string(' ', Console.WindowWidth - Console.CursorLeft));

                    FrameRendered?.Invoke(this, new FrameRenderedEventArgs(i, _frames.Count));

                    // Wait for frame delay
                    if (frame.DelayMs > 0)
                    {
                        await Task.Delay(frame.DelayMs, _cts.Token);
                    }
                }

                loops++;
                if (_loopCount > 0 && loops >= _loopCount)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Animation was stopped
        }
        finally
        {
            // Move cursor below the animation
            Console.SetCursorPosition(0, startRow + _frames[0].Height);
            AnimationCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Start playing the animation in the background
    /// </summary>
    public void Play()
    {
        if (IsPlaying)
            return;

        _playTask = PlayAsync();
    }

    /// <summary>
    /// Stop the animation
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Render a single frame to the console
    /// </summary>
    public void RenderFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frames.Count)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        var frame = _frames[frameIndex];
        string output = _useColor ? frame.ToAnsiString() : frame.ToString();
        Console.WriteLine(output);
    }

    /// <summary>
    /// Get frame data for manual rendering
    /// </summary>
    public AsciiFrame GetFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frames.Count)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        return _frames[frameIndex];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for frame rendered events
/// </summary>
public class FrameRenderedEventArgs : EventArgs
{
    public int FrameIndex { get; }
    public int TotalFrames { get; }

    public FrameRenderedEventArgs(int frameIndex, int totalFrames)
    {
        FrameIndex = frameIndex;
        TotalFrames = totalFrames;
    }
}
