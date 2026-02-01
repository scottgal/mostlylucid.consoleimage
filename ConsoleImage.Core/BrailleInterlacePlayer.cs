// EXPERIMENTAL: BrailleInterlacePlayer - Temporal super-resolution via rapid threshold cycling.
// Generates N braille frames with different Atkinson dithering thresholds and cycles
// them rapidly. The human visual system integrates the subframes, perceiving more
// tonal depth than any single frame can display. Modeled after LCD FRC and DLP
// temporal dithering techniques.
//
// Known issues:
// - Black horizontal bars appear between frames (screen clearing/cursor positioning bug)
// - Frame height mismatch between subframes causes visual artifacts

using System.Diagnostics;
using System.Text;

namespace ConsoleImage.Core;

/// <summary>
///     EXPERIMENTAL: Plays braille interlace frames in a continuous rapid cycle for temporal super-resolution.
///     Each subframe uses a different brightness threshold; the viewer's eye averages them,
///     producing the illusion of more grey levels than braille dots can represent.
///     Known issues: black bars between frames due to screen clearing bug; frame height mismatches.
/// </summary>
public class BrailleInterlacePlayer : IDisposable
{
    private const string SyncStart = "\x1b[?2026h";
    private const string SyncEnd = "\x1b[?2026l";
    private const string AltScreenEnter = "\x1b[?1049h";
    private const string AltScreenExit = "\x1b[?1049l";
    private const string CursorHome = "\x1b[H";
    private const string CursorHide = "\x1b[?25l";
    private const string CursorShow = "\x1b[?25h";

    private static readonly string[] CursorMoveCache = BuildCursorMoveCache(300);

    private readonly List<BrailleFrame> _frames;
    private readonly bool _useAltScreen;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public BrailleInterlacePlayer(List<BrailleFrame> frames, bool useAltScreen = true)
    {
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));
        _useAltScreen = useAltScreen;
    }

    /// <summary>
    ///     Whether the player is currently running.
    /// </summary>
    public bool IsPlaying { get; private set; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Play interlace frames in a continuous loop until cancelled.
    ///     Supports Space to pause/resume and Q/Escape to quit.
    /// </summary>
    public async Task PlayAsync(CancellationToken externalCt = default)
    {
        if (_frames.Count == 0) return;

        ConsoleHelper.EnableAnsiSupport();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _cts.Token;

        // Pre-build frame buffers with diff rendering
        var frameBuffers = BuildFrameBuffers();

        // Get delay from first frame (all frames share the same delay)
        var subframeDelayMs = _frames[0].DelayMs;
        if (subframeDelayMs <= 0) subframeDelayMs = 12; // ~80Hz default

        if (_useAltScreen) Console.Write(AltScreenEnter);
        Console.Write(CursorHide);
        Console.Write("\x1b[2J");
        Console.Out.Flush();

        IsPlaying = true;
        var paused = false;
        var firstCycle = true;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Check for keyboard input
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.Q:
                        case ConsoleKey.Escape:
                            return;

                        case ConsoleKey.Spacebar:
                            paused = !paused;
                            break;
                    }
                }

                if (paused)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                double timeDebtMs = 0;

                for (var i = 0; i < _frames.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    // Check keyboard between subframes too
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        switch (key.Key)
                        {
                            case ConsoleKey.Q:
                            case ConsoleKey.Escape:
                                return;
                            case ConsoleKey.Spacebar:
                                paused = !paused;
                                if (paused) break;
                                continue;
                        }

                        if (paused) break;
                    }

                    var renderStart = Stopwatch.GetTimestamp();

                    // On subsequent cycles, use wrap-around buffer for frame 0
                    // to diff against last frame instead of full redraw
                    var buffer = (i == 0 && !firstCycle && _wrapBuffer != null)
                        ? _wrapBuffer
                        : frameBuffers[i];
                    Console.Write(buffer);
                    Console.Out.Flush();

                    // Adaptive timing
                    var (remainingDelay, newDebt) = FrameTiming.CalculateAdaptiveDelay(
                        subframeDelayMs, renderStart, timeDebtMs);
                    timeDebtMs = newDebt;
                    if (remainingDelay > 0)
                        await FrameTiming.ResponsiveDelayAsync(remainingDelay, ct);
                }

                firstCycle = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        finally
        {
            IsPlaying = false;
            Console.Write(CursorShow);
            if (_useAltScreen)
                Console.Write(AltScreenExit);
            Console.Write("\x1b[0m");
            Console.Out.Flush();
        }
    }

    /// <summary>
    ///     Stop playback.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    /// <summary>
    ///     Pre-build all frame buffers with diff-based rendering for minimal flicker.
    /// </summary>
    private string[] BuildFrameBuffers()
    {
        var frameBuffers = new string[_frames.Count];
        var sb = new StringBuilder(8192);
        Span<int> currStarts = stackalloc int[301];
        Span<int> prevStarts = stackalloc int[301];

        // Determine max height across all frames
        var maxHeight = 0;
        for (var i = 0; i < _frames.Count; i++)
        {
            var lineCount = 1;
            foreach (var c in _frames[i].Content)
                if (c == '\n')
                    lineCount++;
            if (lineCount > maxHeight)
                maxHeight = lineCount;
        }

        for (var i = 0; i < _frames.Count; i++)
        {
            sb.Clear();
            sb.Append(SyncStart);

            if (i == 0)
            {
                // First frame: full redraw
                sb.Append(CursorHome);
                AppendWithLineClearing(sb, _frames[i].Content, maxHeight);
            }
            else
            {
                // Diff against previous frame
                var curr = _frames[i].Content;
                var prev = _frames[i - 1].Content;
                var currLineCount = LineUtils.BuildLineStarts(curr, currStarts);
                var prevLineCount = LineUtils.BuildLineStarts(prev, prevStarts);
                var lineCount = Math.Max(currLineCount, prevLineCount);
                var abandonThreshold = (int)(lineCount * 0.6) + 1;

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
                            sb.Length = diffStart;
                            sb.Append(CursorHome);
                            AppendWithLineClearing(sb, curr, maxHeight);
                            break;
                        }

                        sb.Append(line < CursorMoveCache.Length
                            ? CursorMoveCache[line]
                            : $"\x1b[{line + 1};1H");
                        sb.Append("\x1b[2K");
                        sb.Append(currLine);
                        sb.Append("\x1b[0m");
                    }
                }
            }

            // Also build diff from last frame back to first (for seamless loop)
            sb.Append(SyncEnd);
            frameBuffers[i] = sb.ToString();
        }

        // Build a wrap-around buffer: diff from last frame to first for seamless looping
        sb.Clear();
        sb.Append(SyncStart);
        {
            var curr = _frames[0].Content;
            var prev = _frames[^1].Content;
            var currLineCount = LineUtils.BuildLineStarts(curr, currStarts);
            var prevLineCount = LineUtils.BuildLineStarts(prev, prevStarts);
            var lineCount = Math.Max(currLineCount, prevLineCount);
            var abandonThreshold = (int)(lineCount * 0.6) + 1;

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
                        sb.Length = diffStart;
                        sb.Append(CursorHome);
                        AppendWithLineClearing(sb, curr, maxHeight);
                        break;
                    }

                    sb.Append(line < CursorMoveCache.Length
                        ? CursorMoveCache[line]
                        : $"\x1b[{line + 1};1H");
                    sb.Append("\x1b[2K");
                    sb.Append(currLine);
                    sb.Append("\x1b[0m");
                }
            }
        }
        sb.Append(SyncEnd);

        // Replace index 0 buffer with wrap-around for all loops after the first
        // Store it so we can swap after first cycle
        _wrapBuffer = sb.ToString();

        return frameBuffers;
    }

    private string? _wrapBuffer;

    /// Pre-computed wrap-around buffer used after the first loop cycle
    /// to diff frame 0 against the last frame instead of doing a full redraw.

    private static void AppendWithLineClearing(StringBuilder sb, string content, int maxHeight)
    {
        var lineIdx = 0;
        var lineStart = 0;

        for (var i = 0; i <= content.Length; i++)
        {
            if (i == content.Length || content[i] == '\n')
            {
                sb.Append("\x1b[2K");
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
        }

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
