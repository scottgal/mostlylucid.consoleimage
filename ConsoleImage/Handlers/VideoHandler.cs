// Video file handling for consoleimage CLI

using ConsoleImage.Cli.Utilities;
using ConsoleImage.Core;
using ConsoleImage.Core.Subtitles;
using ConsoleImage.Video.Core;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Cli.Handlers;

/// <summary>
///     Handles video file processing with FFmpeg.
/// </summary>
public static class VideoHandler
{
    /// <summary>
    ///     Handle video file playback, info, and output.
    /// </summary>
    /// <param name="inputPath">Path to video file or URL (FFmpeg can stream from URLs)</param>
    /// <param name="inputInfo">FileInfo for metadata (can be dummy for URLs)</param>
    /// <param name="opts">Handler options</param>
    /// <param name="ct">Cancellation token</param>
    public static async Task<int> HandleAsync(
        string inputPath,
        FileInfo inputInfo,
        VideoHandlerOptions opts,
        CancellationToken ct)
    {
        // Resolve FFmpeg path
        string? ffmpegExe = null;
        string? ffprobePath = null;
        if (!string.IsNullOrEmpty(opts.FfmpegPath))
        {
            if (Directory.Exists(opts.FfmpegPath))
            {
                var exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
                var probeName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
                ffmpegExe = Path.Combine(opts.FfmpegPath, exeName);
                ffprobePath = Path.Combine(opts.FfmpegPath, probeName);
            }
            else if (File.Exists(opts.FfmpegPath))
            {
                ffmpegExe = opts.FfmpegPath;
                var dir = Path.GetDirectoryName(opts.FfmpegPath);
                if (dir != null)
                {
                    var probeName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
                    ffprobePath = Path.Combine(dir, probeName);
                }
            }
        }

        // Check for FFmpeg with progress reporting
        using var ffmpeg = new FFmpegService(
            ffprobePath,
            ffmpegExe,
            !opts.NoHwAccel);

        // Check FFmpeg availability with interactive prompt
        if (!FFmpegProvider.IsAvailable(opts.FfmpegPath))
        {
            var (needsDownload, statusMsg, downloadUrl) = FFmpegProvider.GetDownloadStatus();

            if (needsDownload)
            {
                Console.WriteLine("FFmpeg not found on system.");
                Console.WriteLine($"Cache location: {FFmpegProvider.CacheDirectory}");
                Console.WriteLine();

                var shouldDownload = opts.AutoConfirmDownload;

                if (!shouldDownload && !opts.NoAutoDownload)
                {
                    Console.Write("Would you like to download FFmpeg automatically? (~100MB) [Y/n]: ");
                    var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                    shouldDownload = string.IsNullOrEmpty(response) || response == "y" || response == "yes";
                }
                else if (opts.NoAutoDownload && !opts.AutoConfirmDownload)
                {
                    Console.WriteLine("FFmpeg is required for video playback.");
                    Console.WriteLine("Install manually or run with -y to auto-download.");
                    Console.WriteLine();
                    Console.WriteLine("Installation options:");
                    Console.WriteLine("  Windows: winget install ffmpeg");
                    Console.WriteLine("  macOS:   brew install ffmpeg");
                    Console.WriteLine("  Linux:   apt install ffmpeg  (or equivalent)");
                    return 1;
                }

                if (!shouldDownload)
                {
                    Console.WriteLine("FFmpeg is required for video playback. Exiting.");
                    return 1;
                }

                Console.WriteLine("Downloading FFmpeg...");
            }
        }

        var progress = new Progress<(string Status, double Progress)>(p =>
        {
            Console.Write($"\r{p.Status,-50} {p.Progress:P0}".PadRight(60));
            if (p.Progress >= 1.0) Console.WriteLine();
        });

        await ffmpeg.InitializeAsync(progress, ct);

        // Show video info if requested
        if (opts.ShowInfo)
            return await ShowVideoInfo(ffmpeg, inputPath, ct);

        // Calculate end time from duration if specified
        var end = opts.End;
        if (opts.Duration.HasValue && opts.Start.HasValue)
            end = opts.Start.Value + opts.Duration.Value;
        else if (opts.Duration.HasValue)
            end = opts.Duration.Value;

        // Raw mode requires output file
        if (opts.RawMode && opts.OutputGif == null)
        {
            Console.Error.WriteLine("Error: --raw mode requires --output to specify a GIF file.");
            Console.Error.WriteLine("Example: consolevideo video.mp4 --raw -o keyframes.gif --gif-frames 4");
            return 1;
        }

        // GIF output mode
        if (opts.OutputGif != null)
            return await HandleGifOutput(ffmpeg, inputPath, inputInfo, opts, end, ct);

        // JSON/CIDZ output mode
        if (opts.OutputAsJson && !string.IsNullOrEmpty(opts.JsonOutputPath))
            return await HandleJsonOutput(ffmpeg, inputPath, opts, end, ct);

        // Playback mode
        return await HandlePlayback(inputPath, opts, end, ct);
    }

    private static async Task<int> ShowVideoInfo(FFmpegService ffmpeg, string inputPath, CancellationToken ct)
    {
        var info = await ffmpeg.GetVideoInfoAsync(inputPath, ct);
        if (info == null)
        {
            Console.Error.WriteLine("Error: Could not read video info. Is FFmpeg installed?");
            Console.Error.WriteLine("Use --ffmpeg-path to specify the FFmpeg installation directory.");
            return 1;
        }

        Console.WriteLine($"File: {Path.GetFileName(inputPath)}");
        Console.WriteLine($"Duration: {TimeSpan.FromSeconds(info.Duration):hh\\:mm\\:ss\\.fff}");
        Console.WriteLine($"Resolution: {info.Width}x{info.Height}");
        Console.WriteLine($"Codec: {info.VideoCodec}");
        Console.WriteLine($"Frame Rate: {info.FrameRate:F2} fps");
        Console.WriteLine($"Total Frames: ~{info.TotalFrames}");
        Console.WriteLine($"Bitrate: {info.BitRate / 1000} kbps");
        Console.WriteLine(
            $"Hardware Accel: {(string.IsNullOrEmpty(ffmpeg.HardwareAccelerationType) ? "none" : ffmpeg.HardwareAccelerationType)}");
        return 0;
    }

    private static async Task<int> HandleGifOutput(
        FFmpegService ffmpeg, string inputPath, FileInfo inputInfo, VideoHandlerOptions opts, double? end,
        CancellationToken ct)
    {
        var videoInfo = await ffmpeg.GetVideoInfoAsync(inputPath, ct);
        if (videoInfo == null)
        {
            Console.Error.WriteLine("Error: Could not read video info.");
            return 1;
        }

        // RAW MODE - extract actual video frames to various formats
        if (opts.RawMode)
            return await HandleRawOutput(ffmpeg, inputPath, videoInfo, opts, end, ct);

        // Rendered ASCII/Blocks/Braille to GIF
        return await HandleRenderedGifOutput(ffmpeg, inputPath, inputInfo, videoInfo, opts, end, ct);
    }

    private static async Task<int> HandleRawOutput(
        FFmpegService ffmpeg, string inputPath, VideoInfo videoInfo,
        VideoHandlerOptions opts, double? end, CancellationToken ct)
    {
        var outputPath = opts.OutputGif!.FullName;
        var ext = Path.GetExtension(outputPath).ToLowerInvariant();

        // Route to appropriate handler based on output format
        return ext switch
        {
            ".gif" => await HandleRawGifOutput(ffmpeg, inputPath, videoInfo, opts, end, ct),
            ".webp" => await HandleRawWebpOutput(ffmpeg, inputPath, videoInfo, opts, end, ct),
            ".png" or ".jpg" or ".jpeg" or ".bmp" => await HandleRawImageOutput(ffmpeg, inputPath, videoInfo, opts, end,
                ct),
            ".mp4" or ".webm" or ".mkv" or ".avi" or ".mov" => await HandleRawVideoOutput(ffmpeg, inputPath, opts, end,
                ct),
            _ => await HandleRawGifOutput(ffmpeg, inputPath, videoInfo, opts, end, ct) // Default to GIF
        };
    }

    private static async Task<int> HandleRawGifOutput(
        FFmpegService ffmpeg, string inputPath, VideoInfo videoInfo,
        VideoHandlerOptions opts, double? end, CancellationToken ct)
    {
        // Raw mode uses original video dimensions unless explicitly overridden
        var targetWidth = opts.RawWidth ?? opts.Width ?? videoInfo.Width;
        var targetHeight = opts.RawHeight ??
                           opts.Height ?? (int)(targetWidth * videoInfo.Height / (double)videoInfo.Width);

        // Only limit frames when --gif-frames is explicitly specified
        // Otherwise extract all frames for the specified duration
        var maxFrames = opts.GifFrames;
        var maxLength = opts.GifLength ?? opts.Duration ?? 10.0;
        var rawStartTime = opts.Start ?? 0;
        var rawEndTime = end ?? rawStartTime + maxLength;

        // Smart keyframe mode - use scene detection
        if (opts.SmartKeyframes)
            return await HandleSmartKeyframeExtraction(ffmpeg, inputPath, videoInfo, opts,
                targetWidth, targetHeight, maxFrames, rawStartTime, rawEndTime, ct);

        // Regular raw mode - uniform frame extraction using FFmpeg streaming
        var uniformTargetFps = opts.Fps ?? Math.Min(videoInfo.FrameRate, 10.0);
        var uniformFpsInt = Math.Max(1, (int)uniformTargetFps);

        Console.WriteLine("Extracting video frames to GIF (streaming mode)...");
        Console.WriteLine($"  Output: {opts.OutputGif!.FullName}");
        Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
        Console.WriteLine($"  Target: {targetWidth}x{targetHeight} @ {uniformTargetFps:F1} fps, step {opts.FrameStep}");
        Console.WriteLine($"  Time range: {rawStartTime:F1}s - {rawEndTime:F1}s");
        if (opts.Subtitles != null)
        {
            Console.WriteLine($"  Subtitles: {opts.Subtitles.Entries.Count} entries");
            if (opts.Subtitles.Entries.Count > 0)
            {
                var first = opts.Subtitles.Entries.First();
                var last = opts.Subtitles.Entries.Last();
                Console.WriteLine(
                    $"  Subtitle range: {first.StartTime.TotalSeconds:F1}s - {last.EndTime.TotalSeconds:F1}s");
            }
        }

        Console.WriteLine("  Memory: streaming (1 frame at a time)");

        await using var streamingGif = new FFmpegGifWriter(
            opts.OutputGif.FullName,
            targetWidth,
            targetHeight,
            uniformFpsInt,
            opts.Loop != 1 ? opts.Loop : 0,
            opts.GifColors,
            maxLength,
            maxFrames);

        await streamingGif.StartAsync(null, ct);

        var uniformFrameCount = 0;
        var frameIntervalSec = 1.0 / uniformTargetFps;

        await foreach (var frameImage in ffmpeg.StreamFramesAsync(
                           inputPath, targetWidth, targetHeight,
                           rawStartTime, rawEndTime, opts.FrameStepValue, uniformTargetFps, videoInfo.VideoCodec, ct))
        {
            if (streamingGif.ShouldStop)
            {
                frameImage.Dispose();
                break;
            }

            // Burn subtitle onto frame if subtitles are available
            {
                // Use absolute time (matches offset subtitle times)
                var currentTime = rawStartTime + uniformFrameCount * frameIntervalSec;

                // Use fuzzy lookup - finds subtitle active within ±tolerance of current time
                SubtitleEntry? entry = null;
                if (opts.Subtitles != null)
                    entry = opts.Subtitles.GetActiveAtWithTolerance(currentTime);
                else if (opts.LiveSubtitleProvider != null)
                    entry = opts.LiveSubtitleProvider.Track.GetActiveAtWithTolerance(
                        currentTime);

                if (entry != null) BurnSubtitleOntoImage(frameImage, entry.Text);
            }

            await streamingGif.AddFrameAsync(frameImage, ct);
            frameImage.Dispose();
            uniformFrameCount++;

            Console.Write($"\rStreaming frame {uniformFrameCount}...");
        }

        Console.WriteLine($" Done! ({uniformFrameCount} frames)");
        Console.Write("Finalizing GIF...");
        await streamingGif.FinishAsync(ct);
        Console.WriteLine($" Saved to {opts.OutputGif.FullName}");

        return 0;
    }

    private static async Task<int> HandleRawWebpOutput(
        FFmpegService ffmpeg, string inputPath, VideoInfo videoInfo,
        VideoHandlerOptions opts, double? end, CancellationToken ct)
    {
        // Raw mode uses original video dimensions unless explicitly overridden
        var targetWidth = opts.RawWidth ?? opts.Width ?? videoInfo.Width;
        var targetHeight = opts.RawHeight ??
                           opts.Height ?? (int)(targetWidth * videoInfo.Height / (double)videoInfo.Width);
        var maxFrames = opts.GifFrames;
        var maxLength = opts.GifLength ?? opts.Duration ?? 10.0;
        var rawStartTime = opts.Start ?? 0;
        var rawEndTime = end ?? rawStartTime + maxLength;
        var targetFps = opts.Fps ?? Math.Min(videoInfo.FrameRate, 15.0);

        Console.WriteLine("Extracting video frames to animated WebP...");
        Console.WriteLine($"  Output: {opts.OutputGif!.FullName}");
        Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
        Console.WriteLine($"  Target: {targetWidth}x{targetHeight}, quality {opts.Quality}");

        using var webpImage = new Image<Rgba32>(targetWidth, targetHeight);
        var webpMeta = webpImage.Metadata.GetWebpMetadata();
        webpMeta.RepeatCount = (ushort)(opts.Loop != 1 ? opts.Loop : 0);

        var frameCount = 0;

        await foreach (var frameImage in ffmpeg.StreamFramesAsync(
                           inputPath, targetWidth, targetHeight,
                           rawStartTime, rawEndTime, opts.FrameStepValue, targetFps, videoInfo.VideoCodec, ct))
        {
            // Stop if we've hit the frame limit (if specified)
            if (maxFrames.HasValue && frameCount >= maxFrames.Value)
            {
                frameImage.Dispose();
                break;
            }

            webpImage.Frames.AddFrame(frameImage.Frames.RootFrame);
            frameImage.Dispose();
            frameCount++;
            Console.Write($"\rExtracting frame {frameCount}...");
        }

        if (webpImage.Frames.Count > 1)
            webpImage.Frames.RemoveFrame(0);

        Console.WriteLine($" Done! ({frameCount} frames)");
        Console.Write("Encoding WebP...");
        var encoder = new WebpEncoder { Quality = opts.Quality, FileFormat = WebpFileFormatType.Lossy };
        await webpImage.SaveAsWebpAsync(opts.OutputGif.FullName, encoder, ct);
        Console.WriteLine($" Saved to {opts.OutputGif.FullName}");

        return 0;
    }

    private static async Task<int> HandleRawImageOutput(
        FFmpegService ffmpeg, string inputPath, VideoInfo videoInfo,
        VideoHandlerOptions opts, double? end, CancellationToken ct)
    {
        // Raw mode uses original video dimensions unless explicitly overridden
        var targetWidth = opts.RawWidth ?? opts.Width ?? videoInfo.Width;
        var targetHeight = opts.RawHeight ??
                           opts.Height ?? (int)(targetWidth * videoInfo.Height / (double)videoInfo.Width);
        var maxFrames = opts.GifFrames ?? 1; // Still default to 1 for single image extraction
        var rawStartTime = opts.Start ?? 0;
        var rawEndTime = end ?? rawStartTime + (opts.GifLength ?? opts.Duration ?? 10.0);
        var outputPath = opts.OutputGif!.FullName;
        var ext = Path.GetExtension(outputPath).ToLowerInvariant();

        // Single frame or sequence?
        if (maxFrames == 1 || opts.SmartKeyframes)
        {
            // Smart keyframes - extract scene changes
            if (opts.SmartKeyframes)
            {
                Console.WriteLine("Extracting keyframes as image sequence...");
                var sceneTimestamps = await ffmpeg.DetectSceneChangesAsync(
                    inputPath, opts.SceneThreshold, rawStartTime, rawEndTime, ct);

                var times = new List<double> { rawStartTime };
                times.AddRange(sceneTimestamps.Take(maxFrames - 1));

                var baseName = Path.GetFileNameWithoutExtension(outputPath);
                var dir = Path.GetDirectoryName(outputPath) ?? ".";

                for (var i = 0; i < times.Count; i++)
                {
                    var frame = await ffmpeg.ExtractFrameAsync(inputPath, times[i], targetWidth, targetHeight, ct);
                    if (frame != null)
                    {
                        var framePath = Path.Combine(dir, $"{baseName}_{i + 1:D3}{ext}");
                        await SaveImageAsync(frame, framePath, ext, opts.Quality, ct);
                        frame.Dispose();
                        Console.WriteLine($"Saved {framePath}");
                    }
                }

                return 0;
            }

            // Single frame
            Console.WriteLine($"Extracting single frame at {rawStartTime:F1}s...");
            var singleFrame = await ffmpeg.ExtractFrameAsync(inputPath, rawStartTime, targetWidth, targetHeight, ct);
            if (singleFrame != null)
            {
                await SaveImageAsync(singleFrame, outputPath, ext, opts.Quality, ct);
                singleFrame.Dispose();
                Console.WriteLine($"Saved to {outputPath}");
            }

            return 0;
        }

        // Multiple frames - save as sequence
        Console.WriteLine($"Extracting {maxFrames} frames as image sequence...");
        var basePath = Path.GetFileNameWithoutExtension(outputPath);
        var directory = Path.GetDirectoryName(outputPath) ?? ".";
        var targetFps = opts.Fps ?? Math.Min(videoInfo.FrameRate, 10.0);

        var frameCount = 0;
        await foreach (var frameImage in ffmpeg.StreamFramesAsync(
                           inputPath, targetWidth, targetHeight,
                           rawStartTime, rawEndTime, opts.FrameStepValue, targetFps, videoInfo.VideoCodec, ct))
        {
            if (frameCount >= maxFrames)
            {
                frameImage.Dispose();
                break;
            }

            var framePath = Path.Combine(directory, $"{basePath}_{frameCount + 1:D3}{ext}");
            await SaveImageAsync(frameImage, framePath, ext, opts.Quality, ct);
            frameImage.Dispose();
            frameCount++;
            Console.Write($"\rExtracting frame {frameCount}/{maxFrames}...");
        }

        Console.WriteLine($" Done! Saved {frameCount} frames to {directory}");
        return 0;
    }

    private static async Task SaveImageAsync(Image<Rgba32> image, string path, string ext, int quality,
        CancellationToken ct)
    {
        switch (ext)
        {
            case ".jpg" or ".jpeg":
                await image.SaveAsJpegAsync(path, new JpegEncoder { Quality = quality }, ct);
                break;
            case ".png":
                await image.SaveAsPngAsync(path,
                    new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression }, ct);
                break;
            case ".bmp":
                await image.SaveAsBmpAsync(path, ct);
                break;
            default:
                await image.SaveAsPngAsync(path, ct);
                break;
        }
    }

    private static async Task<int> HandleRawVideoOutput(
        FFmpegService ffmpeg, string inputPath, VideoHandlerOptions opts, double? end, CancellationToken ct)
    {
        var outputPath = opts.OutputGif!.FullName;
        // Get video info to use original dimensions as default
        var videoInfo = await ffmpeg.GetVideoInfoAsync(inputPath, ct);
        var originalWidth = videoInfo?.Width ?? 1920;

        // Raw mode uses original width unless explicitly overridden
        var targetWidth = opts.RawWidth ?? opts.Width ?? originalWidth;
        var rawStartTime = opts.Start ?? 0;
        var duration = opts.Duration ?? (end.HasValue ? end.Value - rawStartTime : 10.0);

        Console.WriteLine("Re-encoding video with FFmpeg...");
        Console.WriteLine($"  Output: {outputPath}");
        Console.WriteLine($"  Source: {videoInfo?.Width ?? 0}x{videoInfo?.Height ?? 0}");
        Console.WriteLine($"  Target width: {targetWidth}, Duration: {duration:F1}s");

        // Use FFmpeg directly for video output
        var success = await ffmpeg.ExtractClipAsync(
            inputPath, outputPath, rawStartTime, duration, targetWidth, opts.Quality, ct);

        if (success)
            Console.WriteLine($"Saved to {outputPath}");
        else
            Console.Error.WriteLine("Failed to create video output");

        return success ? 0 : 1;
    }

    private static async Task<int> HandleSmartKeyframeExtraction(
        FFmpegService ffmpeg, string inputPath, VideoInfo videoInfo, VideoHandlerOptions opts,
        int targetWidth, int targetHeight, int? maxFramesArg, double rawStartTime, double rawEndTime,
        CancellationToken ct)
    {
        // Smart keyframe extraction defaults to 10 keyframes if not specified
        var maxFrames = maxFramesArg ?? 10;

        Console.WriteLine("Smart keyframe extraction (scene detection)...");
        Console.WriteLine($"  Output: {opts.OutputGif!.FullName}");
        Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
        Console.WriteLine($"  Target: {targetWidth}x{targetHeight}, max {maxFrames} keyframes");
        Console.WriteLine();

        Console.Write("Detecting scene changes...");
        var sceneTimestamps = await ffmpeg.DetectSceneChangesAsync(
            inputPath,
            opts.SceneThreshold,
            rawStartTime,
            rawEndTime > rawStartTime ? rawEndTime : null,
            ct);
        Console.WriteLine($" found {sceneTimestamps.Count} scene changes");

        List<double> keyframeTimes;
        if (sceneTimestamps.Count == 0)
        {
            Console.WriteLine("No scene changes detected, using uniform sampling...");
            keyframeTimes = new List<double>();
            var interval = (rawEndTime - rawStartTime) / (maxFrames - 1);
            for (var i = 0; i < maxFrames; i++)
                keyframeTimes.Add(rawStartTime + i * interval);
        }
        else
        {
            keyframeTimes = new List<double> { rawStartTime };
            keyframeTimes.AddRange(sceneTimestamps);

            if (keyframeTimes.Count > 0 && rawEndTime - keyframeTimes.Last() > 1.0)
                keyframeTimes.Add(rawEndTime - 0.5);

            if (keyframeTimes.Count > maxFrames)
            {
                var first = keyframeTimes.First();
                var last = keyframeTimes.Last();
                var middle = keyframeTimes.Skip(1).Take(keyframeTimes.Count - 2)
                    .OrderBy(t => t)
                    .Take(maxFrames - 2)
                    .ToList();
                keyframeTimes = new List<double> { first };
                keyframeTimes.AddRange(middle);
                keyframeTimes.Add(last);
                keyframeTimes = keyframeTimes.Distinct().OrderBy(t => t).ToList();
            }
        }

        Console.WriteLine(
            $"Extracting {keyframeTimes.Count} keyframes at: {string.Join(", ", keyframeTimes.Select(t => $"{t:F1}s"))}");

        var targetFps = opts.Fps ?? 2.0;
        var rawDelayMs = (int)(1000.0 / targetFps);

        using var rawGif = new Image<Rgba32>(targetWidth, targetHeight);
        var gifMetaData = rawGif.Metadata.GetGifMetadata();
        gifMetaData.RepeatCount = (ushort)(opts.Loop != 1 ? opts.Loop : 0);

        var frameCount = 0;
        const double toleranceSeconds = 0.3; // Timing tolerance for subtitle lookup

        foreach (var timestamp in keyframeTimes)
        {
            Console.Write($"\rExtracting frame at {timestamp:F1}s...");
            var frameImage = await ffmpeg.ExtractFrameAsync(
                inputPath, timestamp, targetWidth, targetHeight, ct);

            if (frameImage != null)
            {
                // Burn subtitle onto frame if subtitles are available
                {
                    // Use fuzzy lookup with tolerance for timing imprecision
                    SubtitleEntry? entry = null;
                    if (opts.Subtitles != null)
                        entry = opts.Subtitles.GetActiveAtWithTolerance(timestamp, toleranceSeconds);
                    else if (opts.LiveSubtitleProvider != null)
                        entry = opts.LiveSubtitleProvider.Track.GetActiveAtWithTolerance(
                            timestamp, toleranceSeconds);

                    if (entry != null) BurnSubtitleOntoImage(frameImage, entry.Text);
                }

                var gifFrameMetadata = frameImage.Frames.RootFrame.Metadata.GetGifMetadata();
                gifFrameMetadata.FrameDelay = rawDelayMs / 10;
                rawGif.Frames.AddFrame(frameImage.Frames.RootFrame);
                frameImage.Dispose();
                frameCount++;
            }
        }

        if (rawGif.Frames.Count > 1)
            rawGif.Frames.RemoveFrame(0);

        Console.WriteLine($" Done! ({frameCount} frames)");

        Console.Write("Saving GIF...");
        var encoder = new GifEncoder { ColorTableMode = GifColorTableMode.Local };
        await rawGif.SaveAsGifAsync(opts.OutputGif.FullName, encoder, ct);
        Console.WriteLine($" Saved to {opts.OutputGif.FullName}");

        return 0;
    }

    private static async Task<int> HandleRenderedGifOutput(
        FFmpegService ffmpeg, string inputPath, FileInfo inputInfo, VideoInfo videoInfo,
        VideoHandlerOptions opts, double? end, CancellationToken ct)
    {
        var renderMode = RenderHelpers.GetRenderMode(opts.UseBraille, opts.UseBlocks, false);

        var effectiveAspect = RenderHelpers.GetEffectiveAspectRatio(
            opts.CharAspect, opts.SavedCalibration, renderMode);

        var gifTargetFps = opts.Fps ?? Math.Min(videoInfo.FrameRate, 15.0);
        var frameDelayMs = (int)(1000.0 / gifTargetFps / opts.Speed);

        Console.WriteLine("Rendering video to animated GIF...");
        Console.WriteLine($"  Output: {opts.OutputGif!.FullName}");
        Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
        Console.WriteLine($"  Target: {gifTargetFps:F1} fps, frame step {opts.FrameStep}");

        var gifLoopCount = opts.Loop != 1 ? opts.Loop : 0;

        var gifOptions = new GifWriterOptions
        {
            FontSize = opts.GifFontSize,
            Scale = opts.GifScale,
            MaxColors = Math.Clamp(opts.GifColors, 16, 256),
            LoopCount = gifLoopCount,
            MaxLengthSeconds = opts.Duration ?? end - opts.Start
        };
        using var gifWriter = new GifWriter(gifOptions);

        var tempOptions = new RenderOptions
        {
            Width = opts.Width,
            Height = opts.Height,
            MaxWidth = opts.MaxWidth,
            MaxHeight = Math.Max(80, opts.MaxHeight),
            CharacterAspectRatio = effectiveAspect
        };

        var pixelsPerCharWidth = opts.UseBraille ? 2 : 1;
        var pixelsPerCharHeight = opts.UseBraille ? 4 : opts.UseBlocks ? 2 : 1;
        var (pixelWidth, pixelHeight) = tempOptions.CalculateVisualDimensions(
            videoInfo.Width, videoInfo.Height, pixelsPerCharWidth, pixelsPerCharHeight);

        var charWidth = pixelWidth / pixelsPerCharWidth;
        var charHeight = pixelHeight / pixelsPerCharHeight;

        Console.WriteLine($"  Output: {charWidth}x{charHeight} chars ({pixelWidth}x{pixelHeight} pixels)");
        Console.WriteLine();

        var gifRenderOptions = CreateRenderOptions(opts, charWidth, charHeight, effectiveAspect);

        var effectiveStart = opts.Start ?? 0;
        var effectiveEnd = end ?? videoInfo.Duration;
        var effectiveDuration = effectiveEnd - effectiveStart;
        var estimatedTotalFrames = (int)(effectiveDuration * gifTargetFps / opts.FrameStepValue);

        var statusRenderer = opts.ShowStatus ? new StatusLine(charWidth, !opts.NoColor) : null;
        var renderModeName = RenderHelpers.GetRenderModeName(renderMode);

        // Create subtitle renderer if subtitles are available
        SubtitleRenderer? subtitleRenderer = null;
        if (opts.Subtitles != null) subtitleRenderer = new SubtitleRenderer(charWidth, 2, !opts.NoColor);

        // For pixel modes (Braille/Blocks), we now support burning subtitles/status into the image
        // Keep statusRenderer for building status text even for pixel modes
        var pixelModeNeedsOverlays = (opts.UseBraille || opts.UseBlocks) && (opts.ShowStatus || opts.Subtitles != null);

        var renderedCount = 0;
        var gifStartTime = opts.Start ?? 0;

        await foreach (var frameImage in ffmpeg.StreamFramesAsync(
                           inputPath,
                           pixelWidth,
                           pixelHeight,
                           gifStartTime > 0 ? gifStartTime : null,
                           end,
                           opts.FrameStepValue,
                           gifTargetFps,
                           videoInfo.VideoCodec,
                           ct))
        {
            // Calculate current time for subtitle lookup
            var currentSeconds = effectiveStart + renderedCount * opts.FrameStepValue / gifTargetFps;
            var currentTime = TimeSpan.FromSeconds(currentSeconds);

            // Get subtitle text for current frame (from static track or live provider)
            string? subtitleText = null;
            if (subtitleRenderer != null)
            {
                SubtitleEntry? entry = null;
                if (opts.Subtitles != null)
                    entry = opts.Subtitles.GetActiveAt(currentTime);
                else if (opts.LiveSubtitleProvider != null)
                    entry = opts.LiveSubtitleProvider.Track.GetActiveAtWithTolerance(
                        currentTime.TotalSeconds);
                subtitleText = subtitleRenderer.GetPlainText(entry);
            }

            // Build status info for current frame
            string? statusText = null;
            if (opts.ShowStatus && statusRenderer != null)
            {
                var statusInfo = new StatusLine.StatusInfo
                {
                    FileName = inputInfo.Name,
                    SourceWidth = videoInfo.Width,
                    SourceHeight = videoInfo.Height,
                    OutputWidth = charWidth,
                    OutputHeight = charHeight,
                    RenderMode = renderModeName,
                    CurrentFrame = renderedCount + 1,
                    TotalFrames = estimatedTotalFrames,
                    CurrentTime = currentTime,
                    TotalDuration = TimeSpan.FromSeconds(effectiveDuration),
                    Fps = gifTargetFps
                };
                statusText = statusRenderer.Render(statusInfo);
            }

            if (opts.UseBraille)
            {
                using var renderer = new BrailleRenderer(gifRenderOptions);
                var content = renderer.RenderImage(frameImage);
                var frame = new BrailleFrame(content, frameDelayMs);

                if (pixelModeNeedsOverlays)
                {
                    // Render braille to image and add overlays
                    var brailleImage = GifWriter.RenderBrailleFrameToImage(frame, gifOptions.Scale);
                    gifWriter.AddImageFrameWithOverlays(brailleImage, subtitleText, statusText, frameDelayMs);
                    brailleImage.Dispose();
                }
                else
                {
                    gifWriter.AddBrailleFrame(frame, frameDelayMs);
                }
            }
            else if (opts.UseBlocks)
            {
                using var renderer = new ColorBlockRenderer(gifRenderOptions);
                var content = renderer.RenderImage(frameImage);
                var frame = new ColorBlockFrame(content, frameDelayMs);

                if (pixelModeNeedsOverlays)
                {
                    // Render blocks to image and add overlays
                    var blocksImage = GifWriter.RenderColorBlockFrameToImage(frame, gifOptions.Scale);
                    gifWriter.AddImageFrameWithOverlays(blocksImage, subtitleText, statusText, frameDelayMs);
                    blocksImage.Dispose();
                }
                else
                {
                    gifWriter.AddColorBlockFrame(frame, frameDelayMs);
                }
            }
            else
            {
                using var renderer = new AsciiRenderer(gifRenderOptions);
                var asciiFrame = renderer.RenderImage(frameImage);
                var content = asciiFrame.ToAnsiString();

                // For ASCII mode, append subtitle and status as text
                if (!string.IsNullOrEmpty(subtitleText)) content += "\n" + subtitleText;

                if (!string.IsNullOrEmpty(statusText)) content += "\n" + statusText;

                gifWriter.AddFrame(content, frameDelayMs);
            }

            frameImage.Dispose();
            renderedCount++;
            Console.Write($"\rRendering frames to GIF: {renderedCount}/{estimatedTotalFrames} processed");
        }

        Console.WriteLine($"\rRendering frames to GIF: {renderedCount} frames completed");
        Console.Write("Encoding and saving GIF file...");
        await gifWriter.SaveAsync(opts.OutputGif.FullName, ct);
        Console.WriteLine($" Saved to {opts.OutputGif.FullName}");
        return 0;
    }

    private static async Task<int> HandleJsonOutput(
        FFmpegService ffmpeg, string inputPath, VideoHandlerOptions opts, double? end, CancellationToken ct)
    {
        var videoInfo = await ffmpeg.GetVideoInfoAsync(inputPath, ct);
        if (videoInfo == null)
        {
            Console.Error.WriteLine("Error: Could not read video info.");
            return 1;
        }

        var renderMode = RenderHelpers.GetRenderMode(opts.UseBraille, opts.UseBlocks, false);

        var effectiveAspect = RenderHelpers.GetEffectiveAspectRatio(
            opts.CharAspect, opts.SavedCalibration, renderMode);

        var jsonTargetFps = opts.Fps ?? Math.Min(videoInfo.FrameRate, 30.0);
        var frameDelayMs = (int)(1000.0 / jsonTargetFps / opts.Speed);

        var formatName = opts.OutputAsCompressed ? "compressed document" : "streaming JSON";
        Console.WriteLine($"Rendering video to {formatName}...");
        Console.WriteLine($"  Output: {opts.JsonOutputPath}");
        Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
        Console.WriteLine($"  Target: {jsonTargetFps:F1} fps, frame step {opts.FrameStep}");
        if (!opts.OutputAsCompressed)
            Console.WriteLine("  Press Ctrl+C to stop (document will auto-finalize)");

        var tempOptions = new RenderOptions
        {
            Width = opts.Width,
            Height = opts.Height,
            MaxWidth = opts.MaxWidth,
            MaxHeight = opts.MaxHeight,
            CharacterAspectRatio = effectiveAspect
        };

        var pixelsPerCharWidth = opts.UseBraille ? 2 : 1;
        var pixelsPerCharHeight = opts.UseBraille ? 4 : opts.UseBlocks ? 2 : 1;
        var (pixelWidth, pixelHeight) = tempOptions.CalculateVisualDimensions(
            videoInfo.Width, videoInfo.Height, pixelsPerCharWidth, pixelsPerCharHeight);

        var charWidth = pixelWidth / pixelsPerCharWidth;
        var charHeight = pixelHeight / pixelsPerCharHeight;

        Console.WriteLine($"  Output: {charWidth}x{charHeight} chars");
        Console.WriteLine();

        var jsonRenderOptions = CreateRenderOptions(opts, charWidth, charHeight, effectiveAspect);
        var renderModeName = RenderHelpers.GetRenderModeName(renderMode);

        if (opts.OutputAsCompressed)
            return await HandleCompressedJsonOutput(ffmpeg, inputPath, jsonRenderOptions, opts, videoInfo,
                pixelWidth, pixelHeight, charWidth, charHeight, frameDelayMs, jsonTargetFps, end, renderModeName, ct);

        return await HandleStreamingJsonOutput(ffmpeg, inputPath, jsonRenderOptions, opts, videoInfo,
            pixelWidth, pixelHeight, charWidth, charHeight, frameDelayMs, jsonTargetFps, end, renderModeName, ct);
    }

    private static async Task<int> HandleCompressedJsonOutput(
        FFmpegService ffmpeg, string inputPath, RenderOptions jsonRenderOptions, VideoHandlerOptions opts,
        VideoInfo videoInfo, int pixelWidth, int pixelHeight, int charWidth, int charHeight,
        int frameDelayMs, double jsonTargetFps, double? end, string renderModeName,
        CancellationToken ct)
    {
        // Stream to temp NDJSON file first to avoid memory buildup for long videos
        var tempPath = Path.Combine(Path.GetTempPath(), $"consoleimage_{Guid.NewGuid():N}.ndjson");

        var renderedCount = 0;
        var jsonStartTime = opts.Start ?? 0;

        // Status renderer only - subtitles stored separately in document metadata
        var statusRenderer = opts.ShowStatus
            ? new StatusLine(charWidth, !opts.NoColor)
            : null;

        // Calculate timing info
        var effectiveStart = opts.Start ?? 0;
        var effectiveEnd = end ?? videoInfo.Duration;
        var effectiveDuration = effectiveEnd - effectiveStart;
        var estimatedTotalFrames = (int)(effectiveDuration * jsonTargetFps / opts.FrameStepValue);

        // Create renderer once - reuse across all frames for efficiency
        var (brailleRenderer, blockRenderer, asciiRenderer) = CreateStreamingRenderers(jsonRenderOptions, opts);

        try
        {
            // Stream frames to temp NDJSON (no memory buildup)
            // NOTE: Subtitles are stored separately in document metadata, NOT embedded in frames
            // This avoids delta compression issues and allows subtitles to be toggled during playback
            await using (var docWriter = new StreamingDocumentWriter(
                             tempPath, renderModeName, jsonRenderOptions, inputPath))
            {
                await docWriter.WriteHeaderAsync(ct);

                await foreach (var frameImage in ffmpeg.StreamFramesAsync(
                                   inputPath,
                                   pixelWidth,
                                   pixelHeight,
                                   jsonStartTime > 0 ? jsonStartTime : null,
                                   end,
                                   opts.FrameStepValue,
                                   jsonTargetFps,
                                   videoInfo.VideoCodec,
                                   ct))
                {
                    // Calculate current time for status line (subtitles handled separately)
                    var currentSeconds = effectiveStart + renderedCount * opts.FrameStepValue / jsonTargetFps;
                    var currentTime = TimeSpan.FromSeconds(currentSeconds);

                    // Render frame content using reusable renderer (much faster for streaming)
                    var content =
                        RenderFrameContentWithRenderer(frameImage, brailleRenderer, blockRenderer, asciiRenderer);
                    // Only add status line if requested - subtitles are stored separately
                    content = AppendOverlays(content, null, statusRenderer, opts,
                        inputPath, videoInfo, charWidth, charHeight, renderModeName,
                        renderedCount + 1, estimatedTotalFrames, currentTime, effectiveDuration, jsonTargetFps);

                    await docWriter.WriteFrameAsync(content, frameDelayMs, charWidth, charHeight, ct);
                    frameImage.Dispose();
                    renderedCount++;
                    Console.Write($"\rRendering frames: {renderedCount} processed");
                }

                await docWriter.FinalizeAsync(!ct.IsCancellationRequested, ct);
            }

            // Stream NDJSON directly to optimized format (avoids loading all frames into memory)
            Console.Write("\rCompressing document...                         ");
            var optimized = await OptimizedDocument.FromNdjsonFileAsync(
                tempPath,
                30,
                opts.Dejitter,
                opts.ColorThreshold ?? 15,
                ct);

            // Record subtitle metadata in document settings
            if (opts.Subtitles != null)
            {
                optimized.Settings.SubtitlesEnabled = true;
                optimized.Settings.SubtitleLanguage = opts.Subtitles.Language;
                optimized.Settings.SubtitleSource = opts.Subtitles.SourceFile != null ? "file" : "auto";
            }

            // Save as CIDZ with bundled subtitles (Brotli compressed)
            await CompressedDocumentArchive.SaveOptimizedAsync(optimized, opts.JsonOutputPath!, opts.Subtitles, ct);
            if (opts.Subtitles != null)
                Console.Error.WriteLine($"Bundled {opts.Subtitles.Count} subtitles in archive");

            Console.WriteLine($"\rSaved {renderedCount} frames to {opts.JsonOutputPath}              ");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\rRendering frames: stopped at frame {renderedCount}");
        }
        finally
        {
            // Dispose renderers (returns ArrayPool buffers)
            brailleRenderer?.Dispose();
            blockRenderer?.Dispose();
            asciiRenderer?.Dispose();

            // Cleanup temp file
            if (File.Exists(tempPath))
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    /* Ignore cleanup errors */
                }
        }

        return 0;
    }

    private static async Task<int> HandleStreamingJsonOutput(
        FFmpegService ffmpeg, string inputPath, RenderOptions jsonRenderOptions, VideoHandlerOptions opts,
        VideoInfo videoInfo, int pixelWidth, int pixelHeight, int charWidth, int charHeight,
        int frameDelayMs, double jsonTargetFps, double? end, string renderModeName,
        CancellationToken ct)
    {
        await using var docWriter = new StreamingDocumentWriter(
            opts.JsonOutputPath!,
            renderModeName,
            jsonRenderOptions,
            inputPath);

        // Record subtitle metadata in header settings (sidecar .vtt saved after finalization)
        if (opts.Subtitles != null)
        {
            var outputDir = Path.GetDirectoryName(opts.JsonOutputPath!) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(opts.JsonOutputPath!);
            var vttFileName = baseName + ".vtt";

            docWriter.SetSubtitleMetadata(
                vttFileName,
                opts.Subtitles.Language,
                opts.Subtitles.SourceFile != null ? "file" : "auto");
        }

        await docWriter.WriteHeaderAsync(ct);

        var streamRenderedCount = 0;
        var streamStartTime = opts.Start ?? 0;

        // Status renderer only - subtitles stored separately, not embedded in frames
        var statusRenderer = opts.ShowStatus
            ? new StatusLine(charWidth, !opts.NoColor)
            : null;

        // Calculate timing info
        var effectiveStart = opts.Start ?? 0;
        var effectiveEnd = end ?? videoInfo.Duration;
        var effectiveDuration = effectiveEnd - effectiveStart;
        var estimatedTotalFrames = (int)(effectiveDuration * jsonTargetFps / opts.FrameStepValue);

        // Create renderer once - reuse across all frames for efficiency
        var (brailleRenderer, blockRenderer, asciiRenderer) = CreateStreamingRenderers(jsonRenderOptions, opts);

        try
        {
            await foreach (var frameImage in ffmpeg.StreamFramesAsync(
                               inputPath,
                               pixelWidth,
                               pixelHeight,
                               streamStartTime > 0 ? streamStartTime : null,
                               end,
                               opts.FrameStepValue,
                               jsonTargetFps,
                               videoInfo.VideoCodec,
                               ct))
            {
                // Calculate current time for status display
                var currentSeconds = effectiveStart + streamRenderedCount * opts.FrameStepValue / jsonTargetFps;
                var currentTime = TimeSpan.FromSeconds(currentSeconds);

                // Render frame content using reusable renderer (much faster for streaming)
                var content = RenderFrameContentWithRenderer(frameImage, brailleRenderer, blockRenderer, asciiRenderer);
                content = AppendOverlays(content, null, statusRenderer, opts,
                    inputPath, videoInfo, charWidth, charHeight, renderModeName,
                    streamRenderedCount + 1, estimatedTotalFrames, currentTime, effectiveDuration, jsonTargetFps);

                await docWriter.WriteFrameAsync(content, frameDelayMs, charWidth, charHeight, ct);
                frameImage.Dispose();
                streamRenderedCount++;
                Console.Write($"\rRendering frames to JSON: {streamRenderedCount} written");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\rRendering frames to JSON: stopped at frame {streamRenderedCount}");
        }
        finally
        {
            // Dispose renderers (returns ArrayPool buffers)
            brailleRenderer?.Dispose();
            blockRenderer?.Dispose();
            asciiRenderer?.Dispose();
        }

        Console.Write("\rFinalizing JSON document...                    ");
        await docWriter.FinalizeAsync(!ct.IsCancellationRequested, ct);

        // Save subtitles as sidecar .vtt file
        if (opts.Subtitles != null)
        {
            var outputDir = Path.GetDirectoryName(opts.JsonOutputPath!) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(opts.JsonOutputPath!);
            var vttFileName = baseName + ".vtt";
            var vttPath = Path.Combine(outputDir, vttFileName);

            await opts.Subtitles.SaveAsVttAsync(vttPath, ct);
            Console.Error.WriteLine($"Saved subtitles: {vttFileName}");
        }

        Console.WriteLine($"\rSaved {streamRenderedCount} frames to {opts.JsonOutputPath}              ");
        return 0;
    }

    private static async Task<int> HandlePlayback(
        string inputPath, VideoHandlerOptions opts, double? end, CancellationToken ct)
    {
        var characterSet = opts.Charset;
        if (string.IsNullOrEmpty(characterSet))
            characterSet = opts.Preset?.ToLowerInvariant() switch
            {
                "simple" => CharacterMap.SimpleCharacterSet,
                "block" => CharacterMap.BlockCharacterSet,
                "classic" => CharacterMap.ClassicCharacterSet,
                _ => CharacterMap.ExtendedCharacterSet
            };

        var renderMode = opts.UseBraille ? VideoRenderMode.Braille
            : opts.UseBlocks ? VideoRenderMode.ColorBlocks
            : VideoRenderMode.Ascii;

        var coreRenderMode = renderMode switch
        {
            VideoRenderMode.Braille => RenderMode.Braille,
            VideoRenderMode.ColorBlocks => RenderMode.ColorBlocks,
            _ => RenderMode.Ascii
        };
        var effectiveAspect = RenderHelpers.GetEffectiveAspectRatio(
            opts.CharAspect, opts.SavedCalibration, coreRenderMode);

        var samplingStrategy = opts.Sampling?.ToLowerInvariant() switch
        {
            "keyframe" or "key" => FrameSamplingStrategy.Keyframe,
            "scene" or "sceneaware" => FrameSamplingStrategy.SceneAware,
            "adaptive" => FrameSamplingStrategy.Adaptive,
            _ => FrameSamplingStrategy.Uniform
        };

        // Parse frame step option: "-f s" for smart, "-f 2" for uniform skip
        var (frameStep, smartMode) = opts.ParseFrameStep();
        var samplingMode = smartMode ? FrameSamplingMode.Smart : FrameSamplingMode.Uniform;

        var options = new VideoRenderOptions
        {
            RenderOptions = new RenderOptions
            {
                Width = opts.Width,
                Height = opts.Height,
                MaxWidth = opts.MaxWidth,
                MaxHeight = opts.MaxHeight,
                CharacterSet = characterSet,
                CharacterAspectRatio = effectiveAspect,
                ContrastPower = opts.Contrast,
                Gamma = opts.Gamma,
                UseColor = !opts.NoColor,
                UseGreyscaleAnsi = opts.UseGreyscaleAnsi,
                ColorCount = opts.ColorCount,
                Invert = !opts.NoInvert,
                UseParallelProcessing = !opts.NoParallel,
                EnableEdgeDetection = opts.EnableEdge,
                BackgroundThreshold = opts.BgThreshold,
                DarkBackgroundThreshold = opts.DarkBgThreshold,
                AutoBackgroundSuppression = opts.AutoBg,
                EnableDithering = !opts.NoDither,
                DisableBrailleDithering = opts.NoDither,
                EnableEdgeDirectionChars = !opts.NoEdgeChars,
                DarkTerminalBrightnessThreshold = opts.DarkCutoff,
                LightTerminalBrightnessThreshold = opts.LightCutoff,
                EnableTemporalStability = opts.Dejitter,
                ColorStabilityThreshold = opts.ColorThreshold ?? 15
            },
            StartTime = opts.Start,
            EndTime = end,
            TargetFps = opts.Fps,
            FrameStep = frameStep,
            SamplingStrategy = samplingStrategy,
            SamplingMode = samplingMode,
            DebugMode = opts.DebugMode,
            SceneThreshold = opts.SceneThreshold,
            BufferAheadFrames = Math.Clamp(opts.Buffer, 2, 10),
            SpeedMultiplier = opts.Speed,
            LoopCount = opts.Loop,
            UseAltScreen = !opts.NoAltScreen,
            UseHardwareAcceleration = !opts.NoHwAccel,
            RenderMode = renderMode,
            ShowStatus = opts.ShowStatus,
            SourceFileName = inputPath,
            Subtitles = opts.Subtitles,
            LiveSubtitleProvider = opts.LiveSubtitleProvider
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            using var player = new VideoAnimationPlayer(inputPath, options);
            await player.PlayAsync(cts.Token);

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static RenderOptions CreateRenderOptions(
        VideoHandlerOptions opts, int charWidth, int charHeight, float effectiveAspect)
    {
        return new RenderOptions
        {
            Width = charWidth,
            Height = charHeight,
            MaxWidth = charWidth,
            MaxHeight = charHeight,
            CharacterAspectRatio = effectiveAspect,
            ContrastPower = opts.Contrast,
            Gamma = opts.Gamma,
            UseColor = !opts.NoColor,
            UseGreyscaleAnsi = opts.UseGreyscaleAnsi,
            ColorCount = opts.ColorCount,
            Invert = !opts.NoInvert,
            UseParallelProcessing = !opts.NoParallel,
            EnableEdgeDetection = opts.EnableEdge,
            BackgroundThreshold = opts.BgThreshold,
            DarkBackgroundThreshold = opts.DarkBgThreshold,
            AutoBackgroundSuppression = opts.AutoBg,
            EnableDithering = !opts.NoDither,
            DisableBrailleDithering = opts.NoDither,
            EnableEdgeDirectionChars = !opts.NoEdgeChars,
            DarkTerminalBrightnessThreshold = opts.DarkCutoff,
            LightTerminalBrightnessThreshold = opts.LightCutoff,
            EnableTemporalStability = opts.Dejitter,
            ColorStabilityThreshold = opts.ColorThreshold ?? 15
        };
    }

    /// <summary>
    ///     Render a frame to string content based on render mode.
    ///     Creates a new renderer per call - use the overload with pre-created renderer for streaming.
    /// </summary>
    private static string RenderFrameContent(
        Image<Rgba32> frameImage, RenderOptions renderOptions, VideoHandlerOptions opts)
    {
        if (opts.UseBraille)
        {
            using var renderer = new BrailleRenderer(renderOptions);
            return renderer.RenderImage(frameImage);
        }

        if (opts.UseBlocks)
        {
            using var renderer = new ColorBlockRenderer(renderOptions);
            return renderer.RenderImage(frameImage);
        }

        using var asciiRenderer = new AsciiRenderer(renderOptions);
        var frame = asciiRenderer.RenderImage(frameImage);
        return frame.ToAnsiString();
    }

    /// <summary>
    ///     Render a frame using pre-created renderers (for streaming - reuses buffers).
    ///     Pass the appropriate renderer based on mode.
    /// </summary>
    private static string RenderFrameContentWithRenderer(
        Image<Rgba32> frameImage,
        BrailleRenderer? brailleRenderer,
        ColorBlockRenderer? blockRenderer,
        AsciiRenderer? asciiRenderer)
    {
        if (brailleRenderer != null)
            return brailleRenderer.RenderImage(frameImage);

        if (blockRenderer != null)
            return blockRenderer.RenderImage(frameImage);

        if (asciiRenderer != null)
        {
            var frame = asciiRenderer.RenderImage(frameImage);
            return frame.ToAnsiString();
        }

        throw new InvalidOperationException("No renderer provided");
    }

    /// <summary>
    ///     Create appropriate renderers for video streaming.
    ///     Returns disposable tuple - caller must dispose all non-null renderers.
    /// </summary>
    private static (BrailleRenderer? braille, ColorBlockRenderer? block, AsciiRenderer? ascii)
        CreateStreamingRenderers(RenderOptions renderOptions, VideoHandlerOptions opts)
    {
        if (opts.UseBraille)
            return (new BrailleRenderer(renderOptions), null, null);

        if (opts.UseBlocks)
            return (null, new ColorBlockRenderer(renderOptions), null);

        return (null, null, new AsciiRenderer(renderOptions));
    }

    /// <summary>
    ///     Append subtitle and status overlays to frame content.
    /// </summary>
    private static string AppendOverlays(
        string content,
        SubtitleRenderer? subtitleRenderer,
        StatusLine? statusRenderer,
        VideoHandlerOptions opts,
        string inputPath,
        VideoInfo videoInfo,
        int charWidth, int charHeight,
        string renderModeName,
        int currentFrame, int totalFrames,
        TimeSpan currentTime, double totalDuration, double fps)
    {
        // Append subtitle if available (from static track or live provider)
        if (subtitleRenderer != null)
        {
            SubtitleEntry? entry = null;

            // Try static subtitles first
            if (opts.Subtitles != null)
                entry = opts.Subtitles.GetActiveAt(currentTime);
            // Fall back to live subtitle provider (chunked transcription)
            else if (opts.LiveSubtitleProvider != null)
                entry = opts.LiveSubtitleProvider.Track.GetActiveAtWithTolerance(
                    currentTime.TotalSeconds);

            if (entry != null)
            {
                var subtitleText = subtitleRenderer.RenderEntry(entry);
                if (!string.IsNullOrEmpty(subtitleText)) content += "\n" + subtitleText;
            }
        }

        // Append status line if enabled
        if (statusRenderer != null)
            content += "\n" + statusRenderer.Render(BuildStatusInfo(
                inputPath, videoInfo, charWidth, charHeight, renderModeName,
                currentFrame, totalFrames, currentTime, totalDuration, fps));

        return content;
    }

    /// <summary>
    ///     Build a StatusLine.StatusInfo for the current frame.
    /// </summary>
    private static StatusLine.StatusInfo BuildStatusInfo(
        string inputPath, VideoInfo videoInfo,
        int charWidth, int charHeight, string renderModeName,
        int currentFrame, int totalFrames,
        TimeSpan currentTime, double totalDuration, double fps)
    {
        return new StatusLine.StatusInfo
        {
            FileName = Path.GetFileName(inputPath),
            SourceWidth = videoInfo.Width,
            SourceHeight = videoInfo.Height,
            OutputWidth = charWidth,
            OutputHeight = charHeight,
            RenderMode = renderModeName,
            CurrentFrame = currentFrame,
            TotalFrames = totalFrames,
            CurrentTime = currentTime,
            TotalDuration = TimeSpan.FromSeconds(totalDuration),
            Fps = fps
        };
    }

    /// <summary>
    ///     Burn subtitle text onto an image frame.
    ///     Uses accessible sizing (approximately 5-8% of video height).
    /// </summary>
    private static void BurnSubtitleOntoImage(Image<Rgba32> image, string subtitle)
    {
        var lines = subtitle.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(2).ToArray();
        if (lines.Length == 0) return;

        FontFamily family;
        try
        {
            var families = SystemFonts.Collection.Families;
            family = families.FirstOrDefault(f =>
                f.Name.Contains("Arial", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("Sans", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("Helvetica", StringComparison.OrdinalIgnoreCase));
            family = family == default ? families.First() : family;
        }
        catch
        {
            return; // Skip subtitle if font loading fails
        }

        // Accessible subtitle sizing: ~5-8% of video height
        // WCAG guidelines recommend captions be clearly readable
        // Start with height/14 (~7%) with min 12px for small images, max 64px for large
        var baseFontSize = Math.Clamp(image.Height / 14, 12, 64);

        // Horizontal margin (5% each side = 90% usable width)
        var horizontalMargin = image.Width * 0.05f;
        var maxTextWidth = image.Width - horizontalMargin * 2;

        // Find the longest line and scale font to fit
        var fontSize = baseFontSize;
        var longestLine = lines.OrderByDescending(l => l.Length).First().Trim();

        // Scale down font if text is too wide
        for (var attempts = 0; attempts < 10 && fontSize >= 10; attempts++)
        {
            var testFont = family.CreateFont(fontSize, FontStyle.Bold);
            var textSize = TextMeasurer.MeasureSize(longestLine, new TextOptions(testFont));

            if (textSize.Width <= maxTextWidth)
                break;

            // Reduce font size proportionally
            fontSize = (int)(fontSize * (maxTextWidth / textSize.Width) * 0.95f);
            fontSize = Math.Max(fontSize, 10); // Minimum 10px
        }

        var font = family.CreateFont(fontSize, FontStyle.Bold);

        var textColor = Color.White;
        var outlineColor = Color.Black;

        var bottomMargin = image.Height / 20;
        var lineHeight = fontSize + 4;
        var startY = image.Height - bottomMargin - lines.Length * lineHeight;

        image.Mutate(ctx =>
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var text = lines[i].Trim();
                if (string.IsNullOrEmpty(text)) continue;

                var textOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Origin = new PointF(image.Width / 2f, startY + i * lineHeight)
                };

                // Draw outline
                var outlineOffset = Math.Max(1, fontSize / 10);
                var offsets = new[]
                    { (-outlineOffset, 0), (outlineOffset, 0), (0, -outlineOffset), (0, outlineOffset) };

                foreach (var (dx, dy) in offsets)
                {
                    var outlineOptions = new RichTextOptions(font)
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Origin = new PointF(image.Width / 2f + dx, startY + i * lineHeight + dy)
                    };
                    ctx.DrawText(outlineOptions, text, outlineColor);
                }

                // Draw main text
                ctx.DrawText(textOptions, text, textColor);
            }
        });
    }
}

/// <summary>
///     Options for video handling.
/// </summary>
public class VideoHandlerOptions
{
    // Input/Output
    public FileInfo? OutputGif { get; init; }
    public bool OutputAsJson { get; init; }
    public bool OutputAsCompressed { get; init; }
    public string? JsonOutputPath { get; init; }

    // Dimensions
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int MaxWidth { get; init; }
    public int MaxHeight { get; init; }

    // Time range
    public double? Start { get; init; }
    public double? End { get; init; }
    public double? Duration { get; init; }

    // Playback
    public float Speed { get; init; } = 1.0f;
    public int Loop { get; init; } = 1;
    public double? Fps { get; init; }
    public string? FrameStep { get; init; } = "1";
    public string? Sampling { get; init; }
    public double SceneThreshold { get; init; } = 0.4;

    /// <summary>
    ///     Get the numeric frame step value (for FFmpeg).
    /// </summary>
    public int FrameStepValue => ParseFrameStep().frameStep;

    // Render modes
    public bool UseBlocks { get; init; }
    public bool UseBraille { get; init; }

    // Color/rendering
    public bool NoColor { get; init; }
    public bool UseGreyscaleAnsi { get; init; }
    public int? ColorCount { get; init; }
    public float Contrast { get; init; } = 2.5f;
    public float Gamma { get; init; } = 0.65f;
    public float? CharAspect { get; init; }
    public string? Charset { get; init; }
    public string? Preset { get; init; }
    public CalibrationSettings? SavedCalibration { get; init; }

    // Performance
    public int Buffer { get; init; } = 3;
    public bool NoHwAccel { get; init; }
    public bool NoAltScreen { get; init; }
    public bool NoParallel { get; init; }
    public bool NoDither { get; init; }
    public bool NoEdgeChars { get; init; }

    // FFmpeg
    public string? FfmpegPath { get; init; }
    public bool NoAutoDownload { get; init; }
    public bool AutoConfirmDownload { get; init; }

    // Info/status
    public bool ShowInfo { get; init; }
    public bool ShowStatus { get; init; }
    public int? StatusWidth { get; init; }

    // GIF output
    public int GifFontSize { get; init; } = 10;
    public float GifScale { get; init; } = 1.0f;
    public int GifColors { get; init; } = 64;
    public double? GifLength { get; init; }
    public int? GifFrames { get; init; }

    // Raw/extract mode
    public bool RawMode { get; init; }
    public int? RawWidth { get; init; }
    public int? RawHeight { get; init; }
    public bool SmartKeyframes { get; init; }
    public int Quality { get; init; } = 85;

    // Image adjustments
    public bool NoInvert { get; init; }
    public bool EnableEdge { get; init; }
    public float? BgThreshold { get; init; }
    public float? DarkBgThreshold { get; init; }
    public bool AutoBg { get; init; }
    public float? DarkCutoff { get; init; }
    public float? LightCutoff { get; init; }

    // Temporal stability
    public bool Dejitter { get; init; }
    public int? ColorThreshold { get; init; }

    // Subtitles
    public SubtitleTrack? Subtitles { get; init; }

    /// <summary>
    ///     Live subtitle provider for streaming transcription during playback.
    ///     Takes precedence over static Subtitles when set.
    /// </summary>
    public ILiveSubtitleProvider? LiveSubtitleProvider { get; init; }

    // Debug
    public bool DebugMode { get; init; }

    /// <summary>
    ///     Parse the FrameStep option and return (frameStepValue, isSmartMode).
    /// </summary>
    public (int frameStep, bool smartMode) ParseFrameStep()
    {
        if (string.IsNullOrEmpty(FrameStep))
            return (1, false);

        var lower = FrameStep.ToLowerInvariant().Trim();

        // Smart mode
        if (lower == "s" || lower == "smart")
            return (1, true);

        // Numeric mode
        if (int.TryParse(FrameStep, out var step))
            return (Math.Max(1, step), false);

        // Default fallback
        return (1, false);
    }
}