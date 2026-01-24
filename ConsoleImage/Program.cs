// ConsoleImage - Unified ASCII art CLI for images, GIFs, videos, and documents
// Supports multiple render modes: ASCII, ColorBlocks, Braille, Matrix

using System.CommandLine;
using System.Reflection;
using ConsoleImage.Cli;
using ConsoleImage.Cli.Handlers;
using ConsoleImage.Cli.Utilities;
using ConsoleImage.Core;

// Enable ANSI escape sequence processing on Windows
ConsoleHelper.EnableAnsiSupport();

// Easter egg: if no arguments, play embedded animation then show help
if (args.Length == 0)
{
    await PlayEasterEggAsync();
    ShowHelpAndWait();
    return 0;
}

// Load saved calibration if exists
var savedCalibration = CalibrationHelper.Load();

// Create CLI options and root command
var cliOptions = new CliOptions();
var rootCommand = new RootCommand("Render images, GIFs, videos, and documents as ASCII art");
cliOptions.AddToCommand(rootCommand);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    // Parse all option values
    var inputPath = parseResult.GetValue(cliOptions.Input);
    var input = !string.IsNullOrEmpty(inputPath) ? new FileInfo(inputPath) : null;
    var width = parseResult.GetValue(cliOptions.Width);
    var height = parseResult.GetValue(cliOptions.Height);
    var maxWidth = parseResult.GetValue(cliOptions.MaxWidth);
    var maxHeight = parseResult.GetValue(cliOptions.MaxHeight);
    var start = parseResult.GetValue(cliOptions.Start);
    var end = parseResult.GetValue(cliOptions.End);
    var duration = parseResult.GetValue(cliOptions.Duration);
    var speed = parseResult.GetValue(cliOptions.Speed);
    var loop = parseResult.GetValue(cliOptions.Loop);
    var fps = parseResult.GetValue(cliOptions.Fps);
    var frameStep = parseResult.GetValue(cliOptions.FrameStep);
    var sampling = parseResult.GetValue(cliOptions.Sampling);
    var sceneThreshold = parseResult.GetValue(cliOptions.SceneThreshold);
    var useAscii = parseResult.GetValue(cliOptions.Ascii);
    var useBlocks = parseResult.GetValue(cliOptions.Blocks);
    var useBrailleOpt = parseResult.GetValue(cliOptions.Braille);
    var useMatrix = parseResult.GetValue(cliOptions.Matrix);

    // Braille is default, but --ascii, --blocks, or --matrix override it
    var useBraille = useBrailleOpt && !useAscii && !useBlocks && !useMatrix;
    var matrixColor = parseResult.GetValue(cliOptions.MatrixColor);
    var matrixFullColor = parseResult.GetValue(cliOptions.MatrixFullColor);
    var matrixDensity = parseResult.GetValue(cliOptions.MatrixDensity);
    var matrixSpeed = parseResult.GetValue(cliOptions.MatrixSpeed);
    var matrixAlphabet = parseResult.GetValue(cliOptions.MatrixAlphabet);
    var noColor = parseResult.GetValue(cliOptions.NoColor);
    var colorCount = parseResult.GetValue(cliOptions.Colors);
    var contrast = parseResult.GetValue(cliOptions.Contrast);
    var gamma = parseResult.GetValue(cliOptions.Gamma);
    var charAspect = parseResult.GetValue(cliOptions.CharAspect);
    var charset = parseResult.GetValue(cliOptions.Charset);
    var preset = parseResult.GetValue(cliOptions.Preset);
    var buffer = parseResult.GetValue(cliOptions.Buffer);
    var noHwAccel = parseResult.GetValue(cliOptions.NoHwAccel);
    var noAltScreen = parseResult.GetValue(cliOptions.NoAltScreen);
    var ffmpegPath = parseResult.GetValue(cliOptions.FfmpegPath);
    var showInfo = parseResult.GetValue(cliOptions.Info);
    var calibrate = parseResult.GetValue(cliOptions.Calibrate);
    var saveCalibrationOpt = parseResult.GetValue(cliOptions.SaveCalibration);
    var showStatus = parseResult.GetValue(cliOptions.Status);
    var gifFontSize = parseResult.GetValue(cliOptions.GifFontSize);
    var gifScale = parseResult.GetValue(cliOptions.GifScale);
    var gifColors = colorCount ?? 64;
    var gifFps = parseResult.GetValue(cliOptions.GifFps);
    var gifLength = parseResult.GetValue(cliOptions.GifLength);
    var gifFrames = parseResult.GetValue(cliOptions.GifFrames);
    var gifWidth = parseResult.GetValue(cliOptions.GifWidth);
    var gifHeight = parseResult.GetValue(cliOptions.GifHeight);
    var rawMode = parseResult.GetValue(cliOptions.Raw);
    var rawWidth = parseResult.GetValue(cliOptions.RawWidth);
    var rawHeight = parseResult.GetValue(cliOptions.RawHeight);
    var smartKeyframes = parseResult.GetValue(cliOptions.SmartKeyframes);
    var noAutoDownload = parseResult.GetValue(cliOptions.NoFfmpegDownload);
    var autoConfirmDownload = parseResult.GetValue(cliOptions.FfmpegYes);
    var output = parseResult.GetValue(cliOptions.Output);
    var noInvert = parseResult.GetValue(cliOptions.NoInvert);
    var enableEdge = parseResult.GetValue(cliOptions.Edge);
    var bgThreshold = parseResult.GetValue(cliOptions.BgThreshold);
    var darkBgThreshold = parseResult.GetValue(cliOptions.DarkBgThreshold);
    var autoBg = parseResult.GetValue(cliOptions.AutoBg);
    var noParallel = parseResult.GetValue(cliOptions.NoParallel);
    var noDither = parseResult.GetValue(cliOptions.NoDither);
    var noEdgeChars = parseResult.GetValue(cliOptions.NoEdgeChars);
    var jsonOutput = parseResult.GetValue(cliOptions.Json);
    var darkCutoff = parseResult.GetValue(cliOptions.DarkCutoff);
    var lightCutoff = parseResult.GetValue(cliOptions.LightCutoff);
    var mode = parseResult.GetValue(cliOptions.Mode);
    var dejitter = parseResult.GetValue(cliOptions.Dejitter);
    var colorThreshold = parseResult.GetValue(cliOptions.ColorThreshold);

    // Parse unified output option - auto-detect format from extension
    var (outputAsJson, outputAsCompressed, jsonOutputPath, gifOutputPath) =
        ParseOutputOption(output);

    var outputGif = !string.IsNullOrEmpty(gifOutputPath) ? new FileInfo(gifOutputPath) : null;

    // Calibration mode
    if (calibrate)
        return CalibrationHandler.Handle(
            useBraille, useBlocks, useMatrix,
            charAspect, savedCalibration,
            noColor, saveCalibrationOpt);

    // Input required for non-calibration modes
    if (string.IsNullOrEmpty(inputPath))
    {
        Console.Error.WriteLine(
            "Error: No input file specified. Use --calibrate for aspect ratio testing without a file.");
        return 1;
    }

    // Check if input is a URL (FFmpeg can stream videos directly from URLs)
    var isUrl = UrlHelper.IsUrl(inputPath);
    string inputFullPath;
    string? tempFile = null;

    if (isUrl)
    {
        inputPath = UrlHelper.NormalizeUrl(inputPath);
        Console.Error.WriteLine($"URL detected: {inputPath}");

        // For videos, FFmpeg streams directly - no download needed
        if (UrlHelper.IsLikelyVideo(inputPath))
        {
            Console.Error.WriteLine("Streaming video via FFmpeg...");
            inputFullPath = inputPath;
            var urlExt = UrlHelper.GetExtension(inputPath) ?? "mp4";
            input = new FileInfo($"stream.{urlExt}"); // Dummy for extension detection
        }
        // For images/GIFs, download to temp file first
        else
        {
            Console.Error.Write("Downloading... ");
            try
            {
                var ext = UrlHelper.GetExtension(inputPath) ?? "tmp";
                tempFile = Path.Combine(Path.GetTempPath(), $"consoleimage_{Guid.NewGuid()}.{ext}");

                using var stream = await UrlHelper.DownloadAsync(inputPath, (downloaded, total) =>
                {
                    if (total > 0)
                        Console.Error.Write($"\rDownloading: {downloaded * 100 / total}%   ");
                    else
                        Console.Error.Write($"\rDownloading: {downloaded / 1024}KB   ");
                }, cancellationToken);

                await using var fs = File.Create(tempFile);
                await stream.CopyToAsync(fs, cancellationToken);
                Console.Error.WriteLine("done.");

                inputFullPath = tempFile;
                input = new FileInfo(tempFile);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"failed: {ex.Message}");
                return 1;
            }
        }
    }
    else
    {
        // Local file - resolve path (handles AOT and mapped drives)
        input = new FileInfo(inputPath);
        var resolved = ResolveInputPath(inputPath, input);
        if (resolved == null)
            return 1;
        inputFullPath = resolved;
        input = new FileInfo(inputFullPath);
    }

    // Determine JSON output path if needed
    if (outputAsJson && string.IsNullOrEmpty(jsonOutputPath))
        jsonOutputPath = Path.ChangeExtension(input.FullName, outputAsCompressed ? ".cidz" : ".json");

    // Route to appropriate handler based on file type
    var extension = input.Extension.ToLowerInvariant();

    // Document files (.json, .ndjson, .cidz)
    if (IsDocumentFile(extension, input.Name))
        return await DocumentHandler.HandleAsync(
            input.FullName, speed, loop, outputGif,
            gifFontSize, gifScale, gifColors, cancellationToken);

    // Image files (jpg, png, gif, etc.)
    if (IsImageFile(extension))
        return await ImageHandler.HandleAsync(
            input, width, height, maxWidth, maxHeight,
            charAspect, savedCalibration,
            useBlocks, useBraille,
            useMatrix, matrixColor, matrixFullColor,
            matrixDensity, matrixSpeed, matrixAlphabet,
            noColor, colorCount, contrast, gamma, loop, speed,
            outputGif, gifFontSize, gifScale, gifColors,
            outputAsJson, jsonOutputPath,
            showStatus, cancellationToken);

    // Video files - requires FFmpeg
    // Default width for braille video is 50 (more CPU intensive)
    var videoWidth = width ?? (useBraille ? 50 : null);

    var videoOpts = new VideoHandlerOptions
    {
        // Output
        OutputGif = outputGif,
        OutputAsJson = outputAsJson,
        OutputAsCompressed = outputAsCompressed,
        JsonOutputPath = jsonOutputPath,

        // Dimensions (braille defaults to 50 wide for performance)
        Width = videoWidth,
        Height = height,
        MaxWidth = videoWidth ?? maxWidth,
        MaxHeight = maxHeight,

        // Time range
        Start = start,
        End = end,
        Duration = duration,

        // Playback
        Speed = speed,
        Loop = loop,
        Fps = fps,
        FrameStep = frameStep,
        Sampling = sampling,
        SceneThreshold = sceneThreshold,

        // Render modes
        UseBlocks = useBlocks,
        UseBraille = useBraille,

        // Color/rendering
        NoColor = noColor,
        ColorCount = colorCount,
        Contrast = contrast,
        Gamma = gamma,
        CharAspect = charAspect,
        Charset = charset,
        Preset = preset,
        SavedCalibration = savedCalibration,

        // Performance
        Buffer = buffer,
        NoHwAccel = noHwAccel,
        NoAltScreen = noAltScreen,
        NoParallel = noParallel,
        NoDither = noDither,
        NoEdgeChars = noEdgeChars,

        // FFmpeg
        FfmpegPath = ffmpegPath,
        NoAutoDownload = noAutoDownload,
        AutoConfirmDownload = autoConfirmDownload,

        // Info/status
        ShowInfo = showInfo,
        ShowStatus = showStatus,

        // GIF output
        GifFontSize = gifFontSize,
        GifScale = gifScale,
        GifColors = gifColors,
        GifLength = gifLength,
        GifFrames = gifFrames,

        // Raw/extract
        RawMode = rawMode,
        RawWidth = rawWidth,
        RawHeight = rawHeight,
        SmartKeyframes = smartKeyframes,

        // Image adjustments
        NoInvert = noInvert,
        EnableEdge = enableEdge,
        BgThreshold = bgThreshold,
        DarkBgThreshold = darkBgThreshold,
        AutoBg = autoBg,
        DarkCutoff = darkCutoff,
        LightCutoff = lightCutoff,

        // Temporal stability
        Dejitter = dejitter,
        ColorThreshold = colorThreshold
    };

    var result = await VideoHandler.HandleAsync(inputFullPath, input, videoOpts, cancellationToken);

    // Clean up temp file if we created one
    if (tempFile != null && File.Exists(tempFile))
    {
        try { File.Delete(tempFile); } catch { }
    }

    return result;
});

return await rootCommand.Parse(args).InvokeAsync();

// === Helper Functions ===

static (bool outputAsJson, bool outputAsCompressed, string? jsonOutputPath, string? gifOutputPath)
    ParseOutputOption(string? output)
{
    if (string.IsNullOrEmpty(output))
        return (false, false, null, null);

    // Check for explicit format prefixes first
    if (output.Equals("json", StringComparison.OrdinalIgnoreCase))
        return (true, true, null, null);

    if (output.StartsWith("json:", StringComparison.OrdinalIgnoreCase))
    {
        var path = output[5..];
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            path = path[..^5] + ".cidz";
        return (true, true, path, null);
    }

    if (output.StartsWith("raw:", StringComparison.OrdinalIgnoreCase) ||
        output.StartsWith("uncompressed:", StringComparison.OrdinalIgnoreCase))
    {
        var prefixLen = output.StartsWith("raw:") ? 4 : 13;
        return (true, false, output[prefixLen..], null);
    }

    if (output.Equals("gif", StringComparison.OrdinalIgnoreCase))
        return (false, false, null, null);

    if (output.StartsWith("gif:", StringComparison.OrdinalIgnoreCase))
        return (false, false, null, output[4..]);

    if (output.Equals("cidz", StringComparison.OrdinalIgnoreCase) ||
        output.Equals("compressed", StringComparison.OrdinalIgnoreCase))
        return (true, true, null, null);

    if (output.StartsWith("cidz:", StringComparison.OrdinalIgnoreCase))
        return (true, true, output[5..], null);

    // Auto-detect from extension
    if (output.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        return (false, false, null, output);

    if (output.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        return (true, false, output, null);

    if (output.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
        return (true, false, output, null);

    if (output.EndsWith(".cidz", StringComparison.OrdinalIgnoreCase) ||
        output.EndsWith(".cid.7z", StringComparison.OrdinalIgnoreCase))
        return (true, true, output, null);

    // Unknown extension - warn and default to GIF
    Console.Error.WriteLine(
        $"Warning: Unknown output format for '{output}'. Use .gif for GIF, .cidz for compressed JSON.");
    Console.Error.WriteLine("Assuming GIF output. Use 'json:path' for compressed, 'raw:path.json' for uncompressed.");
    var gifPath = output.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? output : output + ".gif";
    return (false, false, null, gifPath);
}

static string? ResolveInputPath(string inputPath, FileInfo input)
{
    var inputFullPath = inputPath;

    if (!File.Exists(inputFullPath))
        inputFullPath = input.FullName;

    if (!File.Exists(inputFullPath))
    {
        Console.Error.WriteLine($"Error: File not found: {inputPath}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Troubleshooting:");
        Console.Error.WriteLine($"  Original path: {inputPath}");
        Console.Error.WriteLine($"  Resolved path: {input.FullName}");

        if (inputPath.Length >= 2 && inputPath[1] == ':')
        {
            var driveLetter = char.ToUpper(inputPath[0]);
            if (driveLetter >= 'D')
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"  Drive {driveLetter}: may be a mapped network drive.");
                Console.Error.WriteLine(
                    "  Mapped drives are user-specific and may not be accessible in some contexts.");
                Console.Error.WriteLine(
                    "  Try using the full UNC path instead (e.g., \\\\server\\share\\path\\file.mkv)");
            }
        }

        var dir = Path.GetDirectoryName(inputFullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            var dirExists = false;
            try { dirExists = Directory.Exists(dir); } catch { }
            Console.Error.WriteLine($"  Directory exists: {dirExists}");
        }

        return null;
    }

    return inputFullPath;
}

static bool IsDocumentFile(string extension, string fileName)
{
    return extension == ".json" ||
           extension == ".ndjson" ||
           extension == ".cidz" ||
           extension == ".7z" ||
           fileName.EndsWith(".cid.7z", StringComparison.OrdinalIgnoreCase);
}

static bool IsImageFile(string extension)
{
    return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp" or ".tiff" or ".tif";
}

// Easter egg: play embedded Star Wars animation
static async Task PlayEasterEggAsync()
{
    try
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("ConsoleImage.star_wars.json");
        if (stream == null) return;

        // Load JSON directly from embedded resource
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        var doc = ConsoleImageDocument.FromJson(json);
        if (doc == null || doc.Frames.Count == 0) return;

        // Play with cancellation on any key press
        using var cts = new CancellationTokenSource();

        // Start playback
        var player = new UnifiedPlayer(doc, new UnifiedPlayerOptions
        {
            UseAltScreen = true,
            LoopCountOverride = 1
        });

        // Start key listener only if we have interactive console
        Task? keyTask = null;
        if (!Console.IsInputRedirected)
        {
            keyTask = Task.Run(() =>
            {
                try
                {
                    Console.ReadKey(true);
                    cts.Cancel();
                }
                catch { }
            });
        }

        // Play the animation
        try
        {
            await player.PlayAsync(cts.Token);
        }
        catch (OperationCanceledException) { }

        // Cancel key listener if still running
        cts.Cancel();
    }
    catch
    {
        // Silently ignore any errors with easter egg
    }
}

static void ShowHelpAndWait()
{
    Console.WriteLine();
    Console.WriteLine("ConsoleImage v3.0 - ASCII Art Renderer");
    Console.WriteLine("=======================================");
    Console.WriteLine();
    Console.WriteLine("Usage: consoleimage <file> [options]");
    Console.WriteLine();
    Console.WriteLine("Render modes:");
    Console.WriteLine("  (default)    Braille dots - highest detail, smallest output");
    Console.WriteLine("  -a, --ascii  Classic ASCII characters");
    Console.WriteLine("  -b, --blocks Unicode half-blocks (2x vertical resolution)");
    Console.WriteLine("  -M, --matrix Matrix digital rain effect");
    Console.WriteLine();
    Console.WriteLine("Common options:");
    Console.WriteLine("  -w, --width <n>     Output width (default: 50 for video)");
    Console.WriteLine("  -s, --speed <n>     Playback speed multiplier");
    Console.WriteLine("  -l, --loop <n>      Loop count (0 = infinite)");
    Console.WriteLine("  -o, --output <file> Save as .gif or .cidz");
    Console.WriteLine("  -S, --status        Show status line");
    Console.WriteLine("  --colours <n>       Reduce color palette (4, 16, 256)");
    Console.WriteLine("  --dejitter          Reduce animation flickering");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  consoleimage photo.jpg              (braille, auto-sized)");
    Console.WriteLine("  consoleimage movie.mp4 -w 80        (braille, 80 chars wide)");
    Console.WriteLine("  consoleimage movie.mp4 -a -w 120    (ascii mode)");
    Console.WriteLine("  consoleimage animation.gif -o out.gif");
    Console.WriteLine();
    Console.WriteLine("Run 'consoleimage --help' for full options.");
    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    try
    {
        if (!Console.IsInputRedirected)
            Console.ReadKey(true);
    }
    catch { }
}
