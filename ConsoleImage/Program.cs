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

var colorOption = new Option<bool>(
    aliases: ["--color", "-c"],
    description: "Enable colored output using ANSI codes");

var invertOption = new Option<bool>(
    aliases: ["--invert", "-i"],
    description: "Invert the output (light on dark)");

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

var animateOption = new Option<bool>(
    aliases: ["--animate", "-a"],
    description: "Play animated GIFs in the console");

var speedOption = new Option<float>(
    aliases: ["--speed", "-s"],
    getDefaultValue: () => 1.0f,
    description: "Animation speed multiplier");

var loopOption = new Option<int>(
    aliases: ["--loop", "-l"],
    getDefaultValue: () => 1,
    description: "Number of animation loops (0 = infinite)");

// Add options to root command
rootCommand.AddArgument(inputArg);
rootCommand.AddOption(widthOption);
rootCommand.AddOption(heightOption);
rootCommand.AddOption(maxWidthOption);
rootCommand.AddOption(maxHeightOption);
rootCommand.AddOption(colorOption);
rootCommand.AddOption(invertOption);
rootCommand.AddOption(contrastOption);
rootCommand.AddOption(charsetOption);
rootCommand.AddOption(presetOption);
rootCommand.AddOption(outputOption);
rootCommand.AddOption(animateOption);
rootCommand.AddOption(speedOption);
rootCommand.AddOption(loopOption);

rootCommand.SetHandler(async (context) =>
{
    var input = context.ParseResult.GetValueForArgument(inputArg);
    var width = context.ParseResult.GetValueForOption(widthOption);
    var height = context.ParseResult.GetValueForOption(heightOption);
    var maxWidth = context.ParseResult.GetValueForOption(maxWidthOption);
    var maxHeight = context.ParseResult.GetValueForOption(maxHeightOption);
    var useColor = context.ParseResult.GetValueForOption(colorOption);
    var invert = context.ParseResult.GetValueForOption(invertOption);
    var contrast = context.ParseResult.GetValueForOption(contrastOption);
    var charset = context.ParseResult.GetValueForOption(charsetOption);
    var preset = context.ParseResult.GetValueForOption(presetOption);
    var output = context.ParseResult.GetValueForOption(outputOption);
    var animate = context.ParseResult.GetValueForOption(animateOption);
    var speed = context.ParseResult.GetValueForOption(speedOption);
    var loop = context.ParseResult.GetValueForOption(loopOption);

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
        UseColor = useColor,
        Invert = invert,
        ContrastPower = contrast,
        CharacterSet = characterSet,
        AnimationSpeedMultiplier = speed,
        LoopCount = loop
    };

    try
    {
        using var renderer = new AsciiRenderer(options);

        // Check if it's a GIF
        bool isGif = input.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);

        if (isGif && animate)
        {
            // Play animated GIF
            var frames = renderer.RenderGif(input.FullName);

            if (frames.Count > 1)
            {
                Console.WriteLine($"Playing {frames.Count} frames (Press Ctrl+C to stop)...");
                Console.WriteLine();

                using var player = new AsciiAnimationPlayer(frames, useColor, loop);
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
                OutputFrame(frames[0], useColor, output);
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
                OutputFrame(frames[0], useColor, null);
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
            OutputFrame(frame, useColor, output);
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
