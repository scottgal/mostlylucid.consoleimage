// StatusLine - Display information below rendered output
// Shows filename, resolution, progress, duration, etc.

using System.Text;

namespace ConsoleImage.Core;

/// <summary>
///     Displays a status line below rendered ASCII art with file info, progress, etc.
/// </summary>
public class StatusLine
{
    private readonly int _maxWidth;
    private readonly bool _useColor;

    public StatusLine(int maxWidth = 80, bool useColor = true)
    {
        _maxWidth = maxWidth;
        _useColor = useColor;
    }

    /// <summary>
    ///     Render the status line as a string - compact format with essential info
    /// </summary>
    public string Render(StatusInfo info)
    {
        var sb = new StringBuilder();

        // Use dim color for status line
        if (_useColor)
            sb.Append("\x1b[2m"); // Dim

        // Build compact info parts
        var parts = new List<string>();

        // Filename (short)
        if (!string.IsNullOrEmpty(info.FileName))
        {
            var name = Path.GetFileName(info.FileName);
            if (name.Length > 20)
                name = name[..17] + "...";
            parts.Add(name);
        }

        // Mode
        if (!string.IsNullOrEmpty(info.RenderMode))
            parts.Add(info.RenderMode);

        // Time: current/total
        if (info.CurrentTime.HasValue && info.TotalDuration.HasValue)
        {
            var currentSec = (int)info.CurrentTime.Value.TotalSeconds;
            var totalSec = (int)info.TotalDuration.Value.TotalSeconds;
            parts.Add($"{currentSec}s/{totalSec}s");
        }

        // Paused indicator
        if (info.IsPaused)
            parts.Insert(0, "[PAUSED]");

        // Calculate progress
        double progress = 0;
        if (info.ClipProgress.HasValue)
            progress = info.ClipProgress.Value;
        else if (info.CurrentFrame.HasValue && info.TotalFrames.HasValue && info.TotalFrames > 1)
            progress = (double)info.CurrentFrame.Value / info.TotalFrames.Value;
        else if (info.CurrentTime.HasValue && info.TotalDuration.HasValue && info.TotalDuration.Value.TotalSeconds > 0)
            progress = info.CurrentTime.Value.TotalSeconds / info.TotalDuration.Value.TotalSeconds;

        // Join parts
        var infoText = string.Join(" │ ", parts);

        // Constrain total status bar to half the max width
        var maxStatusWidth = _maxWidth / 2;

        // Progress bar - small (8-12 chars)
        var barWidth = Math.Min(12, Math.Max(8, maxStatusWidth / 3));
        var progressBar = RenderProgressBar(progress, barWidth);

        // Fit info text to remaining width
        var availableForInfo = maxStatusWidth - progressBar.Length - 1;
        if (infoText.Length > availableForInfo && availableForInfo > 5)
        {
            // Drop parts from end until it fits
            while (parts.Count > 1 && string.Join(" │ ", parts).Length > availableForInfo)
                parts.RemoveAt(parts.Count - 1);
            infoText = string.Join(" │ ", parts);
            if (infoText.Length > availableForInfo)
                infoText = infoText[..(availableForInfo - 3)] + "...";
        }

        sb.Append(infoText);
        sb.Append(' ');
        sb.Append(progressBar);

        // Reset color
        if (_useColor)
            sb.Append("\x1b[0m");

        return sb.ToString();
    }

    /// <summary>
    ///     Render just a progress bar
    /// </summary>
    public string RenderProgressBar(double progress, int width = 20)
    {
        progress = Math.Clamp(progress, 0, 1);
        var filled = (int)(progress * width);
        var empty = width - filled;

        var bar = new StringBuilder();
        if (_useColor)
            bar.Append("\x1b[32m"); // Green for filled

        bar.Append('[');
        bar.Append(new string('█', filled));

        if (_useColor)
            bar.Append("\x1b[90m"); // Gray for empty

        bar.Append(new string('░', empty));

        if (_useColor)
            bar.Append("\x1b[0m");

        bar.Append(']');

        // Add percentage
        bar.Append($" {progress:P0}");

        return bar.ToString();
    }

    /// <summary>
    ///     Clear the status line (move up and clear)
    /// </summary>
    public static string Clear()
    {
        return "\x1b[2K"; // Clear entire line
    }

    private static string FormatTime(TimeSpan time)
    {
        // Always show total seconds for precision
        var totalSeconds = (int)time.TotalSeconds;
        if (time.TotalHours >= 1)
            return $"{time:h\\:mm\\:ss} ({totalSeconds}s)";
        if (time.TotalMinutes >= 1)
            return $"{time:m\\:ss} ({totalSeconds}s)";
        return $"{totalSeconds}s";
    }

    /// <summary>
    ///     Information to display in the status line
    /// </summary>
    public class StatusInfo
    {
        public string? FileName { get; set; }
        public int? SourceWidth { get; set; }
        public int? SourceHeight { get; set; }
        public int? OutputWidth { get; set; }
        public int? OutputHeight { get; set; }
        public string? RenderMode { get; set; }
        public int? CurrentFrame { get; set; }
        public int? TotalFrames { get; set; }
        /// <summary>Absolute video time (includes -ss offset)</summary>
        public TimeSpan? CurrentTime { get; set; }
        /// <summary>Total video duration (full file, not just clip)</summary>
        public TimeSpan? TotalDuration { get; set; }
        /// <summary>Progress within current playback clip (0.0-1.0), used for progress bar</summary>
        public double? ClipProgress { get; set; }
        public double? Fps { get; set; }
        public string? Codec { get; set; }
        public bool IsPlaying { get; set; } = true;
        public bool IsPaused { get; set; }
        public int? LoopNumber { get; set; }
        public int? TotalLoops { get; set; }
    }
}