// Slideshow handler for directories and glob patterns

using System.Collections.Concurrent;
using System.Text;
using ConsoleImage.Cli.Utilities;
using ConsoleImage.Core;
using ConsoleImage.Core.Subtitles;
using ConsoleImage.Video.Core;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Cli.Handlers;

/// <summary>
///     Slideshow options passed from CLI.
/// </summary>
public record SlideshowOptions
{
    public float SlideDelay { get; init; } = 3.0f;
    public bool Shuffle { get; init; }
    public bool Recursive { get; init; }
    public string SortBy { get; init; } = "date"; // Default: newest first
    public bool SortDesc { get; init; } = true; // Default: descending (newest first)
    public float VideoPreview { get; init; } // 0 = play full video (default)
    public bool GifLoop { get; init; }
    public bool HideStatus { get; init; } // Hide the status line below content

    // Output options
    public string? OutputPath { get; init; } // Output to GIF file
    public int GifFontSize { get; init; } = 10;
    public float GifScale { get; init; } = 1.0f;
    public int GifColors { get; init; } = 64;

    // Render options - smaller defaults for slideshow
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int MaxWidth { get; init; } = 50; // Smaller for slideshow
    public int MaxHeight { get; init; } = 30;
    public bool UseAscii { get; init; }
    public bool UseBlocks { get; init; }
    public bool UseBraille { get; init; } = true;
    public bool UseMatrix { get; init; }
    public bool UseColor { get; init; } = true;
    public bool UseGreyscaleAnsi { get; init; }
    public float Contrast { get; init; } = 2.5f;
    public float Gamma { get; init; } = 0.65f;
    public float? CharAspect { get; init; }
    public bool ShowStatus { get; init; }

    // Playback options - passed through to video/animation
    public int Loop { get; init; } = 1; // 0 = infinite loop
    public float Speed { get; init; } = 1.0f;

    // Subtitle options
    public string? SubsValue { get; init; } // auto|off|<path>|whisper|yt
    public string SubtitleLang { get; init; } = "en";

    // Performance
    public bool NoDither { get; init; }

    // Calibration
    public CalibrationSettings? SavedCalibration { get; init; }
}

/// <summary>
///     Cached rendered content for a slide.
/// </summary>
internal sealed class CachedSlide
{
    public string? RenderedContent { get; set; }
    public List<IAnimationFrame>? AnimationFrames { get; set; }
    public bool IsAnimated => AnimationFrames != null && AnimationFrames.Count > 1;
    public bool IsVideo { get; set; }
    public bool IsDocument { get; set; }
}

/// <summary>
///     Handles slideshow mode for directories and glob patterns.
/// </summary>
public static class SlideshowHandler
{
    // Use HashSet for O(1) extension lookups
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tiff", ".tif" };

    private static readonly HashSet<string> GifExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".gif" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv" };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".cidz", ".json", ".ndjson" };

    // Pre-computed array of all extensions for directory enumeration
    private static readonly string[] AllExtensionPatterns;
    private static readonly HashSet<string> AllExtensions;

    static SlideshowHandler()
    {
        AllExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AllExtensions.UnionWith(ImageExtensions);
        AllExtensions.UnionWith(GifExtensions);
        AllExtensions.UnionWith(VideoExtensions);
        AllExtensions.UnionWith(DocumentExtensions);
        AllExtensionPatterns = AllExtensions.Select(e => $"*{e}").ToArray();
    }

    /// <summary>
    ///     Run slideshow for the given input (directory or glob pattern).
    /// </summary>
    public static async Task<int> HandleAsync(
        string input,
        SlideshowOptions options,
        CancellationToken ct)
    {
        // Find all matching files
        var files = GetMatchingFiles(input, options.Recursive).ToList();

        if (files.Count == 0)
        {
            Console.Error.WriteLine($"No supported files found matching: {input}");
            return 1;
        }

        // Sort files
        files = SortFiles(files, options.SortBy, options.SortDesc, options.Shuffle);

        // If output path specified, render to GIF instead of interactive slideshow
        if (!string.IsNullOrEmpty(options.OutputPath)) return await RenderToGifAsync(files, options, ct);

        Console.Error.WriteLine($"Slideshow: {files.Count} files");
        if (options.SlideDelay <= 0)
            Console.Error.WriteLine("Controls: Left/Right=prev/next, Q=quit (manual mode)");
        else
            Console.Error.WriteLine(
                $"Controls: Space=pause, Left/Right=prev/next, Q=quit ({options.SlideDelay}s delay)");
        Console.Error.WriteLine();

        // Enter alternate screen buffer for clean display
        Console.Write("\x1b[?1049h"); // Alt screen
        Console.Write("\x1b[?25l"); // Hide cursor

        try
        {
            return await RunSlideshowAsync(files, options, ct);
        }
        finally
        {
            // Exit alternate screen and restore cursor
            Console.Write("\x1b[?25h"); // Show cursor
            Console.Write("\x1b[?1049l"); // Exit alt screen
            Console.Write("\x1b[0m"); // Reset colors
        }
    }

    /// <summary>
    ///     Render slideshow to an animated GIF file.
    /// </summary>
    private static async Task<int> RenderToGifAsync(
        List<string> files,
        SlideshowOptions options,
        CancellationToken ct)
    {
        var renderOptions = CreateRenderOptions(options);
        var delayMs = (int)(options.SlideDelay * 1000);

        Console.Error.WriteLine($"Rendering slideshow to GIF: {options.OutputPath}");
        Console.Error.WriteLine($"  Files: {files.Count}");
        Console.Error.WriteLine($"  Frame delay: {delayMs}ms");

        var gifOptions = new GifWriterOptions
        {
            FontSize = options.GifFontSize,
            Scale = options.GifScale,
            MaxColors = Math.Clamp(options.GifColors, 16, 256),
            LoopCount = 0 // Infinite loop by default
        };

        using var gifWriter = new GifWriter(gifOptions);
        var frameIndex = 0;

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;

            frameIndex++;
            Console.Error.Write($"\rRendering frame {frameIndex}/{files.Count}: {Path.GetFileName(file)}");

            try
            {
                var ext = Path.GetExtension(file);

                // Skip videos and documents for GIF output
                if (VideoExtensions.Contains(ext) || DocumentExtensions.Contains(ext))
                {
                    Console.Error.WriteLine(" [skipped - not an image]");
                    continue;
                }

                using var image = await Image.LoadAsync<Rgba32>(file, ct);

                // For animated GIFs, just use first frame
                string content;
                if (options.UseBraille)
                {
                    using var renderer = new BrailleRenderer(renderOptions);
                    content = renderer.RenderImage(image);
                    var frame = new BrailleFrame(content, delayMs);
                    gifWriter.AddBrailleFrame(frame, delayMs);
                }
                else if (options.UseBlocks)
                {
                    using var renderer = new ColorBlockRenderer(renderOptions);
                    content = renderer.RenderImage(image);
                    var frame = new ColorBlockFrame(content, delayMs);
                    gifWriter.AddColorBlockFrame(frame, delayMs);
                }
                else
                {
                    using var renderer = new AsciiRenderer(renderOptions);
                    var asciiFrame = renderer.RenderImage(image);
                    gifWriter.AddFrame(asciiFrame.ToAnsiString(), delayMs);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($" [error: {ex.Message}]");
            }
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("Saving GIF...");

        await gifWriter.SaveAsync(options.OutputPath!, ct);
        Console.Error.WriteLine($"Saved to: {Path.GetFullPath(options.OutputPath!)}");

        return 0;
    }

    private static IEnumerable<string> GetMatchingFiles(string input, bool recursive)
    {
        // Directory - get all supported files
        if (Directory.Exists(input))
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var pattern in AllExtensionPatterns)
            foreach (var file in Directory.EnumerateFiles(input, pattern, searchOption))
                yield return file;

            yield break;
        }

        // Glob pattern
        if (input.Contains('*') || input.Contains('?'))
        {
            var basePath = GetGlobBasePath(input);
            var pattern = input.AsSpan(basePath.Length).TrimStart(Path.DirectorySeparatorChar)
                .TrimStart(Path.AltDirectorySeparatorChar).ToString();

            if (string.IsNullOrEmpty(basePath))
                basePath = ".";

            if (!Directory.Exists(basePath))
                yield break;

            var matcher = new Matcher();
            matcher.AddInclude(pattern);

            if (recursive && !pattern.Contains("**"))
                // Add recursive version of the pattern
                matcher.AddInclude("**/" + pattern);

            var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(basePath)));

            foreach (var match in result.Files)
            {
                var fullPath = Path.Combine(basePath, match.Path);
                var ext = Path.GetExtension(fullPath);
                if (AllExtensions.Contains(ext)) yield return fullPath;
            }
        }
    }

    private static string GetGlobBasePath(string pattern)
    {
        // Find the longest path prefix without wildcards using span to minimize allocations
        var span = pattern.AsSpan();
        var lastSeparator = -1;

        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if (c == '*' || c == '?')
                break;
            if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                lastSeparator = i;
        }

        return lastSeparator >= 0 ? pattern[..lastSeparator] : string.Empty;
    }

    private static List<string> SortFiles(List<string> files, string sortBy, bool descending, bool shuffle)
    {
        if (shuffle || sortBy.Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            // Fisher-Yates shuffle in-place is more efficient
            var rng = new Random();
            for (var i = files.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (files[i], files[j]) = (files[j], files[i]);
            }

            return files;
        }

        var sortByLower = sortBy.ToLowerInvariant();

        // For metadata-based sorts, cache FileInfo to avoid repeated allocations
        if (sortByLower is "date" or "modified" or "size" or "created")
        {
            // Create tuples with cached metadata
            var withInfo = files.Select(f =>
            {
                var info = new FileInfo(f);
                return (Path: f, Info: info);
            }).ToList();

            IEnumerable<(string Path, FileInfo Info)> sorted = sortByLower switch
            {
                "date" or "modified" => withInfo.OrderBy(x => x.Info.LastWriteTimeUtc),
                "size" => withInfo.OrderBy(x => x.Info.Length),
                "created" => withInfo.OrderBy(x => x.Info.CreationTimeUtc),
                _ => withInfo
            };

            if (descending)
                sorted = sorted.Reverse();

            return sorted.Select(x => x.Path).ToList();
        }

        // Name-based sorts don't need FileInfo
        IEnumerable<string> sortedPaths = sortByLower switch
        {
            "ext" or "extension" => files.OrderBy(Path.GetExtension, StringComparer.OrdinalIgnoreCase)
                .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase),
            _ => files.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        };

        return descending ? sortedPaths.Reverse().ToList() : sortedPaths.ToList();
    }

    private static async Task<int> RunSlideshowAsync(
        List<string> files,
        SlideshowOptions options,
        CancellationToken ct)
    {
        var currentIndex = 0;
        var paused = false;
        var quit = false;

        // Create render options once
        var renderOptions = CreateRenderOptions(options);
        var delayMs = (int)(options.SlideDelay * 1000);
        var fileCount = files.Count;

        // Pre-render cache for silky smooth transitions
        var cache = new ConcurrentDictionary<int, CachedSlide>();
        var preRenderCts = new CancellationTokenSource();

        // Start pre-rendering the first few slides
        _ = PreRenderAheadAsync(files, 0, options, renderOptions, cache, 2, preRenderCts.Token);

        try
        {
            while (!quit && !ct.IsCancellationRequested)
            {
                var file = files[currentIndex];
                var fileName = file; // Show full path (truncated by FormatFilenameHeader if needed)
                var ext = Path.GetExtension(file);

                // Clear screen for new slide
                Console.Write("\x1b[?2026h"); // Begin sync
                Console.Write("\x1b[H"); // Home position
                Console.Write("\x1b[2J"); // Clear screen

                // Content starts at line 2 (line 1 reserved for filename header)
                var contentStartLine = 2;

                // Render filename header at row 1
                var headerWidth = Math.Max(renderOptions.MaxWidth, 30);
                var headerText = FormatFilenameHeader(fileName, currentIndex + 1, fileCount, headerWidth);
                Console.Write("\x1b[1;1H"); // Position at row 1
                Console.Write(headerText);
                Console.Write("\x1b[?2026l"); // End sync

                // Check cache first for instant display
                CachedSlide? cached = null;
                cache.TryGetValue(currentIndex, out cached);

                // Track rendered content for status line positioning
                string? renderedContent = null;
                var contentLines = 0;
                var contentWidth = 0;

                // Display the slide
                using var displayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                Task<int> displayTask; // Returns navigation direction (0=none, -1=prev, +1=next)

                if (cached?.RenderedContent != null && !cached.IsAnimated && !cached.IsVideo && !cached.IsDocument)
                {
                    // Use cached static image - instant display!
                    renderedContent = cached.RenderedContent;
                    contentLines = CountLines(renderedContent);
                    contentWidth = GetMaxVisibleWidth(renderedContent);
                    Console.Write($"\x1b[?2026h\x1b[?25l\x1b[{contentStartLine};1H{renderedContent}\x1b[0m\x1b[?2026l");
                    Console.Out.Flush();
                    displayTask = Task.FromResult(0);
                }
                else if (cached?.AnimationFrames != null && cached.IsAnimated)
                {
                    // Use cached GIF frames - play in-place without alt screen
                    if (cached.AnimationFrames.Count > 0)
                    {
                        contentLines = CountLines(cached.AnimationFrames[0].Content);
                        contentWidth = GetMaxVisibleWidth(cached.AnimationFrames[0].Content);
                    }

                    var loopCount = options.GifLoop ? 0 : 1;
                    displayTask = PlayFramesInPlaceAsync(cached.AnimationFrames, loopCount, 1.0f, contentStartLine,
                        displayCts.Token).ContinueWith(_ => 0, TaskContinuationOptions.ExecuteSynchronously);
                }
                else
                {
                    // Not cached - estimate content size from render options
                    contentLines = renderOptions.MaxHeight;
                    contentWidth = renderOptions.MaxWidth;
                    // Not cached - render now (wrap in try-catch to continue on errors)
                    displayTask = SafeDisplayFileAsync(file, ext, options, renderOptions, contentStartLine,
                        displayCts.Token);
                }

                // Calculate status line position  -  constrain width to content width
                var statusLineRow = contentStartLine + contentLines;
                var statusWidth = Math.Max(contentWidth, 30);

                // Show initial status line (unless hidden or display task is still rendering)
                // Don't write status while video/animation is rendering — concurrent console
                // writes corrupt ANSI escape sequences and produce visual artifacts.
                if (!options.HideStatus && displayTask.IsCompleted)
                {
                    var statusText = FormatSlideshowStatus(
                        fileName, currentIndex + 1, fileCount, paused, 0, delayMs, statusWidth);
                    Console.Write($"\x1b[{statusLineRow};1H");
                    Console.Write(statusText);
                    Console.Out.Flush();
                }

                // Start pre-rendering upcoming slides in background
                _ = PreRenderAheadAsync(files, currentIndex, options, renderOptions, cache, 2, preRenderCts.Token);

                // Wait for display/delay to complete or user input
                var elapsed = 0;
                const int pollInterval = 100;
                var userSkipped = false;
                var previousIndex = currentIndex;

                // Unified input loop - handles both animations and static images
                while (!quit && currentIndex == previousIndex)
                {
                    // Check for keyboard input (safely - may fail if not a terminal)
                    if (IsKeyAvailable())
                    {
                        var key = Console.ReadKey(true);
                        switch (key.Key)
                        {
                            case ConsoleKey.Q:
                            case ConsoleKey.Escape:
                                quit = true;
                                await displayCts.CancelAsync();
                                break;

                            case ConsoleKey.Spacebar:
                                paused = !paused;
                                break;

                            // Up/Down arrows for navigation (Up=prev, Down=next)
                            case ConsoleKey.DownArrow:
                            case ConsoleKey.N:
                                await displayCts.CancelAsync();
                                currentIndex = (currentIndex + 1) % fileCount;
                                userSkipped = true;
                                break;

                            case ConsoleKey.UpArrow:
                            case ConsoleKey.P:
                                await displayCts.CancelAsync();
                                currentIndex = (currentIndex - 1 + fileCount) % fileCount;
                                userSkipped = true;
                                break;

                            // Left/Right navigate between files (same as P/N)
                            case ConsoleKey.LeftArrow:
                                await displayCts.CancelAsync();
                                currentIndex = (currentIndex - 1 + fileCount) % fileCount;
                                userSkipped = true;
                                break;

                            case ConsoleKey.RightArrow:
                                await displayCts.CancelAsync();
                                currentIndex = (currentIndex + 1) % fileCount;
                                userSkipped = true;
                                break;

                            case ConsoleKey.Home:
                                await displayCts.CancelAsync();
                                currentIndex = 0;
                                userSkipped = true;
                                break;

                            case ConsoleKey.End:
                                await displayCts.CancelAsync();
                                currentIndex = fileCount - 1;
                                userSkipped = true;
                                break;
                        }
                    }

                    if (userSkipped || quit) break;

                    try
                    {
                        await Task.Delay(pollInterval, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    // Update status line with progress (only when display task is done to
                    // avoid concurrent console writes with video/animation rendering)
                    if (!options.HideStatus && displayTask.IsCompleted)
                    {
                        var statusText = FormatSlideshowStatus(
                            fileName, currentIndex + 1, fileCount, paused, elapsed, delayMs, statusWidth);
                        Console.Write($"\x1b[{statusLineRow};1H");
                        Console.Write(statusText);
                        Console.Out.Flush();
                    }

                    // Check if we should auto-advance (only if delayMs > 0)
                    if (delayMs > 0 && displayTask.IsCompleted && !paused)
                    {
                        elapsed += pollInterval;
                        if (elapsed >= delayMs)
                        {
                            currentIndex = (currentIndex + 1) % fileCount;
                            break;
                        }
                    }
                }

                // Ensure display task is finished and check for navigation from video player
                var navDirection = 0;
                try
                {
                    navDirection = await displayTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when skipping
                }

                // If video player requested navigation (Up/Down during video), apply it
                if (navDirection != 0 && currentIndex == previousIndex)
                {
                    currentIndex = navDirection > 0
                        ? (currentIndex + 1) % fileCount
                        : (currentIndex - 1 + fileCount) % fileCount;
                }

                // Clean up old cache entries (keep current +/- 2)
                CleanCache(cache, currentIndex, fileCount, 3);
            }
        }
        finally
        {
            preRenderCts.Cancel();
        }

        return 0;
    }

    /// <summary>
    ///     Pre-render upcoming slides in the background for silky smooth transitions.
    /// </summary>
    private static async Task PreRenderAheadAsync(
        List<string> files,
        int currentIndex,
        SlideshowOptions options,
        RenderOptions renderOptions,
        ConcurrentDictionary<int, CachedSlide> cache,
        int lookahead,
        CancellationToken ct)
    {
        var fileCount = files.Count;

        // Pre-render next slides
        for (var i = 1; i <= lookahead; i++)
        {
            if (ct.IsCancellationRequested) break;

            var nextIndex = (currentIndex + i) % fileCount;
            if (cache.ContainsKey(nextIndex)) continue;

            var file = files[nextIndex];
            var ext = Path.GetExtension(file);

            try
            {
                var cached = await PreRenderSlideAsync(file, ext, options, renderOptions, ct);
                cache.TryAdd(nextIndex, cached);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Skip failed pre-renders silently
            }
        }

        // Also pre-render previous slide for backwards navigation
        var prevIndex = (currentIndex - 1 + fileCount) % fileCount;
        if (!cache.ContainsKey(prevIndex) && !ct.IsCancellationRequested)
        {
            var file = files[prevIndex];
            var ext = Path.GetExtension(file);

            try
            {
                var cached = await PreRenderSlideAsync(file, ext, options, renderOptions, ct);
                cache.TryAdd(prevIndex, cached);
            }
            catch
            {
                // Skip failed pre-renders silently
            }
        }
    }

    /// <summary>
    ///     Pre-render a single slide for caching.
    /// </summary>
    private static async Task<CachedSlide> PreRenderSlideAsync(
        string file,
        string ext,
        SlideshowOptions options,
        RenderOptions renderOptions,
        CancellationToken ct)
    {
        var cached = new CachedSlide();

        if (ImageExtensions.Contains(ext))
        {
            using var image = await Image.LoadAsync<Rgba32>(file, ct);
            cached.RenderedContent = RenderImage(image, renderOptions, options);
        }
        else if (GifExtensions.Contains(ext))
        {
            using var image = await Image.LoadAsync<Rgba32>(file, ct);

            if (image.Frames.Count <= 1)
            {
                cached.RenderedContent = RenderImage(image, renderOptions, options);
            }
            else
            {
                // Pre-render all GIF frames
                if (options.UseBraille)
                {
                    using var renderer = new BrailleRenderer(renderOptions);
                    cached.AnimationFrames = RenderGifFrames(image, renderer, renderOptions);
                }
                else if (options.UseBlocks)
                {
                    using var renderer = new ColorBlockRenderer(renderOptions);
                    cached.AnimationFrames = RenderGifFrames(image, renderer, renderOptions);
                }
                else
                {
                    using var renderer = new AsciiRenderer(renderOptions);
                    cached.AnimationFrames = renderer.RenderGif(file)
                        .Select(f => (IAnimationFrame)new AsciiFrameAdapter(f, renderOptions.UseColor))
                        .ToList();
                }
            }
        }
        else if (VideoExtensions.Contains(ext))
        {
            cached.IsVideo = true; // Can't pre-render videos
        }
        else if (DocumentExtensions.Contains(ext))
        {
            cached.IsDocument = true; // Documents have their own player
        }

        return cached;
    }

    /// <summary>
    ///     Clean up old cache entries to prevent memory bloat.
    /// </summary>
    private static void CleanCache(
        ConcurrentDictionary<int, CachedSlide> cache,
        int currentIndex,
        int fileCount,
        int keepRange)
    {
        var toRemove = new List<int>();

        foreach (var key in cache.Keys)
        {
            // Calculate distance considering wrap-around
            var distance = Math.Min(
                Math.Abs(key - currentIndex),
                fileCount - Math.Abs(key - currentIndex));

            if (distance > keepRange) toRemove.Add(key);
        }

        foreach (var key in toRemove) cache.TryRemove(key, out _);
    }

    /// <summary>
    ///     Safe wrapper that catches all errors and continues to next slide.
    /// </summary>
    /// <summary>
    ///     Safely display a file, returning navigation direction from video player
    ///     (0 = none, -1 = previous, +1 = next).
    /// </summary>
    private static async Task<int> SafeDisplayFileAsync(
        string file,
        string ext,
        SlideshowOptions options,
        RenderOptions renderOptions,
        int contentStartLine,
        CancellationToken ct)
    {
        try
        {
            return await DisplayFileAsync(file, ext, options, renderOptions, contentStartLine, ct);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation
        }
        catch (Exception ex)
        {
            // Log error but don't crash - just show error and return
            Console.Write("\x1b[?2026h");
            Console.Write($"\x1b[{contentStartLine};1H");
            Console.Error.WriteLine($"Error loading file: {ex.Message}");
            Console.Write("\x1b[?2026l");
            return 0;
        }
    }

    /// <summary>
    ///     Display a file, returning navigation direction from video player
    ///     (0 = none, -1 = previous, +1 = next).
    /// </summary>
    private static async Task<int> DisplayFileAsync(
        string file,
        string ext,
        SlideshowOptions options,
        RenderOptions renderOptions,
        int contentStartLine,
        CancellationToken ct)
    {
        if (ImageExtensions.Contains(ext))
        {
            await DisplayImageAsync(file, renderOptions, options, contentStartLine, ct);
            return 0;
        }

        if (GifExtensions.Contains(ext))
        {
            await DisplayGifAsync(file, renderOptions, options, contentStartLine, ct);
            return 0;
        }

        if (VideoExtensions.Contains(ext))
            return await DisplayVideoAsync(file, options, ct);

        if (DocumentExtensions.Contains(ext))
            await DisplayDocumentAsync(file, options.GifLoop, ct);

        return 0;
    }

    private static async Task DisplayImageAsync(string file, RenderOptions renderOptions, SlideshowOptions options,
        int contentStartLine, CancellationToken ct)
    {
        using var image = await Image.LoadAsync<Rgba32>(file, ct);
        var output = RenderImage(image, renderOptions, options);

        // Atomic write: single Console.Write with sync + cursor hide to prevent any flash
        Console.Write($"\x1b[?2026h\x1b[?25l\x1b[{contentStartLine};1H{output}\x1b[0m\x1b[?2026l");
        Console.Out.Flush();
    }

    private static string RenderImage(Image<Rgba32> image, RenderOptions renderOptions, SlideshowOptions options)
    {
        if (options.UseBraille)
        {
            using var renderer = new BrailleRenderer(renderOptions);
            return renderer.RenderImage(image);
        }

        if (options.UseBlocks)
        {
            using var renderer = new ColorBlockRenderer(renderOptions);
            return renderer.RenderImage(image);
        }

        using var asciiRenderer = new AsciiRenderer(renderOptions);
        var frame = asciiRenderer.RenderImage(image);
        return frame.ToAnsiString();
    }

    private static async Task DisplayGifAsync(string file, RenderOptions renderOptions, SlideshowOptions options,
        int contentStartLine, CancellationToken ct)
    {
        // Load image once and reuse for both frame check and rendering
        using var image = await Image.LoadAsync<Rgba32>(file, ct);

        if (image.Frames.Count <= 1)
        {
            // Static image - render directly from already-loaded image
            var output = RenderImage(image, renderOptions, options);
            Console.Write($"\x1b[?2026h\x1b[?25l\x1b[{contentStartLine};1H{output}\x1b[0m\x1b[?2026l");
            Console.Out.Flush();
            return;
        }

        // Animated GIF - render frames from already-loaded image
        List<IAnimationFrame> frames;
        if (options.UseBraille)
        {
            using var renderer = new BrailleRenderer(renderOptions);
            frames = RenderGifFrames(image, renderer, renderOptions);
        }
        else if (options.UseBlocks)
        {
            using var renderer = new ColorBlockRenderer(renderOptions);
            frames = RenderGifFrames(image, renderer, renderOptions);
        }
        else
        {
            using var renderer = new AsciiRenderer(renderOptions);
            frames = renderer.RenderGif(file)
                .Select(f => (IAnimationFrame)new AsciiFrameAdapter(f, renderOptions.UseColor))
                .ToList();
        }

        // Use GifLoop option or general Loop option (0 = infinite)
        var loopCount = options.GifLoop ? 0 : options.Loop;
        await PlayFramesInPlaceAsync(frames, loopCount, options.Speed, contentStartLine, ct);
    }

    // Render GIF frames from already-loaded image to avoid double-loading
    private static List<IAnimationFrame> RenderGifFrames(Image<Rgba32> image, BrailleRenderer renderer,
        RenderOptions options)
    {
        var frames = new List<IAnimationFrame>(image.Frames.Count);
        var sampleRate = options.FrameSampleRate;

        for (var i = 0; i < image.Frames.Count; i++)
        {
            if (sampleRate > 1 && i % sampleRate != 0)
                continue;

            using var frameImage = image.Frames.CloneFrame(i);
            var content = renderer.RenderImage(frameImage);

            var metadata = image.Frames[i].Metadata.GetGifMetadata();
            var delayMs = metadata.FrameDelay * 10;
            if (delayMs == 0) delayMs = 100;
            delayMs = (int)(delayMs / options.AnimationSpeedMultiplier);

            frames.Add(new BrailleFrame(content, delayMs));
        }

        return frames;
    }

    private static List<IAnimationFrame> RenderGifFrames(Image<Rgba32> image, ColorBlockRenderer renderer,
        RenderOptions options)
    {
        var frames = new List<IAnimationFrame>(image.Frames.Count);
        var sampleRate = options.FrameSampleRate;

        for (var i = 0; i < image.Frames.Count; i++)
        {
            if (sampleRate > 1 && i % sampleRate != 0)
                continue;

            using var frameImage = image.Frames.CloneFrame(i);
            var content = renderer.RenderImage(frameImage);

            var metadata = image.Frames[i].Metadata.GetGifMetadata();
            var delayMs = metadata.FrameDelay * 10;
            if (delayMs == 0) delayMs = 100;
            delayMs = (int)(delayMs / options.AnimationSpeedMultiplier);

            frames.Add(new ColorBlockFrame(content, delayMs));
        }

        return frames;
    }

    /// <summary>
    ///     Display a video file, returning navigation direction from the player
    ///     (0 = none, -1 = previous, +1 = next).
    /// </summary>
    private static async Task<int> DisplayVideoAsync(string file, SlideshowOptions options, CancellationToken ct)
    {
        if (!FFmpegProvider.IsAvailable())
        {
            Console.Error.WriteLine("Video playback requires FFmpeg. Skipping video.");
            return 0;
        }

        try
        {
            // Load subtitles if specified
            SubtitleTrack? subtitles = null;
            if (!string.IsNullOrEmpty(options.SubsValue) && options.SubsValue != "off")
                subtitles = await LoadSubtitlesForVideoAsync(file, options.SubsValue, options.SubtitleLang, ct);

            var videoOptions = new VideoRenderOptions
            {
                RenderOptions = CreateRenderOptions(options),
                EndTime = options.VideoPreview > 0 ? options.VideoPreview : null,
                LoopCount = options.Loop,
                SpeedMultiplier = options.Speed,
                UseAltScreen = false,
                Subtitles = subtitles,
                ContentStartRow = 2, // Reserve row 1 for filename header
                RenderMode = options.UseBraille ? VideoRenderMode.Braille
                    : options.UseBlocks ? VideoRenderMode.ColorBlocks
                    : VideoRenderMode.Ascii
            };

            using var player = new VideoAnimationPlayer(file, videoOptions);
            await player.PlayAsync(ct);
            return player.NavigationRequest;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Video playback error: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    ///     Load subtitles for a video file.
    /// </summary>
    private static async Task<SubtitleTrack?> LoadSubtitlesForVideoAsync(
        string videoPath,
        string subsValue,
        string lang,
        CancellationToken ct)
    {
        try
        {
            // Check if SubsValue is a file path
            if (File.Exists(subsValue)) return await SubtitleParser.ParseAsync(subsValue, ct);

            // Auto mode - search for matching subtitle file
            if (subsValue.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                var subFile = SubtitleResolver.FindMatchingSubtitleFile(videoPath, lang)
                              ?? SubtitleResolver.FindSubtitleInMediaLibrary(videoPath);

                if (!string.IsNullOrEmpty(subFile)) return await SubtitleParser.ParseAsync(subFile, ct);
            }

            return null;
        }
        catch
        {
            return null; // Silent fail for slideshow
        }
    }

    private static async Task DisplayDocumentAsync(string file, bool loop, CancellationToken ct)
    {
        var doc = await ConsoleImageDocument.LoadAsync(file, ct);

        // Auto-detect sidecar subtitle file
        SubtitleTrack? subtitles = null;
        if (doc.Settings.SubtitlesEnabled || !string.IsNullOrEmpty(doc.Settings.SubtitleFile))
        {
            var dir = Path.GetDirectoryName(file) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(file);
            if (baseName.EndsWith(".cid", StringComparison.OrdinalIgnoreCase))
                baseName = baseName[..^4];

            // Check explicit sidecar filename from metadata
            if (!string.IsNullOrEmpty(doc.Settings.SubtitleFile))
            {
                var explicitPath = Path.Combine(dir, doc.Settings.SubtitleFile);
                if (File.Exists(explicitPath))
                    subtitles = await SubtitleParser.ParseAsync(explicitPath, ct);
            }

            // Fall back to same-name .vtt/.srt
            if (subtitles == null)
            {
                var vttPath = Path.Combine(dir, baseName + ".vtt");
                var srtPath = Path.Combine(dir, baseName + ".srt");
                if (File.Exists(vttPath))
                    subtitles = await SubtitleParser.ParseAsync(vttPath, ct);
                else if (File.Exists(srtPath))
                    subtitles = await SubtitleParser.ParseAsync(srtPath, ct);
            }
        }

        using var player = new DocumentPlayer(doc, loopCount: loop ? 0 : 1, subtitles: subtitles);
        await player.PlayAsync(ct);
    }

    private static RenderOptions CreateRenderOptions(SlideshowOptions options)
    {
        var renderMode = RenderHelpers.GetRenderMode(
            options.UseBraille, options.UseBlocks, options.UseMatrix);
        var effectiveAspect = RenderHelpers.GetEffectiveAspectRatio(
            options.CharAspect, options.SavedCalibration, renderMode);

        return new RenderOptions
        {
            Width = options.Width,
            Height = options.Height,
            MaxWidth = options.MaxWidth,
            MaxHeight = options.MaxHeight,
            UseColor = options.UseColor,
            UseGreyscaleAnsi = options.UseGreyscaleAnsi,
            CharacterAspectRatio = effectiveAspect,
            ContrastPower = options.Contrast,
            Gamma = options.Gamma,
            LoopCount = 1,
            DisableBrailleDithering = options.NoDither
        };
    }

    /// <summary>
    ///     Format the filename header displayed at the top of the slideshow.
    ///     Format: "[1/10] filename.ext" — dim, constrained to maxWidth.
    /// </summary>
    private static string FormatFilenameHeader(string fileName, int current, int total, int maxWidth)
    {
        var indexStr = $"[{current}/{total}] ";
        var availableForName = maxWidth - indexStr.Length;

        string namePart;
        if (availableForName < 4)
        {
            namePart = "";
        }
        else if (fileName.Length <= availableForName)
        {
            namePart = fileName;
        }
        else
        {
            var ext = Path.GetExtension(fileName);
            var withoutExt = fileName.AsSpan(0, fileName.Length - ext.Length);
            var maxNameLen = availableForName - ext.Length - 1; // 1 for "…"
            if (maxNameLen > 3 && withoutExt.Length > 0)
                namePart = string.Concat(withoutExt.Slice(0, Math.Min(maxNameLen, withoutExt.Length)), "\u2026", ext);
            else
                namePart = string.Concat(fileName.AsSpan(0, Math.Max(1, Math.Min(availableForName - 1, fileName.Length))), "\u2026");
        }

        var sb = new StringBuilder(maxWidth + 16);
        sb.Append("\x1b[2m"); // Dim
        sb.Append(indexStr);
        sb.Append(namePart);
        // Pad to full width to overwrite any previous content
        var visibleLen = indexStr.Length + namePart.Length;
        if (visibleLen < maxWidth)
            sb.Append(' ', maxWidth - visibleLen);
        sb.Append("\x1b[0m"); // Reset
        return sb.ToString();
    }

    /// <summary>
    ///     Format the slideshow status line with filename, index, and progress bar.
    ///     Width is constrained to maxWidth (matching the rendered content width).
    ///     Format: "filename.ext [1/10] ████░░░░ 2s/3s"
    /// </summary>
    private static string FormatSlideshowStatus(
        string fileName, int current, int total, bool paused,
        int elapsedMs, int totalDelayMs, int maxWidth)
    {
        // Build the visible text first, then wrap with ANSI codes
        var indexStr = $"[{current}/{total}]";

        // Right side: index + optional progress
        string rightPart;
        if (paused)
        {
            rightPart = $"{indexStr} ⏸";
        }
        else if (totalDelayMs > 0)
        {
            var totalSec = totalDelayMs / 1000.0;
            var elapsedSec = elapsedMs / 1000.0;
            var progress = Math.Clamp((double)elapsedMs / totalDelayMs, 0, 1);
            var barWidth = Math.Max(4, Math.Min(10, maxWidth / 5));
            var filled = (int)(progress * barWidth);
            var bar = new string('█', filled) + new string('░', barWidth - filled);
            rightPart = $"{indexStr} {bar} {elapsedSec:F0}s/{totalSec:F0}s";
        }
        else
        {
            rightPart = indexStr;
        }

        // Left side: filename truncated to fit
        var availableForName = maxWidth - rightPart.Length - 1; // 1 for space separator
        string namePart;

        if (availableForName < 4)
        {
            // Not enough room for filename
            namePart = "";
        }
        else if (fileName.Length <= availableForName)
        {
            namePart = fileName;
        }
        else
        {
            // Truncate: keep extension visible
            // Use full path (not just filename) since fileName may include directory
            var ext = Path.GetExtension(fileName);
            var withoutExt = fileName.AsSpan(0, fileName.Length - ext.Length);
            var maxNameLen = availableForName - ext.Length - 1; // 1 for "…"
            if (maxNameLen > 3 && withoutExt.Length > 0)
                namePart = string.Concat(withoutExt.Slice(0, Math.Min(maxNameLen, withoutExt.Length)), "…", ext);
            else
                namePart = string.Concat(fileName.AsSpan(0, Math.Max(1, Math.Min(availableForName - 1, fileName.Length))), "…");
        }

        // Compose: "name  [pad]  right" — pad to exact maxWidth
        var visibleLen = namePart.Length + (namePart.Length > 0 ? 1 : 0) + rightPart.Length;
        var padding = Math.Max(0, maxWidth - visibleLen);

        var sb = new StringBuilder(maxWidth + 16);
        sb.Append("\x1b[2m"); // Dim
        if (namePart.Length > 0)
        {
            sb.Append(namePart);
            sb.Append(' ');
        }

        if (padding > 0) sb.Append(' ', padding);
        sb.Append(rightPart);
        sb.Append("\x1b[0m"); // Reset
        return sb.ToString();
    }

    /// <summary>
    ///     Count newlines in content.
    /// </summary>
    private static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        var count = 1;
        foreach (var c in content)
            if (c == '\n')
                count++;
        return count;
    }

    /// <summary>
    ///     Get the maximum visible width (excluding ANSI codes) across all lines.
    /// </summary>
    private static int GetMaxVisibleWidth(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        var maxWidth = 0;
        var lineStart = 0;

        for (var i = 0; i <= content.Length; i++)
            if (i == content.Length || content[i] == '\n')
            {
                var lineLen = GetVisibleLength(content.AsSpan(lineStart, i - lineStart));
                if (lineLen > maxWidth) maxWidth = lineLen;
                lineStart = i + 1;
            }

        return maxWidth;
    }

    /// <summary>
    ///     Get visible character count excluding ANSI escape sequences.
    /// </summary>
    private static int GetVisibleLength(ReadOnlySpan<char> line)
    {
        var len = 0;
        var inEscape = false;
        foreach (var c in line)
            if (c == '\x1b')
            {
                inEscape = true;
            }
            else if (inEscape)
            {
                if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z') inEscape = false;
            }
            else
            {
                len++;
            }

        return len;
    }

    /// <summary>
    ///     Safely check if a key is available (returns false if not a terminal).
    /// </summary>
    private static bool IsKeyAvailable()
    {
        try
        {
            return Console.KeyAvailable;
        }
        catch (InvalidOperationException)
        {
            // Not a terminal - no keyboard input available
            return false;
        }
    }

    /// <summary>
    ///     Play animation frames without entering/exiting alt screen (we're already in it).
    ///     Uses pre-built atomic frame buffers for flicker-free, cursor-flash-free rendering.
    /// </summary>
    private static async Task PlayFramesInPlaceAsync(
        List<IAnimationFrame> frames,
        int loopCount,
        float speed,
        int contentStartLine,
        CancellationToken ct)
    {
        if (frames.Count == 0) return;

        // Pre-build atomic frame buffers — single Console.Write per frame eliminates cursor flash.
        // Each buffer contains: sync start + cursor hide + position + content + reset + line clearing + sync end
        var frameBuffers = new string[frames.Count];
        var sb = new StringBuilder(frames[0].Content.Length + 256);

        // Count lines per frame to determine max height for clearing
        var maxLineCount = 0;
        var lineCounts = new int[frames.Count];
        for (var f = 0; f < frames.Count; f++)
        {
            var count = 1;
            var content = frames[f].Content;
            for (var i = 0; i < content.Length; i++)
                if (content[i] == '\n') count++;
            lineCounts[f] = count;
            if (count > maxLineCount) maxLineCount = count;
        }

        var positionEscape = $"\x1b[{contentStartLine};1H";
        for (var f = 0; f < frames.Count; f++)
        {
            sb.Clear();
            sb.Append("\x1b[?2026h");   // Sync start
            sb.Append("\x1b[?25l");     // Re-hide cursor (ensures it stays hidden)
            sb.Append(positionEscape);   // Position at content area
            sb.Append(frames[f].Content);
            sb.Append("\x1b[0m");        // Reset colors

            // Clear remaining lines up to max height (handles variable-height frames)
            for (var i = lineCounts[f]; i < maxLineCount; i++)
            {
                sb.Append('\n');
                sb.Append("\x1b[2K");
            }

            sb.Append("\x1b[?2026l");   // Sync end
            frameBuffers[f] = sb.ToString();
        }

        try
        {
            var loops = 0;
            while (!ct.IsCancellationRequested && (loopCount == 0 || loops < loopCount))
            {
                for (var fi = 0; fi < frames.Count; fi++)
                {
                    if (ct.IsCancellationRequested) break;

                    // Write entire frame atomically — single Console.Write eliminates cursor flash
                    Console.Write(frameBuffers[fi]);
                    Console.Out.Flush();

                    var delayMs = Math.Max(1, (int)(frames[fi].DelayMs / speed));
                    await Task.Delay(delayMs, ct);
                }

                loops++;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }
}