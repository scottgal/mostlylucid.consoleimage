// Video to ASCII CLI - FFmpeg-based video player
// Streams video files as ASCII art with intelligent frame sampling

using System.CommandLine;
using ConsoleImage.Core;
using ConsoleImage.Video.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;

// Enable ANSI escape sequence processing on Windows
ConsoleHelper.EnableAnsiSupport();

// Load saved calibration if exists
var savedCalibration = CalibrationHelper.Load();

// Create root command
var rootCommand = new RootCommand("Play video files as ASCII art using FFmpeg");

// Input file argument (optional for --calibrate mode)
// Use string instead of FileInfo for better AOT compatibility
var inputArg = new Argument<string?>("input")
{
    Description = "Path to the video file to play",
    Arity = ArgumentArity.ZeroOrOne
};

// Dimension options
var widthOption = new Option<int?>("--width") { Description = "Output width in characters" };
widthOption.Aliases.Add("-w");

var heightOption = new Option<int?>("--height") { Description = "Output height in characters" };
heightOption.Aliases.Add("-h");

// Detect console window size for defaults
var defaultMaxWidth = 120;
var defaultMaxHeight = 40;
try
{
    if (Console.WindowWidth > 0)
        defaultMaxWidth = Console.WindowWidth - 1;
    if (Console.WindowHeight > 0)
        defaultMaxHeight = Console.WindowHeight - 2;
}
catch
{
}

var maxWidthOption = new Option<int>("--max-width") { Description = "Maximum output width" };
maxWidthOption.DefaultValueFactory = _ => defaultMaxWidth;

var maxHeightOption = new Option<int>("--max-height") { Description = "Maximum output height" };
maxHeightOption.DefaultValueFactory = _ => defaultMaxHeight;

// Time range options
var startOption = new Option<double?>("--start") { Description = "Start time in seconds" };
startOption.Aliases.Add("-ss");

var endOption = new Option<double?>("--end") { Description = "End time in seconds" };
endOption.Aliases.Add("-to");

var durationOption = new Option<double?>("--duration") { Description = "Duration to play in seconds" };
durationOption.Aliases.Add("-t");

// Playback options
var speedOption = new Option<float>("--speed") { Description = "Playback speed multiplier" };
speedOption.DefaultValueFactory = _ => 1.0f;
speedOption.Aliases.Add("-s");

var loopOption = new Option<int>("--loop") { Description = "Number of loops (0 = infinite)" };
loopOption.DefaultValueFactory = _ => 1;
loopOption.Aliases.Add("-l");

var fpsOption = new Option<double?>("--fps") { Description = "Target framerate (default: video's native FPS)" };
fpsOption.Aliases.Add("-r");

// Frame sampling options
var frameStepOption = new Option<int>("--frame-step")
    { Description = "Frame step (1 = every frame, 2 = every 2nd, etc.)" };
frameStepOption.DefaultValueFactory = _ => 1;
frameStepOption.Aliases.Add("-f");

var samplingOption = new Option<string>("--sampling")
    { Description = "Sampling strategy: uniform, keyframe, scene, adaptive" };
samplingOption.DefaultValueFactory = _ => "uniform";

var sceneThresholdOption = new Option<double>("--scene-threshold")
    { Description = "Scene detection threshold (0.0-1.0, lower = more sensitive)" };
sceneThresholdOption.DefaultValueFactory = _ => 0.4;

// Rendering options
var colorBlocksOption = new Option<bool>("--blocks")
    { Description = "Use colored Unicode blocks for high-fidelity output (requires 24-bit color terminal)" };
colorBlocksOption.Aliases.Add("-b");

var brailleOption = new Option<bool>("--braille")
    { Description = "Use braille characters for ultra-high resolution (2x4 dots per cell)" };
brailleOption.Aliases.Add("-B");

var noColorOption = new Option<bool>("--no-color") { Description = "Disable colored output" };

var contrastOption = new Option<float>("--contrast")
    { Description = "Contrast enhancement (1.0 = none, higher = more contrast)" };
contrastOption.DefaultValueFactory = _ => 2.5f;

var gammaOption = new Option<float>("--gamma")
    { Description = "Gamma correction (lower = brighter, higher = darker). Default: 0.65" };
gammaOption.DefaultValueFactory = _ => 0.65f;
gammaOption.Aliases.Add("-g");

var charAspectOption = new Option<float?>("--char-aspect")
    { Description = "Character aspect ratio (width/height). Uses saved calibration or 0.5 if not set." };

var charsetOption = new Option<string?>("--charset") { Description = "Custom character set (light to dark)" };

var presetOption = new Option<string?>("--preset")
    { Description = "Character set preset: extended (default), simple, block, classic" };
presetOption.Aliases.Add("-p");

// Performance options
var bufferOption = new Option<int>("--buffer") { Description = "Number of frames to buffer ahead (2-10)" };
bufferOption.DefaultValueFactory = _ => 3;

var noHwAccelOption = new Option<bool>("--no-hwaccel") { Description = "Disable hardware acceleration" };

var noAltScreenOption = new Option<bool>("--no-alt-screen") { Description = "Disable alternate screen buffer" };

// FFmpeg path option
var ffmpegPathOption = new Option<string?>("--ffmpeg-path") { Description = "Path to FFmpeg executable or directory" };

// Info option
var infoOption = new Option<bool>("--info") { Description = "Show video info and exit" };
infoOption.Aliases.Add("-i");

// Calibrate option - shows a test pattern to verify aspect ratio
var calibrateOption = new Option<bool>("--calibrate")
    { Description = "Display aspect ratio calibration pattern (should show a circle)" };

// Save calibration option
var saveCalibrationOption = new Option<bool>("--save")
    { Description = "Save current --char-aspect to calibration.json (use with --calibrate)" };

// Status line option - shows progress, timing, file info below the video
var statusOption = new Option<bool>("--status")
    { Description = "Show status line below output with progress, timing, file info" };
statusOption.Aliases.Add("-S");

// Unified output option - auto-detects format from extension
// .gif -> animated GIF, .json/.ndjson -> JSON document, other -> text
var outputOption = new Option<string?>("--output")
    { Description = "Output file path. Format auto-detected: .gif for animated GIF, .json for JSON document" };
outputOption.Aliases.Add("-o");

// GIF-specific options (apply when output is .gif)
var gifFontSizeOption = new Option<int>("--gif-font-size")
    { Description = "Font size for GIF output (smaller = smaller file)", DefaultValueFactory = _ => 10 };
var gifScaleOption = new Option<float>("--gif-scale")
    { Description = "Scale factor for GIF output (0.5 = half size)", DefaultValueFactory = _ => 1.0f };
var gifColorsOption = new Option<int>("--gif-colors")
    { Description = "Max colors in GIF palette (16-256, lower = smaller file)", DefaultValueFactory = _ => 64 };
var gifFpsOption = new Option<int>("--gif-fps")
    { Description = "Target FPS for GIF output (lower = smaller file)", DefaultValueFactory = _ => 15 };
var gifLengthOption = new Option<double?>("--gif-length") { Description = "Max length of GIF output in seconds" };
var gifFramesOption = new Option<int?>("--gif-frames") { Description = "Max number of frames for GIF output" };
var gifWidthOption = new Option<int?>("--gif-width")
    { Description = "GIF output width in characters (overrides -w for GIF)" };
var gifHeightOption = new Option<int?>("--gif-height")
    { Description = "GIF output height in characters (overrides -h for GIF)" };

// Raw/extract mode - output actual video frames as GIF (no ASCII rendering)
var rawOption = new Option<bool>("--raw") { Description = "Extract raw video frames as GIF (no ASCII rendering)" };
rawOption.Aliases.Add("--extract");
var rawWidthOption = new Option<int?>("--raw-width")
    { Description = "Width for raw GIF output in pixels (default: 320)" };
var rawHeightOption = new Option<int?>("--raw-height")
    { Description = "Height for raw GIF output in pixels (auto-calculated if not set)" };
var smartKeyframesOption = new Option<bool>("--smart-keyframes")
    { Description = "Use smart scene detection for keyframe extraction (with --raw)" };
smartKeyframesOption.Aliases.Add("--smart");

// FFmpeg download options
var noAutoDownloadOption = new Option<bool>("--no-ffmpeg-download")
    { Description = "Don't auto-download FFmpeg, prompt instead" };
var ffmpegYesOption = new Option<bool>("--yes") { Description = "Auto-confirm FFmpeg download without prompting" };
ffmpegYesOption.Aliases.Add("-y");

// Options matching consoleimage for consistency
var noInvertOption = new Option<bool>("--no-invert")
    { Description = "Don't invert output (for light terminal backgrounds)" };

var edgeOption = new Option<bool>("--edge") { Description = "Enable edge detection to enhance foreground visibility" };
edgeOption.Aliases.Add("-e");

var bgThresholdOption = new Option<float?>("--bg-threshold")
    { Description = "Background suppression threshold (0.0-1.0). Pixels above this brightness are suppressed." };

var darkBgThresholdOption = new Option<float?>("--dark-bg-threshold")
    { Description = "Dark background suppression threshold (0.0-1.0). Pixels below this brightness are suppressed." };

var autoBgOption = new Option<bool>("--auto-bg") { Description = "Automatically detect and suppress background" };

var noParallelOption = new Option<bool>("--no-parallel") { Description = "Disable parallel processing" };

var noDitherOption = new Option<bool>("--no-dither") { Description = "Disable Floyd-Steinberg dithering" };

var noEdgeCharsOption = new Option<bool>("--no-edge-chars")
    { Description = "Disable directional edge characters (/ \\ | -)" };

var jsonOption = new Option<bool>("--json")
    { Description = "Output as JSON (for LLM tool calls and programmatic use)" };
jsonOption.Aliases.Add("-j");

var darkCutoffOption = new Option<float?>("--dark-cutoff")
    { Description = "Dark terminal optimization: skip colors below this brightness (0.0-1.0). Disabled by default." };

var lightCutoffOption = new Option<float?>("--light-cutoff")
    { Description = "Light terminal optimization: skip colors above this brightness (0.0-1.0). Disabled by default." };

var modeOption = new Option<string?>("--mode")
    { Description = "Rendering mode: ascii, blocks, braille, sixel, iterm2, kitty, auto, list" };
modeOption.Aliases.Add("-m");

// Add all options
rootCommand.Arguments.Add(inputArg);
rootCommand.Options.Add(widthOption);
rootCommand.Options.Add(heightOption);
rootCommand.Options.Add(maxWidthOption);
rootCommand.Options.Add(maxHeightOption);
rootCommand.Options.Add(startOption);
rootCommand.Options.Add(endOption);
rootCommand.Options.Add(durationOption);
rootCommand.Options.Add(speedOption);
rootCommand.Options.Add(loopOption);
rootCommand.Options.Add(fpsOption);
rootCommand.Options.Add(frameStepOption);
rootCommand.Options.Add(samplingOption);
rootCommand.Options.Add(sceneThresholdOption);
rootCommand.Options.Add(colorBlocksOption);
rootCommand.Options.Add(brailleOption);
rootCommand.Options.Add(noColorOption);
rootCommand.Options.Add(contrastOption);
rootCommand.Options.Add(gammaOption);
rootCommand.Options.Add(charAspectOption);
rootCommand.Options.Add(charsetOption);
rootCommand.Options.Add(presetOption);
rootCommand.Options.Add(bufferOption);
rootCommand.Options.Add(noHwAccelOption);
rootCommand.Options.Add(noAltScreenOption);
rootCommand.Options.Add(ffmpegPathOption);
rootCommand.Options.Add(infoOption);
rootCommand.Options.Add(calibrateOption);
rootCommand.Options.Add(saveCalibrationOption);
rootCommand.Options.Add(statusOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(gifFontSizeOption);
rootCommand.Options.Add(gifScaleOption);
rootCommand.Options.Add(gifColorsOption);
rootCommand.Options.Add(gifFpsOption);
rootCommand.Options.Add(gifLengthOption);
rootCommand.Options.Add(gifFramesOption);
rootCommand.Options.Add(gifWidthOption);
rootCommand.Options.Add(gifHeightOption);
rootCommand.Options.Add(rawOption);
rootCommand.Options.Add(rawWidthOption);
rootCommand.Options.Add(rawHeightOption);
rootCommand.Options.Add(smartKeyframesOption);
rootCommand.Options.Add(noAutoDownloadOption);
rootCommand.Options.Add(ffmpegYesOption);
rootCommand.Options.Add(noInvertOption);
rootCommand.Options.Add(edgeOption);
rootCommand.Options.Add(bgThresholdOption);
rootCommand.Options.Add(darkBgThresholdOption);
rootCommand.Options.Add(autoBgOption);
rootCommand.Options.Add(noParallelOption);
rootCommand.Options.Add(noDitherOption);
rootCommand.Options.Add(noEdgeCharsOption);
rootCommand.Options.Add(jsonOption);
rootCommand.Options.Add(darkCutoffOption);
rootCommand.Options.Add(lightCutoffOption);
rootCommand.Options.Add(modeOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var inputPath = parseResult.GetValue(inputArg);
    var input = !string.IsNullOrEmpty(inputPath) ? new FileInfo(inputPath) : null;
    var width = parseResult.GetValue(widthOption);
    var height = parseResult.GetValue(heightOption);
    var maxWidth = parseResult.GetValue(maxWidthOption);
    var maxHeight = parseResult.GetValue(maxHeightOption);
    var start = parseResult.GetValue(startOption);
    var end = parseResult.GetValue(endOption);
    var duration = parseResult.GetValue(durationOption);
    var speed = parseResult.GetValue(speedOption);
    var loop = parseResult.GetValue(loopOption);
    var fps = parseResult.GetValue(fpsOption);
    var frameStep = parseResult.GetValue(frameStepOption);
    var sampling = parseResult.GetValue(samplingOption);
    var sceneThreshold = parseResult.GetValue(sceneThresholdOption);
    var useBlocks = parseResult.GetValue(colorBlocksOption);
    var useBraille = parseResult.GetValue(brailleOption);
    var noColor = parseResult.GetValue(noColorOption);
    var contrast = parseResult.GetValue(contrastOption);
    var gamma = parseResult.GetValue(gammaOption);
    var charAspect = parseResult.GetValue(charAspectOption);
    var charset = parseResult.GetValue(charsetOption);
    var preset = parseResult.GetValue(presetOption);
    var buffer = parseResult.GetValue(bufferOption);
    var noHwAccel = parseResult.GetValue(noHwAccelOption);
    var noAltScreen = parseResult.GetValue(noAltScreenOption);
    var ffmpegPath = parseResult.GetValue(ffmpegPathOption);
    var showInfo = parseResult.GetValue(infoOption);
    var calibrate = parseResult.GetValue(calibrateOption);
    var saveCalibration = parseResult.GetValue(saveCalibrationOption);
    var showStatus = parseResult.GetValue(statusOption);
    var gifFontSize = parseResult.GetValue(gifFontSizeOption);
    var gifScale = parseResult.GetValue(gifScaleOption);
    var gifColors = parseResult.GetValue(gifColorsOption);
    var gifFps = parseResult.GetValue(gifFpsOption);
    var gifLength = parseResult.GetValue(gifLengthOption);
    var gifFrames = parseResult.GetValue(gifFramesOption);
    var gifWidth = parseResult.GetValue(gifWidthOption);
    var gifHeight = parseResult.GetValue(gifHeightOption);
    var rawMode = parseResult.GetValue(rawOption);
    var rawWidth = parseResult.GetValue(rawWidthOption);
    var rawHeight = parseResult.GetValue(rawHeightOption);
    var smartKeyframes = parseResult.GetValue(smartKeyframesOption);
    var noAutoDownload = parseResult.GetValue(noAutoDownloadOption);
    var autoConfirmDownload = parseResult.GetValue(ffmpegYesOption);
    var output = parseResult.GetValue(outputOption);
    var noInvert = parseResult.GetValue(noInvertOption);
    var enableEdge = parseResult.GetValue(edgeOption);
    var bgThreshold = parseResult.GetValue(bgThresholdOption);
    var darkBgThreshold = parseResult.GetValue(darkBgThresholdOption);
    var autoBg = parseResult.GetValue(autoBgOption);
    var noParallel = parseResult.GetValue(noParallelOption);
    var noDither = parseResult.GetValue(noDitherOption);
    var noEdgeChars = parseResult.GetValue(noEdgeCharsOption);
    var jsonOutput = parseResult.GetValue(jsonOption);
    var darkCutoff = parseResult.GetValue(darkCutoffOption);
    var lightCutoff = parseResult.GetValue(lightCutoffOption);
    var mode = parseResult.GetValue(modeOption);

    // Parse unified output option - auto-detect format from extension
    var outputAsJson = false;
    string? jsonOutputPath = null;
    string? gifOutputPath = null;

    if (!string.IsNullOrEmpty(output))
    {
        // Check for explicit format prefixes first
        if (output.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            outputAsJson = true;
            // Use input filename with .json extension - will set later when we have input
        }
        else if (output.StartsWith("json:", StringComparison.OrdinalIgnoreCase))
        {
            outputAsJson = true;
            jsonOutputPath = output[5..]; // Everything after "json:"
        }
        else if (output.Equals("gif", StringComparison.OrdinalIgnoreCase))
        {
            // Use input filename with .gif extension - will set later when we have input
        }
        else if (output.StartsWith("gif:", StringComparison.OrdinalIgnoreCase))
        {
            gifOutputPath = output[4..]; // Everything after "gif:"
        }
        // Auto-detect from extension
        else if (output.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            gifOutputPath = output;
        }
        else if (output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                 output.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
        {
            outputAsJson = true;
            jsonOutputPath = output;
        }
        else
        {
            // Unknown extension - default to GIF for video
            Console.Error.WriteLine(
                $"Warning: Unknown output format for '{output}'. Use .gif for animated GIF or .json for JSON document.");
            Console.Error.WriteLine("Assuming GIF output. Use 'json:path' prefix for JSON.");
            gifOutputPath = output.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? output : output + ".gif";
        }
    }

    // Create FileInfo for GIF output if path is set
    var outputGif = !string.IsNullOrEmpty(gifOutputPath) ? new FileInfo(gifOutputPath) : null;

    // Calibration mode - show test pattern to verify aspect ratio
    if (calibrate)
    {
        ConsoleHelper.EnableAnsiSupport();

        // Determine render mode for calibration
        var calibrationMode = useBraille ? RenderMode.Braille
            : useBlocks ? RenderMode.ColorBlocks
            : RenderMode.Ascii;

        // Get effective aspect ratio: explicit > saved for mode > default
        var calibrationAspect = charAspect
                                ?? savedCalibration?.GetAspectRatio(calibrationMode)
                                ?? 0.5f;

        var modeName = calibrationMode switch
        {
            RenderMode.Braille => "Braille",
            RenderMode.ColorBlocks => "Blocks",
            _ => "ASCII"
        };

        Console.WriteLine($"Aspect Ratio Calibration - {modeName} Mode (--char-aspect {calibrationAspect})");
        Console.WriteLine("The shape below should appear as a perfect CIRCLE.");
        Console.WriteLine("If stretched horizontally, decrease --char-aspect (try 0.45)");
        Console.WriteLine("If stretched vertically, increase --char-aspect (try 0.55)");
        Console.WriteLine();

        // Render calibration pattern using the core helper
        var calibrationOutput = CalibrationHelper.RenderCalibrationPattern(
            calibrationMode,
            calibrationAspect,
            !noColor);

        Console.WriteLine(calibrationOutput);
        Console.WriteLine();
        Console.WriteLine($"Current --char-aspect: {calibrationAspect} ({modeName} mode)");

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
            var modeFlag = calibrationMode switch
            {
                RenderMode.Braille => " --braille",
                RenderMode.ColorBlocks => " --blocks",
                _ => ""
            };
            Console.WriteLine($"  consolevideo --calibrate{modeFlag} --char-aspect {calibrationAspect} --save");
        }

        return 0;
    }

    if (input == null)
    {
        Console.Error.WriteLine(
            "Error: No input file specified. Use --calibrate for aspect ratio testing without a file.");
        return 1;
    }

    // For AOT builds, mapped network drives (like X:) may not be accessible.
    // Try the original path first, then fall back to FileInfo resolution.
    var inputFullPath = inputPath!;

    // First try the path exactly as given
    if (!File.Exists(inputFullPath))
        // Try FileInfo resolution (handles relative paths)
        inputFullPath = input.FullName;

    if (!File.Exists(inputFullPath))
    {
        Console.Error.WriteLine($"Error: File not found: {inputPath}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Troubleshooting:");
        Console.Error.WriteLine($"  Original path: {inputPath}");
        Console.Error.WriteLine($"  Resolved path: {input.FullName}");

        // Check if it's a mapped drive issue
        if (inputPath!.Length >= 2 && inputPath[1] == ':')
        {
            var driveLetter = char.ToUpper(inputPath[0]);
            if (driveLetter >= 'D') // Likely a mapped/network drive
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
            try
            {
                dirExists = Directory.Exists(dir);
            }
            catch
            {
            }

            Console.Error.WriteLine($"  Directory exists: {dirExists}");
        }

        return 1;
    }

    // Update input to use the working path
    input = new FileInfo(inputFullPath);

    // Determine JSON output path if needed
    if (outputAsJson && string.IsNullOrEmpty(jsonOutputPath))
        jsonOutputPath = Path.ChangeExtension(input.FullName, ".json");

    // Check if input is a JSON document - load and play it
    // Supports both .json and .ndjson (streaming JSON Lines format)
    if (input.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
        input.Extension.Equals(".ndjson", StringComparison.OrdinalIgnoreCase))
        return await HandleJsonDocument(input.FullName, speed, loop, cancellationToken);

    // Check if input is an image file - handle directly without FFmpeg
    var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif"
    };

    if (imageExtensions.Contains(input.Extension))
        return await HandleImageFile(
            input, width, height, maxWidth, maxHeight, charAspect, savedCalibration,
            parseResult.GetValue(colorBlocksOption), parseResult.GetValue(brailleOption),
            noColor, contrast, gamma, loop, speed, outputGif,
            parseResult.GetValue(gifFontSizeOption), parseResult.GetValue(gifScaleOption),
            parseResult.GetValue(gifColorsOption),
            outputAsJson, jsonOutputPath,
            showStatus,
            cancellationToken);

    // Resolve FFmpeg path
    string? ffmpegExe = null;
    string? ffprobePath = null;
    if (!string.IsNullOrEmpty(ffmpegPath))
    {
        if (Directory.Exists(ffmpegPath))
        {
            // It's a directory - look for ffmpeg inside
            var exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            var probeName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
            ffmpegExe = Path.Combine(ffmpegPath, exeName);
            ffprobePath = Path.Combine(ffmpegPath, probeName);
        }
        else if (File.Exists(ffmpegPath))
        {
            // It's a file path to ffmpeg
            ffmpegExe = ffmpegPath;
            var dir = Path.GetDirectoryName(ffmpegPath);
            if (dir != null)
            {
                var probeName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
                ffprobePath = Path.Combine(dir, probeName);
            }
        }
    }

    // Check for FFmpeg with progress reporting
    using var ffmpeg = new FFmpegService(
        ffprobePath: ffprobePath,
        ffmpegPath: ffmpegExe,
        useHardwareAcceleration: !noHwAccel);

    // Check FFmpeg availability with interactive prompt
    if (!FFmpegProvider.IsAvailable(ffmpegPath))
    {
        var (needsDownload, statusMsg, downloadUrl) = FFmpegProvider.GetDownloadStatus();

        if (needsDownload)
        {
            Console.WriteLine("FFmpeg not found on system.");
            Console.WriteLine($"Cache location: {FFmpegProvider.CacheDirectory}");
            Console.WriteLine();

            var shouldDownload = autoConfirmDownload;

            if (!shouldDownload && !noAutoDownload)
            {
                // Interactive prompt
                Console.Write("Would you like to download FFmpeg automatically? (~100MB) [Y/n]: ");
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                shouldDownload = string.IsNullOrEmpty(response) || response == "y" || response == "yes";
            }
            else if (noAutoDownload && !autoConfirmDownload)
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

    await ffmpeg.InitializeAsync(progress, cancellationToken);

    // Show video info if requested
    if (showInfo)
    {
        var info = await ffmpeg.GetVideoInfoAsync(input.FullName, cancellationToken);
        if (info == null)
        {
            Console.Error.WriteLine("Error: Could not read video info. Is FFmpeg installed?");
            Console.Error.WriteLine("Use --ffmpeg-path to specify the FFmpeg installation directory.");
            return 1;
        }

        Console.WriteLine($"File: {Path.GetFileName(input.FullName)}");
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

    // Calculate end time from duration if specified (must be done before any output mode)
    if (duration.HasValue && start.HasValue)
        end = start.Value + duration.Value;
    else if (duration.HasValue) end = duration.Value;

    // Raw mode requires output file
    if (rawMode && outputGif == null)
    {
        Console.Error.WriteLine("Error: --raw mode requires --output to specify a GIF file.");
        Console.Error.WriteLine("Example: consolevideo video.mp4 --raw -o keyframes.gif --gif-frames 4");
        return 1;
    }

    // GIF output mode - render video frames to animated GIF
    if (outputGif != null)
    {
        var videoInfo = await ffmpeg.GetVideoInfoAsync(input.FullName, cancellationToken);
        if (videoInfo == null)
        {
            Console.Error.WriteLine("Error: Could not read video info.");
            return 1;
        }

        // RAW MODE - extract actual video frames as GIF (no ASCII rendering)
        if (rawMode)
        {
            var targetWidth = rawWidth ?? 320;
            var targetHeight = rawHeight ?? (int)(targetWidth * videoInfo.Height / (double)videoInfo.Width);

            // Calculate limits from existing flags
            var maxFrames = gifFrames ?? 4; // Default to 4 frames for raw/keyframe extraction
            var maxLength = gifLength ?? duration ?? 10.0; // Use -t or --gif-length or default 10s
            var rawStartTime = start ?? 0;
            var rawEndTime = end ?? rawStartTime + maxLength;

            // Smart keyframe mode - use scene detection
            if (smartKeyframes)
            {
                Console.WriteLine("Smart keyframe extraction (scene detection)...");
                Console.WriteLine($"  Output: {outputGif.FullName}");
                Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
                Console.WriteLine($"  Target: {targetWidth}x{targetHeight}, max {maxFrames} keyframes");
                Console.WriteLine();

                // First, detect scene changes using FFmpeg's built-in scene detection
                Console.Write("Detecting scene changes...");
                var sceneTimestamps = await ffmpeg.DetectSceneChangesAsync(
                    input.FullName,
                    sceneThreshold,
                    rawStartTime,
                    rawEndTime > rawStartTime ? rawEndTime : null,
                    cancellationToken);
                Console.WriteLine($" found {sceneTimestamps.Count} scene changes");

                // If no scene changes detected, fall back to uniform sampling
                List<double> keyframeTimes;
                if (sceneTimestamps.Count == 0)
                {
                    Console.WriteLine("No scene changes detected, using uniform sampling...");
                    keyframeTimes = new List<double>();
                    var interval = (rawEndTime - rawStartTime) / (maxFrames - 1);
                    for (var i = 0; i < maxFrames; i++) keyframeTimes.Add(rawStartTime + i * interval);
                }
                else
                {
                    // Always include first frame
                    keyframeTimes = new List<double> { rawStartTime };

                    // Add scene change timestamps
                    keyframeTimes.AddRange(sceneTimestamps);

                    // Always include a frame near the end if not already
                    if (keyframeTimes.Count > 0 && rawEndTime - keyframeTimes.Last() > 1.0)
                        keyframeTimes.Add(rawEndTime - 0.5);

                    // Limit to maxFrames
                    if (keyframeTimes.Count > maxFrames)
                    {
                        // Keep first, last, and distribute the rest
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

                // Calculate frame delay for GIF (spread evenly or use fps if specified)
                var targetFps = fps ?? 2.0; // Default 2fps for keyframe GIF
                var rawDelayMs = (int)(1000.0 / targetFps);

                using var rawGif = new Image<Rgba32>(targetWidth, targetHeight);
                var gifMetaData = rawGif.Metadata.GetGifMetadata();
                gifMetaData.RepeatCount = (ushort)(loop != 1 ? loop : 0);

                var frameCount = 0;
                foreach (var timestamp in keyframeTimes)
                {
                    Console.Write($"\rExtracting frame at {timestamp:F1}s...");
                    var frameImage = await ffmpeg.ExtractFrameAsync(
                        input.FullName, timestamp, targetWidth, targetHeight, cancellationToken);

                    if (frameImage != null)
                    {
                        var gifFrameMetadata = frameImage.Frames.RootFrame.Metadata.GetGifMetadata();
                        gifFrameMetadata.FrameDelay = rawDelayMs / 10;
                        rawGif.Frames.AddFrame(frameImage.Frames.RootFrame);
                        frameImage.Dispose();
                        frameCount++;
                    }
                }

                // Remove the initial empty frame
                if (rawGif.Frames.Count > 1)
                    rawGif.Frames.RemoveFrame(0);

                Console.WriteLine($" Done! ({frameCount} frames)");

                // Save
                Console.Write("Saving GIF...");
                var encoder = new GifEncoder
                {
                    ColorTableMode = GifColorTableMode.Local
                };
                await rawGif.SaveAsGifAsync(outputGif.FullName, encoder, cancellationToken);
                Console.WriteLine($" Saved to {outputGif.FullName}");

                return 0;
            }

            // Regular raw mode - uniform frame extraction using FFmpeg streaming
            // This is memory-efficient: only one frame in memory at a time
            var uniformTargetFps = fps ?? Math.Min(videoInfo.FrameRate, 10.0);
            var uniformFpsInt = Math.Max(1, (int)uniformTargetFps);

            Console.WriteLine("Extracting video frames to GIF (streaming mode)...");
            Console.WriteLine($"  Output: {outputGif.FullName}");
            Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
            Console.WriteLine($"  Target: {targetWidth}x{targetHeight} @ {uniformTargetFps:F1} fps, step {frameStep}");
            Console.WriteLine("  Memory: streaming (1 frame at a time)");

            // Use FFmpeg streaming GIF writer - only one frame in memory at a time
            await using var streamingGif = new FFmpegGifWriter(
                outputGif.FullName,
                targetWidth,
                targetHeight,
                uniformFpsInt,
                loop != 1 ? loop : 0,
                gifColors,
                maxLength,
                maxFrames > 0 ? maxFrames : null);

            await streamingGif.StartAsync(null, cancellationToken);

            var uniformFrameCount = 0;

            await foreach (var frameImage in ffmpeg.StreamFramesAsync(
                               input.FullName, targetWidth, targetHeight,
                               rawStartTime, rawEndTime, frameStep, uniformTargetFps, cancellationToken))
            {
                if (streamingGif.ShouldStop)
                {
                    frameImage.Dispose();
                    break;
                }

                await streamingGif.AddFrameAsync(frameImage, cancellationToken);
                frameImage.Dispose(); // Dispose immediately - already written to FFmpeg
                uniformFrameCount++;

                Console.Write($"\rStreaming frame {uniformFrameCount}...");
            }

            Console.WriteLine($" Done! ({uniformFrameCount} frames)");
            Console.Write("Finalizing GIF...");
            await streamingGif.FinishAsync(cancellationToken);
            Console.WriteLine($" Saved to {outputGif.FullName}");

            return 0;
        }

        // Determine render mode from flags
        var gifUseBlocks = parseResult.GetValue(colorBlocksOption);
        var gifUseBraille = parseResult.GetValue(brailleOption);
        var gifRenderMode = gifUseBraille ? RenderMode.Braille
            : gifUseBlocks ? RenderMode.ColorBlocks
            : RenderMode.Ascii;

        // Get effective aspect ratio
        var gifEffectiveAspect = charAspect
                                 ?? savedCalibration?.GetAspectRatio(gifRenderMode)
                                 ?? 0.5f;

        // Calculate target FPS for GIF
        var gifTargetFps = fps ?? Math.Min(videoInfo.FrameRate, 15.0);
        var frameDelayMs = (int)(1000.0 / gifTargetFps / speed);

        Console.WriteLine("Rendering video to animated GIF...");
        Console.WriteLine($"  Output: {outputGif.FullName}");
        Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
        Console.WriteLine($"  Target: {gifTargetFps:F1} fps, frame step {frameStep}");

        // GIF loop: 0 = infinite, which is the default for GIFs
        // Only use explicit loop count if user specified it (loop != 1 which is the CLI default)
        var gifLoopCount = loop != 1 ? loop : 0;

        var gifOptions = new GifWriterOptions
        {
            FontSize = gifFontSize,
            Scale = gifScale,
            MaxColors = Math.Clamp(gifColors, 16, 256),
            LoopCount = gifLoopCount,
            MaxLengthSeconds = duration ?? end - start
        };
        using var gifWriter = new GifWriter(gifOptions);

        // Create appropriate renderer
        var renderedCount = 0;
        var gifStartTime = start ?? 0;

        // Use RenderOptions to calculate dimensions consistently with all other renderers
        // This ensures -w 150 or -h 80 respects aspect ratio in all modes
        var tempOptions = new RenderOptions
        {
            Width = width,
            Height = height,
            MaxWidth = maxWidth,
            MaxHeight = Math.Max(80, maxHeight), // GIF output uses larger height for better quality
            CharacterAspectRatio = gifEffectiveAspect
        };

        // Get character dimensions from shared calculation
        // pixelsPerCharWidth/Height varies by render mode
        var pixelsPerCharWidth = gifUseBraille ? 2 : 1;
        var pixelsPerCharHeight = gifUseBraille ? 4 : gifUseBlocks ? 2 : 1;
        var (pixelWidth, pixelHeight) = tempOptions.CalculateVisualDimensions(
            videoInfo.Width, videoInfo.Height, pixelsPerCharWidth, pixelsPerCharHeight);

        // Calculate character dimensions from pixel dimensions
        var charWidth = pixelWidth / pixelsPerCharWidth;
        var charHeight = pixelHeight / pixelsPerCharHeight;

        Console.WriteLine($"  Output: {charWidth}x{charHeight} chars ({pixelWidth}x{pixelHeight} pixels)");
        Console.WriteLine();

        // Create final render options with calculated dimensions
        // Width/Height set to exact values so renderer doesn't resize
        var gifRenderOptions = new RenderOptions
        {
            Width = charWidth,
            Height = charHeight,
            MaxWidth = charWidth,
            MaxHeight = charHeight,
            CharacterAspectRatio = gifEffectiveAspect,
            ContrastPower = contrast,
            Gamma = gamma,
            UseColor = !noColor,
            Invert = !noInvert,
            UseParallelProcessing = !noParallel,
            EnableEdgeDetection = enableEdge,
            BackgroundThreshold = bgThreshold,
            DarkBackgroundThreshold = darkBgThreshold,
            AutoBackgroundSuppression = autoBg,
            EnableDithering = !noDither,
            EnableEdgeDirectionChars = !noEdgeChars,
            DarkTerminalBrightnessThreshold = darkCutoff,
            LightTerminalBrightnessThreshold = lightCutoff
        };

        // Estimate total frames for status display
        var effectiveStart = gifStartTime;
        var effectiveEnd = end ?? videoInfo.Duration;
        var effectiveDuration = effectiveEnd - effectiveStart;
        var estimatedTotalFrames = (int)(effectiveDuration * gifTargetFps / frameStep);

        // Create status line renderer if enabled
        var statusRenderer = showStatus ? new StatusLine(charWidth, !noColor) : null;
        var renderModeName = gifUseBraille ? "Braille" : gifUseBlocks ? "Blocks" : "ASCII";

        // Note: Status line in GIF output only supported for ASCII mode
        // Braille and ColorBlocks use pixel-based rendering that can't easily mix with text
        if (showStatus && (gifUseBraille || gifUseBlocks))
        {
            Console.WriteLine("Note: Status line in GIF output is only supported for ASCII mode.");
            statusRenderer = null;
        }

        await foreach (var frameImage in ffmpeg.StreamFramesAsync(
                           input.FullName,
                           pixelWidth,
                           pixelHeight,
                           gifStartTime > 0 ? gifStartTime : null,
                           end,
                           frameStep,
                           gifTargetFps,
                           cancellationToken))
        {
            // Render frame based on mode using appropriate specialized renderer
            if (gifUseBraille)
            {
                using var renderer = new BrailleRenderer(gifRenderOptions);
                var content = renderer.RenderImage(frameImage);
                var frame = new BrailleFrame(content, frameDelayMs);
                gifWriter.AddBrailleFrame(frame, frameDelayMs);
            }
            else if (gifUseBlocks)
            {
                using var renderer = new ColorBlockRenderer(gifRenderOptions);
                var content = renderer.RenderImage(frameImage);
                var frame = new ColorBlockFrame(content, frameDelayMs);
                gifWriter.AddColorBlockFrame(frame, frameDelayMs);
            }
            else
            {
                using var renderer = new AsciiRenderer(gifRenderOptions);
                var asciiFrame = renderer.RenderImage(frameImage);
                var content = asciiFrame.ToAnsiString();

                // Append status line if enabled (ASCII mode only)
                if (statusRenderer != null)
                {
                    var statusInfo = new StatusLine.StatusInfo
                    {
                        FileName = input.Name,
                        SourceWidth = videoInfo.Width,
                        SourceHeight = videoInfo.Height,
                        OutputWidth = charWidth,
                        OutputHeight = charHeight,
                        RenderMode = renderModeName,
                        CurrentFrame = renderedCount + 1,
                        TotalFrames = estimatedTotalFrames,
                        CurrentTime = TimeSpan.FromSeconds(effectiveStart + renderedCount * frameStep / gifTargetFps),
                        TotalDuration = TimeSpan.FromSeconds(effectiveDuration),
                        Fps = gifTargetFps
                    };
                    content += "\n" + statusRenderer.Render(statusInfo);
                }

                gifWriter.AddFrame(content, frameDelayMs);
            }

            frameImage.Dispose();
            renderedCount++;
            Console.Write($"\rRendering frames to GIF: {renderedCount}/{estimatedTotalFrames} processed");
        }

        Console.WriteLine($"\rRendering frames to GIF: {renderedCount} frames completed");
        Console.Write("Encoding and saving GIF file...");
        await gifWriter.SaveAsync(outputGif.FullName, cancellationToken);
        Console.WriteLine($" Saved to {outputGif.FullName}");
        return 0;
    }

    // Streaming JSON output mode - render video frames to JSON document incrementally
    if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
    {
        var videoInfo = await ffmpeg.GetVideoInfoAsync(input.FullName, cancellationToken);
        if (videoInfo == null)
        {
            Console.Error.WriteLine("Error: Could not read video info.");
            return 1;
        }

        // Determine render mode from flags
        var jsonUseBlocks = parseResult.GetValue(colorBlocksOption);
        var jsonUseBraille = parseResult.GetValue(brailleOption);
        var jsonRenderMode = jsonUseBraille ? RenderMode.Braille
            : jsonUseBlocks ? RenderMode.ColorBlocks
            : RenderMode.Ascii;

        // Get effective aspect ratio
        var jsonEffectiveAspect = charAspect
                                  ?? savedCalibration?.GetAspectRatio(jsonRenderMode)
                                  ?? 0.5f;

        // Calculate target FPS for JSON
        var jsonTargetFps = fps ?? Math.Min(videoInfo.FrameRate, 30.0);
        var frameDelayMs = (int)(1000.0 / jsonTargetFps / speed);

        Console.WriteLine("Streaming video to JSON document...");
        Console.WriteLine($"  Output: {jsonOutputPath}");
        Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
        Console.WriteLine($"  Target: {jsonTargetFps:F1} fps, frame step {frameStep}");
        Console.WriteLine("  Press Ctrl+C to stop (document will auto-finalize)");

        // Use RenderOptions to calculate dimensions
        var tempOptions = new RenderOptions
        {
            Width = width,
            Height = height,
            MaxWidth = maxWidth,
            MaxHeight = maxHeight,
            CharacterAspectRatio = jsonEffectiveAspect
        };

        // Get pixel dimensions
        var pixelsPerCharWidth = jsonUseBraille ? 2 : 1;
        var pixelsPerCharHeight = jsonUseBraille ? 4 : jsonUseBlocks ? 2 : 1;
        var (pixelWidth, pixelHeight) = tempOptions.CalculateVisualDimensions(
            videoInfo.Width, videoInfo.Height, pixelsPerCharWidth, pixelsPerCharHeight);

        var charWidth = pixelWidth / pixelsPerCharWidth;
        var charHeight = pixelHeight / pixelsPerCharHeight;

        Console.WriteLine($"  Output: {charWidth}x{charHeight} chars");
        Console.WriteLine();

        // Create render options
        var jsonRenderOptions = new RenderOptions
        {
            Width = charWidth,
            Height = charHeight,
            MaxWidth = charWidth,
            MaxHeight = charHeight,
            CharacterAspectRatio = jsonEffectiveAspect,
            ContrastPower = contrast,
            Gamma = gamma,
            UseColor = !noColor,
            Invert = !noInvert,
            UseParallelProcessing = !noParallel,
            EnableEdgeDetection = enableEdge,
            BackgroundThreshold = bgThreshold,
            DarkBackgroundThreshold = darkBgThreshold,
            AutoBackgroundSuppression = autoBg,
            EnableDithering = !noDither,
            EnableEdgeDirectionChars = !noEdgeChars,
            DarkTerminalBrightnessThreshold = darkCutoff,
            LightTerminalBrightnessThreshold = lightCutoff
        };

        var renderModeName = jsonUseBraille ? "Braille" : jsonUseBlocks ? "ColorBlocks" : "ASCII";

        // Create streaming document writer
        await using var docWriter = new StreamingDocumentWriter(
            jsonOutputPath,
            renderModeName,
            jsonRenderOptions,
            input.FullName);

        await docWriter.WriteHeaderAsync(cancellationToken);

        var renderedCount = 0;
        var jsonStartTime = start ?? 0;

        try
        {
            await foreach (var frameImage in ffmpeg.StreamFramesAsync(
                               input.FullName,
                               pixelWidth,
                               pixelHeight,
                               jsonStartTime > 0 ? jsonStartTime : null,
                               end,
                               frameStep,
                               jsonTargetFps,
                               cancellationToken))
            {
                // Render frame based on mode
                if (jsonUseBraille)
                {
                    using var renderer = new BrailleRenderer(jsonRenderOptions);
                    var content = renderer.RenderImage(frameImage);
                    await docWriter.WriteFrameAsync(content, frameDelayMs, ct: cancellationToken);
                }
                else if (jsonUseBlocks)
                {
                    using var renderer = new ColorBlockRenderer(jsonRenderOptions);
                    var content = renderer.RenderImage(frameImage);
                    await docWriter.WriteFrameAsync(content, frameDelayMs, ct: cancellationToken);
                }
                else
                {
                    using var renderer = new AsciiRenderer(jsonRenderOptions);
                    var frame = renderer.RenderImage(frameImage);
                    await docWriter.WriteFrameAsync(frame, jsonRenderOptions, cancellationToken);
                }

                frameImage.Dispose();
                renderedCount++;
                Console.Write($"\rRendering frames to JSON: {renderedCount} written");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\rRendering frames to JSON: stopped at frame {renderedCount}");
        }

        Console.Write("\rFinalizing JSON document...                    ");
        await docWriter.FinalizeAsync(!cancellationToken.IsCancellationRequested, cancellationToken);
        Console.WriteLine($"\rSaved {renderedCount} frames to {jsonOutputPath}              ");
        return 0;
    }

    // Determine character set
    var characterSet = charset;
    if (string.IsNullOrEmpty(characterSet))
        characterSet = preset?.ToLowerInvariant() switch
        {
            "simple" => CharacterMap.SimpleCharacterSet,
            "block" => CharacterMap.BlockCharacterSet,
            "classic" => CharacterMap.DefaultCharacterSet,
            _ => CharacterMap.ExtendedCharacterSet
        };

    // Determine render mode from flags
    var renderMode = useBraille ? VideoRenderMode.Braille
        : useBlocks ? VideoRenderMode.ColorBlocks
        : VideoRenderMode.Ascii;

    // Get effective aspect ratio: explicit > saved for mode > default
    var coreRenderMode = renderMode switch
    {
        VideoRenderMode.Braille => RenderMode.Braille,
        VideoRenderMode.ColorBlocks => RenderMode.ColorBlocks,
        _ => RenderMode.Ascii
    };
    var effectiveAspect = charAspect
                          ?? savedCalibration?.GetAspectRatio(coreRenderMode)
                          ?? 0.5f;

    // Parse sampling strategy
    var samplingStrategy = sampling?.ToLowerInvariant() switch
    {
        "keyframe" or "key" => FrameSamplingStrategy.Keyframe,
        "scene" or "sceneaware" => FrameSamplingStrategy.SceneAware,
        "adaptive" => FrameSamplingStrategy.Adaptive,
        _ => FrameSamplingStrategy.Uniform
    };

    // Build options
    var options = new VideoRenderOptions
    {
        RenderOptions = new RenderOptions
        {
            Width = width,
            Height = height,
            MaxWidth = maxWidth,
            MaxHeight = maxHeight,
            CharacterSet = characterSet,
            CharacterAspectRatio = effectiveAspect,
            ContrastPower = contrast,
            Gamma = gamma,
            UseColor = !noColor,
            Invert = !noInvert,
            UseParallelProcessing = !noParallel,
            EnableEdgeDetection = enableEdge,
            BackgroundThreshold = bgThreshold,
            DarkBackgroundThreshold = darkBgThreshold,
            AutoBackgroundSuppression = autoBg,
            EnableDithering = !noDither,
            EnableEdgeDirectionChars = !noEdgeChars,
            DarkTerminalBrightnessThreshold = darkCutoff,
            LightTerminalBrightnessThreshold = lightCutoff
        },
        StartTime = start,
        EndTime = end,
        TargetFps = fps,
        FrameStep = Math.Max(1, frameStep),
        SamplingStrategy = samplingStrategy,
        SceneThreshold = sceneThreshold,
        BufferAheadFrames = Math.Clamp(buffer, 2, 10),
        SpeedMultiplier = speed,
        LoopCount = loop,
        UseAltScreen = !noAltScreen,
        UseHardwareAcceleration = !noHwAccel,
        RenderMode = renderMode,
        ShowStatus = showStatus,
        SourceFileName = input.FullName
    };

    try
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var player = new VideoAnimationPlayer(input.FullName, options);
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
});

return await rootCommand.Parse(args).InvokeAsync();

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
        if (doc.IsAnimated) Console.Error.WriteLine($"Duration: {doc.TotalDurationMs}ms");

        // Override settings if specified
        var effectiveSpeed = speed != 1.0f ? speed : doc.Settings.AnimationSpeedMultiplier;
        var effectiveLoop = loop != 1 ? loop : doc.Settings.LoopCount;

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
/// Handle image files (JPG, PNG, GIF, etc.) directly without FFmpeg.
/// This makes the video CLI a superset of the image CLI.
/// </summary>
static async Task<int> HandleImageFile(
    FileInfo input,
    int? width, int? height, int maxWidth, int maxHeight,
    float? charAspect, CalibrationSettings? savedCalibration,
    bool useBlocks, bool useBraille,
    bool noColor, float contrast, float gamma, int loop, float speed,
    FileInfo? outputGif,
    int gifFontSize, float gifScale, int gifColors,
    bool outputAsJson, string? jsonOutputPath,
    bool showStatus,
    CancellationToken cancellationToken)
{
    ConsoleHelper.EnableAnsiSupport();

    // Determine render mode
    var renderMode = useBraille ? RenderMode.Braille
        : useBlocks ? RenderMode.ColorBlocks
        : RenderMode.Ascii;

    // Get effective aspect ratio
    var effectiveAspect = charAspect
                          ?? savedCalibration?.GetAspectRatio(renderMode)
                          ?? 0.5f;

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
        AnimationSpeedMultiplier = speed
    };

    // Check if it's an animated GIF
    var isAnimatedGif = input.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);

    if (outputGif != null)
    {
        // GIF output mode
        // GIF loop: 0 = infinite, which is the default for GIFs
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
            // Render animated GIF frames
            Console.WriteLine("Rendering animated GIF...");
            Console.WriteLine($"  Output: {outputGif.FullName}");

            // Status line in GIF output only supported for ASCII mode
            var renderModeName = useBraille ? "Braille" : useBlocks ? "Blocks" : "ASCII";
            StatusLine? statusRenderer = null;
            if (showStatus)
            {
                if (useBraille || useBlocks)
                    Console.WriteLine("Note: Status line in GIF output is only supported for ASCII mode.");
                else
                    statusRenderer = new StatusLine(maxWidth, !noColor);
            }

            if (useBraille)
            {
                using var renderer = new BrailleRenderer(options);
                var frames = renderer.RenderGif(input.FullName);
                var frameList = frames.ToList();
                var totalFrames = frameList.Count;
                var frameIndex = 0;

                foreach (var frame in frameList)
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
                var frames = renderer.RenderGif(input.FullName);
                var frameList = frames.ToList();
                var totalFrames = frameList.Count;
                var frameIndex = 0;

                foreach (var frame in frameList)
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
                var frames = renderer.RenderGif(input.FullName);
                var frameList = frames.ToList();
                var totalFrames = frameList.Count;
                var frameIndex = 0;

                foreach (var frame in frameList)
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
        }
        else
        {
            // Render single image as single-frame GIF
            Console.WriteLine($"Rendering image to GIF: {outputGif.FullName}");

            if (useBraille)
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
                gifWriter.AddFrame(frame, 1000);
            }
        }

        await gifWriter.SaveAsync(outputGif.FullName, cancellationToken);
        Console.WriteLine($"Saved to {outputGif.FullName}");
        return 0;
    }

    // Display mode - show image/animation in terminal
    if (isAnimatedGif)
    {
        // Play animated GIF
        Console.WriteLine($"Playing animated GIF: {input.Name}");
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine();

        // Render GIF frames and play with simple animation loop
        List<IAnimationFrame> frames;
        if (useBraille)
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
            // Wrap AsciiFrame in AsciiFrameAdapter since AsciiFrame doesn't implement IAnimationFrame
            var darkThreshold = options.Invert ? options.DarkTerminalBrightnessThreshold : null;
            var lightThreshold = !options.Invert ? options.LightTerminalBrightnessThreshold : null;
            frames = renderer.RenderGif(input.FullName)
                .Select(f => (IAnimationFrame)new AsciiFrameAdapter(f, options.UseColor, darkThreshold, lightThreshold))
                .ToList();
        }

        // Simple frame-by-frame playback
        await PlayFramesAsync(frames, loop, speed, cancellationToken);
    }
    else
    {
        // Display single image
        if (useBraille)
        {
            using var renderer = new BrailleRenderer(options);
            var frame = renderer.RenderFileToFrame(input.FullName);
            Console.WriteLine(frame.Content);

            // Save as JSON if requested
            if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            {
                var doc = ConsoleImageDocument.FromBrailleFrames(new[] { frame }, options, input.FullName);
                await doc.SaveAsync(jsonOutputPath, cancellationToken);
                Console.Error.WriteLine($"Saved JSON document to: {jsonOutputPath}");
            }
        }
        else if (useBlocks)
        {
            using var renderer = new ColorBlockRenderer(options);
            var frame = renderer.RenderFileToFrame(input.FullName);
            Console.WriteLine(frame.Content);

            // Save as JSON if requested
            if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            {
                var doc = ConsoleImageDocument.FromColorBlockFrames(new[] { frame }, options, input.FullName);
                await doc.SaveAsync(jsonOutputPath, cancellationToken);
                Console.Error.WriteLine($"Saved JSON document to: {jsonOutputPath}");
            }
        }
        else
        {
            using var renderer = new AsciiRenderer(options);
            var frame = renderer.RenderFile(input.FullName);
            Console.WriteLine(frame.ToAnsiString());

            // Save as JSON if requested
            if (outputAsJson && !string.IsNullOrEmpty(jsonOutputPath))
            {
                var doc = ConsoleImageDocument.FromAsciiFrames(new[] { frame }, options, input.FullName);
                await doc.SaveAsync(jsonOutputPath, cancellationToken);
                Console.Error.WriteLine($"Saved JSON document to: {jsonOutputPath}");
            }
        }
    }

    return 0;
}

/// <summary>
/// Simple frame-by-frame animation playback.
/// </summary>
static async Task PlayFramesAsync(List<IAnimationFrame> frames, int loopCount, float speed, CancellationToken ct)
{
    if (frames.Count == 0) return;

    // Get initial frame dimensions for cursor positioning
    var firstContent = frames[0].Content;
    var frameHeight = firstContent.Split('\n').Length;

    // Enter alternate screen buffer
    Console.Write("\x1b[?1049h"); // Enter alt screen
    Console.Write("\x1b[?25l"); // Hide cursor

    try
    {
        var loops = 0;
        while (!ct.IsCancellationRequested && (loopCount == 0 || loops < loopCount))
        {
            foreach (var frame in frames)
            {
                if (ct.IsCancellationRequested) break;

                // Move to top-left and render frame
                Console.Write("\x1b[H"); // Home position
                Console.Write(frame.Content);

                // Wait for frame delay
                var delayMs = Math.Max(1, (int)(frame.DelayMs / speed));
                await Task.Delay(delayMs, ct);
            }

            loops++;
        }
    }
    catch (OperationCanceledException)
    {
    }
    finally
    {
        Console.Write("\x1b[?25h"); // Show cursor
        Console.Write("\x1b[?1049l"); // Exit alt screen
    }
}