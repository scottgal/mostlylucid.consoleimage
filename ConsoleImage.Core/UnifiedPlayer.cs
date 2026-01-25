// UnifiedPlayer - Consolidated player for ConsoleImageDocument playback
// Replaces separate DocumentPlayer, AsciiAnimationPlayer implementations
// Supports all document formats including compressed archives

using System.Text;
using ConsoleImage.Core.Subtitles;

namespace ConsoleImage.Core;

/// <summary>
/// Unified player for ConsoleImageDocument playback.
/// Supports JSON, NDJSON (streaming), and compressed (.cidz, .7z) formats.
/// Consolidates functionality from DocumentPlayer and AsciiAnimationPlayer.
/// </summary>
public class UnifiedPlayer : IDisposable
{
    private readonly ConsoleImageDocument _document;
    private readonly UnifiedPlayerOptions _options;
    private readonly StatusLine? _statusLine;
    private readonly SubtitleRenderer? _subtitleRenderer;
    private readonly SubtitleTrack? _subtitleTrack;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Event raised when a frame is rendered.
    /// </summary>
    public event Action<int, int>? FrameRendered;

    /// <summary>
    /// Event raised when playback completes.
    /// </summary>
    public event Action<int>? PlaybackComplete;

    /// <summary>
    /// Create player from a document.
    /// </summary>
    public UnifiedPlayer(ConsoleImageDocument document, UnifiedPlayerOptions? options = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _options = options ?? new UnifiedPlayerOptions();

        // Get frame width for status line and subtitles
        int frameWidth = 120;
        if (_document.Frames.Count > 0)
            frameWidth = _document.Frames[0].Width;
        try { frameWidth = Math.Min(frameWidth, Console.WindowWidth - 1); } catch { }

        if (_options.ShowStatus)
        {
            _statusLine = new StatusLine(frameWidth, true);
        }

        // Initialize subtitle support if document has subtitles and display is enabled
        if (_options.ShowSubtitles && _document.Subtitles != null)
        {
            _subtitleTrack = _document.Subtitles.ToTrack();
            _subtitleRenderer = new SubtitleRenderer(frameWidth, 2, true);
        }
    }

    /// <summary>
    /// Load and create player from file path.
    /// Auto-detects format (JSON, NDJSON, compressed).
    /// </summary>
    public static async Task<UnifiedPlayer> LoadAsync(
        string path,
        UnifiedPlayerOptions? options = null,
        CancellationToken ct = default)
    {
        var doc = await ConsoleImageDocument.LoadAsync(path, ct);
        var opts = options ?? new UnifiedPlayerOptions();
        opts.SourceFileName ??= Path.GetFileName(path);
        return new UnifiedPlayer(doc, opts);
    }

    /// <summary>
    /// Play the document with animation support.
    /// </summary>
    public async Task PlayAsync(CancellationToken externalCt = default)
    {
        if (_document.Frames.Count == 0)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _cts.Token;

        ConsoleHelper.EnableAnsiSupport();

        // Enter alternate screen if requested
        if (_options.UseAltScreen)
            Console.Write("\x1b[?1049h");

        Console.Write("\x1b[?25l"); // Hide cursor
        Console.Out.Flush();

        try
        {
            var loopsDone = 0;
            var effectiveLoops = _options.LoopCountOverride ?? _document.Settings.LoopCount;

            while (!ct.IsCancellationRequested)
            {
                await PlayFramesAsync(loopsDone + 1, effectiveLoops, ct);
                loopsDone++;

                if (effectiveLoops > 0 && loopsDone >= effectiveLoops)
                    break;
            }

            PlaybackComplete?.Invoke(loopsDone);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        finally
        {
            Console.Write("\x1b[?25h"); // Show cursor
            if (_options.UseAltScreen)
                Console.Write("\x1b[?1049l");
            Console.Write("\x1b[0m");
            Console.Out.Flush();
        }
    }

    /// <summary>
    /// Display a single frame or all frames sequentially (no animation timing).
    /// </summary>
    public void Display(int? frameIndex = null)
    {
        ConsoleHelper.EnableAnsiSupport();

        if (frameIndex.HasValue)
        {
            if (frameIndex < 0 || frameIndex >= _document.Frames.Count)
                throw new ArgumentOutOfRangeException(nameof(frameIndex));

            Console.Write(_document.Frames[frameIndex.Value].Content);
            Console.Write("\x1b[0m");
        }
        else
        {
            foreach (var frame in _document.Frames)
            {
                Console.Write(frame.Content);
                Console.Write("\x1b[0m\n");
            }
        }
        Console.Out.Flush();
    }

    /// <summary>
    /// Stop playback if running in background.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Get document information as formatted string.
    /// </summary>
    public string GetInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Source: {_document.SourceFile ?? "(unknown)"}");
        sb.AppendLine($"Mode: {_document.RenderMode}");
        sb.AppendLine($"Frames: {_document.FrameCount}");
        sb.AppendLine($"Animated: {_document.IsAnimated}");
        if (_document.IsAnimated)
        {
            sb.AppendLine($"Duration: {TimeSpan.FromMilliseconds(_document.TotalDurationMs):mm\\:ss\\.fff}");
            var avgDelay = _document.Frames.Average(f => f.DelayMs);
            sb.AppendLine($"Avg FPS: {1000.0 / avgDelay:F1}");
        }
        if (_document.Frames.Count > 0)
        {
            sb.AppendLine($"Size: {_document.Frames[0].Width}x{_document.Frames[0].Height}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Get the underlying document.
    /// </summary>
    public ConsoleImageDocument Document => _document;

    private async Task PlayFramesAsync(int currentLoop, int totalLoops, CancellationToken ct)
    {
        var speedMultiplier = _options.SpeedMultiplierOverride ?? _document.Settings.AnimationSpeedMultiplier;
        if (speedMultiplier <= 0) speedMultiplier = 1.0f;

        string? previousFrame = null;
        var totalFrames = _document.Frames.Count;

        // Track current playback time for subtitle sync
        long currentTimeMs = 0;

        for (int i = 0; i < totalFrames; i++)
        {
            ct.ThrowIfCancellationRequested();

            var frame = _document.Frames[i];
            var buffer = BuildFrameBuffer(frame.Content, previousFrame, i == 0);

            // Track lines used for overlays (subtitles + status)
            int extraLines = 0;

            // Add subtitles if enabled and document has them
            if (_subtitleRenderer != null && _subtitleTrack != null)
            {
                var currentTime = TimeSpan.FromMilliseconds(currentTimeMs);
                var activeSubtitle = _subtitleTrack.GetActiveAt(currentTime.TotalSeconds);
                var subtitleContent = _subtitleRenderer.RenderEntry(activeSubtitle);

                if (!string.IsNullOrEmpty(subtitleContent))
                {
                    // Position subtitles below frame content
                    var subtitleLines = subtitleContent.Split('\n');
                    for (int j = 0; j < subtitleLines.Length; j++)
                    {
                        buffer += $"\x1b[{frame.Height + 1 + j};1H\x1b[2K{subtitleLines[j]}";
                    }
                    extraLines = subtitleLines.Length;
                }
                else
                {
                    // Clear subtitle area when no active subtitle
                    buffer += $"\x1b[{frame.Height + 1};1H\x1b[2K";
                    buffer += $"\x1b[{frame.Height + 2};1H\x1b[2K";
                    extraLines = 2; // Reserve space for subtitles
                }
            }

            // Add status line if enabled (below subtitles)
            if (_statusLine != null)
            {
                var statusInfo = new StatusLine.StatusInfo
                {
                    FileName = _options.SourceFileName ?? _document.SourceFile ?? "document",
                    SourceWidth = frame.Width,
                    SourceHeight = frame.Height,
                    OutputWidth = frame.Width,
                    OutputHeight = frame.Height,
                    RenderMode = _document.RenderMode,
                    CurrentFrame = i + 1,
                    TotalFrames = totalFrames,
                    LoopNumber = currentLoop,
                    TotalLoops = totalLoops,
                    CurrentTime = TimeSpan.FromMilliseconds(currentTimeMs),
                    TotalDuration = TimeSpan.FromMilliseconds(_document.TotalDurationMs)
                };
                buffer += $"\x1b[{frame.Height + 1 + extraLines};1H\x1b[2K{_statusLine.Render(statusInfo)}";
            }

            // End synchronized output
            buffer += "\x1b[?2026l";

            Console.Write(buffer);
            Console.Out.Flush();

            previousFrame = frame.Content;
            FrameRendered?.Invoke(i, totalFrames);

            // Frame delay with speed multiplier
            var delayMs = (int)(frame.DelayMs / speedMultiplier);
            if (delayMs > 0)
            {
                await ResponsiveDelayAsync(delayMs, ct);
            }

            // Update current time for next frame's subtitle lookup
            currentTimeMs += frame.DelayMs;
        }
    }

    private static string BuildFrameBuffer(string content, string? previousContent, bool isFirstFrame)
    {
        var sb = new StringBuilder();
        sb.Append("\x1b[?2026h"); // Begin synchronized output

        if (isFirstFrame || previousContent == null)
        {
            sb.Append("\x1b[H"); // Home cursor
            sb.Append(content);
        }
        else
        {
            // Diff-based rendering for efficiency
            var currLines = content.Split('\n');
            var prevLines = previousContent.Split('\n');
            var maxLines = Math.Max(currLines.Length, prevLines.Length);
            var changedLines = 0;

            // Count changes
            for (int i = 0; i < maxLines; i++)
            {
                var currLine = i < currLines.Length ? currLines[i].TrimEnd('\r') : "";
                var prevLine = i < prevLines.Length ? prevLines[i].TrimEnd('\r') : "";
                if (currLine != prevLine) changedLines++;
            }

            // If more than 60% changed, do full redraw
            if (changedLines > maxLines * 0.6)
            {
                sb.Append("\x1b[H");
                sb.Append(content);
            }
            else
            {
                // Only update changed lines
                for (int i = 0; i < maxLines; i++)
                {
                    var currLine = i < currLines.Length ? currLines[i].TrimEnd('\r') : "";
                    var prevLine = i < prevLines.Length ? prevLines[i].TrimEnd('\r') : "";

                    if (currLine != prevLine)
                    {
                        sb.Append($"\x1b[{i + 1};1H");
                        sb.Append(currLine);

                        var currVisible = GetVisibleLength(currLine);
                        var prevVisible = GetVisibleLength(prevLine);
                        if (currVisible < prevVisible)
                            sb.Append(new string(' ', prevVisible - currVisible));

                        sb.Append("\x1b[0m");
                    }
                }
            }
        }

        return sb.ToString();
    }

    private static int GetVisibleLength(string line)
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

    private static async Task ResponsiveDelayAsync(int totalMs, CancellationToken ct)
    {
        const int chunkMs = 50;
        var remaining = totalMs;
        while (remaining > 0 && !ct.IsCancellationRequested)
        {
            var delay = Math.Min(remaining, chunkMs);
            await Task.Delay(delay, ct);
            remaining -= delay;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Dispose();
    }
}

/// <summary>
/// Options for UnifiedPlayer.
/// </summary>
public class UnifiedPlayerOptions
{
    /// <summary>
    /// Override the loop count from document settings.
    /// 0 = infinite, null = use document setting.
    /// </summary>
    public int? LoopCountOverride { get; set; }

    /// <summary>
    /// Override the speed multiplier from document settings.
    /// 1.0 = normal, 2.0 = double speed, 0.5 = half speed.
    /// </summary>
    public float? SpeedMultiplierOverride { get; set; }

    /// <summary>
    /// Use alternate screen buffer (preserves terminal scrollback).
    /// </summary>
    public bool UseAltScreen { get; set; } = true;

    /// <summary>
    /// Show status line below output.
    /// </summary>
    public bool ShowStatus { get; set; }

    /// <summary>
    /// Show subtitles during playback (if document contains subtitle track).
    /// Default is true - subtitles are shown when available.
    /// </summary>
    public bool ShowSubtitles { get; set; } = true;

    /// <summary>
    /// Source file name for status display.
    /// </summary>
    public string? SourceFileName { get; set; }
}
