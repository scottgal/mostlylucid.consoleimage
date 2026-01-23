// DocumentPlayer - Plays back ConsoleImageDocument frames to the console

using System.Text;

namespace ConsoleImage.Core;

/// <summary>
///     Plays ConsoleImageDocument frames to the console with animation support.
/// </summary>
public class DocumentPlayer : IDisposable
{
    private readonly ConsoleImageDocument _document;
    private readonly int _loopCount;
    private readonly float _speedMultiplier;
    private bool _disposed;

    public DocumentPlayer(
        ConsoleImageDocument document,
        float? speedMultiplier = null,
        int? loopCount = null)
    {
        _document = document;
        _speedMultiplier = speedMultiplier ?? document.Settings.AnimationSpeedMultiplier;
        _loopCount = loopCount ?? document.Settings.LoopCount;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Play the document to the console
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

        // Calculate max height for cursor positioning
        var maxHeight = _document.Frames.Max(f => f.Height);

        // Hide cursor during animation
        Console.Write("\x1b[?25l");

        // Track if this is the very first frame
        var isFirstFrame = true;

        try
        {
            var loopsRemaining = _loopCount == 0 ? int.MaxValue : _loopCount;

            while (loopsRemaining > 0 && !ct.IsCancellationRequested)
            {
                for (var i = 0; i < _document.Frames.Count && !ct.IsCancellationRequested; i++)
                {
                    var frame = _document.Frames[i];

                    // Move cursor to start position (except for very first frame)
                    // Content may or may not end with newline - check and adjust
                    if (!isFirstFrame)
                    {
                        // If content ends with newline, cursor is on next line, so move up maxHeight
                        // If not, cursor is on last line, so move up maxHeight - 1
                        var endsWithNewline = frame.Content.EndsWith('\n') || frame.Content.EndsWith("\r\n");
                        var linesToMove = endsWithNewline ? maxHeight : maxHeight - 1;
                        Console.Write($"\x1b[{linesToMove}A\r");
                    }
                    isFirstFrame = false;

                    // Use synchronized output if supported
                    Console.Write("\x1b[?2026h");
                    Console.Write(frame.Content);
                    Console.Write("\x1b[?2026l");
                    Console.Out.Flush();

                    // Wait for frame delay
                    if (frame.DelayMs > 0)
                    {
                        var delay = (int)(frame.DelayMs / _speedMultiplier);
                        await Task.Delay(delay, ct);
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
            Console.Write("\x1b[?25h");
            Console.Write("\x1b[0m"); // Reset colors
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

        return info.ToString();
    }
}