// ConsolePlayer - Plays PlayerDocument frames to the console
// Zero dependencies beyond .NET runtime

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ConsoleImage.Player;

/// <summary>
///     Plays ConsoleImage documents directly to the console.
///     Supports smooth animation with DECSET 2026 synchronized output.
/// </summary>
public class ConsolePlayer : IDisposable
{
    private readonly int _loopCount;
    private readonly PlayerSubtitleTrack? _subtitles;
    private readonly int _subtitleWidth;
    private bool _disposed;

    /// <summary>
    ///     Create a player for the given document.
    /// </summary>
    /// <param name="document">The document to play</param>
    /// <param name="speedMultiplier">Speed override (null = use document settings)</param>
    /// <param name="loopCount">Loop count override (null = use document settings, 0 = infinite)</param>
    /// <param name="subtitlePath">Optional path to SRT/VTT subtitle file</param>
    public ConsolePlayer(
        PlayerDocument document,
        float? speedMultiplier = null,
        int? loopCount = null,
        string? subtitlePath = null)
    {
        ConsoleHelper.EnableAnsiSupport();
        Document = document;
        SpeedMultiplier = speedMultiplier ?? document.Settings.AnimationSpeedMultiplier;
        _loopCount = loopCount ?? document.Settings.LoopCount;
        _subtitleWidth = document.Settings.MaxWidth > 0 ? document.Settings.MaxWidth : 80;

        // Load external subtitles if provided
        if (!string.IsNullOrEmpty(subtitlePath) && File.Exists(subtitlePath))
            _subtitles = LoadSubtitlesFromFile(subtitlePath);
    }

    /// <summary>
    ///     The document being played
    /// </summary>
    public PlayerDocument Document { get; }

    /// <summary>
    ///     Current playback speed multiplier. Can be changed during playback.
    ///     Values > 1 speed up, values &lt; 1 slow down.
    /// </summary>
    public float SpeedMultiplier { get; set; }

    /// <summary>
    ///     Maximum playback duration in milliseconds (content time, not wall time).
    ///     Null or 0 = no limit. Playback stops after this amount of content has played.
    /// </summary>
    public int? MaxDurationMs { get; set; }

    /// <summary>
    ///     First frame to play (inclusive, 0-based). Null = start from beginning.
    /// </summary>
    public int? StartFrame { get; set; }

    /// <summary>
    ///     Last frame to play (exclusive, 0-based). Null = play to end.
    /// </summary>
    public int? EndFrame { get; set; }

    /// <summary>
    ///     Frame step for downsampling. 1 = every frame (default), 2 = every other frame, etc.
    ///     Skipped frames still contribute to elapsed time for subtitle sync.
    /// </summary>
    public int FrameStep { get; set; } = 1;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Event raised before each frame is displayed
    /// </summary>
    public event Action<int, int>? OnFrameChanged;

    /// <summary>
    ///     Event raised when a loop completes
    /// </summary>
    public event Action<int>? OnLoopComplete;

    /// <summary>
    ///     Play the document asynchronously with animation support.
    ///     Respects StartFrame, EndFrame, MaxDurationMs, FrameStep, and SpeedMultiplier.
    /// </summary>
    public async Task PlayAsync(CancellationToken ct = default)
    {
        if (Document.Frames.Count == 0)
            return;

        // Resolve frame range
        var firstFrame = Math.Clamp(StartFrame ?? 0, 0, Document.Frames.Count - 1);
        var lastFrame = Math.Clamp(EndFrame ?? Document.Frames.Count, 1, Document.Frames.Count);
        var step = Math.Max(1, FrameStep);

        // Single frame - just display it
        if (!Document.IsAnimated || lastFrame - firstFrame <= 1)
        {
            Console.Write(Document.Frames[firstFrame].Content);
            DisplaySubtitle(0);
            return;
        }

        var maxHeight = Document.Frames.Max(f => f.Height);
        var subtitleLines = _subtitles != null ? 2 : 0;
        var totalHeight = maxHeight + subtitleLines;

        // Hide cursor during animation
        Console.Write("\x1b[?25l");

        try
        {
            var loopsRemaining = _loopCount == 0 ? int.MaxValue : _loopCount;
            var currentLoop = 0;
            var totalElapsedMs = 0;
            var maxDuration = MaxDurationMs ?? 0;
            var isFirstFrame = true;

            while (loopsRemaining > 0 && !ct.IsCancellationRequested)
            {
                for (var i = firstFrame; i < lastFrame && !ct.IsCancellationRequested; i += step)
                {
                    // Check duration limit
                    if (maxDuration > 0 && totalElapsedMs >= maxDuration)
                        goto done;

                    var frame = Document.Frames[i];
                    OnFrameChanged?.Invoke(i, Document.Frames.Count);

                    // Move cursor to start (except first displayed frame)
                    if (!isFirstFrame)
                        Console.Write($"\x1b[{totalHeight}A\r");
                    isFirstFrame = false;

                    // Synchronized output for flicker-free rendering
                    Console.Write("\x1b[?2026h");
                    Console.Write(frame.Content);

                    // Display subtitle if available
                    if (_subtitles != null) DisplaySubtitle(totalElapsedMs / 1000.0);

                    Console.Write("\x1b[?2026l");
                    Console.Out.Flush();

                    // Accumulate elapsed time for all frames in the step (including skipped)
                    var stepDelayMs = 0;
                    for (var s = 0; s < step && i + s < lastFrame; s++)
                        stepDelayMs += Document.Frames[i + s].DelayMs;
                    totalElapsedMs += stepDelayMs;

                    // Adaptive delay: compensate for render time
                    if (stepDelayMs > 0)
                    {
                        var renderStart = Stopwatch.GetTimestamp();
                        var targetDelay = (int)(stepDelayMs / SpeedMultiplier);

                        // Subtract time already spent rendering
                        var renderTimeMs = (Stopwatch.GetTimestamp() - renderStart) * 1000.0 /
                                           Stopwatch.Frequency;
                        var remainingDelay = Math.Max(1, targetDelay - (int)renderTimeMs);
                        await Task.Delay(remainingDelay, ct);
                    }
                }

                currentLoop++;
                OnLoopComplete?.Invoke(currentLoop);

                if (_loopCount != 0)
                    loopsRemaining--;
            }

            done: ;
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        finally
        {
            // Restore cursor and reset colors
            Console.Write("\x1b[?25h");
            Console.Write("\x1b[0m");
        }
    }

    /// <summary>
    ///     Play subtitles only (no video) - useful as a subtitle viewer.
    /// </summary>
    /// <param name="subtitlePath">Path to SRT/VTT file</param>
    /// <param name="ct">Cancellation token</param>
    public static async Task PlaySubtitlesOnlyAsync(string subtitlePath, CancellationToken ct = default)
    {
        var subtitles = LoadSubtitlesFromFile(subtitlePath);
        if (subtitles == null || subtitles.Entries.Count == 0)
        {
            Console.Error.WriteLine($"No subtitles found in: {subtitlePath}");
            return;
        }

        Console.Error.WriteLine($"Playing subtitles: {Path.GetFileName(subtitlePath)}");
        Console.Error.WriteLine($"Entries: {subtitles.Entries.Count}");

        var lastEntry = -1;
        var startTime = DateTime.UtcNow;

        // Hide cursor
        Console.Write("\x1b[?25l");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var entry = subtitles.GetActiveAt(elapsed / 1000.0);

                if (entry != null && entry.Index != lastEntry)
                {
                    // Clear previous subtitle (2 lines)
                    Console.Write("\x1b[2K\r");
                    Console.CursorTop = Math.Max(0, Console.CursorTop - 1);
                    Console.Write("\x1b[2K\r");

                    // Display new subtitle
                    var lines = WrapText(entry.Text.Trim(), 80);
                    Console.WriteLine(lines.Length > 0 ? lines[0] : "");
                    Console.WriteLine(lines.Length > 1 ? lines[1] : "");

                    lastEntry = entry.Index;
                }
                else if (entry == null && lastEntry != -1)
                {
                    // Clear subtitle when none active
                    Console.Write("\x1b[2K\r");
                    Console.CursorTop = Math.Max(0, Console.CursorTop - 1);
                    Console.Write("\x1b[2K\r");
                    lastEntry = -1;
                }

                // Check if we've passed the last subtitle
                var lastSub = subtitles.Entries.LastOrDefault();
                if (lastSub != null && elapsed > lastSub.EndMs + 2000)
                    break;

                await Task.Delay(50, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        finally
        {
            Console.Write("\x1b[?25h");
            Console.Write("\x1b[0m");
            Console.WriteLine();
        }
    }

    private void DisplaySubtitle(double seconds)
    {
        if (_subtitles == null) return;

        var entry = _subtitles.GetActiveAt(seconds);
        var text = entry?.Text.Trim() ?? "";

        // Wrap and display 2 lines below the frame
        var lines = WrapText(text, _subtitleWidth);
        Console.WriteLine();
        Console.Write("\x1b[2K"); // Clear line
        Console.WriteLine(CenterText(lines.Length > 0 ? lines[0] : "", _subtitleWidth));
        Console.Write("\x1b[2K"); // Clear line
        Console.Write(CenterText(lines.Length > 1 ? lines[1] : "", _subtitleWidth));
    }

    private static string[] WrapText(string text, int maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        if (text.Length <= maxWidth) return new[] { text };

        // Simple word wrap to 2 lines
        var words = text.Split(' ');
        var line1 = "";
        var line2 = "";

        foreach (var word in words)
            if (line1.Length + word.Length + 1 <= maxWidth)
                line1 = line1.Length == 0 ? word : line1 + " " + word;
            else if (line2.Length + word.Length + 1 <= maxWidth)
                line2 = line2.Length == 0 ? word : line2 + " " + word;
            else
                break; // Text too long, truncate

        return string.IsNullOrEmpty(line2) ? new[] { line1 } : new[] { line1, line2 };
    }

    private static string CenterText(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length >= width) return text.Substring(0, width);
        var pad = (width - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private static PlayerSubtitleTrack? LoadSubtitlesFromFile(string path)
    {
        if (!File.Exists(path)) return null;

        var track = new PlayerSubtitleTrack { SourceFile = path };
        var lines = File.ReadAllLines(path);
        var isVtt = path.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase);

        var index = 0;
        var i = 0;

        // Skip VTT header
        if (isVtt)
            while (i < lines.Length && !lines[i].Contains("-->"))
                i++;

        while (i < lines.Length)
        {
            var line = lines[i].Trim();

            // Skip empty lines and index numbers
            if (string.IsNullOrEmpty(line) || (int.TryParse(line, out _) && !line.Contains("-->")))
            {
                i++;
                continue;
            }

            // Parse timestamp line: "00:00:01,000 --> 00:00:04,000"
            if (line.Contains("-->"))
            {
                var parts = line.Split(new[] { "-->" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var start = ParseTimestamp(parts[0].Trim());
                    var end = ParseTimestamp(parts[1].Trim().Split(' ')[0]); // Remove any trailing style info

                    // Collect text lines
                    i++;
                    var text = new StringBuilder();
                    while (i < lines.Length && !string.IsNullOrEmpty(lines[i].Trim()) &&
                           !int.TryParse(lines[i].Trim(), out _) && !lines[i].Contains("-->"))
                    {
                        if (text.Length > 0) text.Append(' ');
                        // Strip HTML tags
                        var cleaned = Regex.Replace(lines[i], "<[^>]+>", "");
                        text.Append(cleaned.Trim());
                        i++;
                    }

                    track.Entries.Add(new PlayerSubtitleEntry
                    {
                        Index = index++,
                        StartMs = start,
                        EndMs = end,
                        Text = text.ToString()
                    });
                    continue;
                }
            }

            i++;
        }

        return track.Entries.Count > 0 ? track : null;
    }

    private static long ParseTimestamp(string ts)
    {
        // Formats: "00:00:01,000" (SRT) or "00:00:01.000" (VTT) or "00:01.000" (VTT short)
        ts = ts.Replace(',', '.');
        var parts = ts.Split(':');

        long hours = 0, minutes = 0;
        double seconds = 0;

        if (parts.Length == 3)
        {
            long.TryParse(parts[0], out hours);
            long.TryParse(parts[1], out minutes);
            double.TryParse(parts[2], NumberStyles.Any,
                CultureInfo.InvariantCulture, out seconds);
        }
        else if (parts.Length == 2)
        {
            long.TryParse(parts[0], out minutes);
            double.TryParse(parts[1], NumberStyles.Any,
                CultureInfo.InvariantCulture, out seconds);
        }

        return (hours * 3600 + minutes * 60) * 1000 + (long)(seconds * 1000);
    }

    /// <summary>
    ///     Display a single frame (or all frames without animation).
    /// </summary>
    public void Display(bool showAllFrames = false)
    {
        if (Document.Frames.Count == 0)
            return;

        if (!showAllFrames || !Document.IsAnimated)
        {
            Console.Write(Document.Frames[0].Content);
            return;
        }

        foreach (var frame in Document.Frames)
        {
            Console.Write(frame.Content);
            Console.WriteLine();
            Console.WriteLine($"--- Frame (delay: {frame.DelayMs}ms) ---");
        }
    }

    /// <summary>
    ///     Get document and player info as a string.
    /// </summary>
    public string GetInfo()
    {
        var info = new StringBuilder();
        info.AppendLine($"Version: {Document.Version}");
        info.AppendLine($"Created: {Document.Created:O}");
        if (!string.IsNullOrEmpty(Document.SourceFile))
            info.AppendLine($"Source: {Document.SourceFile}");
        info.AppendLine($"Render Mode: {Document.RenderMode}");
        info.AppendLine($"Frames: {Document.FrameCount}");
        if (Document.IsAnimated)
        {
            info.AppendLine($"Duration: {Document.TotalDurationMs}ms");
            info.AppendLine($"Speed: {SpeedMultiplier}x");
            info.AppendLine($"Loop Count: {(_loopCount == 0 ? "infinite" : _loopCount.ToString())}");
        }

        info.AppendLine($"Size: {Document.Settings.MaxWidth}x{Document.Settings.MaxHeight}");
        info.AppendLine($"Color: {(Document.Settings.UseColor ? "yes" : "no")}");

        return info.ToString();
    }

    /// <summary>
    ///     Create a player directly from a JSON file.
    /// </summary>
    /// <param name="path">Path to the document file</param>
    /// <param name="speedMultiplier">Speed multiplier (null = use document default)</param>
    /// <param name="loopCount">Loop count (null = use document default, 0 = infinite)</param>
    /// <param name="subtitlePath">Optional path to SRT/VTT subtitle file</param>
    /// <param name="ct">Cancellation token</param>
    public static async Task<ConsolePlayer> FromFileAsync(
        string path,
        float? speedMultiplier = null,
        int? loopCount = null,
        string? subtitlePath = null,
        CancellationToken ct = default)
    {
        var doc = await PlayerDocument.LoadAsync(path, ct);
        return new ConsolePlayer(doc, speedMultiplier, loopCount, subtitlePath);
    }

    /// <summary>
    ///     Create a player directly from a JSON string.
    /// </summary>
    public static ConsolePlayer FromJson(
        string json,
        float? speedMultiplier = null,
        int? loopCount = null,
        string? subtitlePath = null)
    {
        var doc = PlayerDocument.FromJson(json);
        return new ConsolePlayer(doc, speedMultiplier, loopCount, subtitlePath);
    }

}