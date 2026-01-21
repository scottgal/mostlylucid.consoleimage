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
var inputArg = new Argument<FileInfo?>("input")
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

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var input = parseResult.GetValue(inputArg);
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
        var output = CalibrationHelper.RenderCalibrationPattern(
            calibrationMode,
            calibrationAspect,
            useColor: !noColor,
            width: 40,
            height: 20);

        Console.WriteLine(output);
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

    if (!input.Exists)
    {
        Console.Error.WriteLine($"Error: File not found: {input.FullName}");
        return 1;
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

    // Calculate end time from duration if specified
    if (duration.HasValue && start.HasValue)
    {
        end = start.Value + duration.Value;
    }
    else if (duration.HasValue)
    {
        end = duration.Value;
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
        RenderMode = renderMode
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
