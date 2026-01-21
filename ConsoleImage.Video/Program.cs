// Video to ASCII CLI - FFmpeg-based video player
// Streams video files as ASCII art with intelligent frame sampling

using System.CommandLine;
using ConsoleImage.Core;
using ConsoleImage.Video.Core;

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
int defaultMaxWidth = 120;
int defaultMaxHeight = 40;
try
{
    if (Console.WindowWidth > 0)
        defaultMaxWidth = Console.WindowWidth - 1;
    if (Console.WindowHeight > 0)
        defaultMaxHeight = Console.WindowHeight - 2;
}
catch { }

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
var frameStepOption = new Option<int>("--frame-step") { Description = "Frame step (1 = every frame, 2 = every 2nd, etc.)" };
frameStepOption.DefaultValueFactory = _ => 1;
frameStepOption.Aliases.Add("-f");

var samplingOption = new Option<string>("--sampling") { Description = "Sampling strategy: uniform, keyframe, scene, adaptive" };
samplingOption.DefaultValueFactory = _ => "uniform";

var sceneThresholdOption = new Option<double>("--scene-threshold") { Description = "Scene detection threshold (0.0-1.0, lower = more sensitive)" };
sceneThresholdOption.DefaultValueFactory = _ => 0.4;

// Rendering options
var colorBlocksOption = new Option<bool>("--blocks") { Description = "Use colored Unicode blocks for high-fidelity output (requires 24-bit color terminal)" };
colorBlocksOption.Aliases.Add("-b");

var brailleOption = new Option<bool>("--braille") { Description = "Use braille characters for ultra-high resolution (2x4 dots per cell)" };
brailleOption.Aliases.Add("-B");

var noColorOption = new Option<bool>("--no-color") { Description = "Disable colored output" };

var contrastOption = new Option<float>("--contrast") { Description = "Contrast enhancement (1.0 = none, higher = more contrast)" };
contrastOption.DefaultValueFactory = _ => 2.5f;

var gammaOption = new Option<float>("--gamma") { Description = "Gamma correction (lower = brighter, higher = darker). Default: 0.65" };
gammaOption.DefaultValueFactory = _ => 0.65f;
gammaOption.Aliases.Add("-g");

var charAspectOption = new Option<float?>("--char-aspect") { Description = "Character aspect ratio (width/height). Uses saved calibration or 0.5 if not set." };

var charsetOption = new Option<string?>("--charset") { Description = "Custom character set (light to dark)" };

var presetOption = new Option<string?>("--preset") { Description = "Character set preset: extended (default), simple, block, classic" };
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
var calibrateOption = new Option<bool>("--calibrate") { Description = "Display aspect ratio calibration pattern (should show a circle)" };

// Save calibration option
var saveCalibrationOption = new Option<bool>("--save") { Description = "Save current --char-aspect to calibration.json (use with --calibrate)" };

// Status line option - shows progress, timing, file info below the video
var statusOption = new Option<bool>("--status") { Description = "Show status line below output with progress, timing, file info" };
statusOption.Aliases.Add("-S");

// Unified output option - auto-detects format from extension
// .gif -> animated GIF, .json/.ndjson -> JSON document, other -> text
var outputOption = new Option<string?>("--output") { Description = "Output file path. Format auto-detected: .gif for animated GIF, .json for JSON document" };
outputOption.Aliases.Add("-o");

// GIF-specific options (apply when output is .gif)
var gifFontSizeOption = new Option<int>("--gif-font-size") { Description = "Font size for GIF output (smaller = smaller file)", DefaultValueFactory = _ => 10 };
var gifScaleOption = new Option<float>("--gif-scale") { Description = "Scale factor for GIF output (0.5 = half size)", DefaultValueFactory = _ => 1.0f };
var gifColorsOption = new Option<int>("--gif-colors") { Description = "Max colors in GIF palette (16-256, lower = smaller file)", DefaultValueFactory = _ => 64 };

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

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var inputPath = parseResult.GetValue(inputArg);
    FileInfo? input = !string.IsNullOrEmpty(inputPath) ? new FileInfo(inputPath) : null;
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
    var output = parseResult.GetValue(outputOption);

    // Parse unified output option - auto-detect format from extension
    bool outputAsJson = false;
    bool outputAsGif = false;
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
            outputAsGif = true;
            // Use input filename with .gif extension - will set later when we have input
        }
        else if (output.StartsWith("gif:", StringComparison.OrdinalIgnoreCase))
        {
            outputAsGif = true;
            gifOutputPath = output[4..]; // Everything after "gif:"
        }
        // Auto-detect from extension
        else if (output.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            outputAsGif = true;
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
            Console.Error.WriteLine($"Warning: Unknown output format for '{output}'. Use .gif for animated GIF or .json for JSON document.");
            Console.Error.WriteLine("Assuming GIF output. Use 'json:path' prefix for JSON.");
            outputAsGif = true;
            gifOutputPath = output.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? output : output + ".gif";
        }
    }

    // Create FileInfo for GIF output if path is set
    FileInfo? outputGif = !string.IsNullOrEmpty(gifOutputPath) ? new FileInfo(gifOutputPath) : null;

    // Calibration mode - show test pattern to verify aspect ratio
    if (calibrate)
    {
        ConsoleHelper.EnableAnsiSupport();

        // Determine render mode for calibration
        var calibrationMode = useBraille ? RenderMode.Braille
            : useBlocks ? RenderMode.ColorBlocks
            : RenderMode.Ascii;

        // Get effective aspect ratio: explicit > saved for mode > default
        float calibrationAspect = charAspect
            ?? savedCalibration?.GetAspectRatio(calibrationMode)
            ?? 0.5f;

        string modeName = calibrationMode switch
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
            useColor: !noColor,
            width: 40,
            height: 20);

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
            string modeFlag = calibrationMode switch
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
        Console.Error.WriteLine("Error: No input file specified. Use --calibrate for aspect ratio testing without a file.");
        return 1;
    }

    // For AOT builds, mapped network drives (like X:) may not be accessible.
    // Try the original path first, then fall back to FileInfo resolution.
    string inputFullPath = inputPath!;

    // First try the path exactly as given
    if (!File.Exists(inputFullPath))
    {
        // Try FileInfo resolution (handles relative paths)
        inputFullPath = input.FullName;
    }

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
            char driveLetter = char.ToUpper(inputPath[0]);
            if (driveLetter >= 'D') // Likely a mapped/network drive
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"  Drive {driveLetter}: may be a mapped network drive.");
                Console.Error.WriteLine("  Mapped drives are user-specific and may not be accessible in some contexts.");
                Console.Error.WriteLine("  Try using the full UNC path instead (e.g., \\\\server\\share\\path\\file.mkv)");
            }
        }

        var dir = Path.GetDirectoryName(inputFullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            bool dirExists = false;
            try { dirExists = Directory.Exists(dir); } catch { }
            Console.Error.WriteLine($"  Directory exists: {dirExists}");
        }

        return 1;
    }

    // Update input to use the working path
    input = new FileInfo(inputFullPath);

    // Determine JSON output path if needed
    if (outputAsJson && string.IsNullOrEmpty(jsonOutputPath))
    {
        jsonOutputPath = Path.ChangeExtension(input.FullName, ".json");
    }

    // Check if input is a JSON document - load and play it
    // Supports both .json and .ndjson (streaming JSON Lines format)
    if (input.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
        input.Extension.Equals(".ndjson", StringComparison.OrdinalIgnoreCase))
    {
        return await HandleJsonDocument(input.FullName, speed, loop, cancellationToken);
    }

    // Check if input is an image file - handle directly without FFmpeg
    var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif"
    };

    if (imageExtensions.Contains(input.Extension))
    {
        return await HandleImageFile(
            input, width, height, maxWidth, maxHeight, charAspect, savedCalibration,
            parseResult.GetValue(colorBlocksOption), parseResult.GetValue(brailleOption),
            noColor, contrast, gamma, loop, speed, outputGif,
            parseResult.GetValue(gifFontSizeOption), parseResult.GetValue(gifScaleOption),
            parseResult.GetValue(gifColorsOption),
            outputAsJson, jsonOutputPath,
            showStatus,
            cancellationToken);
    }

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

    // Show FFmpeg status and initialize (may download if not found)
    if (!FFmpegProvider.IsAvailable(ffmpegPath))
    {
        Console.WriteLine("FFmpeg not found. Downloading...");
        Console.WriteLine($"Cache location: {FFmpegProvider.CacheDirectory}");
        Console.WriteLine();
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
        Console.WriteLine($"Hardware Accel: {(string.IsNullOrEmpty(ffmpeg.HardwareAccelerationType) ? "none" : ffmpeg.HardwareAccelerationType)}");
        return 0;
    }

    // Calculate end time from duration if specified (must be done before any output mode)
    if (duration.HasValue && start.HasValue)
    {
        end = start.Value + duration.Value;
    }
    else if (duration.HasValue)
    {
        end = duration.Value;
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

        // Determine render mode from flags
        bool gifUseBlocks = parseResult.GetValue(colorBlocksOption);
        bool gifUseBraille = parseResult.GetValue(brailleOption);
        var gifRenderMode = gifUseBraille ? RenderMode.Braille
            : gifUseBlocks ? RenderMode.ColorBlocks
            : RenderMode.Ascii;

        // Get effective aspect ratio
        float gifEffectiveAspect = charAspect
            ?? savedCalibration?.GetAspectRatio(gifRenderMode)
            ?? 0.5f;

        // Calculate target FPS for GIF
        double gifTargetFps = fps ?? Math.Min(videoInfo.FrameRate, 15.0);
        int frameDelayMs = (int)(1000.0 / gifTargetFps / speed);

        Console.WriteLine($"Rendering video to animated GIF...");
        Console.WriteLine($"  Output: {outputGif.FullName}");
        Console.WriteLine($"  Source: {videoInfo.Width}x{videoInfo.Height} @ {videoInfo.FrameRate:F2} fps");
        Console.WriteLine($"  Target: {gifTargetFps:F1} fps, frame step {frameStep}");

        // GIF loop: 0 = infinite, which is the default for GIFs
        // Only use explicit loop count if user specified it (loop != 1 which is the CLI default)
        int gifLoopCount = loop != 1 ? loop : 0;

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
        int renderedCount = 0;
        double gifStartTime = start ?? 0;

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
        int pixelsPerCharWidth = gifUseBraille ? 2 : 1;
        int pixelsPerCharHeight = gifUseBraille ? 4 : (gifUseBlocks ? 2 : 1);
        var (pixelWidth, pixelHeight) = tempOptions.CalculateVisualDimensions(
            videoInfo.Width, videoInfo.Height, pixelsPerCharWidth, pixelsPerCharHeight);

        // Calculate character dimensions from pixel dimensions
        int charWidth = pixelWidth / pixelsPerCharWidth;
        int charHeight = pixelHeight / pixelsPerCharHeight;

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
            Invert = true,
            UseParallelProcessing = true
        };

        // Estimate total frames for status display
        double effectiveStart = gifStartTime;
        double effectiveEnd = end ?? videoInfo.Duration;
        double effectiveDuration = effectiveEnd - effectiveStart;
        int estimatedTotalFrames = (int)(effectiveDuration * gifTargetFps / frameStep);

        // Create status line renderer if enabled
        StatusLine? statusRenderer = showStatus ? new StatusLine(charWidth, !noColor) : null;
        string renderModeName = gifUseBraille ? "Braille" : (gifUseBlocks ? "Blocks" : "ASCII");

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
                string content = asciiFrame.ToAnsiString();

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
                        CurrentTime = TimeSpan.FromSeconds(effectiveStart + (renderedCount * frameStep / gifTargetFps)),
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
        bool jsonUseBlocks = parseResult.GetValue(colorBlocksOption);
        bool jsonUseBraille = parseResult.GetValue(brailleOption);
        var jsonRenderMode = jsonUseBraille ? RenderMode.Braille
            : jsonUseBlocks ? RenderMode.ColorBlocks
            : RenderMode.Ascii;

        // Get effective aspect ratio
        float jsonEffectiveAspect = charAspect
            ?? savedCalibration?.GetAspectRatio(jsonRenderMode)
            ?? 0.5f;

        // Calculate target FPS for JSON
        double jsonTargetFps = fps ?? Math.Min(videoInfo.FrameRate, 30.0);
        int frameDelayMs = (int)(1000.0 / jsonTargetFps / speed);

        Console.WriteLine($"Streaming video to JSON document...");
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
        int pixelsPerCharWidth = jsonUseBraille ? 2 : 1;
        int pixelsPerCharHeight = jsonUseBraille ? 4 : (jsonUseBlocks ? 2 : 1);
        var (pixelWidth, pixelHeight) = tempOptions.CalculateVisualDimensions(
            videoInfo.Width, videoInfo.Height, pixelsPerCharWidth, pixelsPerCharHeight);

        int charWidth = pixelWidth / pixelsPerCharWidth;
        int charHeight = pixelHeight / pixelsPerCharHeight;

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
            Invert = true,
            UseParallelProcessing = true
        };

        string renderModeName = jsonUseBraille ? "Braille" : (jsonUseBlocks ? "ColorBlocks" : "ASCII");

        // Create streaming document writer
        await using var docWriter = new StreamingDocumentWriter(
            jsonOutputPath,
            renderModeName,
            jsonRenderOptions,
            input.FullName);

        await docWriter.WriteHeaderAsync(cancellationToken);

        int renderedCount = 0;
        double jsonStartTime = start ?? 0;

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
        await docWriter.FinalizeAsync(isComplete: !cancellationToken.IsCancellationRequested, cancellationToken);
        Console.WriteLine($"\rSaved {renderedCount} frames to {jsonOutputPath}              ");
        return 0;
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
            _ => CharacterMap.ExtendedCharacterSet
        };
    }

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
    float effectiveAspect = charAspect
        ?? savedCalibration?.GetAspectRatio(coreRenderMode)
        ?? 0.5f;

    // Parse sampling strategy
    var samplingStrategy = (sampling?.ToLowerInvariant()) switch
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
            Invert = true,
            UseParallelProcessing = true,
            DarkTerminalBrightnessThreshold = 0.1f,
            LightTerminalBrightnessThreshold = 0.9f
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
        if (doc.IsAnimated)
        {
            Console.Error.WriteLine($"Duration: {doc.TotalDurationMs}ms");
        }

        // Override settings if specified
        float effectiveSpeed = speed != 1.0f ? speed : doc.Settings.AnimationSpeedMultiplier;
        int effectiveLoop = loop != 1 ? loop : doc.Settings.LoopCount;

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
    float effectiveAspect = charAspect
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
    bool isAnimatedGif = input.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);

    if (outputGif != null)
    {
        // GIF output mode
        // GIF loop: 0 = infinite, which is the default for GIFs
        int gifLoopCount = loop != 1 ? loop : 0;

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
            Console.WriteLine($"Rendering animated GIF...");
            Console.WriteLine($"  Output: {outputGif.FullName}");

            // Status line in GIF output only supported for ASCII mode
            string renderModeName = useBraille ? "Braille" : (useBlocks ? "Blocks" : "ASCII");
            StatusLine? statusRenderer = null;
            if (showStatus)
            {
                if (useBraille || useBlocks)
                {
                    Console.WriteLine("Note: Status line in GIF output is only supported for ASCII mode.");
                }
                else
                {
                    statusRenderer = new StatusLine(maxWidth, !noColor);
                }
            }

            if (useBraille)
            {
                using var renderer = new BrailleRenderer(options);
                var frames = renderer.RenderGif(input.FullName);
                var frameList = frames.ToList();
                int totalFrames = frameList.Count;
                int frameIndex = 0;

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
                int totalFrames = frameList.Count;
                int frameIndex = 0;

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
                int totalFrames = frameList.Count;
                int frameIndex = 0;

                foreach (var frame in frameList)
                {
                    string content = frame.ToAnsiString();
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
            float? darkThreshold = options.Invert ? options.DarkTerminalBrightnessThreshold : null;
            float? lightThreshold = !options.Invert ? options.LightTerminalBrightnessThreshold : null;
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
    int frameHeight = firstContent.Split('\n').Length;

    // Enter alternate screen buffer
    Console.Write("\x1b[?1049h"); // Enter alt screen
    Console.Write("\x1b[?25l");   // Hide cursor

    try
    {
        int loops = 0;
        while (!ct.IsCancellationRequested && (loopCount == 0 || loops < loopCount))
        {
            foreach (var frame in frames)
            {
                if (ct.IsCancellationRequested) break;

                // Move to top-left and render frame
                Console.Write("\x1b[H"); // Home position
                Console.Write(frame.Content);

                // Wait for frame delay
                int delayMs = Math.Max(1, (int)(frame.DelayMs / speed));
                await Task.Delay(delayMs, ct);
            }
            loops++;
        }
    }
    catch (OperationCanceledException) { }
    finally
    {
        Console.Write("\x1b[?25h");   // Show cursor
        Console.Write("\x1b[?1049l"); // Exit alt screen
    }
}
