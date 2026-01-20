// ASCII Art CLI - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering

using System.CommandLine;
using ConsoleImage.Core;

// Enable ANSI escape sequence processing on Windows consoles
ConsoleHelper.EnableAnsiSupport();

// Create root command
var rootCommand = new RootCommand("Convert images to ASCII art using shape-matching algorithm");

// Input file argument
var inputArg = new Argument<FileInfo>("input")
{
    Description = "Path to the image or GIF file to convert"
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

var charsetOption = new Option<string?>("--charset") { Description = "Custom character set (ordered from light to dark)" };

var presetOption = new Option<string?>("--preset") { Description = "Character set preset: extended (default), simple, block, classic" };
presetOption.Aliases.Add("-p");

var outputOption = new Option<FileInfo?>("--output") { Description = "Write output to file instead of console" };
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

var noAltScreenOption = new Option<bool>("--no-alt-screen") { Description = "Disable alternate screen buffer for animations (keeps output in scrollback)" };

var noParallelOption = new Option<bool>("--no-parallel") { Description = "Disable parallel processing" };

var noDitherOption = new Option<bool>("--no-dither") { Description = "Disable Floyd-Steinberg dithering" };

var noEdgeDirOption = new Option<bool>("--no-edge-chars") { Description = "Disable directional characters (/ \\ | -)" };

var jsonOption = new Option<bool>("--json") { Description = "Output as JSON (for LLM tool calls and programmatic use)" };
jsonOption.Aliases.Add("-j");

var darkCutoffOption = new Option<float?>("--dark-cutoff") { Description = "Dark terminal optimization: skip colors below this brightness (0.0-1.0, default: 0.1). Use 0 to disable." };
var lightCutoffOption = new Option<float?>("--light-cutoff") { Description = "Light terminal optimization: skip colors above this brightness (0.0-1.0, default: 0.9). Use 1 to disable." };

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
rootCommand.Options.Add(noAltScreenOption);
rootCommand.Options.Add(noParallelOption);
rootCommand.Options.Add(noDitherOption);
rootCommand.Options.Add(noEdgeDirOption);
rootCommand.Options.Add(jsonOption);
rootCommand.Options.Add(darkCutoffOption);
rootCommand.Options.Add(lightCutoffOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var input = parseResult.GetValue(inputArg)!;
    var width = parseResult.GetValue(widthOption);
    var height = parseResult.GetValue(heightOption);
    var aspectRatio = parseResult.GetValue(aspectRatioOption);
    var maxWidth = parseResult.GetValue(maxWidthOption);
    var maxHeight = parseResult.GetValue(maxHeightOption);
    var noColor = parseResult.GetValue(noColorOption);
    var noInvert = parseResult.GetValue(noInvertOption);
    var contrast = parseResult.GetValue(contrastOption);
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
    var noAltScreen = parseResult.GetValue(noAltScreenOption);
    var noParallel = parseResult.GetValue(noParallelOption);
    var noDither = parseResult.GetValue(noDitherOption);
    var noEdgeChars = parseResult.GetValue(noEdgeDirOption);
    var jsonOutput = parseResult.GetValue(jsonOption);
    var darkCutoff = parseResult.GetValue(darkCutoffOption);
    var lightCutoff = parseResult.GetValue(lightCutoffOption);

    // JSON mode helper - manual serialization for AOT compatibility
    void OutputJson(string json) => Console.WriteLine(json);
    string JsonEscape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\x1b", "\\u001b");

    if (!input.Exists)
    {
        if (jsonOutput)
        {
            OutputJson($"{{\"success\":false,\"error\":\"File not found: {JsonEscape(input.FullName)}\"}}");
            return 1;
        }
        Console.Error.WriteLine($"Error: File not found: {input.FullName}");
        return 1;
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

    var options = new RenderOptions
    {
        Width = width,
        Height = height,
        CharacterAspectRatio = aspectRatio ?? 0.5f,
        MaxWidth = maxWidth,
        MaxHeight = maxHeight,
        UseColor = !noColor || colorBlocks,
        Invert = !noInvert,
        ContrastPower = contrast,
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
        DarkTerminalBrightnessThreshold = darkCutoff ?? 0.1f,
        LightTerminalBrightnessThreshold = lightCutoff ?? 0.9f
    };

    try
    {
        // Check if it's a GIF
        bool isGif = input.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);

        // Use color blocks mode for high-fidelity output
        if (colorBlocks)
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
                        return renderer.RenderGif(input.FullName);
                    },
                    explicitWidth: width,
                    explicitHeight: height,
                    loopCount: loop,
                    useAltScreen: !noAltScreen,
                    targetFps: framerate
                );

                await player.PlayAsync(cts.Token);
            }
            else
            {
                // Static image rendering
                using var blockRenderer = new ColorBlockRenderer(options);
                string result = blockRenderer.RenderFile(input.FullName);
                if (output != null)
                {
                    File.WriteAllText(output.FullName, result);
                    Console.WriteLine($"Written to {output.FullName}");
                }
                else
                {
                    Console.WriteLine(result);
                    Console.Write("\x1b[0m"); // Reset colors
                }
            }
        }
        else if (braille)
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
                        return renderer.RenderGif(input.FullName);
                    },
                    explicitWidth: width,
                    explicitHeight: height,
                    loopCount: loop,
                    useAltScreen: !noAltScreen,
                    targetFps: framerate
                );

                await player.PlayAsync(cts.Token);
            }
            else
            {
                using var brailleRenderer = new BrailleRenderer(options);
                string result = brailleRenderer.RenderFile(input.FullName);
                if (output != null)
                {
                    File.WriteAllText(output.FullName, result);
                    Console.WriteLine($"Written to {output.FullName}");
                }
                else
                {
                    Console.WriteLine(result);
                    Console.Write("\x1b[0m");
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
                        var asciiFrames = renderer.RenderGif(input.FullName);
                        // Convert AsciiFrames to IAnimationFrame using adapter
                        return asciiFrames.Select(f => new AsciiFrameAdapter(f, !noColor, effectiveDarkThreshold, effectiveLightThreshold)).ToList<IAnimationFrame>();
                    },
                    explicitWidth: width,
                    explicitHeight: height,
                    loopCount: loop,
                    useAltScreen: !noAltScreen,
                    targetFps: framerate
                );

                await player.PlayAsync(cts.Token);
            }
            else if (isGif)
            {
                // Render all GIF frames as static output
                using var renderer = new AsciiRenderer(options);
                var frames = renderer.RenderGif(input.FullName);

                if (output != null)
                {
                    // Write all frames to file
                    using var writer = new StreamWriter(output.FullName);
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
                    Console.WriteLine($"Wrote {frames.Count} frames to {output.FullName}");
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
                var frame = renderer.RenderFile(input.FullName);
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
                    OutputFrame(frame, !noColor, output, options);
                }
            }
        }
        return 0;
    }
    catch (Exception ex)
    {
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

static void OutputFrame(AsciiFrame frame, bool useColor, FileInfo? output, RenderOptions options)
{
    // Determine brightness thresholds based on terminal mode (Invert)
    float? darkThreshold = options.Invert ? options.DarkTerminalBrightnessThreshold : null;
    float? lightThreshold = !options.Invert ? options.LightTerminalBrightnessThreshold : null;

    string result = useColor ? frame.ToAnsiString(darkThreshold, lightThreshold) : frame.ToString();

    if (output != null)
    {
        File.WriteAllText(output.FullName, frame.ToString()); // Don't write ANSI codes to file
        Console.WriteLine($"Written to {output.FullName}");
    }
    else
    {
        Console.WriteLine(result);
    }
}
