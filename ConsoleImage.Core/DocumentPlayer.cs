// DocumentPlayer - Plays back ConsoleImageDocument frames to the console
// Optimized for low allocation: diff-based rendering, pre-cached escape sequences,
// reusable StringBuilder for frame buffer construction.
// Supports sidecar subtitle tracks with optimized sequential timing.

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
        int frameWidth = 0;
        for (int i = 0; i < _document.Frames.Count; i++)
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
            try { displayWidth = Math.Min(displayWidth, Console.WindowWidth - 1); } catch { }
            subtitleRenderer = new SubtitleRenderer(displayWidth, 2, _document.Settings.UseColor);
        }

        // Subtitle row is right below the frame content
        var subtitleRow = maxHeight + 1;

        // Reusable StringBuilder for frame buffer (avoids per-frame allocation)
        var frameSb = new StringBuilder(8192);
        string? previousContent = null;
        string? previousSubtitle = null;

        // Hide cursor during animation
        Console.Write(CursorHide);

        try
        {
            var loopsRemaining = _loopCount == 0 ? int.MaxValue : _loopCount;

            while (loopsRemaining > 0 && !ct.IsCancellationRequested)
            {
                double cumulativeMs = 0;

                for (var i = 0; i < _document.Frames.Count && !ct.IsCancellationRequested; i++)
                {
                    var frame = _document.Frames[i];

                    // Build optimized frame buffer with diff rendering
                    var buffer = BuildFrameBuffer(frameSb, frame.Content, previousContent, i == 0 && previousContent == null);
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

                    // Wait for frame delay with responsive cancellation
                    if (frame.DelayMs > 0)
                    {
                        var adjustedDelay = (int)(frame.DelayMs / _speedMultiplier);
                        cumulativeMs += frame.DelayMs;
                        if (adjustedDelay > 0)
                            await ResponsiveDelay(adjustedDelay, ct);
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
            // Show cursor again
            Console.Write(CursorShow);
            Console.Write(ColorReset);
        }
    }

    /// <summary>
    ///     Build frame buffer with diff-based rendering.
    ///     Only updates lines that changed between frames.
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
            // Count lines in both frames
            var currLineCount = CountLines(content);
            var prevLineCount = CountLines(previousContent);
            var maxLines = Math.Max(currLineCount, prevLineCount);

            // First pass: count changes to decide diff vs full redraw
            var changedLines = 0;
            for (int line = 0; line < maxLines; line++)
            {
                var currLine = GetLineSpan(content, line);
                var prevLine = GetLineSpan(previousContent, line);
                if (!currLine.SequenceEqual(prevLine)) changedLines++;
            }

            // If >60% changed, full redraw is faster
            if (changedLines > maxLines * 0.6)
            {
                sb.Append(CursorHome);
                sb.Append(content);
            }
            else
            {
                // Diff rendering: only update changed lines
                for (int line = 0; line < maxLines; line++)
                {
                    var currLine = GetLineSpan(content, line);
                    var prevLine = GetLineSpan(previousContent, line);

                    if (!currLine.SequenceEqual(prevLine))
                    {
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
        }

        sb.Append(SyncEnd);
        return sb.ToString();
    }

    /// <summary>
    ///     Count newlines in a string without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        var count = 1;
        foreach (var c in content)
        {
            if (c == '\n') count++;
        }
        return count;
    }

    /// <summary>
    ///     Get a line from content by index without allocating the split array.
    /// </summary>
    private static ReadOnlySpan<char> GetLineSpan(string content, int lineIndex)
    {
        if (string.IsNullOrEmpty(content)) return ReadOnlySpan<char>.Empty;

        var currentLine = 0;
        var lineStart = 0;

        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                if (currentLine == lineIndex)
                {
                    var end = i;
                    if (end > lineStart && content[end - 1] == '\r') end--;
                    return content.AsSpan(lineStart, end - lineStart);
                }
                currentLine++;
                lineStart = i + 1;
            }
        }

        // Last line (no trailing newline)
        if (currentLine == lineIndex && lineStart < content.Length)
        {
            var end = content.Length;
            if (end > lineStart && content[end - 1] == '\r') end--;
            return content.AsSpan(lineStart, end - lineStart);
        }

        return ReadOnlySpan<char>.Empty;
    }

    /// <summary>
    ///     Get visible character count excluding ANSI escape sequences.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetVisibleLength(ReadOnlySpan<char> line)
    {
        int len = 0;
        bool inEscape = false;

        foreach (char c in line)
        {
            if (c == '\x1b')
                inEscape = true;
            else if (inEscape)
            {
                if (c == 'm') inEscape = false;
            }
            else
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
    ///     Delay with responsive cancellation (checks every 50ms).
    /// </summary>
    private static async Task ResponsiveDelay(int totalMs, CancellationToken ct)
    {
        const int chunkMs = 50;
        var remaining = totalMs;

        while (remaining > 0 && !ct.IsCancellationRequested)
        {
            var delay = Math.Min(remaining, chunkMs);
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            remaining -= delay;
        }
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
