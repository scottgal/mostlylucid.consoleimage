// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Animation player for displaying animated GIFs in the console

using System.Diagnostics;
using System.Text;

namespace ConsoleImage.Core;

/// <summary>
///     Plays ASCII art animations in the console.
///     Uses DECSET 2026 synchronized output for flicker-free rendering on supported terminals.
/// </summary>
public class AsciiAnimationPlayer : IDisposable
{
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

    // Pre-cached cursor move escape sequences
    private static readonly string[] CursorMoveCache = BuildCursorMoveCache(300);

    private readonly float? _darkThreshold;
    private readonly IReadOnlyList<AsciiFrame> _frames;
    private readonly float? _lightThreshold;
    private readonly int _loopCount;
    private readonly float? _targetFps;
    private readonly bool _useAltScreen;
    private readonly bool _useColor;
    private readonly bool _useDiffRendering;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private Task? _playTask;

    public AsciiAnimationPlayer(IReadOnlyList<AsciiFrame> frames, bool useColor = false, int loopCount = 0,
        bool useDiffRendering = true, bool useAltScreen = true, float? targetFps = null, float? darkThreshold = null,
        float? lightThreshold = null)
    {
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));
        _useColor = useColor;
        _loopCount = loopCount;
        _useDiffRendering = useDiffRendering;
        _useAltScreen = useAltScreen;
        _targetFps = targetFps;
        _darkThreshold = darkThreshold;
        _lightThreshold = lightThreshold;
    }

    /// <summary>
    ///     Current frame index
    /// </summary>
    public int CurrentFrame { get; private set; }

    /// <summary>
    ///     Total number of frames
    /// </summary>
    public int FrameCount => _frames.Count;

    /// <summary>
    ///     Whether the animation is currently playing
    /// </summary>
    public bool IsPlaying => _playTask != null && !_playTask.IsCompleted;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Event raised when a frame is rendered
    /// </summary>
    public event EventHandler<FrameRenderedEventArgs>? FrameRendered;

    /// <summary>
    ///     Event raised when animation completes
    /// </summary>
    public event EventHandler? AnimationCompleted;

    /// <summary>
    ///     Play the animation in the console
    /// </summary>
    public async Task PlayAsync(CancellationToken cancellationToken = default)
    {
        if (_frames.Count == 0)
            return;

        // Ensure ANSI escape sequences are enabled on Windows
        ConsoleHelper.EnableAnsiSupport();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        var loops = 0;

        // Pre-build entire frame buffers as single atomic strings
        // This eliminates flicker by writing everything in one Console.Write call
        var frameBuffers = new string[_frames.Count];
        var maxHeight = 0;

        // First pass: render all frames and determine max height (no LINQ allocation)
        var frameStrings = new string[_frames.Count];
        var frameLineCounts = new int[_frames.Count];
        for (var i = 0; i < _frames.Count; i++)
        {
            frameStrings[i] =
                _useColor ? _frames[i].ToAnsiString(_darkThreshold, _lightThreshold) : _frames[i].ToString();
            var lineCount = 1;
            foreach (var c in frameStrings[i])
                if (c == '\n')
                    lineCount++;
            frameLineCounts[i] = lineCount;
            if (lineCount > maxHeight)
                maxHeight = lineCount;
        }

        // Second pass: build atomic frame buffers with diff-based rendering
        // Uses O(N) offset-based line access instead of O(N²) per-line scanning
        var sb = new StringBuilder(8192);
        Span<int> currStarts = stackalloc int[301];
        Span<int> prevStarts = stackalloc int[301];

        for (var i = 0; i < _frames.Count; i++)
        {
            sb.Clear();
            sb.Append(SyncStart); // Begin synchronized output

            if (i == 0 || !_useDiffRendering)
            {
                // First frame or diff disabled: full redraw
                sb.Append(CursorHome);
                AppendWithLineClearing(sb, frameStrings[i], maxHeight);
            }
            else
            {
                // Diff against previous frame using offset-based line access
                var curr = frameStrings[i];
                var prev = frameStrings[i - 1];
                var currLineCount = LineUtils.BuildLineStarts(curr, currStarts);
                var prevLineCount = LineUtils.BuildLineStarts(prev, prevStarts);
                var lineCount = Math.Max(currLineCount, prevLineCount);
                var abandonThreshold = (int)(lineCount * 0.6) + 1;

                // Single pass: compare and build diff, abandon if >60% changed
                var diffStart = sb.Length;
                var changes = 0;

                for (var line = 0; line < lineCount; line++)
                {
                    var currLine = LineUtils.GetLineFromStarts(curr, currStarts, currLineCount, line);
                    var prevLine = LineUtils.GetLineFromStarts(prev, prevStarts, prevLineCount, line);

                    if (!currLine.SequenceEqual(prevLine))
                    {
                        changes++;
                        if (changes >= abandonThreshold)
                        {
                            // >60% changed: full redraw is more efficient
                            sb.Length = diffStart;
                            sb.Append(CursorHome);
                            AppendWithLineClearing(sb, curr, maxHeight);
                            break;
                        }

                        sb.Append(line < CursorMoveCache.Length ? CursorMoveCache[line] : $"\x1b[{line + 1};1H");
                        sb.Append("\x1b[2K"); // Clear line
                        sb.Append(currLine);
                        sb.Append("\x1b[0m");
                    }
                }
            }

            sb.Append(SyncEnd);
            frameBuffers[i] = sb.ToString();
        }

        // Calculate fixed frame delay if target FPS is set
        int? fixedDelayMs = _targetFps.HasValue && _targetFps.Value > 0
            ? (int)(1000f / _targetFps.Value)
            : null;

        // Enter alternate screen buffer if enabled (preserves scrollback)
        if (_useAltScreen) Console.Write(AltScreenEnter);
        Console.Write(CursorHide);
        // Clear entire screen to prevent residual content from flashing
        Console.Write("\x1b[2J");
        Console.Out.Flush();

        try
        {
            while (!token.IsCancellationRequested)
            {
                double timeDebtMs = 0;

                for (var i = 0; i < _frames.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var targetDelayMs = (double)(fixedDelayMs ?? _frames[i].DelayMs);

                    // Adaptive frame skipping: drop frames when behind schedule
                    if (i < _frames.Count - 1 &&
                        FrameTiming.ShouldSkipFrame(targetDelayMs, ref timeDebtMs))
                        continue;

                    var renderStart = Stopwatch.GetTimestamp();

                    CurrentFrame = i;

                    // Write entire frame atomically - single Console.Write eliminates flicker
                    Console.Write(frameBuffers[i]);
                    Console.Out.Flush();

                    FrameRendered?.Invoke(this, new FrameRenderedEventArgs(i, _frames.Count));

                    // Adaptive timing: compensate for render time
                    if (targetDelayMs > 0)
                    {
                        var (remainingDelay, newDebt) = FrameTiming.CalculateAdaptiveDelay(
                            targetDelayMs, renderStart, timeDebtMs);
                        timeDebtMs = newDebt;
                        if (remainingDelay > 0)
                            await FrameTiming.ResponsiveDelayAsync(remainingDelay, token);
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
                Console.Write(AltScreenExit);
            else
                Console.WriteLine();
            AnimationCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Start playing the animation in the background
    /// </summary>
    public void Play()
    {
        if (IsPlaying)
            return;

        _playTask = PlayAsync();
    }

    /// <summary>
    ///     Stop the animation
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    /// <summary>
    ///     Render a single frame to the console
    /// </summary>
    public void RenderFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frames.Count)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        var frame = _frames[frameIndex];
        var output = _useColor ? frame.ToAnsiString(_darkThreshold, _lightThreshold) : frame.ToString();
        Console.WriteLine(output);
    }

    /// <summary>
    ///     Get frame data for manual rendering
    /// </summary>
    public AsciiFrame GetFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frames.Count)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        return _frames[frameIndex];
    }

    /// <summary>
    ///     Append frame content with line clearing for full redraws.
    /// </summary>
    private static void AppendWithLineClearing(StringBuilder sb, string content, int maxHeight)
    {
        var lineIdx = 0;
        var lineStart = 0;

        for (var i = 0; i <= content.Length; i++)
            if (i == content.Length || content[i] == '\n')
            {
                sb.Append("\x1b[2K"); // Clear line
                var end = i;
                if (end > lineStart && content[end - 1] == '\r') end--;
                if (end > lineStart)
                    sb.Append(content.AsSpan(lineStart, end - lineStart));
                sb.Append("\x1b[0m");
                lineIdx++;

                if (i < content.Length && lineIdx < maxHeight)
                    sb.Append('\n');

                lineStart = i + 1;
            }

        // Pad remaining lines with clears
        while (lineIdx < maxHeight)
        {
            sb.Append('\n');
            sb.Append("\x1b[2K");
            lineIdx++;
        }
    }

    private static string[] BuildCursorMoveCache(int maxLines)
    {
        var cache = new string[maxLines];
        for (var i = 0; i < maxLines; i++)
            cache[i] = $"\x1b[{i + 1};1H";
        return cache;
    }
}

/// <summary>
///     Event arguments for frame rendered events
/// </summary>
public class FrameRenderedEventArgs : EventArgs
{
    public FrameRenderedEventArgs(int frameIndex, int totalFrames)
    {
        FrameIndex = frameIndex;
        TotalFrames = totalFrames;
    }

    public int FrameIndex { get; }
    public int TotalFrames { get; }
}