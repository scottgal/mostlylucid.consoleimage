// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Animation player for displaying animated GIFs in the console

namespace ConsoleImage.Core;

/// <summary>
/// Plays ASCII art animations in the console.
/// Uses DECSET 2026 synchronized output for flicker-free rendering on supported terminals.
/// </summary>
public class AsciiAnimationPlayer : IDisposable
{
    private readonly IReadOnlyList<AsciiFrame> _frames;
    private readonly bool _useColor;
    private readonly int _loopCount;
    private readonly bool _useDiffRendering;
    private CancellationTokenSource? _cts;
    private Task? _playTask;
    private bool _disposed;

    // DECSET 2026 - Synchronized Output (supported by WezTerm, Windows Terminal, Ghostty, Alacritty, etc.)
    // Batches all output until reset, then renders atomically - eliminates flicker
    private const string SyncStart = "\x1b[?2026h";
    private const string SyncEnd = "\x1b[?2026l";

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

    public AsciiAnimationPlayer(IReadOnlyList<AsciiFrame> frames, bool useColor = false, int loopCount = 0, bool useDiffRendering = true)
    {
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));
        _useColor = useColor;
        _loopCount = loopCount;
        _useDiffRendering = useDiffRendering;
    }

    /// <summary>
    /// Play the animation in the console
    /// </summary>
    public async Task PlayAsync(CancellationToken cancellationToken = default)
    {
        if (_frames.Count == 0)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        int loops = 0;
        int frameHeight = _frames[0].Height;
        int startRow = Console.CursorTop;

        // Pre-buffer frame strings - use diff rendering if enabled
        string[] frameStrings;
        if (_useDiffRendering && _frames.Count > 1)
        {
            frameStrings = FrameDiffer.ComputeDiffs(_frames, _useColor);
        }
        else
        {
            frameStrings = new string[_frames.Count];
            for (int i = 0; i < _frames.Count; i++)
            {
                frameStrings[i] = _useColor ? _frames[i].ToAnsiString() : _frames[i].ToString();
            }
        }

        // Hide cursor for smoother animation
        Console.Write("\x1b[?25l");

        // Save cursor position
        Console.Write("\x1b[s");

        // Flush to ensure cursor is hidden before we start
        Console.Out.Flush();

        try
        {
            while (!token.IsCancellationRequested)
            {
                for (int i = 0; i < _frames.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    CurrentFrame = i;

                    // Start synchronized output - terminal will batch until SyncEnd
                    Console.Write(SyncStart);

                    // For diff rendering: first frame or after loop needs full render
                    // For non-diff: always restore position
                    if (!_useDiffRendering || i == 0)
                    {
                        Console.Write("\x1b[u"); // Restore to saved position
                    }

                    // Write the frame (or diff)
                    Console.Write(frameStrings[i]);

                    if (!_useDiffRendering || i == 0)
                    {
                        Console.Write("\x1b[0m");
                    }

                    // End synchronized output - terminal renders atomically now
                    Console.Write(SyncEnd);

                    // Flush to ensure frame is displayed immediately
                    Console.Out.Flush();

                    FrameRendered?.Invoke(this, new FrameRenderedEventArgs(i, _frames.Count));

                    // Wait for frame delay with responsive cancellation
                    int delayMs = _frames[i].DelayMs;
                    if (delayMs > 0)
                    {
                        await ResponsiveDelay(delayMs, token);
                    }
                }

                loops++;
                if (_loopCount > 0 && loops >= _loopCount)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Animation was stopped - this is expected
        }
        finally
        {
            // Show cursor again
            Console.Write("\x1b[?25h");

            // Move cursor below the animation
            Console.Write($"\x1b[{startRow + frameHeight + 1};1H");
            AnimationCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Delay that responds quickly to cancellation
    /// </summary>
    private static async Task ResponsiveDelay(int totalMs, CancellationToken token)
    {
        const int chunkMs = 50; // Check cancellation every 50ms
        int remaining = totalMs;

        while (remaining > 0 && !token.IsCancellationRequested)
        {
            int delay = Math.Min(remaining, chunkMs);
            try
            {
                await Task.Delay(delay, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            remaining -= delay;
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
