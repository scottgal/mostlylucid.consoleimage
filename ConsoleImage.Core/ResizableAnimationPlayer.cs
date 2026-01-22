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
    private readonly bool _showStatus;
    private readonly string? _fileName;
    private readonly string? _renderMode;
    private readonly StatusLine? _statusLine;

    private int _lastConsoleWidth;
    private int _lastConsoleHeight;
    private IReadOnlyList<IAnimationFrame>? _frames;
    private string[]? _frameBuffers;
    private string[]? _transitionBuffers; // Interpolated frames for smooth looping
    private int _transitionDelayMs; // Delay for each transition frame
    private int _maxFrameHeight;

    public ResizableAnimationPlayer(
        RenderFramesDelegate renderFrames,
        int? explicitWidth = null,
        int? explicitHeight = null,
        int loopCount = 0,
        bool useAltScreen = true,
        float? targetFps = null,
        bool showStatus = false,
        string? fileName = null,
        string? renderMode = null)
    {
        _renderFrames = renderFrames;
        _explicitWidth = explicitWidth;
        _explicitHeight = explicitHeight;
        _loopCount = loopCount;
        _useAltScreen = useAltScreen;
        _targetFps = targetFps;
        _showStatus = showStatus;
        _fileName = fileName;
        _renderMode = renderMode;

        if (_showStatus)
        {
            int statusWidth = 120;
            try { statusWidth = Console.WindowWidth - 1; } catch { }
            _statusLine = new StatusLine(statusWidth, useColor: true);
        }
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
                var frames = _frames; // Capture to avoid null warnings
                var frameBuffers = _frameBuffers;
                for (int i = 0; i < frames.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Check for resize
                    if (CheckAndHandleResize())
                    {
                        i = 0; // Restart from first frame
                        loopsDone = 0; // Reset loop count on resize
                        frames = _frames;
                        frameBuffers = _frameBuffers;
                        if (frames == null || frameBuffers == null) break;
                    }

                    // Write frame with status line
                    Console.Write(frameBuffers[i]);

                    // Write status line if enabled
                    if (_statusLine != null)
                    {
                        var statusInfo = new StatusLine.StatusInfo
                        {
                            FileName = _fileName,
                            RenderMode = _renderMode,
                            CurrentFrame = i + 1,
                            TotalFrames = frames.Count,
                            LoopNumber = loopsDone + 1,
                            TotalLoops = _loopCount
                        };
                        // Position cursor below frame and render status
                        Console.Write($"\x1b[{_maxFrameHeight + 1};1H\x1b[2K{_statusLine.Render(statusInfo)}");
                    }

                    Console.Out.Flush();

                    // Delay
                    int delayMs = fixedDelayMs ?? frames[i].DelayMs;
                    if (delayMs > 0)
                    {
                        await ResponsiveDelay(delayMs, cancellationToken);
                    }
                }

                // Play transition frames for smooth looping (if available and looping continues)
                if (_transitionBuffers != null && _transitionBuffers.Length > 0)
                {
                    bool shouldContinue = _loopCount == 0 || loopsDone < _loopCount - 1;
                    if (shouldContinue && !cancellationToken.IsCancellationRequested)
                    {
                        foreach (var transitionBuffer in _transitionBuffers)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            Console.Write(transitionBuffer);
                            Console.Out.Flush();
                            if (_transitionDelayMs > 0)
                            {
                                await ResponsiveDelay(_transitionDelayMs, cancellationToken);
                            }
                        }
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
        _maxFrameHeight = 0;
        int maxLineWidth = 0;

        for (int f = 0; f < _frames.Count; f++)
        {
            frameLines[f] = _frames[f].Content.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
            if (frameLines[f].Length > _maxFrameHeight)
                _maxFrameHeight = frameLines[f].Length;
            foreach (var line in frameLines[f])
            {
                // Count visible characters (skip ANSI sequences)
                int visibleLen = GetVisibleLength(line);
                if (visibleLen > maxLineWidth)
                    maxLineWidth = visibleLen;
            }
        }
        int maxFrameHeight = _maxFrameHeight;

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

        // Create smooth loop transition frames
        CreateSmoothLoopTransition(frameLines, maxFrameHeight, maxLineWidth);
    }

    /// <summary>
    /// Create interpolated transition frames for smooth looping.
    /// Uses progressive line-by-line updates to crossfade from last frame to first.
    /// </summary>
    private void CreateSmoothLoopTransition(string[][] frameLines, int maxFrameHeight, int maxLineWidth)
    {
        if (_frames == null || _frames.Count < 2)
        {
            _transitionBuffers = null;
            return;
        }

        var lastLines = frameLines[_frames.Count - 1];
        var firstLines = frameLines[0];

        // Find which lines differ between last and first frame
        var changedLineIndices = new List<int>();
        for (int lineIdx = 0; lineIdx < maxFrameHeight; lineIdx++)
        {
            string lastLine = lineIdx < lastLines.Length ? lastLines[lineIdx] : "";
            string firstLine = lineIdx < firstLines.Length ? firstLines[lineIdx] : "";
            if (lastLine != firstLine)
            {
                changedLineIndices.Add(lineIdx);
            }
        }

        int changedCount = changedLineIndices.Count;

        // If frames are identical, no transition needed
        if (changedCount == 0)
        {
            _transitionBuffers = null;
            return;
        }

        // If too different (>70%), GIF wasn't designed to loop smoothly - instant transition
        if (changedCount > maxFrameHeight * 0.7)
        {
            // Create a single full-redraw transition frame
            _transitionBuffers = new string[1];
            var sb = new System.Text.StringBuilder();
            sb.Append("\x1b[?2026h");
            sb.Append("\x1b[H");

            for (int lineIdx = 0; lineIdx < maxFrameHeight; lineIdx++)
            {
                if (lineIdx < firstLines.Length)
                {
                    sb.Append(firstLines[lineIdx]);
                    int visibleLen = GetVisibleLength(firstLines[lineIdx]);
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
            sb.Append("\x1b[?2026l");
            _transitionBuffers[0] = sb.ToString();
            _transitionDelayMs = 16; // Quick transition
            return;
        }

        // Create 2-4 interpolated transition frames based on difference amount
        int transitionFrameCount = changedCount switch
        {
            <= 3 => 1,   // Few changes: 1 frame
            <= 8 => 2,   // Moderate changes: 2 frames
            <= 15 => 3,  // More changes: 3 frames
            _ => 4       // Many changes: 4 frames
        };

        _transitionBuffers = new string[transitionFrameCount];

        // Use average frame delay for transitions, or 50ms default
        int avgDelay = _frames.Count > 0 ? _frames.Sum(f => f.DelayMs) / _frames.Count : 50;
        _transitionDelayMs = Math.Max(16, avgDelay / 2); // Half speed for smooth transition

        // Distribute changed lines across transition frames
        int linesPerFrame = (changedCount + transitionFrameCount - 1) / transitionFrameCount;

        for (int t = 0; t < transitionFrameCount; t++)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("\x1b[?2026h"); // Sync start

            // Calculate which lines to update in this transition frame
            int startIdx = t * linesPerFrame;
            int endIdx = Math.Min(startIdx + linesPerFrame, changedCount);

            // Also include all lines from previous transition frames (cumulative)
            for (int i = 0; i <= Math.Min((t + 1) * linesPerFrame - 1, changedCount - 1); i++)
            {
                int lineIdx = changedLineIndices[i];
                string firstLine = lineIdx < firstLines.Length ? firstLines[lineIdx] : "";
                string lastLine = lineIdx < lastLines.Length ? lastLines[lineIdx] : "";

                sb.Append($"\x1b[{lineIdx + 1};1H");
                sb.Append(firstLine);
                int firstVisible = GetVisibleLength(firstLine);
                int lastVisible = GetVisibleLength(lastLine);
                if (firstVisible < lastVisible)
                    sb.Append(new string(' ', lastVisible - firstVisible));
                sb.Append("\x1b[0m");
            }

            sb.Append("\x1b[?2026l"); // Sync end
            _transitionBuffers[t] = sb.ToString();
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
