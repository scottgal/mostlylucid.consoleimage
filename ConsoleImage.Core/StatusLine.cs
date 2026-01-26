// StatusLine - Display information below rendered output
// Shows filename, resolution, progress, duration, etc.
// Optimized for low allocation: reusable StringBuilder, pre-cached bar characters.

using System.Text;

namespace ConsoleImage.Core;

/// <summary>
///     Displays a status line below rendered ASCII art with file info, progress, etc.
/// </summary>
public class StatusLine
{
    // Pre-cached progress bar fill strings (avoids new string('█', N) per frame)
    private static readonly string FilledBar = new('█', 20);
    private static readonly string EmptyBar = new('░', 20);
    private readonly StringBuilder _barSb = new(64);
    private readonly int _maxWidth;

    // Pre-allocated reusable buffers
    private readonly StringBuilder _sb = new(256);
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
        _sb.Clear();

        // Use dim color for status line
        if (_useColor)
            _sb.Append("\x1b[2m"); // Dim

        // Build compact info parts directly into StringBuilder (avoids List + string.Join)
        var partCount = 0;

        // Paused indicator first
        if (info.IsPaused)
        {
            _sb.Append("[PAUSED]");
            partCount++;
        }

        // Filename (short)
        if (!string.IsNullOrEmpty(info.FileName))
        {
            if (partCount > 0) _sb.Append(" \u2502 ");
            var name = Path.GetFileName(info.FileName);
            if (name.Length > 20)
            {
                _sb.Append(name, 0, 17);
                _sb.Append("...");
            }
            else
            {
                _sb.Append(name);
            }

            partCount++;
        }

        // Mode
        if (!string.IsNullOrEmpty(info.RenderMode))
        {
            if (partCount > 0) _sb.Append(" \u2502 ");
            _sb.Append(info.RenderMode);
            partCount++;
        }

        // Time: current/total
        if (info.CurrentTime.HasValue && info.TotalDuration.HasValue)
        {
            if (partCount > 0) _sb.Append(" \u2502 ");
            var currentSec = (int)info.CurrentTime.Value.TotalSeconds;
            var totalSec = (int)info.TotalDuration.Value.TotalSeconds;
            _sb.Append(currentSec);
            _sb.Append("s/");
            _sb.Append(totalSec);
            _sb.Append('s');
        }

        // Calculate progress
        double progress = 0;
        if (info.ClipProgress.HasValue)
            progress = info.ClipProgress.Value;
        else if (info.CurrentFrame.HasValue && info.TotalFrames.HasValue && info.TotalFrames > 1)
            progress = (double)info.CurrentFrame.Value / info.TotalFrames.Value;
        else if (info.CurrentTime.HasValue && info.TotalDuration.HasValue && info.TotalDuration.Value.TotalSeconds > 0)
            progress = info.CurrentTime.Value.TotalSeconds / info.TotalDuration.Value.TotalSeconds;

        // Constrain total status bar to half the max width
        var maxStatusWidth = _maxWidth / 2;

        // Progress bar - small (8-12 chars)
        var barWidth = Math.Min(12, Math.Max(8, maxStatusWidth / 3));

        // Truncate info text if needed (check current length vs available space)
        var progressBarEstimate = barWidth + 7; // [bar] + space + " 100%"
        var availableForInfo = maxStatusWidth - progressBarEstimate;
        if (_sb.Length > availableForInfo && availableForInfo > 5)
        {
            _sb.Length = availableForInfo - 3;
            _sb.Append("...");
        }

        _sb.Append(' ');
        AppendProgressBar(_sb, progress, barWidth);

        // Reset color
        if (_useColor)
            _sb.Append("\x1b[0m");

        return _sb.ToString();
    }

    /// <summary>
    ///     Append a progress bar directly to a StringBuilder (avoids intermediate string).
    /// </summary>
    private void AppendProgressBar(StringBuilder sb, double progress, int width)
    {
        progress = Math.Clamp(progress, 0, 1);
        var filled = (int)(progress * width);
        var empty = width - filled;

        if (_useColor)
            sb.Append("\x1b[32m"); // Green for filled

        sb.Append('[');
        if (filled > 0)
            sb.Append(FilledBar, 0, Math.Min(filled, FilledBar.Length));

        if (_useColor)
            sb.Append("\x1b[90m"); // Gray for empty

        if (empty > 0)
            sb.Append(EmptyBar, 0, Math.Min(empty, EmptyBar.Length));

        if (_useColor)
            sb.Append("\x1b[0m");

        sb.Append(']');

        // Add percentage
        var pct = (int)(progress * 100);
        sb.Append(' ');
        sb.Append(pct);
        sb.Append('%');
    }

    /// <summary>
    ///     Render just a progress bar (standalone, returns new string)
    /// </summary>
    public string RenderProgressBar(double progress, int width = 20)
    {
        _barSb.Clear();
        AppendProgressBar(_barSb, progress, width);
        return _barSb.ToString();
    }

    /// <summary>
    ///     Clear the status line (move up and clear)
    /// </summary>
    public static string Clear()
    {
        return "\x1b[2K"; // Clear entire line
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