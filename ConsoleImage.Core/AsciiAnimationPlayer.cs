// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Animation player for displaying animated GIFs in the console

using System.Linq;

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
    private readonly bool _useAltScreen;
    private CancellationTokenSource? _cts;
    private Task? _playTask;
    private bool _disposed;

    // DECSET 2026 - Synchronized Output (supported by WezTerm, Windows Terminal, Ghostty, Alacritty, etc.)
    // Batches all output until reset, then renders atomically - eliminates flicker
    private const string SyncStart = "\x1b[?2026h";
    private const string SyncEnd = "\x1b[?2026l";

    // Alternate screen buffer - prevents animation from trashing scrollback
    private const string AltScreenEnter = "\x1b[?1049h";
    private const string AltScreenExit = "\x1b[?1049l";
    private const string CursorHome = "\x1b[H";
    private const string CursorHide = "\x1b[?25l";
    private const string CursorShow = "\x1b[?25h";

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

    public AsciiAnimationPlayer(IReadOnlyList<AsciiFrame> frames, bool useColor = false, int loopCount = 0, bool useDiffRendering = true, bool useAltScreen = true)
    {
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));
        _useColor = useColor;
        _loopCount = loopCount;
        _useDiffRendering = useDiffRendering;
        _useAltScreen = useAltScreen;
    }

    /// <summary>
    /// Play the animation in the console
    /// </summary>
    public async Task PlayAsync(CancellationToken cancellationToken = default)
    {
        if (_frames.Count == 0)
            return;

        // Ensure ANSI escape sequences are enabled on Windows
        ConsoleHelper.EnableAnsiSupport();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        int loops = 0;

        // Pre-buffer frame lines for line-by-line rendering
        // Use StringSplitOptions to handle \r\n properly and avoid empty entries
        var frameLines = new string[_frames.Count][];
        int maxHeight = 0;
        for (int i = 0; i < _frames.Count; i++)
        {
            string frameStr = _useColor ? _frames[i].ToAnsiString() : _frames[i].ToString();
            // Split by \n and remove any trailing \r from Windows line endings
            frameLines[i] = frameStr.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
            if (frameLines[i].Length > maxHeight)
                maxHeight = frameLines[i].Length;
        }

        // Enter alternate screen buffer if enabled (preserves scrollback)
        if (_useAltScreen)
        {
            Console.Write(AltScreenEnter);
        }
        Console.Write(CursorHide);

        try
        {
            while (!token.IsCancellationRequested)
            {
                for (int i = 0; i < _frames.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    CurrentFrame = i;

                    // Start synchronized output batch
                    Console.Write(SyncStart);

                    // Home cursor instead of moving up (faster, less flicker)
                    Console.Write(CursorHome);

                    // Write frame line by line, padding to maxHeight
                    var lines = frameLines[i];
                    for (int lineIdx = 0; lineIdx < maxHeight; lineIdx++)
                    {
                        Console.Write("\x1b[2K"); // Clear entire line
                        if (lineIdx < lines.Length)
                            Console.Write(lines[lineIdx]);
                        if (lineIdx < maxHeight - 1)
                            Console.WriteLine();
                    }
                    Console.Write("\x1b[0m");

                    // End synchronized output - flush atomically
                    Console.Write(SyncEnd);
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
            Console.Write(CursorShow);
            if (_useAltScreen)
            {
                Console.Write(AltScreenExit);
            }
            else
            {
                Console.WriteLine();
            }
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
