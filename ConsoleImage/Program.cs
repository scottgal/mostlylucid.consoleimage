// ASCII Art CLI - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering

using System.CommandLine;
using ConsoleImage.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Enable ANSI escape sequence processing on Windows consoles
ConsoleHelper.EnableAnsiSupport();

// Load saved calibration if exists
var savedCalibration = CalibrationHelper.Load();

// Create root command
var rootCommand = new RootCommand("Convert images to ASCII art using shape-matching algorithm");

// Input file/URL argument (optional for --calibrate mode and --mode list)
var inputArg = new Argument<string?>("input")
{
    Description = "Path to image file or URL (http:// or https://)",
    Arity = ArgumentArity.ZeroOrOne
};

// Options - System.CommandLine 2.0.2 API
var widthOption = new Option<int?>("--width") { Description = "Output width in characters" };
widthOption.Aliases.Add("-w");

var heightOption = new Option<int?>("--height") { Description = "Output height in characters (auto-calculated from width by default)" };
heightOption.Aliases.Add("-h");

var aspectRatioOption = new Option<float?>("--aspect-ratio") { Description = "Character aspect ratio (default: 0.5, meaning chars are 2x taller than wide)" };
aspectRatioOption.Aliases.Add("-a");

// Detect console window size for defaults (with fallbacks)
int defaultMaxWidth = 120;
int defaultMaxHeight = 40;
try
{
    if (Console.WindowWidth > 0)
        defaultMaxWidth = Console.WindowWidth - 1; // -1 to avoid line wrapping
    if (Console.WindowHeight > 0)
        defaultMaxHeight = Console.WindowHeight - 2; // -2 for prompt space
}
catch
{
    // Console size detection not available (piped output, no console, etc.)
}

var maxWidthOption = new Option<int>("--max-width")
{
    Description = "Maximum output width (default: console width)",
    DefaultValueFactory = _ => defaultMaxWidth
};

var maxHeightOption = new Option<int>("--max-height")
{
    Description = "Maximum output height (default: console height)",
    DefaultValueFactory = _ => defaultMaxHeight
};

var noColorOption = new Option<bool>("--no-color") { Description = "Disable colored output (monochrome)" };

var noInvertOption = new Option<bool>("--no-invert") { Description = "Don't invert output (for light backgrounds)" };

var contrastOption = new Option<float>("--contrast")
{
    Description = "Contrast enhancement power (1.0 = none, higher = more contrast)",
    DefaultValueFactory = _ => 2.5f
};

var gammaOption = new Option<float>("--gamma")
{
    Description = "Gamma correction (< 1.0 brightens, > 1.0 darkens)",
    DefaultValueFactory = _ => 0.65f
};
gammaOption.Aliases.Add("-g");

var charsetOption = new Option<string?>("--charset") { Description = "Custom character set (ordered from light to dark)" };

var presetOption = new Option<string?>("--preset") { Description = "Character set preset: extended (default), simple, block, classic" };
presetOption.Aliases.Add("-p");

var outputOption = new Option<string?>("--output") { Description = "Output: file path, 'gif:path.gif' for animated GIF, or just 'gif' (uses input name)" };
outputOption.Aliases.Add("-o");

var noAnimateOption = new Option<bool>("--no-animate") { Description = "Don't animate GIFs - just show first frame" };

var speedOption = new Option<float>("--speed")
{
    Description = "Animation speed multiplier",
    DefaultValueFactory = _ => 1.0f
};
speedOption.Aliases.Add("-s");

var loopOption = new Option<int>("--loop")
{
    Description = "Number of animation loops (0 = infinite, default)",
    DefaultValueFactory = _ => 0
};
loopOption.Aliases.Add("-l");

var framerateOption = new Option<float?>("--framerate") { Description = "Fixed framerate in FPS (overrides GIF timing)" };
framerateOption.Aliases.Add("-r");

var frameSampleOption = new Option<int>("--frame-sample")
{
    Description = "Frame sampling rate (1 = every frame, 2 = every 2nd, etc.). Higher values reduce processing time.",
    DefaultValueFactory = _ => 1
};
frameSampleOption.Aliases.Add("-f");

var edgeOption = new Option<bool>("--edge") { Description = "Enable edge detection to enhance foreground visibility" };
edgeOption.Aliases.Add("-e");

var bgThresholdOption = new Option<float?>("--bg-threshold") { Description = "Background suppression threshold (0.0-1.0). Pixels above this brightness are suppressed." };

var darkBgThresholdOption = new Option<float?>("--dark-bg-threshold") { Description = "Dark background suppression threshold (0.0-1.0). Pixels below this brightness are suppressed." };

var autoBgOption = new Option<bool>("--auto-bg") { Description = "Automatically detect and suppress background" };

var colorBlocksOption = new Option<bool>("--blocks") { Description = "Use colored Unicode blocks for high-fidelity output (requires 24-bit color terminal)" };
colorBlocksOption.Aliases.Add("-b");

var brailleOption = new Option<bool>("--braille") { Description = "Use braille characters for ultra-high resolution (2x4 dots per cell)" };
brailleOption.Aliases.Add("-B");

var matrixOption = new Option<bool>("--matrix") { Description = "Use Matrix digital rain effect (falling characters with glow)" };
matrixOption.Aliases.Add("-M");

var matrixColorOption = new Option<string?>("--matrix-color") { Description = "Matrix color: green (default), red, blue, amber, cyan, purple, or hex (#RRGGBB)" };
var matrixFullColorOption = new Option<bool>("--matrix-fullcolor") { Description = "Use source image colors with Matrix lighting effect" };
var matrixDensityOption = new Option<float>("--matrix-density") { Description = "Matrix rain density (0.1-2.0, default 0.5)", DefaultValueFactory = _ => 0.5f };
var matrixSpeedOption = new Option<float>("--matrix-speed") { Description = "Matrix rain speed multiplier (0.5-3.0, default 1.0)", DefaultValueFactory = _ => 1.0f };
var matrixAsciiOption = new Option<bool>("--matrix-ascii") { Description = "Use ASCII characters only (no katakana) - better font compatibility" };
var matrixFpsOption = new Option<int>("--matrix-fps") { Description = "Matrix animation FPS (5-60, default 20)", DefaultValueFactory = _ => 20 };
var matrixAlphabetOption = new Option<string?>("--matrix-alphabet") { Description = "Custom character set for Matrix rain (e.g., 'HELLO' or '01')" };
var matrixEdgeDetectOption = new Option<bool>("--matrix-edge-detect") { Description = "Enable edge detection - rain collects on horizontal edges (shoulders, ledges)" };
matrixEdgeDetectOption.Aliases.Add("--matrix-reveal");
var matrixEdgePersistOption = new Option<float>("--matrix-edge-persist") { Description = "Edge persistence strength (0.0-1.0, default 0.7)", DefaultValueFactory = _ => 0.7f };
var matrixBrightPersistOption = new Option<bool>("--matrix-bright-persist") { Description = "Enable brightness persistence - brighter areas glow longer" };

var noAltScreenOption = new Option<bool>("--no-alt-screen") { Description = "Disable alternate screen buffer for animations (keeps output in scrollback)" };

var noParallelOption = new Option<bool>("--no-parallel") { Description = "Disable parallel processing" };

var noDitherOption = new Option<bool>("--no-dither") { Description = "Disable Floyd-Steinberg dithering" };

var noEdgeDirOption = new Option<bool>("--no-edge-chars") { Description = "Disable directional characters (/ \\ | -)" };

var jsonOption = new Option<bool>("--json") { Description = "Output as JSON (for LLM tool calls and programmatic use)" };
jsonOption.Aliases.Add("-j");

var darkCutoffOption = new Option<float?>("--dark-cutoff") { Description = "Dark terminal optimization: skip colors below this brightness (0.0-1.0). Disabled by default." };
var lightCutoffOption = new Option<float?>("--light-cutoff") { Description = "Light terminal optimization: skip colors above this brightness (0.0-1.0). Disabled by default." };

// Calibration options
var calibrateOption = new Option<bool>("--calibrate") { Description = "Display aspect ratio calibration pattern (should show a circle)" };
var saveCalibrationOption = new Option<bool>("--save") { Description = "Save current aspect ratio to calibration.json (use with --calibrate)" };

// Status line option - shows file info, resolution, progress below the image
var statusOption = new Option<bool>("--status") { Description = "Show status line below output with file info, resolution, progress" };
statusOption.Aliases.Add("-S");

// Mode selection option (supports: ascii, blocks, braille, matrix, sixel, iterm2, kitty, auto, list)
var modeOption = new Option<string?>("--mode") { Description = "Rendering mode: ascii, blocks, braille, matrix, sixel, iterm2, kitty, auto, list (shows available modes)" };
modeOption.Aliases.Add("-m");

// GIF output options - save rendered output as animated GIF
var outputGifOption = new Option<FileInfo?>("--output-gif") { Description = "Save rendered output as animated GIF (emulated console)" };

var gifLengthOption = new Option<double?>("--gif-length") { Description = "Length of GIF output in seconds (for videos/long animations)" };
var gifFramesOption = new Option<int?>("--gif-frames") { Description = "Number of frames for GIF output" };
var gifFontSizeOption = new Option<int>("--gif-font-size") { Description = "Font size for GIF output (smaller = smaller file)", DefaultValueFactory = _ => 10 };
var gifScaleOption = new Option<float>("--gif-scale") { Description = "Scale factor for GIF output (0.5 = half size)", DefaultValueFactory = _ => 1.0f };
var gifFpsOption = new Option<int>("--gif-fps") { Description = "Target FPS for GIF output (lower = smaller file)", DefaultValueFactory = _ => 10 };
var gifColorsOption = new Option<int>("--gif-colors") { Description = "Max colors in GIF palette (16-256, lower = smaller file)", DefaultValueFactory = _ => 64 };
var gifWidthOption = new Option<int?>("--gif-width") { Description = "GIF output width in characters (preserves aspect ratio if height not set)" };
var gifHeightOption = new Option<int?>("--gif-height") { Description = "GIF output height in characters (preserves aspect ratio if width not set)" };

// Add options to root command
rootCommand.Arguments.Add(inputArg);
rootCommand.Options.Add(widthOption);
rootCommand.Options.Add(heightOption);
rootCommand.Options.Add(aspectRatioOption);
rootCommand.Options.Add(maxWidthOption);
rootCommand.Options.Add(maxHeightOption);
rootCommand.Options.Add(noColorOption);
rootCommand.Options.Add(noInvertOption);
rootCommand.Options.Add(contrastOption);
rootCommand.Options.Add(gammaOption);
rootCommand.Options.Add(charsetOption);
rootCommand.Options.Add(presetOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(noAnimateOption);
rootCommand.Options.Add(speedOption);
rootCommand.Options.Add(loopOption);
rootCommand.Options.Add(framerateOption);
rootCommand.Options.Add(frameSampleOption);
rootCommand.Options.Add(edgeOption);
rootCommand.Options.Add(bgThresholdOption);
rootCommand.Options.Add(darkBgThresholdOption);
rootCommand.Options.Add(autoBgOption);
rootCommand.Options.Add(colorBlocksOption);
rootCommand.Options.Add(brailleOption);
rootCommand.Options.Add(matrixOption);
rootCommand.Options.Add(matrixColorOption);
rootCommand.Options.Add(matrixFullColorOption);
rootCommand.Options.Add(matrixDensityOption);
rootCommand.Options.Add(matrixSpeedOption);
rootCommand.Options.Add(matrixAsciiOption);
rootCommand.Options.Add(matrixFpsOption);
rootCommand.Options.Add(matrixAlphabetOption);
rootCommand.Options.Add(matrixEdgeDetectOption);
rootCommand.Options.Add(matrixEdgePersistOption);
rootCommand.Options.Add(matrixBrightPersistOption);
rootCommand.Options.Add(noAltScreenOption);
rootCommand.Options.Add(noParallelOption);
rootCommand.Options.Add(noDitherOption);
rootCommand.Options.Add(noEdgeDirOption);
rootCommand.Options.Add(jsonOption);
rootCommand.Options.Add(darkCutoffOption);
rootCommand.Options.Add(lightCutoffOption);
rootCommand.Options.Add(calibrateOption);
rootCommand.Options.Add(saveCalibrationOption);
rootCommand.Options.Add(statusOption);
rootCommand.Options.Add(modeOption);
rootCommand.Options.Add(outputGifOption);
rootCommand.Options.Add(gifLengthOption);
rootCommand.Options.Add(gifFramesOption);
rootCommand.Options.Add(gifFontSizeOption);
rootCommand.Options.Add(gifScaleOption);
rootCommand.Options.Add(gifFpsOption);
rootCommand.Options.Add(gifColorsOption);
rootCommand.Options.Add(gifWidthOption);
rootCommand.Options.Add(gifHeightOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var input = parseResult.GetValue(inputArg);
    var mode = parseResult.GetValue(modeOption);
    var width = parseResult.GetValue(widthOption);
    var height = parseResult.GetValue(heightOption);
    var aspectRatio = parseResult.GetValue(aspectRatioOption);
    var maxWidth = parseResult.GetValue(maxWidthOption);
    var maxHeight = parseResult.GetValue(maxHeightOption);
    var noColor = parseResult.GetValue(noColorOption);
    var noInvert = parseResult.GetValue(noInvertOption);
    var contrast = parseResult.GetValue(contrastOption);
    var gamma = parseResult.GetValue(gammaOption);
    var charset = parseResult.GetValue(charsetOption);
    var preset = parseResult.GetValue(presetOption);
    var output = parseResult.GetValue(outputOption);
    var noAnimate = parseResult.GetValue(noAnimateOption);
    var speed = parseResult.GetValue(speedOption);
    var loop = parseResult.GetValue(loopOption);
    var framerate = parseResult.GetValue(framerateOption);
    var frameSample = parseResult.GetValue(frameSampleOption);
    var enableEdge = parseResult.GetValue(edgeOption);
    var bgThreshold = parseResult.GetValue(bgThresholdOption);
    var darkBgThreshold = parseResult.GetValue(darkBgThresholdOption);
    var autoBg = parseResult.GetValue(autoBgOption);
    var colorBlocks = parseResult.GetValue(colorBlocksOption);
    var braille = parseResult.GetValue(brailleOption);
    var matrix = parseResult.GetValue(matrixOption);
    var matrixColor = parseResult.GetValue(matrixColorOption);
    var matrixFullColor = parseResult.GetValue(matrixFullColorOption);
    var matrixDensity = parseResult.GetValue(matrixDensityOption);
    var matrixSpeed = parseResult.GetValue(matrixSpeedOption);
    var matrixAscii = parseResult.GetValue(matrixAsciiOption);
    var matrixFps = parseResult.GetValue(matrixFpsOption);
    var matrixAlphabet = parseResult.GetValue(matrixAlphabetOption);
    var matrixEdgeDetect = parseResult.GetValue(matrixEdgeDetectOption);
    var matrixEdgePersist = parseResult.GetValue(matrixEdgePersistOption);
    var matrixBrightPersist = parseResult.GetValue(matrixBrightPersistOption);
    var noAltScreen = parseResult.GetValue(noAltScreenOption);
    var noParallel = parseResult.GetValue(noParallelOption);
    var noDither = parseResult.GetValue(noDitherOption);
    var noEdgeChars = parseResult.GetValue(noEdgeDirOption);
    var jsonOutput = parseResult.GetValue(jsonOption);
    var darkCutoff = parseResult.GetValue(darkCutoffOption);
    var lightCutoff = parseResult.GetValue(lightCutoffOption);
    var calibrate = parseResult.GetValue(calibrateOption);
    var saveCalibration = parseResult.GetValue(saveCalibrationOption);
    var showStatus = parseResult.GetValue(statusOption);
    var gifLength = parseResult.GetValue(gifLengthOption);
    var gifFrames = parseResult.GetValue(gifFramesOption);
    var gifFontSize = parseResult.GetValue(gifFontSizeOption);
    var gifScale = parseResult.GetValue(gifScaleOption);
    var gifFps = parseResult.GetValue(gifFpsOption);
    var gifColors = parseResult.GetValue(gifColorsOption);
    var gifWidth = parseResult.GetValue(gifWidthOption);
    var gifHeight = parseResult.GetValue(gifHeightOption);

    // Parse output option: can be "path.txt", "gif", "gif:path.gif", "json", "json:path.json"
    string? outputFile = null;
    bool outputAsGif = false;
    string? gifOutputPath = null;
    bool outputAsJson = false;
    string? jsonOutputPath = null;

    if (!string.IsNullOrEmpty(output))
    {
        if (output.Equals("gif", StringComparison.OrdinalIgnoreCase))
        {
            outputAsGif = true;
            // Use input filename with .gif extension - will set later when we have input
        }
        else if (output.StartsWith("gif:", StringComparison.OrdinalIgnoreCase))
        {
            outputAsGif = true;
            gifOutputPath = output[4..]; // Everything after "gif:"
        }
        else if (output.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            outputAsGif = true;
            gifOutputPath = output;
        }
        else if (output.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            outputAsJson = true;
            // Use input filename with .json extension - will set later when we have input
        }
        else if (output.StartsWith("json:", StringComparison.OrdinalIgnoreCase))
        {
            outputAsJson = true;
            jsonOutputPath = output[5..]; // Everything after "json:"
        }
        else if (output.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            outputAsJson = true;
            jsonOutputPath = output;
        }
        else
        {
            outputFile = output;
        }
    }

    // Helper to create GIF output options
    // Default to 10 seconds for animated sources unless explicitly set
    // Use --gif-length 0 for unlimited
    GifWriterOptions CreateGifOptions(bool isAnimatedSource = false)
    {
        // --gif-length 0 means unlimited (null)
        double? effectiveLength = gifLength.HasValue
            ? (gifLength.Value <= 0 ? null : gifLength.Value)  // 0 or negative = unlimited
            : (isAnimatedSource ? 10.0 : null);                // Default 10s for animations

        return new GifWriterOptions
        {
            FontSize = gifFontSize,
            Scale = gifScale,
            MaxColors = Math.Clamp(gifColors, 16, 256),
            TargetFps = gifFps,
            MaxLengthSeconds = effectiveLength,
            MaxFrames = gifFrames,
            LoopCount = loop
        };
    }

    // Helper to check if we should stop adding frames (memory efficiency)
    bool ShouldStopAddingFrames(GifWriter writer, GifWriterOptions opts, double elapsedMs)
    {
        if (opts.MaxFrames.HasValue && writer.FrameCount >= opts.MaxFrames.Value)
            return true;
        if (opts.MaxLengthSeconds.HasValue && elapsedMs / 1000.0 >= opts.MaxLengthSeconds.Value)
            return true;
        return false;
    }

    // JSON mode helper - manual serialization for AOT compatibility
    void OutputJson(string json) => Console.WriteLine(json);
    string JsonEscape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\x1b", "\\u001b");

    // Mode list - show all available rendering modes
    if (mode?.Equals("list", StringComparison.OrdinalIgnoreCase) == true)
    {
        Console.WriteLine(UnifiedRenderer.GetProtocolList());
        Console.WriteLine("Usage: consoleimage image.jpg --mode blocks");
        Console.WriteLine("       consoleimage image.jpg --mode auto   (auto-detect best)");
        Console.WriteLine("       consoleimage image.jpg --mode kitty  (use Kitty protocol)");
        return 0;
    }

    // Calibration mode - show test pattern to verify aspect ratio
    if (calibrate)
    {
        // Determine render mode for calibration
        var calibrationMode = braille ? RenderMode.Braille
            : colorBlocks ? RenderMode.ColorBlocks
            : matrix ? RenderMode.Matrix
            : RenderMode.Ascii;

        // Get effective aspect ratio: explicit > saved for mode > default
        float calibrationAspect = aspectRatio
            ?? savedCalibration?.GetAspectRatio(calibrationMode)
            ?? 0.5f;

        string modeName = calibrationMode switch
        {
            RenderMode.Braille => "Braille",
            RenderMode.ColorBlocks => "Blocks",
            RenderMode.Matrix => "Matrix",
            _ => "ASCII"
        };

        Console.WriteLine($"Aspect Ratio Calibration - {modeName} Mode (--aspect-ratio {calibrationAspect})");
        Console.WriteLine("The shape below should appear as a perfect CIRCLE.");
        Console.WriteLine("If stretched horizontally, decrease --aspect-ratio (try 0.45)");
        Console.WriteLine("If stretched vertically, increase --aspect-ratio (try 0.55)");
        Console.WriteLine();
        Console.WriteLine("Suggested values by platform:");
        Console.WriteLine("  Windows Terminal:  0.5");
        Console.WriteLine("  Windows Console:   0.5");
        Console.WriteLine("  macOS Terminal:    0.5");
        Console.WriteLine("  iTerm2:            0.5");
        Console.WriteLine("  Linux (gnome):     0.45-0.5");
        Console.WriteLine("  VS Code Terminal:  0.5");
        Console.WriteLine();

        // Render calibration pattern using the core helper
        var calOutput = CalibrationHelper.RenderCalibrationPattern(
            calibrationMode,
            calibrationAspect,
            useColor: !noColor,
            width: 40,
            height: 20);

        Console.WriteLine(calOutput);
        Console.WriteLine();
        Console.WriteLine($"Current --aspect-ratio: {calibrationAspect} ({modeName} mode)");

        // Save calibration if requested
        if (saveCalibration)
        {
            // Start with existing settings or defaults
            var baseSettings = savedCalibration ?? new CalibrationSettings();
            // Update only the current mode's aspect ratio
            var settings = baseSettings.WithAspectRatio(calibrationMode, calibrationAspect);
            CalibrationHelper.Save(settings);
            Console.WriteLine($"Saved {modeName} calibration to: {CalibrationHelper.GetDefaultPath()}");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("Once the circle looks correct, run with --save to remember this setting:");
            string modeFlag = calibrationMode switch
            {
                RenderMode.Braille => " --braille",
                RenderMode.ColorBlocks => " --blocks",
                RenderMode.Matrix => " --matrix",
                _ => ""
            };
            Console.WriteLine($"  consoleimage --calibrate{modeFlag} --aspect-ratio {calibrationAspect} --save");
        }

        return 0;
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        Console.Error.WriteLine("Error: No input file or URL specified. Use --calibrate for aspect ratio testing without a file.");
        Console.Error.WriteLine("       Use --mode list to see available rendering modes.");
        return 1;
    }

    // Check if input is a URL
    bool isUrl = UrlHelper.IsUrl(input);

    // Validate file exists (for local files)
    if (!isUrl && !File.Exists(input))
    {
        if (jsonOutput)
        {
            OutputJson($"{{\"success\":false,\"error\":\"File not found: {JsonEscape(input)}\"}}");
            return 1;
        }
        Console.Error.WriteLine($"Error: File not found: {input}");
        return 1;
    }

    // Determine GIF output path if needed
    if (outputAsGif && string.IsNullOrEmpty(gifOutputPath))
    {
        // Generate output path from input filename
        string baseName = isUrl
            ? Path.GetFileNameWithoutExtension(new Uri(input).AbsolutePath)
            : Path.GetFileNameWithoutExtension(input);
        if (string.IsNullOrEmpty(baseName)) baseName = "output";
        gifOutputPath = baseName + "_ascii.gif";
    }

    // Determine JSON output path if needed
    if (outputAsJson && string.IsNullOrEmpty(jsonOutputPath))
    {
        string baseName = isUrl
            ? Path.GetFileNameWithoutExtension(new Uri(input).AbsolutePath)
            : Path.GetFileNameWithoutExtension(input);
        if (string.IsNullOrEmpty(baseName)) baseName = "output";
        jsonOutputPath = baseName + "_ascii.json";
    }

    // Check if input is a JSON document (load and play it)
    // Supports both .json and .ndjson (streaming JSON Lines format)
    if (!isUrl && (input.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                   input.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase)))
    {
        return await HandleJsonDocument(input, speed, loop, cancellationToken);
    }

    // Show download progress for URLs
    string inputPath = input;
    MemoryStream? downloadedStream = null;
    if (isUrl)
    {
        Console.Error.Write("Downloading... ");
        long lastPercent = -1;
        downloadedStream = await UrlHelper.DownloadAsync(input, (downloaded, total) =>
        {
            if (total > 0)
            {
                long percent = (downloaded * 100) / total;
                if (percent != lastPercent)
                {
                    Console.Error.Write($"\rDownloading... {percent}%");
                    lastPercent = percent;
                }
            }
            else
            {
                Console.Error.Write($"\rDownloading... {downloaded / 1024}KB");
            }
        }, cancellationToken);
        Console.Error.WriteLine(" Done!");
    }

    // Determine character set
    string? characterSet = charset;
    if (string.IsNullOrEmpty(characterSet))
    {
        characterSet = (preset?.ToLowerInvariant()) switch
        {
            "simple" => CharacterMap.SimpleCharacterSet,
            "block" => CharacterMap.BlockCharacterSet,
            "classic" => CharacterMap.DefaultCharacterSet,
            _ => CharacterMap.ExtendedCharacterSet  // Extended is the default (better quality)
        };
    }

    // Determine render mode and get effective aspect ratio from saved calibration
    var renderMode = braille ? RenderMode.Braille
        : colorBlocks ? RenderMode.ColorBlocks
        : RenderMode.Ascii;
    float effectiveAspect = aspectRatio
        ?? savedCalibration?.GetAspectRatio(renderMode)
        ?? 0.5f;

    var options = new RenderOptions
    {
        Width = width,
        Height = height,
        CharacterAspectRatio = effectiveAspect,
        MaxWidth = maxWidth,
        MaxHeight = maxHeight,
        UseColor = !noColor || colorBlocks,
        Invert = !noInvert,
        ContrastPower = contrast,
        Gamma = gamma,
        CharacterSet = characterSet,
        AnimationSpeedMultiplier = speed,
        LoopCount = loop,
        FrameSampleRate = frameSample,
        EnableEdgeDetection = enableEdge,
        BackgroundThreshold = bgThreshold,
        DarkBackgroundThreshold = darkBgThreshold,
        AutoBackgroundSuppression = autoBg,
        UseParallelProcessing = !noParallel,
        EnableDithering = !noDither,
        EnableEdgeDirectionChars = !noEdgeChars,
        // Brightness thresholds for terminal optimization
        // A value of 0 for dark cutoff or 1 for light cutoff effectively disables that threshold
        DarkTerminalBrightnessThreshold = darkCutoff,
        LightTerminalBrightnessThreshold = lightCutoff
    };

    try
    {
        // Determine file extension (for format detection)
        string extension = isUrl
            ? "." + (UrlHelper.GetExtension(input) ?? "jpg")
            : Path.GetExtension(input);

        // Check if it's a GIF
        bool isGif = extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);

        // Parse mode option - override --blocks and --braille flags
        bool useBlocks = colorBlocks;
        bool useBraille = braille;
        bool useMatrix = matrix;
        TerminalProtocol? explicitProtocol = null;

        if (!string.IsNullOrEmpty(mode))
        {
            switch (mode.ToLowerInvariant())
            {
                case "ascii":
                    useBlocks = false;
                    useBraille = false;
                    useMatrix = false;
                    break;
                case "blocks":
                case "colorblocks":
                    useBlocks = true;
                    useBraille = false;
                    // Don't override useMatrix - allow combining --mode blocks --matrix
                    break;
                case "braille":
                    useBlocks = false;
                    useBraille = true;
                    useMatrix = false;
                    break;
                case "matrix":
                    // Don't override useBlocks - allow combining --matrix --blocks
                    useBraille = false;
                    useMatrix = true;
                    break;
                case "matrix-blocks":
                case "matrixblocks":
                    useBlocks = true;
                    useBraille = false;
                    useMatrix = true;
                    break;
                case "sixel":
                    explicitProtocol = TerminalProtocol.Sixel;
                    break;
                case "iterm2":
                case "iterm":
                    explicitProtocol = TerminalProtocol.ITerm2;
                    break;
                case "kitty":
                    explicitProtocol = TerminalProtocol.Kitty;
                    break;
                case "auto":
                    explicitProtocol = TerminalCapabilities.DetectBestProtocol();
                    break;
            }
        }

        // Helper to create MatrixOptions from CLI parameters
        MatrixOptions CreateMatrixOptions()
        {
            var opts = matrixFullColor ? MatrixOptions.FullColor : new MatrixOptions();
            opts.Density = Math.Clamp(matrixDensity, 0.1f, 2.0f);
            opts.SpeedMultiplier = Math.Clamp(matrixSpeed, 0.5f, 3.0f);
            opts.UseAsciiOnly = matrixAscii;
            opts.UseBlockMode = useBlocks && useMatrix; // Combine Matrix + Blocks
            opts.TargetFps = Math.Clamp(matrixFps, 5, 60);
            opts.CustomAlphabet = matrixAlphabet; // Custom character set (e.g., "HELLO" or "01")

            // Edge detection and persistence options
            opts.EnableEdgeDetection = matrixEdgeDetect;
            opts.EdgePersistence = Math.Clamp(matrixEdgePersist, 0.0f, 1.0f);
            opts.EnableBrightnessPersistence = matrixBrightPersist;

            if (!matrixFullColor && !string.IsNullOrEmpty(matrixColor))
            {
                opts.BaseColor = matrixColor.ToLowerInvariant() switch
                {
                    "green" => new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 255, 65, 255), // #00FF41 authentic Matrix
                    "red" => new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 50, 50, 255),
                    "blue" => new SixLabors.ImageSharp.PixelFormats.Rgba32(50, 100, 255, 255),
                    "amber" or "orange" => new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 176, 0, 255),
                    "cyan" => new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 255, 255, 255),
                    "purple" or "magenta" => new SixLabors.ImageSharp.PixelFormats.Rgba32(180, 0, 255, 255),
                    "white" => new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 255, 255, 255),
                    "pink" => new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 100, 200, 255),
                    _ when matrixColor.StartsWith('#') => ParseHexColor(matrixColor),
                    _ => new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 255, 65, 255) // Default authentic Matrix green
                };
            }
            return opts;
        }

        SixLabors.ImageSharp.PixelFormats.Rgba32 ParseHexColor(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6 &&
                byte.TryParse(hex[0..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                return new SixLabors.ImageSharp.PixelFormats.Rgba32(r, g, b, 255);
            }
            return new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 255, 0, 255); // Default green
        }

        // GIF output mode - render and save as animated GIF
        if (outputAsGif && !string.IsNullOrEmpty(gifOutputPath))
        {
            Console.WriteLine($"Rendering to GIF: {gifOutputPath}");
            // Pass isGif to apply default 10s limit for animated sources
            var gifOptions = CreateGifOptions(isAnimatedSource: isGif);
            using var gifWriter = new GifWriter(gifOptions);
            double totalElapsedMs = 0;

            // Create separate RenderOptions for GIF output if dimensions specified
            // Use higher MaxHeight for GIF output to preserve aspect ratio (not constrained by console)
            var gifRenderOptions = new RenderOptions
            {
                Width = gifWidth ?? options.Width,
                Height = gifHeight ?? options.Height,
                MaxWidth = gifWidth ?? options.MaxWidth,
                MaxHeight = gifHeight ?? Math.Max(200, options.MaxHeight),
                CharacterAspectRatio = options.CharacterAspectRatio,
                UseColor = options.UseColor,
                Invert = options.Invert,
                ContrastPower = options.ContrastPower,
                Gamma = options.Gamma,
                CharacterSet = options.CharacterSet,
                AnimationSpeedMultiplier = options.AnimationSpeedMultiplier,
                LoopCount = options.LoopCount,
                FrameSampleRate = options.FrameSampleRate,
                EnableDithering = options.EnableDithering,
                EnableEdgeDirectionChars = options.EnableEdgeDirectionChars,
                UseParallelProcessing = options.UseParallelProcessing,
                AutoBackgroundSuppression = options.AutoBackgroundSuppression
            };

            if (isGif)
            {
                // Render animated GIF frames using mode-specific methods
                if (useMatrix)
                {
                    var matrixOpts = CreateMatrixOptions();
                    using var renderer = new MatrixRenderer(gifRenderOptions, matrixOpts);
                    var matrixFrames = downloadedStream != null
                        ? renderer.RenderGifStream(downloadedStream)
                        : renderer.RenderGif(input);
                    var sampledFrames = frameSample > 1
                        ? matrixFrames.Where((f, i) => i % frameSample == 0).ToList()
                        : matrixFrames.ToList();

                    int frameIndex = 0;
                    int totalFrames = sampledFrames.Count;
                    foreach (var frame in sampledFrames)
                    {
                        if (ShouldStopAddingFrames(gifWriter, gifOptions, totalElapsedMs)) break;
                        int delayMs = frame.DelayMs * frameSample;
                        gifWriter.AddMatrixFrame(frame, delayMs, matrixOpts.UseBlockMode);
                        totalElapsedMs += delayMs;
                        frameIndex++;
                        Console.Write($"\rProcessing frame {frameIndex}/{totalFrames}...");
                    }
                }
                else if (useBraille)
                {
                    using var renderer = new BrailleRenderer(gifRenderOptions);
                    var brailleFrames = renderer.RenderGifFrames(input);
                    var sampledFrames = frameSample > 1
                        ? brailleFrames.Where((f, i) => i % frameSample == 0).ToList()
                        : brailleFrames.ToList();

                    int frameIndex = 0;
                    int totalFrames = sampledFrames.Count;
                    foreach (var frame in sampledFrames)
                    {
                        if (ShouldStopAddingFrames(gifWriter, gifOptions, totalElapsedMs)) break;
                        int delayMs = frame.DelayMs * frameSample;
                        gifWriter.AddBrailleFrame(frame, delayMs);
                        totalElapsedMs += delayMs;
                        frameIndex++;
                        Console.Write($"\rProcessing frame {frameIndex}/{totalFrames}...");
                    }
                }
                else if (useBlocks)
                {
                    using var renderer = new ColorBlockRenderer(gifRenderOptions);
                    var blockFrames = downloadedStream != null
                        ? renderer.RenderGifFrames(downloadedStream)
                        : renderer.RenderGifFrames(input);
                    var sampledFrames = frameSample > 1
                        ? blockFrames.Where((f, i) => i % frameSample == 0).ToList()
                        : blockFrames.ToList();

                    int frameIndex = 0;
                    int totalFrames = sampledFrames.Count;
                    foreach (var frame in sampledFrames)
                    {
                        if (ShouldStopAddingFrames(gifWriter, gifOptions, totalElapsedMs)) break;
                        int delayMs = frame.DelayMs * frameSample;
                        gifWriter.AddColorBlockFrame(frame, delayMs);
                        totalElapsedMs += delayMs;
                        frameIndex++;
                        Console.Write($"\rProcessing frame {frameIndex}/{totalFrames}...");
                    }
                }
                else
                {
                    using var renderer = new AsciiRenderer(gifRenderOptions);
                    var asciiFrames = downloadedStream != null
                        ? renderer.RenderGifStream(downloadedStream)
                        : renderer.RenderGif(input);
                    var sampledFrames = frameSample > 1
                        ? asciiFrames.Where((f, i) => i % frameSample == 0).ToList()
                        : asciiFrames.ToList();

                    int frameIndex = 0;
                    int totalFrames = sampledFrames.Count;
                    foreach (var frame in sampledFrames)
                    {
                        if (ShouldStopAddingFrames(gifWriter, gifOptions, totalElapsedMs)) break;
                        int delayMs = frame.DelayMs * frameSample;
                        gifWriter.AddFrame(frame, delayMs);
                        totalElapsedMs += delayMs;
                        frameIndex++;
                        Console.Write($"\rProcessing frame {frameIndex}/{totalFrames}...");
                    }
                }
                Console.WriteLine(" Done!");
                if (gifOptions.MaxLengthSeconds.HasValue && totalElapsedMs / 1000.0 >= gifOptions.MaxLengthSeconds.Value)
                    Console.WriteLine($"(Limited to {gifOptions.MaxLengthSeconds.Value}s - use --gif-length 0 for unlimited)");
            }
            else
            {
                // Static image - single frame (or animated Matrix for static images)
                if (useMatrix)
                {
                    // For static images, generate Matrix animation
                    var matrixOpts = CreateMatrixOptions();
                    using var renderer = new MatrixRenderer(gifRenderOptions, matrixOpts);
                    using var img = downloadedStream != null
                        ? Image.Load<Rgba32>(downloadedStream)
                        : Image.Load<Rgba32>(input);

                    // Calculate frame count from existing options:
                    // --gif-frames takes priority, then --gif-length, then default 3 seconds
                    int targetFps = matrixOpts.TargetFps;
                    int totalFrames = gifFrames
                        ?? (gifLength.HasValue ? (int)(gifLength.Value * targetFps) : targetFps * 3);

                    var matrixFrames = new List<MatrixFrame>();
                    for (int f = 0; f < totalFrames; f++)
                    {
                        matrixFrames.Add(renderer.RenderImage(img));
                    }

                    int frameIndex = 0;
                    foreach (var frame in matrixFrames)
                    {
                        gifWriter.AddMatrixFrame(frame, frame.DelayMs, matrixOpts.UseBlockMode);
                        frameIndex++;
                        Console.Write($"\rGenerating Matrix frames {frameIndex}/{matrixFrames.Count}...");
                    }
                }
                else if (useBraille)
                {
                    using var renderer = new BrailleRenderer(gifRenderOptions);
                    var frame = downloadedStream != null
                        ? renderer.RenderImageToFrame(Image.Load<Rgba32>(downloadedStream))
                        : renderer.RenderFileToFrame(input);
                    gifWriter.AddBrailleFrame(frame, 1000);
                }
                else if (useBlocks)
                {
                    using var renderer = new ColorBlockRenderer(gifRenderOptions);
                    var frame = downloadedStream != null
                        ? renderer.RenderStreamToFrame(downloadedStream)
                        : renderer.RenderFileToFrame(input);
                    gifWriter.AddColorBlockFrame(frame, 1000);
                }
                else
                {
                    using var renderer = new AsciiRenderer(gifRenderOptions);
                    var frame = downloadedStream != null
                        ? renderer.RenderStream(downloadedStream)
                        : renderer.RenderFile(input);
                    gifWriter.AddFrame(frame, 1000);
                }
            }

            Console.Write("Saving GIF...");
            await gifWriter.SaveAsync(gifOutputPath, cancellationToken);
            Console.WriteLine($" Saved to {gifOutputPath}");
            downloadedStream?.Dispose();
            return 0;
        }

        // If using a native image protocol (sixel, iterm2, kitty), render with UnifiedRenderer
        if (explicitProtocol.HasValue &&
            explicitProtocol.Value is TerminalProtocol.Sixel or TerminalProtocol.ITerm2 or TerminalProtocol.Kitty)
        {
            using var renderer = new UnifiedRenderer(explicitProtocol.Value, options);
            string result;
            if (downloadedStream != null)
            {
                downloadedStream.Position = 0;
                result = renderer.RenderStream(downloadedStream);
            }
            else
            {
                result = renderer.RenderFile(input);
            }

            if (outputFile != null)
            {
                // Native protocols don't write well to files, warn user
                Console.Error.WriteLine("Warning: Native image protocols (sixel, iterm2, kitty) are designed for terminal display.");
                File.WriteAllText(outputFile, result);
                Console.WriteLine($"Written to {outputFile}");
            }
            else
            {
                Console.Write(result);
            }
            downloadedStream?.Dispose();
            return 0;
        }

        // Matrix mode - digital rain effect
        if (useMatrix)
        {
            var matrixOpts = CreateMatrixOptions();

            if (isGif && !noAnimate)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                var player = new ResizableAnimationPlayer(
                    renderFrames: (maxW, maxH) =>
                    {
                        var renderOptions = options.Clone();
                        renderOptions.MaxWidth = maxW;
                        renderOptions.MaxHeight = maxH;
                        using var renderer = new MatrixRenderer(renderOptions, matrixOpts);
                        if (downloadedStream != null)
                        {
                            var ms = new MemoryStream(downloadedStream.ToArray());
                            return renderer.RenderGifStream(ms);
                        }
                        return renderer.RenderGif(input);
                    },
                    explicitWidth: width,
                    explicitHeight: height,
                    loopCount: loop,
                    useAltScreen: !noAltScreen,
                    targetFps: framerate,
                    showStatus: showStatus,
                    fileName: input,
                    renderMode: "Matrix"
                );

                await player.PlayAsync(cts.Token);
            }
            else
            {
                // Static image or animated display
                using var renderer = new MatrixRenderer(options, matrixOpts);
                MatrixFrame frame;
                if (downloadedStream != null)
                {
                    downloadedStream.Position = 0;
                    frame = renderer.RenderStream(downloadedStream);
                }
                else
                {
                    frame = renderer.RenderFile(input);
                }

                if (outputFile != null)
                {
                    File.WriteAllText(outputFile, frame.Content);
                    Console.WriteLine($"Written to {outputFile}");
                }
                else
                {
                    // For static images with Matrix, run an animation loop
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    using var img = downloadedStream != null
                        ? Image.Load<Rgba32>(downloadedStream)
                        : Image.Load<Rgba32>(input);

                    // Use alternate screen if not disabled
                    if (!noAltScreen)
                        Console.Write("\x1b[?1049h");

                    try
                    {
                        int frameCount = 0;
                        while (!cts.Token.IsCancellationRequested && (loop == 0 || frameCount < loop * 100))
                        {
                            var matrixFrame = renderer.RenderImage(img);

                            // Move cursor to home and output frame
                            Console.Write("\x1b[H");
                            Console.Write(matrixFrame.Content);

                            if (showStatus)
                            {
                                var lines = matrixFrame.Content.Split('\n');
                                var frameHeight = lines.Length;
                                var frameWidth = lines.Length > 0 ? GetVisibleLength(lines[0]) : 0;
                                Console.Write("\x1b[0m\n");
                                DisplayStatusLine(input, frameCount, null, frameWidth, frameHeight, "Matrix", !noColor);
                            }

                            await Task.Delay(matrixFrame.DelayMs, cts.Token);
                            frameCount++;
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        if (!noAltScreen)
                            Console.Write("\x1b[?1049l");
                        Console.Write("\x1b[0m");
                    }
                }
            }
        }
        // Use color blocks mode for high-fidelity output
        else if (useBlocks)
        {
            if (isGif && !noAnimate)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                var player = new ResizableAnimationPlayer(
                    renderFrames: (maxW, maxH) =>
                    {
                        var renderOptions = options.Clone();
                        renderOptions.MaxWidth = maxW;
                        renderOptions.MaxHeight = maxH;
                        using var renderer = new ColorBlockRenderer(renderOptions);
                        if (downloadedStream != null)
                        {
                            var ms = new MemoryStream(downloadedStream.ToArray());
                            return renderer.RenderGifStream(ms);
                        }
                        return renderer.RenderGif(input);
                    },
                    explicitWidth: width,
                    explicitHeight: height,
                    loopCount: loop,
                    useAltScreen: !noAltScreen,
                    targetFps: framerate,
                    showStatus: showStatus,
                    fileName: input,
                    renderMode: "Blocks"
                );

                await player.PlayAsync(cts.Token);
            }
            else
            {
                // Static image rendering
                using var blockRenderer = new ColorBlockRenderer(options);
                ColorBlockFrame frame;
                if (downloadedStream != null)
                {
                    downloadedStream.Position = 0;
                    frame = blockRenderer.RenderStreamToFrame(downloadedStream);
                }
                else
                {
                    frame = blockRenderer.RenderFileToFrame(input);
                }
                if (outputFile != null)
                {
                    File.WriteAllText(outputFile, frame.Content);
                    Console.WriteLine($"Written to {outputFile}");
                }
                else
                {
                    Console.WriteLine(frame.Content);
                    Console.Write("\x1b[0m"); // Reset colors
                    if (showStatus)
                    {
                        var lines = frame.Content.Split('\n');
                        var frameHeight = lines.Length;
                        var frameWidth = lines.Length > 0 ? GetVisibleLength(lines[0]) : 0;
                        DisplayStatusLine(input, null, null, frameWidth, frameHeight, "Blocks", !noColor);
                    }
                }

                // Save as JSON document if requested
                if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
                {
                    var doc = ConsoleImageDocument.FromColorBlockFrames(new[] { frame }, options, input);
                    await doc.SaveAsync(jsonOutputPath, cancellationToken);
                    Console.Error.WriteLine($"Saved JSON document to: {jsonOutputPath}");
                }
            }
        }
        else if (useBraille)
        {
            // Braille rendering (2x4 dots per cell)
            if (isGif && !noAnimate)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                var player = new ResizableAnimationPlayer(
                    renderFrames: (maxW, maxH) =>
                    {
                        var renderOptions = options.Clone();
                        renderOptions.MaxWidth = maxW;
                        renderOptions.MaxHeight = maxH;
                        using var renderer = new BrailleRenderer(renderOptions);
                        if (downloadedStream != null)
                        {
                            var ms = new MemoryStream(downloadedStream.ToArray());
                            using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(ms);
                            return renderer.RenderGif(input); // BrailleRenderer doesn't have RenderGifStream yet
                        }
                        return renderer.RenderGif(input);
                    },
                    explicitWidth: width,
                    explicitHeight: height,
                    loopCount: loop,
                    useAltScreen: !noAltScreen,
                    targetFps: framerate,
                    showStatus: showStatus,
                    fileName: input,
                    renderMode: "Braille"
                );

                await player.PlayAsync(cts.Token);
            }
            else
            {
                using var brailleRenderer = new BrailleRenderer(options);
                BrailleFrame frame;
                if (downloadedStream != null)
                {
                    downloadedStream.Position = 0;
                    using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(downloadedStream);
                    frame = brailleRenderer.RenderImageToFrame(img);
                }
                else
                {
                    frame = brailleRenderer.RenderFileToFrame(input);
                }
                if (outputFile != null)
                {
                    File.WriteAllText(outputFile, frame.Content);
                    Console.WriteLine($"Written to {outputFile}");
                }
                else
                {
                    Console.WriteLine(frame.Content);
                    Console.Write("\x1b[0m");
                    if (showStatus)
                    {
                        var lines = frame.Content.Split('\n');
                        var frameHeight = lines.Length;
                        var frameWidth = lines.Length > 0 ? GetVisibleLength(lines[0]) : 0;
                        DisplayStatusLine(input, null, null, frameWidth, frameHeight, "Braille", !noColor);
                    }
                }

                // Save as JSON document if requested
                if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
                {
                    var doc = ConsoleImageDocument.FromBrailleFrames(new[] { frame }, options, input);
                    await doc.SaveAsync(jsonOutputPath, cancellationToken);
                    Console.Error.WriteLine($"Saved JSON document to: {jsonOutputPath}");
                }
            }
        }
        else
        {
            // Standard ASCII rendering
            if (isGif && !noAnimate)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                // Determine brightness thresholds based on terminal mode (Invert)
                float? effectiveDarkThreshold = !noInvert ? options.DarkTerminalBrightnessThreshold : null;
                float? effectiveLightThreshold = noInvert ? options.LightTerminalBrightnessThreshold : null;

                var player = new ResizableAnimationPlayer(
                    renderFrames: (maxW, maxH) =>
                    {
                        var renderOptions = options.Clone();
                        renderOptions.MaxWidth = maxW;
                        renderOptions.MaxHeight = maxH;
                        using var renderer = new AsciiRenderer(renderOptions);
                        IReadOnlyList<AsciiFrame> asciiFrames;
                        if (downloadedStream != null)
                        {
                            var ms = new MemoryStream(downloadedStream.ToArray());
                            asciiFrames = renderer.RenderGifStream(ms);
                        }
                        else
                        {
                            asciiFrames = renderer.RenderGif(input);
                        }
                        // Convert AsciiFrames to IAnimationFrame using adapter
                        return asciiFrames.Select(f => new AsciiFrameAdapter(f, !noColor, effectiveDarkThreshold, effectiveLightThreshold)).ToList<IAnimationFrame>();
                    },
                    explicitWidth: width,
                    explicitHeight: height,
                    loopCount: loop,
                    useAltScreen: !noAltScreen,
                    targetFps: framerate,
                    showStatus: showStatus,
                    fileName: input,
                    renderMode: "ASCII"
                );

                await player.PlayAsync(cts.Token);
            }
            else if (isGif)
            {
                // Render all GIF frames as static output
                using var renderer = new AsciiRenderer(options);
                IReadOnlyList<AsciiFrame> frames;
                if (downloadedStream != null)
                {
                    downloadedStream.Position = 0;
                    frames = renderer.RenderGifStream(downloadedStream);
                }
                else
                {
                    frames = renderer.RenderGif(input);
                }

                if (outputFile != null)
                {
                    // Write all frames to file
                    using var writer = new StreamWriter(outputFile);
                    for (int i = 0; i < frames.Count; i++)
                    {
                        if (i > 0)
                        {
                            writer.WriteLine();
                            writer.WriteLine($"--- Frame {i + 1}/{frames.Count} (delay: {frames[i].DelayMs}ms) ---");
                            writer.WriteLine();
                        }
                        writer.WriteLine(frames[i].ToString());
                    }
                    Console.WriteLine($"Wrote {frames.Count} frames to {outputFile}");
                }
                else
                {
                    // Output first frame only
                    OutputFrame(frames[0], !noColor, null, options);
                    if (frames.Count > 1)
                    {
                        Console.WriteLine($"\n(GIF has {frames.Count} frames - use --animate to play)");
                    }
                }
            }
            else
            {
                // Render single image
                using var renderer = new AsciiRenderer(options);
                AsciiFrame frame;
                if (downloadedStream != null)
                {
                    downloadedStream.Position = 0;
                    frame = renderer.RenderStream(downloadedStream);
                }
                else
                {
                    frame = renderer.RenderFile(input);
                }
                // Determine brightness thresholds based on terminal mode (Invert)
                float? effectiveDarkThreshold = !noInvert ? options.DarkTerminalBrightnessThreshold : null;
                float? effectiveLightThreshold = noInvert ? options.LightTerminalBrightnessThreshold : null;

                if (jsonOutput)
                {
                    OutputJson($@"{{
  ""success"": true,
  ""type"": ""image"",
  ""width"": {frame.Width},
  ""height"": {frame.Height},
  ""content"": ""{JsonEscape(frame.ToString())}"",
  ""ansiContent"": ""{JsonEscape(frame.ToAnsiString(effectiveDarkThreshold, effectiveLightThreshold))}""
}}");
                }
                else
                {
                    OutputFrame(frame, !noColor, outputFile, options);
                    if (showStatus && outputFile == null)
                    {
                        DisplayStatusLine(input, null, null, frame.Width, frame.Height, "ASCII", !noColor);
                    }
                }

                // Save as JSON document if requested
                if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
                {
                    var doc = ConsoleImageDocument.FromAsciiFrames(new[] { frame }, options, input);
                    await doc.SaveAsync(jsonOutputPath, cancellationToken);
                    Console.Error.WriteLine($"Saved JSON document to: {jsonOutputPath}");
                }
            }
        }
        downloadedStream?.Dispose();
        return 0;
    }
    catch (Exception ex)
    {
        downloadedStream?.Dispose();
        if (jsonOutput)
        {
            OutputJson($"{{\"success\":false,\"error\":\"{JsonEscape(ex.Message)}\"}}");
            return 1;
        }
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
});

return await rootCommand.Parse(args).InvokeAsync();

static void OutputFrame(AsciiFrame frame, bool useColor, string? outputPath, RenderOptions options)
{
    // Determine brightness thresholds based on terminal mode (Invert)
    float? darkThreshold = options.Invert ? options.DarkTerminalBrightnessThreshold : null;
    float? lightThreshold = !options.Invert ? options.LightTerminalBrightnessThreshold : null;

    string result = useColor ? frame.ToAnsiString(darkThreshold, lightThreshold) : frame.ToString();

    if (outputPath != null)
    {
        File.WriteAllText(outputPath, frame.ToString()); // Don't write ANSI codes to file
        Console.WriteLine($"Written to {outputPath}");
    }
    else
    {
        Console.WriteLine(result);
    }
}

/// <summary>
/// Load and play a ConsoleImageDocument JSON file
/// </summary>
static async Task<int> HandleJsonDocument(string path, float speed, int loop, CancellationToken ct)
{
    try
    {
        Console.Error.WriteLine($"Loading document: {path}");
        var doc = await ConsoleImageDocument.LoadAsync(path, ct);

        // Display document info
        Console.Error.WriteLine($"Loaded: {doc.RenderMode} mode, {doc.FrameCount} frame(s)");
        if (doc.IsAnimated)
        {
            Console.Error.WriteLine($"Duration: {doc.TotalDurationMs}ms");
        }

        // Override settings if specified
        float effectiveSpeed = speed != 1.0f ? speed : doc.Settings.AnimationSpeedMultiplier;
        int effectiveLoop = loop != 0 ? loop : doc.Settings.LoopCount;

        // Play the document
        using var player = new DocumentPlayer(doc, effectiveSpeed, effectiveLoop);

        if (doc.IsAnimated)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await player.PlayAsync(cts.Token);
        }
        else
        {
            player.Display();
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error loading document: {ex.Message}");
        return 1;
    }
}

/// <summary>
/// Display status line below the rendered output with file info, resolution, etc.
/// </summary>
static void DisplayStatusLine(
    string? fileName,
    int? sourceWidth, int? sourceHeight,
    int outputWidth, int outputHeight,
    string renderMode,
    bool useColor,
    int? currentFrame = null,
    int? totalFrames = null)
{
    int statusWidth = 120;
    try { statusWidth = Console.WindowWidth - 1; } catch { }

    var statusLine = new StatusLine(statusWidth, useColor);
    var info = new StatusLine.StatusInfo
    {
        FileName = fileName,
        SourceWidth = sourceWidth,
        SourceHeight = sourceHeight,
        OutputWidth = outputWidth,
        OutputHeight = outputHeight,
        RenderMode = renderMode,
        CurrentFrame = currentFrame,
        TotalFrames = totalFrames
    };

    Console.WriteLine(statusLine.Render(info));
}

/// <summary>
/// Get visible character count, excluding ANSI escape sequences.
/// </summary>
static int GetVisibleLength(string line)
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
