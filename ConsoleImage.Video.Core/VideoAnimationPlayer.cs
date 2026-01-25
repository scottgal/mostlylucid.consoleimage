using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using ConsoleImage.Core;
using ConsoleImage.Core.Subtitles;
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
    private readonly StatusLine? _statusLine;

    private VideoInfo? _videoInfo;
    private int _renderWidth;
    private int _renderHeight;
    private int _lastConsoleWidth;
    private int _lastConsoleHeight;
    private int _currentFrame;
    private int _currentLoop;
    private int _totalFramesEstimate;

    // Delta rendering support for braille mode
    private CellData[,]? _previousBrailleCells;

    // Smart frame sampling
    private SmartFrameSampler? _smartSampler;

    // Subtitle rendering
    private SubtitleRenderer? _subtitleRenderer;
    private string? _lastSubtitleContent;

    // Keyboard controls
    private bool _isPaused;
    private bool _requestQuit;
    private double? _seekRequest; // Requested seek position in seconds (null = no seek)
    private double _currentPosition; // Current playback position in seconds

    /// <summary>
    /// Event raised when playback state changes (pause/resume).
    /// </summary>
    public event Action<bool>? OnPausedChanged;

    /// <summary>
    /// Seek step in seconds for arrow key navigation.
    /// </summary>
    public double SeekStepSeconds { get; set; } = 10.0;

    public VideoAnimationPlayer(string videoPath, VideoRenderOptions? options = null)
    {
        _videoPath = videoPath;
        _options = options ?? VideoRenderOptions.Default;
        _ffmpeg = new FFmpegService(
            useHardwareAcceleration: _options.UseHardwareAcceleration);

        if (_options.ShowStatus)
        {
            // Use explicit width if provided, otherwise auto-detect
            int statusWidth = _options.StatusWidth ?? 120;
            if (!_options.StatusWidth.HasValue)
            {
                try { statusWidth = Console.WindowWidth - 1; } catch { }
            }
            _statusLine = new StatusLine(statusWidth, _options.RenderOptions.UseColor);
        }
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

        // Estimate total frames for progress
        var effectiveDuration = _options.GetEffectiveDuration(_videoInfo.Duration);
        _totalFramesEstimate = (int)(effectiveDuration * effectiveFps);

        // Enter alternate screen and initialize clean state
        Console.Write("\x1b[0m");   // Reset all attributes first
        if (_options.UseAltScreen)
        {
            Console.Write("\x1b[?1049h"); // Enter alt screen
        }
        Console.Write("\x1b[?25l"); // Hide cursor
        Console.Write("\x1b[2J");   // Clear screen (ED2 - entire screen)
        Console.Write("\x1b[H");    // Home cursor
        Console.Out.Flush();

        // Initialize subtitle renderer if subtitles are available
        UpdateSubtitleRenderer();

        // Initialize smart frame sampler if enabled
        if (_options.SamplingMode == FrameSamplingMode.Smart)
        {
            _smartSampler = new SmartFrameSampler(_options.SmartSkipThreshold)
            {
                DebugMode = _options.DebugMode
            };
        }

        int loopsDone = 0;
        _currentLoop = 1;

        // Effective start time (can be modified by seeking)
        var effectiveStartTime = _options.StartTime ?? 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested && !_requestQuit)
            {
                // Check for seek request
                if (_seekRequest.HasValue)
                {
                    effectiveStartTime = Math.Clamp(_seekRequest.Value, 0, _videoInfo.Duration - 1);
                    _seekRequest = null;
                    Console.Write("\x1b[2J\x1b[H"); // Clear screen for new position
                    Console.Out.Flush();
                }

                // Check for resize
                if (CheckForResize())
                {
                    Console.Write("\x1b[2J\x1b[H");
                    Console.Out.Flush();
                    // Re-create subtitle renderer with new dimensions
                    UpdateSubtitleRenderer();
                }

                _currentFrame = 0;
                await PlayStreamAsync(frameDelayMs, effectiveFps, effectiveStartTime, cancellationToken);

                if (_requestQuit) break;
                if (_seekRequest.HasValue) continue; // Restart for seek

                loopsDone++;
                _currentLoop++;
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
    private async Task PlayStreamAsync(int frameDelayMs, double effectiveFps, double startTime, CancellationToken ct)
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
                    startTime,
                    _options.EndTime,
                    _options.FrameStep,
                    _options.TargetFps,
                    _videoInfo?.VideoCodec,
                    ct))
                {
                    using (image)
                    {
                        string frameContent;

                        // Use smart sampling if enabled
                        if (_smartSampler != null)
                        {
                            // Clone image for the render func since original is disposed
                            frameContent = _smartSampler.ProcessFrame(image, img => RenderFrame(renderer, img));
                        }
                        else
                        {
                            // Standard rendering
                            frameContent = RenderFrame(renderer, image);
                        }

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
        while (!ct.IsCancellationRequested && !_requestQuit)
        {
            // Check for keyboard input (including during buffer waits)
            if (IsKeyAvailable())
            {
                var key = Console.ReadKey(true);
                HandleKeyPress(key);
            }

            // Check for seek/quit request
            if (_seekRequest.HasValue || _requestQuit)
                break;

            // Wait for a frame to be available
            while (frameBuffer.IsEmpty && !renderingComplete && !ct.IsCancellationRequested && !_seekRequest.HasValue)
            {
                if (IsKeyAvailable())
                {
                    var key = Console.ReadKey(true);
                    HandleKeyPress(key);
                }
                await Task.Delay(1, ct);
            }

            // Re-check after waiting
            if (_seekRequest.HasValue || _requestQuit)
                break;

            if (frameBuffer.TryDequeue(out var frameContent))
            {
                frameIndex++;
                _currentFrame = frameIndex;

                // Calculate absolute video time (includes seek position)
                var absoluteTime = startTime + (frameIndex / effectiveFps);
                _currentPosition = absoluteTime; // Track for seeking

                // Build status line if enabled
                string? statusContent = null;
                if (_statusLine != null && _videoInfo != null)
                {

                    // Clip duration for progress bar calculation
                    var clipDuration = _options.GetEffectiveDuration(_videoInfo.Duration);
                    var clipProgress = frameIndex / effectiveFps;

                    var statusInfo = new StatusLine.StatusInfo
                    {
                        FileName = _options.SourceFileName ?? _videoPath,
                        SourceWidth = _videoInfo.Width,
                        SourceHeight = _videoInfo.Height,
                        OutputWidth = _renderWidth,
                        OutputHeight = _renderHeight,
                        RenderMode = _options.RenderMode.ToString(),
                        CurrentFrame = frameIndex,
                        TotalFrames = _totalFramesEstimate > 0 ? _totalFramesEstimate : null,
                        // Absolute video time (shows actual position in video)
                        CurrentTime = TimeSpan.FromSeconds(absoluteTime),
                        // Total video duration (not just clip)
                        TotalDuration = TimeSpan.FromSeconds(_videoInfo.Duration),
                        // Clip progress for the progress bar (0-1 within playback window)
                        ClipProgress = clipDuration > 0 ? clipProgress / clipDuration : 0,
                        Fps = effectiveFps,
                        Codec = _videoInfo.VideoCodec,
                        LoopNumber = _currentLoop,
                        TotalLoops = _options.LoopCount
                    };
                    statusContent = _statusLine.Render(statusInfo);
                }

                // Render subtitle if available (live transcription or static subtitles)
                string? subtitleContent = null;
                // Calculate current video time (frameIndex is 1-based after increment, so use frameIndex-1 for 0-based time)
                var currentTime = _options.StartTime ?? 0;
                currentTime += (frameIndex - 1) / effectiveFps;

                // Use live subtitle provider if available (takes precedence)
                if (_subtitleRenderer != null && _options.LiveSubtitleProvider != null)
                {
                    // Wait for transcription to catch up if needed - prevents subtitle desync
                    if (!_options.LiveSubtitleProvider.HasSubtitlesReadyFor(currentTime))
                    {
                        // Show "Transcribing..." indicator while waiting
                        subtitleContent = _subtitleRenderer.RenderText("⏳ Transcribing...");
                        var waitingBuffer = BuildFrameBuffer(frameContent, previousFrame, frameIndex == 1, statusContent, subtitleContent, _lastSubtitleContent);
                        Console.Write(waitingBuffer);
                        Console.Out.Flush();

                        var waitResult = await _options.LiveSubtitleProvider.WaitForTranscriptionAsync(
                            currentTime, timeoutMs: 15000, ct);

                        // Kick off transcription for upcoming frames
                        _ = _options.LiveSubtitleProvider.EnsureTranscribedUpToAsync(currentTime + 30, ct);
                    }

                    var entry = _options.LiveSubtitleProvider.Track.GetActiveAt(currentTime);
                    subtitleContent = _subtitleRenderer.RenderEntry(entry);
                }
                else if (_subtitleRenderer != null && _options.Subtitles != null)
                {
                    var entry = _options.Subtitles.GetActiveAt(currentTime);
                    subtitleContent = _subtitleRenderer.RenderEntry(entry);
                }

                // Build optimized frame buffer with status line and subtitles included
                var buffer = BuildFrameBuffer(frameContent, previousFrame, frameIndex == 1, statusContent, subtitleContent, _lastSubtitleContent);
                previousFrame = frameContent;
                _lastSubtitleContent = subtitleContent;

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
            VideoRenderMode.Braille => RenderBrailleFrame((BrailleRenderer)renderer, image),
            _ => RenderAsciiFrame((AsciiRenderer)renderer, image)
        };
    }

    /// <summary>
    /// Render braille frame with optional delta optimization.
    /// </summary>
    private string RenderBrailleFrame(BrailleRenderer renderer, Image<Rgba32> image)
    {
        // Use delta rendering if temporal stability is enabled
        if (_options.RenderOptions.EnableTemporalStability)
        {
            var colorThreshold = _options.RenderOptions.ColorStabilityThreshold;
            var (output, cells) = renderer.RenderWithDelta(image, _previousBrailleCells, colorThreshold);
            _previousBrailleCells = cells;
            return output;
        }

        // Standard full-frame rendering
        return renderer.RenderImage(image);
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
    /// <summary>
    /// Count newlines in a string without allocating an array.
    /// </summary>
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
    /// Get a line from content by index without allocating the entire split array.
    /// Returns empty span if index is out of range.
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
                    // Found the line, trim trailing \r if present
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

    private static string BuildFrameBuffer(
        string content,
        string? previousContent,
        bool isFirstFrame,
        string? statusLine = null,
        string? subtitleContent = null,
        string? previousSubtitle = null)
    {
        var sb = new StringBuilder();
        sb.Append("\x1b[?2026h"); // Begin synchronized output

        // Count frame lines for status/subtitle positioning (without allocating)
        var frameLines = CountLines(content);

        if (isFirstFrame || previousContent == null)
        {
            // Full redraw for first frame
            sb.Append("\x1b[H"); // Home cursor
            sb.Append(content);
        }
        else
        {
            // Diff-based rendering using spans to avoid allocations
            var currLineCount = frameLines;
            var prevLineCount = CountLines(previousContent);
            var maxLines = Math.Max(currLineCount, prevLineCount);
            var changedLines = 0;

            // First pass: count changes (using spans)
            for (int i = 0; i < maxLines; i++)
            {
                var currLine = GetLineSpan(content, i);
                var prevLine = GetLineSpan(previousContent, i);
                if (!currLine.SequenceEqual(prevLine)) changedLines++;
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
                    var currLine = GetLineSpan(content, i);
                    var prevLine = GetLineSpan(previousContent, i);

                    if (!currLine.SequenceEqual(prevLine))
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

        // Track extra rows used for positioning
        var extraRows = 0;
        var maxSubtitleLines = 2; // Fixed subtitle area height

        // Render subtitles at fixed position below frame (overwrite, don't clear)
        if (!string.IsNullOrEmpty(subtitleContent))
        {
            var subtitleLineCount = CountLines(subtitleContent);
            for (var i = 0; i < maxSubtitleLines; i++)
            {
                sb.Append($"\x1b[{frameLines + 1 + i};1H"); // Move to subtitle row
                if (i < subtitleLineCount)
                {
                    // Write subtitle line (already padded to width by SubtitleRenderer)
                    sb.Append(GetLineSpan(subtitleContent, i));
                }
                else
                {
                    // Blank line for unused subtitle rows - pad with spaces
                    sb.Append(new string(' ', 120)); // Clear unused line
                }
                sb.Append("\x1b[0m"); // Reset colors after each line
            }
            extraRows = maxSubtitleLines;
        }
        else if (!string.IsNullOrEmpty(previousSubtitle))
        {
            // Clear previous subtitle lines by overwriting with spaces
            for (var i = 0; i < maxSubtitleLines; i++)
            {
                sb.Append($"\x1b[{frameLines + 1 + i};1H");
                sb.Append(new string(' ', 120)); // Overwrite with spaces
            }
        }

        // Render status line at fixed position below subtitles (overwrite, don't clear)
        if (!string.IsNullOrEmpty(statusLine))
        {
            var statusRow = frameLines + 1 + maxSubtitleLines;
            sb.Append($"\x1b[{statusRow};1H"); // Move to status line row
            sb.Append(statusLine);
            sb.Append("\x1b[0m"); // Reset colors
        }

        sb.Append("\x1b[?2026l"); // End synchronized output
        return sb.ToString();
    }

    /// <summary>
    /// Get visible character count (excluding ANSI sequences).
    /// </summary>
    private static int GetVisibleLength(string line) => GetVisibleLength(line.AsSpan());

    /// <summary>
    /// Get visible character count (excluding ANSI sequences) - span version.
    /// </summary>
    private static int GetVisibleLength(ReadOnlySpan<char> line)
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
    /// Create or update the subtitle renderer based on current render dimensions.
    /// </summary>
    private void UpdateSubtitleRenderer()
    {
        // Check if we have any subtitle source (static or live)
        var hasStaticSubtitles = _options.Subtitles?.HasEntries == true;
        var hasLiveSubtitles = _options.LiveSubtitleProvider != null;

        if (!hasStaticSubtitles && !hasLiveSubtitles)
            return;

        int subtitleWidth = _renderWidth;
        // For blocks/braille, convert pixel width to char width
        if (_options.RenderMode == VideoRenderMode.ColorBlocks)
            subtitleWidth = _renderWidth;
        else if (_options.RenderMode == VideoRenderMode.Braille)
            subtitleWidth = _renderWidth / 2;

        _subtitleRenderer = new SubtitleRenderer(subtitleWidth, 2, _options.RenderOptions.UseColor);
        _lastSubtitleContent = null; // Reset cached content after resize
    }

    /// <summary>
    /// Responsive delay with cancellation checks and keyboard handling.
    /// </summary>
    private async Task ResponsiveDelayAsync(int totalMs, CancellationToken ct)
    {
        const int chunkMs = 50;
        var remaining = totalMs;

        while ((remaining > 0 || _isPaused) && !ct.IsCancellationRequested && !_requestQuit)
        {
            // Check for keyboard input
            if (IsKeyAvailable())
            {
                var key = Console.ReadKey(true);
                HandleKeyPress(key);
            }

            // When paused, just wait without counting down
            if (_isPaused)
            {
                await Task.Delay(chunkMs, ct);
                continue;
            }

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
    /// Handle keyboard input during playback.
    /// </summary>
    private void HandleKeyPress(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Spacebar:
                _isPaused = !_isPaused;
                OnPausedChanged?.Invoke(_isPaused);
                // Show pause indicator
                if (_isPaused)
                {
                    Console.Write("\x1b[s"); // Save cursor
                    Console.Write("\x1b[1;1H"); // Move to top-left
                    Console.Write("\x1b[43;30m PAUSED \x1b[0m"); // Yellow background
                    Console.Write("\x1b[u"); // Restore cursor
                }
                else
                {
                    Console.Write("\x1b[s"); // Save cursor
                    Console.Write("\x1b[1;1H"); // Move to top-left
                    Console.Write("        "); // Clear pause indicator
                    Console.Write("\x1b[u"); // Restore cursor
                }
                Console.Out.Flush();
                break;

            case ConsoleKey.RightArrow:
                // Seek forward
                _seekRequest = _currentPosition + SeekStepSeconds;
                ShowIndicator($">> +{SeekStepSeconds}s");
                break;

            case ConsoleKey.LeftArrow:
                // Seek backward
                _seekRequest = Math.Max(0, _currentPosition - SeekStepSeconds);
                ShowIndicator($"<< -{SeekStepSeconds}s");
                break;

            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                _requestQuit = true;
                break;

            // Mode hints (full mode switching would require restart)
            case ConsoleKey.A:
                ShowIndicator("ASCII mode: use -a flag");
                break;
            case ConsoleKey.B:
                if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
                    ShowIndicator("Braille mode: default or use -B flag");
                else
                    ShowIndicator("Blocks mode: use -b flag");
                break;
            case ConsoleKey.M:
                if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
                    ShowIndicator("Matrix mode: use -M flag");
                else
                    ShowIndicator("Monochrome: use --mono flag");
                break;

            // Seek step adjustment
            case ConsoleKey.OemPlus:
            case ConsoleKey.Add:
                SeekStepSeconds = Math.Min(60, SeekStepSeconds * 2);
                ShowIndicator($"Seek step: {SeekStepSeconds}s");
                break;
            case ConsoleKey.OemMinus:
            case ConsoleKey.Subtract:
                SeekStepSeconds = Math.Max(5, SeekStepSeconds / 2);
                ShowIndicator($"Seek step: {SeekStepSeconds}s");
                break;
        }
    }

    private void ShowIndicator(string text)
    {
        Console.Write("\x1b[s"); // Save cursor
        Console.Write("\x1b[1;1H"); // Move to top-left
        Console.Write($"\x1b[46;30m {text} \x1b[0m"); // Cyan background
        Console.Write("\x1b[u"); // Restore cursor
        Console.Out.Flush();
    }

    /// <summary>
    /// Safely check if a key is available (handles non-terminal scenarios).
    /// </summary>
    private static bool IsKeyAvailable()
    {
        try
        {
            return Console.KeyAvailable;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _ffmpeg.Dispose();
    }
}
