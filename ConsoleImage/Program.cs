// ASCII Art CLI - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering

using System.CommandLine;
using ConsoleImage.Core;

// Create root command
var rootCommand = new RootCommand("Convert images to ASCII art using shape-matching algorithm")
{
    Name = "ascii-image"
};

// Input file argument
var inputArg = new Argument<FileInfo>(
    name: "input",
    description: "Path to the image or GIF file to convert");

// Options
var widthOption = new Option<int?>(
    aliases: ["--width", "-w"],
    description: "Output width in characters");

var heightOption = new Option<int?>(
    aliases: ["--height", "-h"],
    description: "Output height in characters");

var maxWidthOption = new Option<int>(
    aliases: ["--max-width"],
    getDefaultValue: () => 120,
    description: "Maximum output width");

var maxHeightOption = new Option<int>(
    aliases: ["--max-height"],
    getDefaultValue: () => 60,
    description: "Maximum output height");

var noColorOption = new Option<bool>(
    aliases: ["--no-color"],
    description: "Disable colored output (monochrome)");

var noInvertOption = new Option<bool>(
    aliases: ["--no-invert"],
    description: "Don't invert output (for light backgrounds)");

var contrastOption = new Option<float>(
    aliases: ["--contrast"],
    getDefaultValue: () => 2.5f,
    description: "Contrast enhancement power (1.0 = none, higher = more contrast)");

var charsetOption = new Option<string?>(
    aliases: ["--charset"],
    description: "Custom character set (ordered from light to dark)");

var presetOption = new Option<string?>(
    aliases: ["--preset", "-p"],
    description: "Character set preset: default, simple, block");

var outputOption = new Option<FileInfo?>(
    aliases: ["--output", "-o"],
    description: "Write output to file instead of console");

var noAnimateOption = new Option<bool>(
    aliases: ["--no-animate"],
    description: "Don't animate GIFs - just show first frame");

var speedOption = new Option<float>(
    aliases: ["--speed", "-s"],
    getDefaultValue: () => 1.0f,
    description: "Animation speed multiplier");

var loopOption = new Option<int>(
    aliases: ["--loop", "-l"],
    getDefaultValue: () => 0,
    description: "Number of animation loops (0 = infinite, default)");

var edgeOption = new Option<bool>(
    aliases: ["--edge", "-e"],
    description: "Enable edge detection to enhance foreground visibility");

var bgThresholdOption = new Option<float?>(
    aliases: ["--bg-threshold"],
    description: "Background suppression threshold (0.0-1.0). Pixels above this brightness are suppressed.");

var darkBgThresholdOption = new Option<float?>(
    aliases: ["--dark-bg-threshold"],
    description: "Dark background suppression threshold (0.0-1.0). Pixels below this brightness are suppressed.");

var autoBgOption = new Option<bool>(
    aliases: ["--auto-bg"],
    description: "Automatically detect and suppress background");

var colorBlocksOption = new Option<bool>(
    aliases: ["--blocks", "-b"],
    description: "Use colored Unicode blocks for high-fidelity output (requires 24-bit color terminal)");

var noParallelOption = new Option<bool>(
    aliases: ["--no-parallel"],
    description: "Disable parallel processing");

// Add options to root command
rootCommand.AddArgument(inputArg);
rootCommand.AddOption(widthOption);
rootCommand.AddOption(heightOption);
rootCommand.AddOption(maxWidthOption);
rootCommand.AddOption(maxHeightOption);
rootCommand.AddOption(noColorOption);
rootCommand.AddOption(noInvertOption);
rootCommand.AddOption(contrastOption);
rootCommand.AddOption(charsetOption);
rootCommand.AddOption(presetOption);
rootCommand.AddOption(outputOption);
rootCommand.AddOption(noAnimateOption);
rootCommand.AddOption(speedOption);
rootCommand.AddOption(loopOption);
rootCommand.AddOption(edgeOption);
rootCommand.AddOption(bgThresholdOption);
rootCommand.AddOption(darkBgThresholdOption);
rootCommand.AddOption(autoBgOption);
rootCommand.AddOption(colorBlocksOption);
rootCommand.AddOption(noParallelOption);

rootCommand.SetHandler(async (context) =>
{
    var input = context.ParseResult.GetValueForArgument(inputArg);
    var width = context.ParseResult.GetValueForOption(widthOption);
    var height = context.ParseResult.GetValueForOption(heightOption);
    var maxWidth = context.ParseResult.GetValueForOption(maxWidthOption);
    var maxHeight = context.ParseResult.GetValueForOption(maxHeightOption);
    var noColor = context.ParseResult.GetValueForOption(noColorOption);
    var noInvert = context.ParseResult.GetValueForOption(noInvertOption);
    var contrast = context.ParseResult.GetValueForOption(contrastOption);
    var charset = context.ParseResult.GetValueForOption(charsetOption);
    var preset = context.ParseResult.GetValueForOption(presetOption);
    var output = context.ParseResult.GetValueForOption(outputOption);
    var noAnimate = context.ParseResult.GetValueForOption(noAnimateOption);
    var speed = context.ParseResult.GetValueForOption(speedOption);
    var loop = context.ParseResult.GetValueForOption(loopOption);
    var enableEdge = context.ParseResult.GetValueForOption(edgeOption);
    var bgThreshold = context.ParseResult.GetValueForOption(bgThresholdOption);
    var darkBgThreshold = context.ParseResult.GetValueForOption(darkBgThresholdOption);
    var autoBg = context.ParseResult.GetValueForOption(autoBgOption);
    var colorBlocks = context.ParseResult.GetValueForOption(colorBlocksOption);
    var noParallel = context.ParseResult.GetValueForOption(noParallelOption);

    if (!input.Exists)
    {
        Console.Error.WriteLine($"Error: File not found: {input.FullName}");
        context.ExitCode = 1;
        return;
    }

    // Determine character set
    string? characterSet = charset;
    if (string.IsNullOrEmpty(characterSet) && !string.IsNullOrEmpty(preset))
    {
        characterSet = preset.ToLowerInvariant() switch
        {
            "simple" => CharacterMap.SimpleCharacterSet,
            "block" => CharacterMap.BlockCharacterSet,
            "default" or _ => CharacterMap.DefaultCharacterSet
        };
    }

    var options = new RenderOptions
    {
        Width = width,
        Height = height,
        MaxWidth = maxWidth,
        MaxHeight = maxHeight,
        UseColor = !noColor || colorBlocks,
        Invert = !noInvert,
        ContrastPower = contrast,
        CharacterSet = characterSet,
        AnimationSpeedMultiplier = speed,
        LoopCount = loop,
        EnableEdgeDetection = enableEdge,
        BackgroundThreshold = bgThreshold,
        DarkBackgroundThreshold = darkBgThreshold,
        AutoBackgroundSuppression = autoBg,
        UseParallelProcessing = !noParallel
    };

    try
    {
        // Check if it's a GIF
        bool isGif = input.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);

        // Use color blocks mode for high-fidelity output
        if (colorBlocks)
        {
            using var blockRenderer = new ColorBlockRenderer(options);

            if (isGif && !noAnimate)
            {
                var frames = blockRenderer.RenderGif(input.FullName);

                if (frames.Count > 1)
                {
                    Console.WriteLine($"Playing {frames.Count} frames in color block mode (Press Ctrl+C to stop)...");
                    Console.WriteLine();

                    using var cts = new CancellationTokenSource();
                    Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    int startRow = Console.CursorTop;
                    int loops = 0;

                    while (!cts.Token.IsCancellationRequested)
                    {
                        foreach (var frame in frames)
                        {
                            if (cts.Token.IsCancellationRequested) break;

                            try { Console.SetCursorPosition(0, startRow); } catch { }
                            Console.Write(frame.Content);
                            Console.Write("\x1b[0m");

                            if (frame.DelayMs > 0)
                            {
                                await Task.Delay(frame.DelayMs, cts.Token);
                            }
                        }

                        loops++;
                        if (loop > 0 && loops >= loop) break;
                    }
                }
                else
                {
                    Console.WriteLine(frames[0].Content);
                    Console.Write("\x1b[0m");
                }
            }
            else
            {
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
        else
        {
            // Standard ASCII rendering
            using var renderer = new AsciiRenderer(options);

            if (isGif && !noAnimate)
            {
                // Play animated GIF
                var frames = renderer.RenderGif(input.FullName);

                if (frames.Count > 1)
                {
                    Console.WriteLine($"Playing {frames.Count} frames (Press Ctrl+C to stop)...");
                    Console.WriteLine();

                    using var player = new AsciiAnimationPlayer(frames, !noColor, loop);
                    using var cts = new CancellationTokenSource();

                    Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    await player.PlayAsync(cts.Token);
                }
                else
                {
                    // Single frame GIF, just render it
                    OutputFrame(frames[0], !noColor, output);
                }
            }
            else if (isGif)
            {
                // Render all GIF frames as static output
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
                    OutputFrame(frames[0], !noColor, null);
                    if (frames.Count > 1)
                    {
                        Console.WriteLine($"\n(GIF has {frames.Count} frames - use --animate to play)");
                    }
                }
            }
            else
            {
                // Render single image
                var frame = renderer.RenderFile(input.FullName);
                OutputFrame(frame, !noColor, output);
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        context.ExitCode = 1;
    }
});

return await rootCommand.InvokeAsync(args);

static void OutputFrame(AsciiFrame frame, bool useColor, FileInfo? output)
{
    string result = useColor ? frame.ToAnsiString() : frame.ToString();

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
