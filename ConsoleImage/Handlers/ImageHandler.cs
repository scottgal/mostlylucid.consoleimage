// Image file handling for consoleimage CLI

using ConsoleImage.Cli.Utilities;
using ConsoleImage.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static ConsoleImage.Core.MarkdownRenderer;

namespace ConsoleImage.Cli.Handlers;

/// <summary>
///     Handles image and GIF file processing.
/// </summary>
public static class ImageHandler
{
    /// <summary>
    ///     Handle image files (JPG, PNG, GIF, etc.) directly without FFmpeg.
    /// </summary>
    public static async Task<int> HandleAsync(
        FileInfo input,
        int? width, int? height, int maxWidth, int maxHeight,
        float? charAspect, CalibrationSettings? savedCalibration,
        bool useBlocks, bool useBraille,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool noColor, int? colorCount, float contrast, float gamma, int loop, float speed,
        FileInfo? outputGif,
        int gifFontSize, float gifScale, int gifColors,
        bool outputAsJson, string? jsonOutputPath,
        bool showStatus,
        string? markdownPath, string? markdownFormatStr,
        CancellationToken ct)
    {
        ConsoleHelper.EnableAnsiSupport();

        // Determine render mode
        var renderMode = RenderHelpers.GetRenderMode(useBraille, useBlocks, useMatrix);

        // Get effective aspect ratio
        var effectiveAspect = RenderHelpers.GetEffectiveAspectRatio(charAspect, savedCalibration, renderMode);

        // Create render options
        var options = new RenderOptions
        {
            Width = width,
            Height = height,
            MaxWidth = maxWidth,
            MaxHeight = maxHeight,
            CharacterAspectRatio = effectiveAspect,
            ContrastPower = contrast,
            Gamma = gamma,
            UseColor = !noColor,
            Invert = true,
            UseParallelProcessing = true,
            LoopCount = loop,
            AnimationSpeedMultiplier = speed,
            ColorCount = colorCount
        };

        // Check if it's an animated GIF
        var isAnimatedGif = input.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);

        if (outputGif != null)
            return await HandleGifOutput(input, options, outputGif, isAnimatedGif,
                useMatrix, matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet,
                useBraille, useBlocks, noColor, maxWidth, showStatus, loop, gifFontSize, gifScale, gifColors, ct);

        if (isAnimatedGif && outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            return await HandleJsonOutput(input, options, jsonOutputPath,
                useMatrix, matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet,
                useBraille, useBlocks, ct);

        // Display mode
        if (isAnimatedGif)
            return await HandleAnimatedDisplay(input, options,
                useMatrix, matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet,
                useBraille, useBlocks, loop, speed, ct);

        return await HandleStaticDisplay(input, options,
            useMatrix, matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet,
            useBraille, useBlocks, outputAsJson, jsonOutputPath,
            markdownPath, markdownFormatStr, maxWidth, maxHeight, ct);
    }

    private static async Task<int> HandleGifOutput(
        FileInfo input, RenderOptions options, FileInfo outputGif, bool isAnimatedGif,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool useBraille, bool useBlocks, bool noColor, int maxWidth, bool showStatus,
        int loop, int gifFontSize, float gifScale, int gifColors, CancellationToken ct)
    {
        var gifLoopCount = loop != 1 ? loop : 0;

        var gifOptions = new GifWriterOptions
        {
            FontSize = gifFontSize,
            Scale = gifScale,
            MaxColors = Math.Clamp(gifColors, 16, 256),
            LoopCount = gifLoopCount
        };
        using var gifWriter = new GifWriter(gifOptions);

        if (isAnimatedGif)
        {
            Console.WriteLine("Rendering animated GIF...");
            Console.WriteLine($"  Output: {outputGif.FullName}");

            var renderModeName = useBraille ? "Braille" : useBlocks ? "Blocks" : useMatrix ? "Matrix" : "ASCII";
            StatusLine? statusRenderer = null;
            if (showStatus)
                statusRenderer = new StatusLine(maxWidth, !noColor);

            await RenderAnimatedGifFrames(input, options, gifWriter,
                useMatrix, matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet,
                useBraille, useBlocks, statusRenderer, renderModeName);
        }
        else
        {
            Console.WriteLine($"Rendering image to GIF: {outputGif.FullName}");
            RenderStaticGifFrame(input, options, gifWriter,
                useMatrix, matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet,
                useBraille, useBlocks);
        }

        await gifWriter.SaveAsync(outputGif.FullName, ct);
        Console.WriteLine($"Saved to {outputGif.FullName}");
        return 0;
    }

    private static Task RenderAnimatedGifFrames(
        FileInfo input, RenderOptions options, GifWriter gifWriter,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool useBraille, bool useBlocks,
        StatusLine? statusRenderer, string renderModeName)
    {
        if (useMatrix)
        {
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed,
                matrixAlphabet);
            using var renderer = new MatrixRenderer(options, matrixOpts);
            var frames = renderer.RenderGif(input.FullName);
            var totalFrames = frames.Count;
            var frameIndex = 0;

            foreach (var frame in frames)
            {
                if (statusRenderer != null)
                {
                    var statusText = statusRenderer.Render(BuildImageStatusInfo(
                        input.Name, renderModeName, frameIndex + 1, totalFrames));
                    // Matrix renders as text - append status
                    gifWriter.AddFrame(frame.Content + "\n" + statusText, frame.DelayMs);
                }
                else
                {
                    gifWriter.AddFrame(frame.Content, frame.DelayMs);
                }

                frameIndex++;
                Console.Write($"\rRendering frames to GIF: {frameIndex}/{totalFrames}");
            }

            Console.WriteLine();
        }
        else if (useBraille)
        {
            using var renderer = new BrailleRenderer(options);
            var frames = renderer.RenderGif(input.FullName).ToList();
            var totalFrames = frames.Count;
            var frameIndex = 0;

            foreach (var frame in frames)
            {
                if (statusRenderer != null)
                {
                    var statusText = statusRenderer.Render(BuildImageStatusInfo(
                        input.Name, renderModeName, frameIndex + 1, totalFrames));
                    var brailleImage = GifWriter.RenderBrailleFrameToImage(frame);
                    gifWriter.AddImageFrameWithOverlays(brailleImage, null, statusText, frame.DelayMs);
                    brailleImage.Dispose();
                }
                else
                {
                    gifWriter.AddBrailleFrame(frame, frame.DelayMs);
                }

                frameIndex++;
                Console.Write($"\rRendering frames to GIF: {frameIndex}/{totalFrames}");
            }

            Console.WriteLine();
        }
        else if (useBlocks)
        {
            using var renderer = new ColorBlockRenderer(options);
            var frames = renderer.RenderGif(input.FullName).ToList();
            var totalFrames = frames.Count;
            var frameIndex = 0;

            foreach (var frame in frames)
            {
                if (statusRenderer != null)
                {
                    var statusText = statusRenderer.Render(BuildImageStatusInfo(
                        input.Name, renderModeName, frameIndex + 1, totalFrames));
                    var blocksImage = GifWriter.RenderColorBlockFrameToImage(frame);
                    gifWriter.AddImageFrameWithOverlays(blocksImage, null, statusText, frame.DelayMs);
                    blocksImage.Dispose();
                }
                else
                {
                    gifWriter.AddColorBlockFrame(frame, frame.DelayMs);
                }

                frameIndex++;
                Console.Write($"\rRendering frames to GIF: {frameIndex}/{totalFrames}");
            }

            Console.WriteLine();
        }
        else
        {
            using var renderer = new AsciiRenderer(options);
            var frames = renderer.RenderGif(input.FullName).ToList();
            var totalFrames = frames.Count;
            var frameIndex = 0;

            foreach (var frame in frames)
            {
                var content = frame.ToAnsiString();
                if (statusRenderer != null)
                {
                    var statusText = statusRenderer.Render(BuildImageStatusInfo(
                        input.Name, renderModeName, frameIndex + 1, totalFrames));
                    content += "\n" + statusText;
                }

                gifWriter.AddFrame(content, frame.DelayMs);
                frameIndex++;
                Console.Write($"\rRendering frames to GIF: {frameIndex}/{totalFrames}");
            }

            Console.WriteLine();
        }

        return Task.CompletedTask;
    }

    private static StatusLine.StatusInfo BuildImageStatusInfo(
        string fileName, string renderMode, int currentFrame, int totalFrames)
    {
        return new StatusLine.StatusInfo
        {
            FileName = fileName,
            RenderMode = renderMode,
            CurrentFrame = currentFrame,
            TotalFrames = totalFrames
        };
    }

    private static void RenderStaticGifFrame(
        FileInfo input, RenderOptions options, GifWriter gifWriter,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool useBraille, bool useBlocks)
    {
        if (useMatrix)
        {
            // Matrix mode animates even on static images - generate 5 seconds of rain
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed,
                matrixAlphabet);
            using var renderer = new MatrixRenderer(options, matrixOpts);
            using var image = Image.Load<Rgba32>(input.FullName);

            var frameCount = matrixOpts.TargetFps * 5; // 5 seconds of animation
            var delayMs = 1000 / matrixOpts.TargetFps;
            var frameIndex = 0;

            foreach (var frame in renderer.RenderContinuous(image, frameCount))
            {
                gifWriter.AddFrame(frame.Content, delayMs);
                frameIndex++;
                Console.Write($"\rRendering Matrix frames to GIF: {frameIndex}/{frameCount}");
            }

            Console.WriteLine();
        }
        else if (useBraille)
        {
            using var renderer = new BrailleRenderer(options);
            var content = renderer.RenderFile(input.FullName);
            var frame = new BrailleFrame(content, 1000);
            gifWriter.AddBrailleFrame(frame, 1000);
        }
        else if (useBlocks)
        {
            using var renderer = new ColorBlockRenderer(options);
            var content = renderer.RenderFile(input.FullName);
            var frame = new ColorBlockFrame(content, 1000);
            gifWriter.AddColorBlockFrame(frame, 1000);
        }
        else
        {
            using var renderer = new AsciiRenderer(options);
            var frame = renderer.RenderFile(input.FullName);
            gifWriter.AddFrame(frame.ToAnsiString(), 1000);
        }
    }

    private static async Task<int> HandleJsonOutput(
        FileInfo input, RenderOptions options, string jsonOutputPath,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool useBraille, bool useBlocks, CancellationToken ct)
    {
        Console.Error.WriteLine($"Rendering animated GIF to {jsonOutputPath}...");

        if (useMatrix)
        {
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed,
                matrixAlphabet);
            using var renderer = new MatrixRenderer(options, matrixOpts);
            var frames = renderer.RenderGif(input.FullName);
            Console.Error.WriteLine($"Rendering {frames.Count} frames in Matrix mode...");
            var doc = ConsoleImageDocument.FromMatrixFrames(frames, options, input.FullName);
            await doc.SaveAsync(jsonOutputPath, ct);
        }
        else if (useBraille)
        {
            using var renderer = new BrailleRenderer(options);
            var frames = renderer.RenderGifFrames(input.FullName);
            Console.Error.WriteLine($"Rendering {frames.Count} frames in Braille mode...");
            var doc = ConsoleImageDocument.FromBrailleFrames(frames, options, input.FullName);
            await doc.SaveAsync(jsonOutputPath, ct);
        }
        else if (useBlocks)
        {
            using var renderer = new ColorBlockRenderer(options);
            var frames = renderer.RenderGifFrames(input.FullName);
            Console.Error.WriteLine($"Rendering {frames.Count} frames in Blocks mode...");
            var doc = ConsoleImageDocument.FromColorBlockFrames(frames, options, input.FullName);
            await doc.SaveAsync(jsonOutputPath, ct);
        }
        else
        {
            using var renderer = new AsciiRenderer(options);
            var frames = renderer.RenderGif(input.FullName);
            Console.Error.WriteLine($"Rendering {frames.Count} frames in ASCII mode...");
            var doc = ConsoleImageDocument.FromAsciiFrames(frames, options, input.FullName);
            await doc.SaveAsync(jsonOutputPath, ct);
        }

        var fullPath = Path.GetFullPath(jsonOutputPath);
        Console.Error.WriteLine($"Saved animated document to: {fullPath}");
        return 0;
    }

    private static async Task<int> HandleAnimatedDisplay(
        FileInfo input, RenderOptions options,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool useBraille, bool useBlocks, int loop, float speed, CancellationToken ct)
    {
        Console.WriteLine($"Playing animated GIF: {input.Name}");
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine();

        if (useMatrix)
        {
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed,
                matrixAlphabet);
            using var renderer = new MatrixRenderer(options, matrixOpts);
            var frames = renderer.RenderGif(input.FullName).Cast<IAnimationFrame>().ToList();
            await RenderHelpers.PlayFramesAsync(frames, loop, speed, ct);
        }
        else if (useBraille)
        {
            using var renderer = new BrailleRenderer(options);
            var frames = renderer.RenderGif(input.FullName).Cast<IAnimationFrame>().ToList();
            await RenderHelpers.PlayFramesAsync(frames, loop, speed, ct);
        }
        else if (useBlocks)
        {
            using var renderer = new ColorBlockRenderer(options);
            var frames = renderer.RenderGif(input.FullName).Cast<IAnimationFrame>().ToList();
            await RenderHelpers.PlayFramesAsync(frames, loop, speed, ct);
        }
        else
        {
            // ASCII mode — use AsciiAnimationPlayer which has proper synchronized output,
            // diff rendering, and line clearing for flicker-free animation.
            // Frame delays already include speed multiplier from RenderOptions.AnimationSpeedMultiplier.
            using var renderer = new AsciiRenderer(options);
            var asciiFrames = renderer.RenderGif(input.FullName);
            var darkThreshold = options.Invert ? options.DarkTerminalBrightnessThreshold : null;
            var lightThreshold = !options.Invert ? options.LightTerminalBrightnessThreshold : null;
            using var player = new AsciiAnimationPlayer(
                asciiFrames, options.UseColor, loop,
                useDiffRendering: true, useAltScreen: true,
                darkThreshold: darkThreshold, lightThreshold: lightThreshold);
            await player.PlayAsync(ct);
        }

        return 0;
    }

    private static async Task<int> HandleStaticDisplay(
        FileInfo input, RenderOptions options,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool useBraille, bool useBlocks,
        bool outputAsJson, string? jsonOutputPath,
        string? markdownPath, string? markdownFormatStr,
        int maxWidth, int maxHeight, CancellationToken ct)
    {
        // Parse markdown format
        var mdFormat = markdownFormatStr?.ToLowerInvariant() switch
        {
            "html" => MarkdownFormat.Html,
            "svg" => MarkdownFormat.Svg,
            "ansi" => MarkdownFormat.Ansi,
            _ => MarkdownFormat.Plain
        };

        string? ansiContent = null;

        if (useMatrix)
        {
            // Matrix mode always animates continuously, even on still images
            // This is the fun part - the rain keeps falling!
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed,
                matrixAlphabet);
            using var renderer = new MatrixRenderer(options, matrixOpts);
            using var image = Image.Load<Rgba32>(input.FullName);

            // For JSON output, generate a fixed number of frames
            if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            {
                var frameCount = matrixOpts.TargetFps * 10; // 10 seconds of animation
                var frames = renderer.RenderContinuous(image, frameCount).ToList();
                var doc = ConsoleImageDocument.FromMatrixFrames(frames, options, input.FullName);
                await doc.SaveAsync(jsonOutputPath, ct);
                Console.Error.WriteLine($"Saved {frameCount} Matrix frames to: {jsonOutputPath}");
                ansiContent = frames.FirstOrDefault()?.Content;
            }
            // For markdown output, render single frame
            else if (!string.IsNullOrEmpty(markdownPath))
            {
                var frame = renderer.RenderImage(image);
                ansiContent = frame.Content;
                Console.WriteLine(frame.Content);
            }
            // For display, animate continuously until Ctrl+C (space to pause)
            else
            {
                await PlayMatrixContinuousAsync(renderer, matrixOpts, image, 0, 1.0f, ct);
                return 0;
            }
        }
        else if (useBraille)
        {
            using var renderer = new BrailleRenderer(options);
            var frame = renderer.RenderFileToFrame(input.FullName);
            ansiContent = frame.Content;
            Console.WriteLine(frame.Content);

            if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            {
                var doc = ConsoleImageDocument.FromBrailleFrames(new[] { frame }, options, input.FullName);
                await doc.SaveAsync(jsonOutputPath, ct);
                Console.Error.WriteLine($"Saved document to: {jsonOutputPath}");
            }
        }
        else if (useBlocks)
        {
            using var renderer = new ColorBlockRenderer(options);
            var frame = renderer.RenderFileToFrame(input.FullName);
            ansiContent = frame.Content;
            Console.WriteLine(frame.Content);

            if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            {
                var doc = ConsoleImageDocument.FromColorBlockFrames(new[] { frame }, options, input.FullName);
                await doc.SaveAsync(jsonOutputPath, ct);
                Console.Error.WriteLine($"Saved document to: {jsonOutputPath}");
            }
        }
        else
        {
            using var renderer = new AsciiRenderer(options);
            var frame = renderer.RenderFile(input.FullName);
            ansiContent = frame.ToAnsiString();
            Console.WriteLine(ansiContent);

            if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            {
                var doc = ConsoleImageDocument.FromAsciiFrames(new[] { frame }, options, input.FullName);
                await doc.SaveAsync(jsonOutputPath, ct);
                Console.Error.WriteLine($"Saved document to: {jsonOutputPath}");
            }
        }

        // Handle markdown output
        if (!string.IsNullOrEmpty(markdownPath) && !string.IsNullOrEmpty(ansiContent))
        {
            var title = Path.GetFileNameWithoutExtension(input.Name);

            // If output is .svg, save SVG directly
            if (markdownPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                await SaveSvgAsync(ansiContent, markdownPath, ct: ct);
                Console.Error.WriteLine($"Saved SVG to: {markdownPath}");
            }
            else
            {
                await SaveMarkdownAsync(ansiContent, markdownPath, mdFormat, title, ct);
                Console.Error.WriteLine($"Saved markdown ({mdFormat}) to: {markdownPath}");
            }
        }

        return 0;
    }

    /// <summary>
    ///     Common animation loop for Matrix rain playback with pause/resume support.
    /// </summary>
    private static async Task PlayMatrixAnimationLoopAsync(
        Func<IEnumerable<MatrixFrame>> frameGenerator,
        int delayMs,
        CancellationToken ct)
    {
        ConsoleHelper.EnableAnsiSupport();

        var frameHeight = 0;
        var isFirstFrame = true;
        var isPaused = false;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Check for space to pause/resume at batch boundary
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Spacebar)
                        isPaused = !isPaused;
                    else if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                        return;
                }

                if (isPaused)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                // Generate frames in batches
                foreach (var frame in frameGenerator())
                {
                    if (ct.IsCancellationRequested) break;

                    // Check for pause during batch
                    while (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Spacebar)
                            isPaused = !isPaused;
                        else if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                            return;
                    }

                    if (isPaused)
                    {
                        await Task.Delay(50, ct);
                        continue;
                    }

                    // Move cursor up for subsequent frames (N lines = N-1 newlines, cursor on line N-1)
                    if (!isFirstFrame && frameHeight > 1)
                        Console.Write($"\x1b[{frameHeight - 1}A");
                    else
                        isFirstFrame = false;

                    // Move to column 0 and use synchronized output for flicker-free rendering
                    Console.Write("\r\x1b[?2026h");
                    Console.Write(frame.Content);
                    Console.Write("\x1b[?2026l");

                    if (frameHeight == 0)
                        frameHeight = frame.Content.Split('\n').Length;

                    await Task.Delay(delayMs, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit via Ctrl+C
        }
    }

    /// <summary>
    ///     Play continuous Matrix rain animation over an image until cancelled.
    ///     Space to pause, Q/Escape to quit.
    /// </summary>
    private static async Task PlayMatrixContinuousAsync(
        MatrixRenderer renderer, MatrixOptions matrixOpts, Image<Rgba32> image, int loop, float speed,
        CancellationToken ct)
    {
        var delayMs = (int)(1000.0 / matrixOpts.TargetFps / speed);
        await PlayMatrixAnimationLoopAsync(
            () => renderer.RenderContinuous(image, 100),
            delayMs,
            ct);
    }

    /// <summary>
    ///     Play pure Matrix rain effect with no image until cancelled.
    /// </summary>
    public static async Task PlayPureMatrixAsync(
        RenderOptions options, MatrixOptions matrixOpts, int loop, float speed, CancellationToken ct)
    {
        using var renderer = new MatrixRenderer(options, matrixOpts);
        var delayMs = (int)(1000.0 / matrixOpts.TargetFps / speed);
        await PlayMatrixAnimationLoopAsync(
            () => renderer.RenderPureRainContinuous(100),
            delayMs,
            ct);
    }

    /// <summary>
    ///     Render pure Matrix rain effect to an animated GIF.
    /// </summary>
    public static async Task<int> HandlePureMatrixGifAsync(
        RenderOptions options, MatrixOptions matrixOpts, FileInfo outputGif,
        int gifFontSize, float gifScale, int gifColors, int loop, CancellationToken ct)
    {
        Console.WriteLine($"Rendering pure Matrix rain to GIF: {outputGif.FullName}");

        var gifLoopCount = loop != 1 ? loop : 0;
        var gifOptions = new GifWriterOptions
        {
            FontSize = gifFontSize,
            Scale = gifScale,
            MaxColors = Math.Clamp(gifColors, 16, 256),
            LoopCount = gifLoopCount
        };
        using var gifWriter = new GifWriter(gifOptions);
        using var renderer = new MatrixRenderer(options, matrixOpts);

        var frameCount = matrixOpts.TargetFps * 5; // 5 seconds of animation
        var delayMs = 1000 / matrixOpts.TargetFps;
        var frameIndex = 0;

        foreach (var frame in renderer.RenderPureRainContinuous(frameCount))
        {
            if (ct.IsCancellationRequested) break;

            gifWriter.AddFrame(frame.Content, delayMs);
            frameIndex++;
            Console.Write($"\rRendering Matrix frames to GIF: {frameIndex}/{frameCount}");
        }

        Console.WriteLine();

        await gifWriter.SaveAsync(outputGif.FullName, ct);
        Console.WriteLine($"Saved to {outputGif.FullName}");
        return 0;
    }
}