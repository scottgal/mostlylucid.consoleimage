using System.Collections.Concurrent;
using System.Text;
using ConsoleImage.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Video.Core;

/// <summary>
/// Streaming video player that renders ASCII frames on-the-fly.
/// Uses a small lookahead buffer to avoid loading entire video into memory.
/// </summary>
public class VideoAnimationPlayer : IDisposable
{
    private readonly FFmpegService _ffmpeg;
    private readonly VideoRenderOptions _options;
    private readonly string _videoPath;

    private VideoInfo? _videoInfo;
    private int _renderWidth;
    private int _renderHeight;
    private int _lastConsoleWidth;
    private int _lastConsoleHeight;

    public VideoAnimationPlayer(string videoPath, VideoRenderOptions? options = null)
    {
        _videoPath = videoPath;
        _options = options ?? VideoRenderOptions.Default;
        _ffmpeg = new FFmpegService(
            useHardwareAcceleration: _options.UseHardwareAcceleration);
    }

    /// <summary>
    /// Play the video with streaming frame rendering.
    /// </summary>
    public async Task PlayAsync(CancellationToken cancellationToken = default)
    {
        ConsoleHelper.EnableAnsiSupport();

        // Get video info
        _videoInfo = await _ffmpeg.GetVideoInfoAsync(_videoPath, cancellationToken);
        if (_videoInfo == null)
            throw new InvalidOperationException("Could not read video info");

        // Calculate render dimensions
        UpdateRenderDimensions();

        // Calculate timing
        var effectiveFps = (_options.TargetFps ?? _videoInfo.FrameRate) / _options.FrameStep;
        if (effectiveFps <= 0) effectiveFps = 24;
        var baseFrameDelayMs = (int)(1000.0 / effectiveFps);
        var frameDelayMs = (int)(baseFrameDelayMs / _options.SpeedMultiplier);

        // Enter alternate screen
        if (_options.UseAltScreen)
            Console.Write("\x1b[?1049h");
        Console.Write("\x1b[?25l"); // Hide cursor
        Console.Write("\x1b[2J");   // Clear screen
        Console.Out.Flush();

        int loopsDone = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Check for resize
                if (CheckForResize())
                {
                    Console.Write("\x1b[2J\x1b[H");
                    Console.Out.Flush();
                }

                await PlayStreamAsync(frameDelayMs, cancellationToken);

                loopsDone++;
                if (_options.LoopCount > 0 && loopsDone >= _options.LoopCount)
                    break;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            Console.Write("\x1b[?25h"); // Show cursor
            if (_options.UseAltScreen)
                Console.Write("\x1b[?1049l"); // Exit alt screen
            Console.Write("\x1b[0m"); // Reset colors
        }
    }

    /// <summary>
    /// Stream and render frames with lookahead buffer.
    /// </summary>
    private async Task PlayStreamAsync(int frameDelayMs, CancellationToken ct)
    {
        // Create renderer based on mode
        using var renderer = CreateRenderer();

        // Buffer for lookahead frames
        var frameBuffer = new ConcurrentQueue<string>();
        var renderingComplete = false;
        var renderException = default(Exception);

        // Start background rendering task
        var renderTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var image in _ffmpeg.StreamFramesAsync(
                    _videoPath,
                    _renderWidth,
                    _renderHeight,
                    _options.StartTime,
                    _options.EndTime,
                    _options.FrameStep,
                    _options.TargetFps,
                    ct))
                {
                    using (image)
                    {
                        // Render frame to ASCII
                        var frameContent = RenderFrame(renderer, image);

                        // Wait if buffer is full
                        while (frameBuffer.Count >= _options.BufferAheadFrames && !ct.IsCancellationRequested)
                        {
                            await Task.Delay(5, ct);
                        }

                        if (ct.IsCancellationRequested) break;

                        frameBuffer.Enqueue(frameContent);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                renderException = ex;
            }
            finally
            {
                renderingComplete = true;
            }
        }, ct);

        string? previousFrame = null;
        var frameIndex = 0;

        // Play frames as they become available
        while (!ct.IsCancellationRequested)
        {
            // Wait for a frame to be available
            while (frameBuffer.IsEmpty && !renderingComplete && !ct.IsCancellationRequested)
            {
                await Task.Delay(1, ct);
            }

            if (frameBuffer.TryDequeue(out var frameContent))
            {
                // Build optimized frame buffer
                var buffer = BuildFrameBuffer(frameContent, previousFrame, frameIndex == 0);
                previousFrame = frameContent;
                frameIndex++;

                // Write frame
                Console.Write(buffer);
                Console.Out.Flush();

                // Delay for timing
                if (frameDelayMs > 0)
                {
                    await ResponsiveDelayAsync(frameDelayMs, ct);
                }

                // Check for resize during playback
                if (CheckForResize())
                {
                    // Abort current stream and restart with new dimensions
                    break;
                }
            }
            else if (renderingComplete)
            {
                break;
            }
        }

        try
        {
            await renderTask;
        }
        catch (OperationCanceledException) { }

        if (renderException != null)
            throw renderException;
    }

    /// <summary>
    /// Create appropriate renderer based on mode.
    /// For blocks/braille, we set exact dimensions to prevent double-resize.
    /// </summary>
    private IDisposable CreateRenderer()
    {
        if (_options.RenderMode == VideoRenderMode.ColorBlocks)
        {
            // Set Width to exact pixel width so ColorBlockRenderer doesn't resize
            var opts = _options.RenderOptions.Clone();
            opts.Width = _renderWidth;
            opts.Height = _renderHeight / 2; // Height in lines (2 pixels per line)
            return new ColorBlockRenderer(opts);
        }
        else if (_options.RenderMode == VideoRenderMode.Braille)
        {
            var opts = _options.RenderOptions.Clone();
            opts.Width = _renderWidth / 2;  // Width in chars (2 pixels per char)
            opts.Height = _renderHeight / 4; // Height in chars (4 pixels per char)
            return new BrailleRenderer(opts);
        }
        else
        {
            // ASCII mode: set explicit dimensions to prevent recalculation
            var opts = _options.RenderOptions.Clone();
            opts.Width = _renderWidth;
            opts.Height = _renderHeight;
            return new AsciiRenderer(opts);
        }
    }

    /// <summary>
    /// Render a single frame to ASCII string.
    /// </summary>
    private string RenderFrame(IDisposable renderer, Image<Rgba32> image)
    {
        return _options.RenderMode switch
        {
            VideoRenderMode.ColorBlocks => ((ColorBlockRenderer)renderer).RenderImage(image),
            VideoRenderMode.Braille => ((BrailleRenderer)renderer).RenderImage(image),
            _ => RenderAsciiFrame((AsciiRenderer)renderer, image)
        };
    }

    /// <summary>
    /// Render ASCII frame with color support.
    /// </summary>
    private string RenderAsciiFrame(AsciiRenderer renderer, Image<Rgba32> image)
    {
        var frame = renderer.RenderImage(image);

        if (_options.RenderOptions.UseColor)
        {
            var darkThreshold = _options.RenderOptions.Invert
                ? _options.RenderOptions.DarkTerminalBrightnessThreshold
                : null;
            var lightThreshold = !_options.RenderOptions.Invert
                ? _options.RenderOptions.LightTerminalBrightnessThreshold
                : null;

            return frame.ToAnsiString(darkThreshold, lightThreshold);
        }

        return frame.ToString();
    }

    /// <summary>
    /// Build optimized frame buffer with diff-based rendering.
    /// </summary>
    private static string BuildFrameBuffer(string content, string? previousContent, bool isFirstFrame)
    {
        var sb = new StringBuilder();
        sb.Append("\x1b[?2026h"); // Begin synchronized output

        if (isFirstFrame || previousContent == null)
        {
            // Full redraw for first frame
            sb.Append("\x1b[H"); // Home cursor
            sb.Append(content);
        }
        else
        {
            // Diff-based rendering
            var currLines = content.Split('\n');
            var prevLines = previousContent.Split('\n');
            var maxLines = Math.Max(currLines.Length, prevLines.Length);
            var changedLines = 0;

            // First pass: count changes
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
                        sb.Append($"\x1b[{i + 1};1H"); // Move to line
                        sb.Append(currLine);

                        // Pad to clear previous content
                        var currVisible = GetVisibleLength(currLine);
                        var prevVisible = GetVisibleLength(prevLine);
                        if (currVisible < prevVisible)
                        {
                            sb.Append(new string(' ', prevVisible - currVisible));
                        }
                        sb.Append("\x1b[0m"); // Reset colors
                    }
                }
            }
        }

        sb.Append("\x1b[?2026l"); // End synchronized output
        return sb.ToString();
    }

    /// <summary>
    /// Get visible character count (excluding ANSI sequences).
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
                if (c == 'm') inEscape = false;
            }
            else
            {
                len++;
            }
        }

        return len;
    }

    /// <summary>
    /// Update render dimensions based on console size and options.
    /// </summary>
    private void UpdateRenderDimensions()
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

        _lastConsoleWidth = consoleWidth;
        _lastConsoleHeight = consoleHeight;

        // Use explicit dimensions or calculate from console size
        var maxWidth = _options.RenderOptions.Width ?? Math.Min(consoleWidth, _options.RenderOptions.MaxWidth);
        var maxHeight = _options.RenderOptions.Height ?? Math.Min(consoleHeight, _options.RenderOptions.MaxHeight);

        // Calculate dimensions maintaining aspect ratio
        if (_videoInfo != null)
        {
            var videoAspect = (float)_videoInfo.Width / _videoInfo.Height;

            // All modes use CharacterAspectRatio for proper visual aspect correction
            var charAspect = _options.RenderOptions.CharacterAspectRatio;

            if (_options.RenderMode == VideoRenderMode.ColorBlocks)
            {
                // ColorBlockRenderer: each char = 1 pixel wide, 2 pixels tall (half-blocks)
                // Visual container: chars are charAspect wide x 1.0 tall
                // So visual width = maxWidth * charAspect, visual height = maxHeight
                float visualContainerWidth = maxWidth * charAspect;
                float visualContainerHeight = maxHeight;
                float containerVisualAspect = visualContainerWidth / visualContainerHeight;

                float outputVisualWidth, outputVisualHeight;
                if (videoAspect > containerVisualAspect)
                {
                    // Width-constrained
                    outputVisualWidth = visualContainerWidth;
                    outputVisualHeight = visualContainerWidth / videoAspect;
                }
                else
                {
                    // Height-constrained
                    outputVisualHeight = visualContainerHeight;
                    outputVisualWidth = visualContainerHeight * videoAspect;
                }

                // Convert visual to pixels:
                // Horizontal: 1 pixel per char, chars = visualWidth / charAspect
                // Vertical: 2 pixels per char height, pixels = visualHeight * 2
                _renderWidth = Math.Max(1, (int)(outputVisualWidth / charAspect));
                _renderHeight = Math.Max(2, (int)(outputVisualHeight * 2));

                // Ensure even height for half-block pairing
                if (_renderHeight % 2 != 0) _renderHeight++;
            }
            else if (_options.RenderMode == VideoRenderMode.Braille)
            {
                // BrailleRenderer: each char = 2 pixels wide, 4 pixels tall
                // Visual container: chars are charAspect wide x 1.0 tall
                float visualContainerWidth = maxWidth * charAspect;
                float visualContainerHeight = maxHeight;
                float containerVisualAspect = visualContainerWidth / visualContainerHeight;

                float outputVisualWidth, outputVisualHeight;
                if (videoAspect > containerVisualAspect)
                {
                    outputVisualWidth = visualContainerWidth;
                    outputVisualHeight = visualContainerWidth / videoAspect;
                }
                else
                {
                    outputVisualHeight = visualContainerHeight;
                    outputVisualWidth = visualContainerHeight * videoAspect;
                }

                // Convert visual to pixels:
                // Horizontal: 2 pixels per char, chars = visualWidth / charAspect, pixels = chars * 2
                // Vertical: 4 pixels per char height, pixels = visualHeight * 4
                _renderWidth = Math.Max(2, (int)((outputVisualWidth / charAspect) * 2));
                _renderHeight = Math.Max(4, (int)(outputVisualHeight * 4));

                // Ensure dimensions are multiples of 2x4 for braille
                _renderWidth = (_renderWidth / 2) * 2;
                _renderHeight = (_renderHeight / 4) * 4;
            }
            else
            {
                // ASCII mode: each char = 1 pixel, compensate for char aspect ratio
                var adjustedAspect = videoAspect / charAspect;

                if (adjustedAspect > (float)maxWidth / maxHeight)
                {
                    _renderWidth = maxWidth;
                    _renderHeight = Math.Max(1, (int)(maxWidth / adjustedAspect));
                }
                else
                {
                    _renderHeight = maxHeight;
                    _renderWidth = Math.Max(1, (int)(maxHeight * adjustedAspect));
                }
            }
        }
        else
        {
            _renderWidth = maxWidth;
            _renderHeight = maxHeight;
        }
    }

    /// <summary>
    /// Check for console resize.
    /// </summary>
    private bool CheckForResize()
    {
        try
        {
            var currentWidth = Console.WindowWidth - 1;
            var currentHeight = Console.WindowHeight - 2;

            if (currentWidth != _lastConsoleWidth || currentHeight != _lastConsoleHeight)
            {
                UpdateRenderDimensions();
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Responsive delay with cancellation checks.
    /// </summary>
    private static async Task ResponsiveDelayAsync(int totalMs, CancellationToken ct)
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

    public void Dispose()
    {
        _ffmpeg.Dispose();
    }
}
