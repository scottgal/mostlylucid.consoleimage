// Image file handling for consoleimage CLI

using ConsoleImage.Cli.Utilities;
using ConsoleImage.Core;

namespace ConsoleImage.Cli.Handlers;

/// <summary>
/// Handles image and GIF file processing.
/// </summary>
public static class ImageHandler
{
    /// <summary>
    /// Handle image files (JPG, PNG, GIF, etc.) directly without FFmpeg.
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
        CancellationToken ct)
    {
        ConsoleHelper.EnableAnsiSupport();

        // Determine render mode
        var renderMode = useBraille ? RenderMode.Braille
            : useBlocks ? RenderMode.ColorBlocks
            : useMatrix ? RenderMode.Matrix
            : RenderMode.Ascii;

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
            useBraille, useBlocks, outputAsJson, jsonOutputPath, maxWidth, maxHeight, ct);
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
            {
                if (useBraille || useBlocks || useMatrix)
                    Console.WriteLine("Note: Status line in GIF output is only supported for ASCII mode.");
                else
                    statusRenderer = new StatusLine(maxWidth, !noColor);
            }

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

    private static async Task RenderAnimatedGifFrames(
        FileInfo input, RenderOptions options, GifWriter gifWriter,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool useBraille, bool useBlocks,
        StatusLine? statusRenderer, string renderModeName)
    {
        if (useMatrix)
        {
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet);
            using var renderer = new MatrixRenderer(options, matrixOpts);
            var frames = renderer.RenderGif(input.FullName);
            var totalFrames = frames.Count;
            var frameIndex = 0;

            foreach (var frame in frames)
            {
                gifWriter.AddFrame(frame.Content, frame.DelayMs);
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
                gifWriter.AddBrailleFrame(frame, frame.DelayMs);
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
                gifWriter.AddColorBlockFrame(frame, frame.DelayMs);
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
                    var statusInfo = new StatusLine.StatusInfo
                    {
                        FileName = input.Name,
                        RenderMode = renderModeName,
                        CurrentFrame = frameIndex + 1,
                        TotalFrames = totalFrames
                    };
                    content += "\n" + statusRenderer.Render(statusInfo);
                }
                gifWriter.AddFrame(content, frame.DelayMs);
                frameIndex++;
                Console.Write($"\rRendering frames to GIF: {frameIndex}/{totalFrames}");
            }
            Console.WriteLine();
        }
        await Task.CompletedTask;
    }

    private static void RenderStaticGifFrame(
        FileInfo input, RenderOptions options, GifWriter gifWriter,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool useBraille, bool useBlocks)
    {
        if (useMatrix)
        {
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet);
            using var renderer = new MatrixRenderer(options, matrixOpts);
            var frame = renderer.RenderFile(input.FullName);
            gifWriter.AddFrame(frame.Content, 1000);
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
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet);
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

        List<IAnimationFrame> frames;
        if (useMatrix)
        {
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet);
            using var renderer = new MatrixRenderer(options, matrixOpts);
            frames = renderer.RenderGif(input.FullName).Cast<IAnimationFrame>().ToList();
        }
        else if (useBraille)
        {
            using var renderer = new BrailleRenderer(options);
            frames = renderer.RenderGif(input.FullName).Cast<IAnimationFrame>().ToList();
        }
        else if (useBlocks)
        {
            using var renderer = new ColorBlockRenderer(options);
            frames = renderer.RenderGif(input.FullName).Cast<IAnimationFrame>().ToList();
        }
        else
        {
            using var renderer = new AsciiRenderer(options);
            var darkThreshold = options.Invert ? options.DarkTerminalBrightnessThreshold : null;
            var lightThreshold = !options.Invert ? options.LightTerminalBrightnessThreshold : null;
            frames = renderer.RenderGif(input.FullName)
                .Select(f => (IAnimationFrame)new AsciiFrameAdapter(f, options.UseColor, darkThreshold, lightThreshold))
                .ToList();
        }

        await RenderHelpers.PlayFramesAsync(frames, loop, speed, ct);
        return 0;
    }

    private static async Task<int> HandleStaticDisplay(
        FileInfo input, RenderOptions options,
        bool useMatrix, string? matrixColor, bool matrixFullColor,
        float? matrixDensity, float? matrixSpeed, string? matrixAlphabet,
        bool useBraille, bool useBlocks,
        bool outputAsJson, string? jsonOutputPath,
        int maxWidth, int maxHeight, CancellationToken ct)
    {
        if (useMatrix)
        {
            var matrixOpts = RenderHelpers.BuildMatrixOptions(matrixColor, matrixFullColor, matrixDensity, matrixSpeed, matrixAlphabet);
            using var renderer = new MatrixRenderer(options, matrixOpts);
            var frame = renderer.RenderFile(input.FullName);
            Console.WriteLine(frame.Content);

            if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            {
                var doc = ConsoleImageDocument.FromMatrixFrames(new[] { frame }, options, input.FullName);
                await doc.SaveAsync(jsonOutputPath, ct);
                Console.Error.WriteLine($"Saved document to: {jsonOutputPath}");
            }
        }
        else if (useBraille)
        {
            using var renderer = new BrailleRenderer(options);
            var frame = renderer.RenderFileToFrame(input.FullName);
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
            Console.WriteLine(frame.ToAnsiString());

            if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            {
                var doc = ConsoleImageDocument.FromAsciiFrames(new[] { frame }, options, input.FullName);
                await doc.SaveAsync(jsonOutputPath, ct);
                Console.Error.WriteLine($"Saved document to: {jsonOutputPath}");
            }
        }

        return 0;
    }
}
