// ASCII Art Renderer - Resizable animation player
// Handles dynamic console resize during animation playback

namespace ConsoleImage.Core;

/// <summary>
/// A frame with content and timing information
/// </summary>
public interface IAnimationFrame
{
    string Content { get; }
    int DelayMs { get; }
}

/// <summary>
/// Wraps an AsciiFrame to implement IAnimationFrame
/// </summary>
public class AsciiFrameAdapter : IAnimationFrame
{
    private readonly string _content;
    public string Content => _content;
    public int DelayMs { get; }

    public AsciiFrameAdapter(AsciiFrame frame, bool useColor, float? darkThreshold = null, float? lightThreshold = null)
    {
        _content = useColor ? frame.ToAnsiString(darkThreshold, lightThreshold) : frame.ToString();
        DelayMs = frame.DelayMs;
    }
}

/// <summary>
/// Delegate for rendering frames at a specific size
/// </summary>
/// <param name="maxWidth">Maximum width in characters</param>
/// <param name="maxHeight">Maximum height in characters</param>
/// <returns>List of rendered frames</returns>
public delegate IReadOnlyList<IAnimationFrame> RenderFramesDelegate(int maxWidth, int maxHeight);

/// <summary>
/// Animation player that supports dynamic console resize.
/// Re-renders frames when the console window size changes.
/// </summary>
public class ResizableAnimationPlayer
{
    private readonly RenderFramesDelegate _renderFrames;
    private readonly int? _explicitWidth;
    private readonly int? _explicitHeight;
    private readonly int _loopCount;
    private readonly bool _useAltScreen;
    private readonly float? _targetFps;

    private int _lastConsoleWidth;
    private int _lastConsoleHeight;
    private IReadOnlyList<IAnimationFrame>? _frames;
    private string[]? _frameBuffers;

    public ResizableAnimationPlayer(
        RenderFramesDelegate renderFrames,
        int? explicitWidth = null,
        int? explicitHeight = null,
        int loopCount = 0,
        bool useAltScreen = true,
        float? targetFps = null)
    {
        _renderFrames = renderFrames;
        _explicitWidth = explicitWidth;
        _explicitHeight = explicitHeight;
        _loopCount = loopCount;
        _useAltScreen = useAltScreen;
        _targetFps = targetFps;
    }

    /// <summary>
    /// Play the animation with dynamic resize support
    /// </summary>
    public async Task PlayAsync(CancellationToken cancellationToken = default)
    {
        ConsoleHelper.EnableAnsiSupport();

        // Initial render
        RenderFramesForCurrentSize();

        if (_frames == null || _frames.Count <= 1)
        {
            if (_frames?.Count == 1)
            {
                Console.WriteLine(_frames[0].Content);
                Console.Write("\x1b[0m");
            }
            return;
        }

        int? fixedDelayMs = _targetFps.HasValue && _targetFps.Value > 0
            ? (int)(1000f / _targetFps.Value)
            : null;

        // Enter alternate screen
        if (_useAltScreen)
            Console.Write("\x1b[?1049h");
        Console.Write("\x1b[?25l"); // Hide cursor
        Console.Write("\x1b[2J");   // Clear screen
        Console.Out.Flush();

        int loopsDone = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested && _frames != null && _frameBuffers != null)
            {
                for (int i = 0; i < _frames.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Check for resize
                    if (CheckAndHandleResize())
                    {
                        i = 0; // Restart from first frame
                        if (_frames == null || _frameBuffers == null) break;
                    }

                    // Write frame
                    Console.Write(_frameBuffers![i]);
                    Console.Out.Flush();

                    // Delay
                    int delayMs = fixedDelayMs ?? _frames[i].DelayMs;
                    if (delayMs > 0)
                    {
                        await ResponsiveDelay(delayMs, cancellationToken);
                    }
                }

                loopsDone++;
                if (_loopCount > 0 && loopsDone >= _loopCount) break;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            Console.Write("\x1b[?25h"); // Show cursor
            if (_useAltScreen)
                Console.Write("\x1b[?1049l"); // Exit alt screen
        }
    }

    private void RenderFramesForCurrentSize()
    {
        int consoleWidth, consoleHeight;
        try
        {
            consoleWidth = Console.WindowWidth - 1;
            consoleHeight = Console.WindowHeight - 2;
        }
        catch
        {
            consoleWidth = 120;
            consoleHeight = 40;
        }

        // Skip if size hasn't changed
        if (consoleWidth == _lastConsoleWidth && consoleHeight == _lastConsoleHeight && _frames != null)
            return;

        _lastConsoleWidth = consoleWidth;
        _lastConsoleHeight = consoleHeight;

        // Use explicit dimensions if provided, otherwise use console size
        int maxWidth = _explicitWidth ?? consoleWidth;
        int maxHeight = _explicitHeight ?? consoleHeight;

        // Render frames at current size
        _frames = _renderFrames(maxWidth, maxHeight);

        if (_frames.Count <= 1)
            return;

        // Build frame buffers using diff-based rendering to eliminate flicker
        var frameLines = new string[_frames.Count][];
        int maxFrameHeight = 0;
        int maxLineWidth = 0;

        for (int f = 0; f < _frames.Count; f++)
        {
            frameLines[f] = _frames[f].Content.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
            if (frameLines[f].Length > maxFrameHeight)
                maxFrameHeight = frameLines[f].Length;
            foreach (var line in frameLines[f])
            {
                // Count visible characters (skip ANSI sequences)
                int visibleLen = GetVisibleLength(line);
                if (visibleLen > maxLineWidth)
                    maxLineWidth = visibleLen;
            }
        }

        _frameBuffers = new string[_frames.Count];

        // First frame: clear screen once and write full content
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("\x1b[?2026h"); // Sync start
            sb.Append("\x1b[2J");     // Clear screen
            sb.Append("\x1b[H");      // Home cursor

            var lines = frameLines[0];
            for (int lineIdx = 0; lineIdx < maxFrameHeight; lineIdx++)
            {
                if (lineIdx < lines.Length)
                {
                    sb.Append(lines[lineIdx]);
                    // Pad with spaces to ensure full overwrite on subsequent frames
                    int visibleLen = GetVisibleLength(lines[lineIdx]);
                    if (visibleLen < maxLineWidth)
                        sb.Append(new string(' ', maxLineWidth - visibleLen));
                }
                else
                {
                    sb.Append(new string(' ', maxLineWidth));
                }
                sb.Append("\x1b[0m"); // Reset colors at end of line
                if (lineIdx < maxFrameHeight - 1)
                    sb.Append('\n');
            }

            sb.Append("\x1b[?2026l"); // Sync end
            _frameBuffers[0] = sb.ToString();
        }

        // Subsequent frames: use diff-based rendering (only update changed lines)
        for (int f = 1; f < _frames.Count; f++)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("\x1b[?2026h"); // Sync start

            var prevLines = frameLines[f - 1];
            var currLines = frameLines[f];
            int changedLines = 0;

            for (int lineIdx = 0; lineIdx < maxFrameHeight; lineIdx++)
            {
                string prevLine = lineIdx < prevLines.Length ? prevLines[lineIdx] : "";
                string currLine = lineIdx < currLines.Length ? currLines[lineIdx] : "";

                if (prevLine != currLine)
                {
                    changedLines++;
                    // Move cursor to line start (1-indexed), no clear - just overwrite
                    sb.Append($"\x1b[{lineIdx + 1};1H");
                    sb.Append(currLine);
                    // Pad with spaces to overwrite any leftover from previous frame
                    int currVisible = GetVisibleLength(currLine);
                    int prevVisible = GetVisibleLength(prevLine);
                    if (currVisible < prevVisible)
                        sb.Append(new string(' ', prevVisible - currVisible));
                    sb.Append("\x1b[0m"); // Reset colors
                }
            }

            // If more than 70% of lines changed, do a full redraw instead
            if (changedLines > maxFrameHeight * 0.7)
            {
                sb.Clear();
                sb.Append("\x1b[?2026h"); // Sync start
                sb.Append("\x1b[H");      // Home cursor (no clear)

                var lines = currLines;
                for (int lineIdx = 0; lineIdx < maxFrameHeight; lineIdx++)
                {
                    if (lineIdx < lines.Length)
                    {
                        sb.Append(lines[lineIdx]);
                        int visibleLen = GetVisibleLength(lines[lineIdx]);
                        if (visibleLen < maxLineWidth)
                            sb.Append(new string(' ', maxLineWidth - visibleLen));
                    }
                    else
                    {
                        sb.Append(new string(' ', maxLineWidth));
                    }
                    sb.Append("\x1b[0m");
                    if (lineIdx < maxFrameHeight - 1)
                        sb.Append('\n');
                }
            }

            sb.Append("\x1b[?2026l"); // Sync end
            _frameBuffers[f] = sb.ToString();
        }
    }

    /// <summary>
    /// Get the visible character count of a string (excluding ANSI escape sequences)
    /// </summary>
    private static int GetVisibleLength(string line)
    {
        int len = 0;
        bool inEscape = false;

        foreach (char c in line)
        {
            if (c == '\x1b')
            {
                inEscape = true;
            }
            else if (inEscape)
            {
                if (c == 'm') // End of SGR sequence
                    inEscape = false;
            }
            else
            {
                len++;
            }
        }

        return len;
    }

    private bool CheckAndHandleResize()
    {
        int currentWidth = 0, currentHeight = 0;
        try
        {
            currentWidth = Console.WindowWidth - 1;
            currentHeight = Console.WindowHeight - 2;
        }
        catch { return false; }

        if (currentWidth != _lastConsoleWidth || currentHeight != _lastConsoleHeight)
        {
            // Clear screen before re-rendering
            Console.Write("\x1b[2J\x1b[H");
            Console.Out.Flush();
            RenderFramesForCurrentSize();
            return true;
        }

        return false;
    }

    private static async Task ResponsiveDelay(int totalMs, CancellationToken token)
    {
        const int chunkMs = 50;
        int remaining = totalMs;

        while (remaining > 0 && !token.IsCancellationRequested)
        {
            int delay = Math.Min(remaining, chunkMs);
            try
            {
                await Task.Delay(delay, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            remaining -= delay;
        }
    }
}
