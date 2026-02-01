// DocumentPlayer - Plays back ConsoleImageDocument frames to the console
// Optimized for low allocation: diff-based rendering, pre-cached escape sequences,
// reusable StringBuilder for frame buffer construction.
// Supports sidecar subtitle tracks with optimized sequential timing.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using ConsoleImage.Core.Subtitles;

namespace ConsoleImage.Core;

/// <summary>
///     Plays ConsoleImageDocument frames to the console with animation support.
///     Uses diff-based rendering to only update changed lines between frames,
///     synchronized output for flicker-free display, and pre-cached escape sequences.
///     Supports subtitle rendering from sidecar .vtt/.srt tracks.
/// </summary>
public class DocumentPlayer : IDisposable
{
    // Pre-allocated ANSI escape sequences
    private const string SyncStart = "\x1b[?2026h";
    private const string SyncEnd = "\x1b[?2026l";
    private const string CursorHide = "\x1b[?25l";
    private const string CursorShow = "\x1b[?25h";
    private const string CursorHome = "\x1b[H";
    private const string ColorReset = "\x1b[0m";
    private const string AltScreenOn = "\x1b[?1049h";
    private const string AltScreenOff = "\x1b[?1049l";
    private const string ClearScreen = "\x1b[2J";

    // Pre-cached cursor move strings (avoids interpolation per frame)
    private static readonly string[] CursorMoveCache = BuildCursorMoveCache(300);
    private static readonly string BlankLine200 = new(' ', 200);

    private readonly ConsoleImageDocument _document;
    private readonly int _loopCount;
    private readonly float _speedMultiplier;
    private readonly SubtitleTrack? _subtitles;
    private bool _disposed;

    public DocumentPlayer(
        ConsoleImageDocument document,
        float? speedMultiplier = null,
        int? loopCount = null,
        SubtitleTrack? subtitles = null)
    {
        ConsoleHelper.EnableAnsiSupport();
        _document = document;
        _speedMultiplier = speedMultiplier ?? document.Settings.AnimationSpeedMultiplier;
        _loopCount = loopCount ?? document.Settings.LoopCount;
        _subtitles = subtitles;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Play the document to the console with diff-based rendering.
    /// </summary>
    public async Task PlayAsync(CancellationToken ct = default)
    {
        if (_document.Frames.Count == 0)
            return;

        // For single frame, just display it
        if (!_document.IsAnimated)
        {
            Console.Write(_document.Frames[0].Content);
            return;
        }

        // Pre-compute max height without LINQ allocation
        var maxHeight = 0;
        var frameWidth = 0;
        for (var i = 0; i < _document.Frames.Count; i++)
        {
            var h = _document.Frames[i].Height;
            if (h > maxHeight) maxHeight = h;
            if (_document.Frames[i].Width > frameWidth)
                frameWidth = _document.Frames[i].Width;
        }

        // Initialize subtitle renderer if we have a track
        SubtitleRenderer? subtitleRenderer = null;
        if (_subtitles != null && _subtitles.HasEntries)
        {
            var displayWidth = frameWidth > 0 ? frameWidth : _document.Settings.MaxWidth;
            try
            {
                displayWidth = Math.Min(displayWidth, Console.WindowWidth - 1);
            }
            catch
            {
            }

            subtitleRenderer = new SubtitleRenderer(displayWidth, 2, _document.Settings.UseColor);
        }

        // Subtitle row is right below the frame content
        var subtitleRow = maxHeight + 1;

        // Reusable StringBuilder for frame buffer (avoids per-frame allocation)
        var frameSb = new StringBuilder(8192);
        string? previousContent = null;
        string? previousSubtitle = null;

        // Enter alternate screen and hide cursor
        Console.Write(AltScreenOn);
        Console.Write(ClearScreen);
        Console.Write(CursorHide);

        try
        {
            var loopsRemaining = _loopCount == 0 ? int.MaxValue : _loopCount;

            while (loopsRemaining > 0 && !ct.IsCancellationRequested)
            {
                double cumulativeMs = 0;
                double timeDebtMs = 0;

                for (var i = 0; i < _document.Frames.Count && !ct.IsCancellationRequested; i++)
                {
                    var frame = _document.Frames[i];
                    var targetDelayMs = (double)frame.DelayMs / _speedMultiplier;

                    // Adaptive frame skipping: drop frames when behind schedule
                    if (i < _document.Frames.Count - 1 &&
                        FrameTiming.ShouldSkipFrame(targetDelayMs, ref timeDebtMs))
                    {
                        cumulativeMs += frame.DelayMs;
                        continue;
                    }

                    var renderStart = Stopwatch.GetTimestamp();

                    // Build optimized frame buffer with diff rendering
                    var buffer = BuildFrameBuffer(frameSb, frame.Content, previousContent,
                        i == 0 && previousContent == null);
                    previousContent = frame.Content;

                    // Write frame atomically
                    Console.Write(buffer);

                    // Render subtitles below the frame if we have a track
                    if (subtitleRenderer != null)
                    {
                        var entry = _subtitles!.GetActiveAt(TimeSpan.FromMilliseconds(cumulativeMs));
                        var subtitleContent = subtitleRenderer.RenderAtPosition(entry, subtitleRow);

                        // Only write subtitle if changed (avoid flickering)
                        if (subtitleContent != previousSubtitle)
                        {
                            Console.Write(subtitleContent);
                            previousSubtitle = subtitleContent;
                        }
                    }

                    Console.Out.Flush();
                    cumulativeMs += frame.DelayMs;

                    // Adaptive timing: compensate for render time
                    if (targetDelayMs > 0)
                    {
                        var (remainingDelay, newDebt) = FrameTiming.CalculateAdaptiveDelay(
                            targetDelayMs, renderStart, timeDebtMs);
                        timeDebtMs = newDebt;
                        if (remainingDelay > 0)
                            await FrameTiming.ResponsiveDelayAsync(remainingDelay, ct);
                    }
                }

                if (_loopCount != 0)
                    loopsRemaining--;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        finally
        {
            // Restore terminal state
            Console.Write(CursorShow);
            Console.Write(ColorReset);
            Console.Write(AltScreenOff);
        }
    }

    /// <summary>
    ///     Build frame buffer with diff-based rendering.
    ///     Only updates lines that changed between frames.
    ///     Uses pre-computed line offsets for O(N) total instead of O(N²) per-line scanning,
    ///     and merges change-counting with diff output into a single pass.
    /// </summary>
    private static string BuildFrameBuffer(StringBuilder sb, string content, string? previousContent, bool isFirstFrame)
    {
        sb.Clear();
        sb.Append(SyncStart);

        if (isFirstFrame || previousContent == null)
        {
            // Full redraw for first frame
            sb.Append(CursorHome);
            sb.Append(content);
        }
        else
        {
            // Build line offset tables in O(N) - stackalloc avoids heap allocation
            Span<int> currStarts = stackalloc int[301];
            Span<int> prevStarts = stackalloc int[301];
            var currLineCount = LineUtils.BuildLineStarts(content, currStarts);
            var prevLineCount = LineUtils.BuildLineStarts(previousContent, prevStarts);
            var maxLines = Math.Max(currLineCount, prevLineCount);

            // Pre-compute threshold for abandoning diff in favor of full redraw
            var abandonThreshold = (int)(maxLines * 0.6) + 1;

            // Single pass: compare lines and build diff output simultaneously.
            // If too many lines changed, abandon diff and fall back to full redraw.
            var diffStart = sb.Length;
            var changedLines = 0;

            for (var line = 0; line < maxLines; line++)
            {
                var currLine = LineUtils.GetLineFromStarts(content, currStarts, currLineCount, line);
                var prevLine = LineUtils.GetLineFromStarts(previousContent, prevStarts, prevLineCount, line);

                if (!currLine.SequenceEqual(prevLine))
                {
                    changedLines++;

                    // Too many changes - full redraw is faster
                    if (changedLines >= abandonThreshold)
                    {
                        sb.Length = diffStart;
                        sb.Append(CursorHome);
                        sb.Append(content);
                        break;
                    }

                    sb.Append(GetCursorMove(line));
                    sb.Append(currLine);

                    // Pad to clear leftover characters from longer previous line
                    var currVisible = GetVisibleLength(currLine);
                    var prevVisible = GetVisibleLength(prevLine);
                    if (currVisible < prevVisible)
                    {
                        var padding = Math.Min(prevVisible - currVisible, BlankLine200.Length);
                        sb.Append(BlankLine200.AsSpan(0, padding));
                    }

                    sb.Append(ColorReset);
                }
            }
        }

        sb.Append(SyncEnd);
        return sb.ToString();
    }

    /// <summary>
    ///     Get visible character count excluding ANSI escape sequences.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetVisibleLength(ReadOnlySpan<char> line)
    {
        var len = 0;
        var inEscape = false;

        foreach (var c in line)
            if (c == '\x1b')
            {
                inEscape = true;
            }
            else if (inEscape)
            {
                if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z') inEscape = false;
            }
            else
            {
                len++;
            }

        return len;
    }

    /// <summary>
    ///     Get cached cursor move escape sequence.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetCursorMove(int line)
    {
        return line < CursorMoveCache.Length ? CursorMoveCache[line] : $"\x1b[{line + 1};1H";
    }

    private static string[] BuildCursorMoveCache(int maxLines)
    {
        var cache = new string[maxLines];
        for (var i = 0; i < maxLines; i++)
            cache[i] = $"\x1b[{i + 1};1H";
        return cache;
    }

    /// <summary>
    ///     Display the document without animation (first frame only or all frames sequentially)
    /// </summary>
    public void Display(bool showAllFrames = false)
    {
        if (_document.Frames.Count == 0)
            return;

        if (!showAllFrames || !_document.IsAnimated)
        {
            Console.Write(_document.Frames[0].Content);
            return;
        }

        foreach (var frame in _document.Frames)
        {
            Console.Write(frame.Content);
            Console.WriteLine();
            Console.WriteLine($"--- Frame (delay: {frame.DelayMs}ms) ---");
        }
    }

    /// <summary>
    ///     Get document info as a string
    /// </summary>
    public string GetInfo()
    {
        var info = new StringBuilder();
        info.AppendLine($"Type: {_document.Type}");
        info.AppendLine($"Version: {_document.Version}");
        info.AppendLine($"Created: {_document.Created:O}");
        if (!string.IsNullOrEmpty(_document.SourceFile))
            info.AppendLine($"Source: {_document.SourceFile}");
        info.AppendLine($"Render Mode: {_document.RenderMode}");
        info.AppendLine($"Frames: {_document.FrameCount}");
        if (_document.IsAnimated)
        {
            info.AppendLine($"Duration: {_document.TotalDurationMs}ms");
            info.AppendLine($"Speed: {_document.Settings.AnimationSpeedMultiplier}x");
            info.AppendLine(
                $"Loop Count: {(_document.Settings.LoopCount == 0 ? "infinite" : _document.Settings.LoopCount.ToString())}");
        }

        info.AppendLine($"Size: {_document.Settings.MaxWidth}x{_document.Settings.MaxHeight}");
        info.AppendLine($"Color: {(_document.Settings.UseColor ? "yes" : "no")}");
        info.AppendLine($"Gamma: {_document.Settings.Gamma}");
        info.AppendLine($"Contrast: {_document.Settings.ContrastPower}");
        if (_document.Settings.SubtitlesEnabled)
        {
            info.AppendLine($"Subtitles: yes (sidecar: {_document.Settings.SubtitleFile ?? "auto-detect"})");
            if (!string.IsNullOrEmpty(_document.Settings.SubtitleLanguage))
                info.AppendLine($"Subtitle Language: {_document.Settings.SubtitleLanguage}");
        }

        if (_subtitles != null)
            info.AppendLine($"Subtitle Track: {_subtitles.Count} entries");

        return info.ToString();
    }
}