// StatusLine - Display information below rendered output
// Shows filename, resolution, progress, duration, etc.

using System.Text;

namespace ConsoleImage.Core;

/// <summary>
/// Displays a status line below rendered ASCII art with file info, progress, etc.
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
    /// Information to display in the status line
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
        public TimeSpan? CurrentTime { get; set; }
        public TimeSpan? TotalDuration { get; set; }
        public double? Fps { get; set; }
        public string? Codec { get; set; }
        public bool IsPlaying { get; set; } = true;
        public bool IsPaused { get; set; }
        public int? LoopNumber { get; set; }
        public int? TotalLoops { get; set; }
    }

    /// <summary>
    /// Render the status line as a string
    /// </summary>
    public string Render(StatusInfo info)
    {
        var sb = new StringBuilder();

        // Use dim color for status line
        if (_useColor)
            sb.Append("\x1b[2m"); // Dim

        // Build components
        var parts = new List<string>();

        // Filename (truncated if needed)
        if (!string.IsNullOrEmpty(info.FileName))
        {
            var name = Path.GetFileName(info.FileName);
            if (name.Length > 30)
                name = name[..27] + "...";
            parts.Add(name);
        }

        // Resolution
        if (info.SourceWidth.HasValue && info.SourceHeight.HasValue)
        {
            var res = $"{info.SourceWidth}x{info.SourceHeight}";
            if (info.OutputWidth.HasValue && info.OutputHeight.HasValue)
                res += $"→{info.OutputWidth}x{info.OutputHeight}";
            parts.Add(res);
        }

        // Render mode
        if (!string.IsNullOrEmpty(info.RenderMode))
            parts.Add(info.RenderMode);

        // Codec
        if (!string.IsNullOrEmpty(info.Codec))
            parts.Add(info.Codec);

        // FPS
        if (info.Fps.HasValue)
            parts.Add($"{info.Fps:F1}fps");

        // Frame progress
        if (info.CurrentFrame.HasValue && info.TotalFrames.HasValue && info.TotalFrames > 1)
        {
            parts.Add($"Frame {info.CurrentFrame}/{info.TotalFrames}");
        }

        // Time progress
        if (info.CurrentTime.HasValue && info.TotalDuration.HasValue)
        {
            var current = FormatTime(info.CurrentTime.Value);
            var total = FormatTime(info.TotalDuration.Value);
            parts.Add($"{current}/{total}");
        }

        // Loop info
        if (info.LoopNumber.HasValue)
        {
            if (info.TotalLoops.HasValue && info.TotalLoops > 0)
                parts.Add($"Loop {info.LoopNumber}/{info.TotalLoops}");
            else if (info.TotalLoops == 0)
                parts.Add($"Loop {info.LoopNumber}/∞");
        }

        // Paused indicator
        if (info.IsPaused)
            parts.Add("[PAUSED]");

        // Join with separator
        var line = string.Join(" │ ", parts);

        // Progress bar if we have time info
        string progressBar = "";
        if (info.CurrentTime.HasValue && info.TotalDuration.HasValue && info.TotalDuration.Value.TotalSeconds > 0)
        {
            var progress = info.CurrentTime.Value.TotalSeconds / info.TotalDuration.Value.TotalSeconds;
            progressBar = RenderProgressBar(progress, Math.Min(20, _maxWidth / 4));
        }
        else if (info.CurrentFrame.HasValue && info.TotalFrames.HasValue && info.TotalFrames > 1)
        {
            var progress = (double)info.CurrentFrame.Value / info.TotalFrames.Value;
            progressBar = RenderProgressBar(progress, Math.Min(20, _maxWidth / 4));
        }

        // Combine line and progress bar
        if (!string.IsNullOrEmpty(progressBar))
        {
            var availableWidth = _maxWidth - progressBar.Length - 2;
            if (line.Length > availableWidth)
                line = line[..(availableWidth - 3)] + "...";
            line = line.PadRight(availableWidth) + " " + progressBar;
        }
        else if (line.Length > _maxWidth)
        {
            line = line[..(_maxWidth - 3)] + "...";
        }

        sb.Append(line);

        // Reset color
        if (_useColor)
            sb.Append("\x1b[0m");

        return sb.ToString();
    }

    /// <summary>
    /// Render just a progress bar
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
    /// Clear the status line (move up and clear)
    /// </summary>
    public static string Clear()
    {
        return "\x1b[2K"; // Clear entire line
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return time.ToString(@"h\:mm\:ss");
        return time.ToString(@"m\:ss");
    }
}
