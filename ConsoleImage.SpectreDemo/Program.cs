// Spectre.Console integration demo for ConsoleImage
// Shows how to use ConsoleImage.Spectre renderables in Spectre layouts

using ConsoleImage.Core;
using ConsoleImage.Spectre;
using Spectre.Console;
using Spectre.Console.Rendering;
using RenderOptions = ConsoleImage.Core.RenderOptions;

// Ensure ANSI support is enabled
ConsoleHelper.EnableAnsiSupport();

if (args.Length == 0)
{
    AnsiConsole.MarkupLine("[yellow]Usage:[/] ConsoleImage.SpectreDemo <image1> [[image2]] [[image3]] ...");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[cyan]Modes:[/]");
    AnsiConsole.MarkupLine("  [dim]--ascii[/]      Standard ASCII art (default)");
    AnsiConsole.MarkupLine("  [dim]--blocks[/]     High-fidelity color blocks");
    AnsiConsole.MarkupLine("  [dim]--braille[/]    Ultra-high resolution braille");
    AnsiConsole.MarkupLine("  [dim]--matrix[/]     Matrix digital rain effect");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[dim]Examples:[/]");
    AnsiConsole.MarkupLine("  ConsoleImage.SpectreDemo image.png");
    AnsiConsole.MarkupLine("  ConsoleImage.SpectreDemo a.gif b.gif --blocks");
    AnsiConsole.MarkupLine("  ConsoleImage.SpectreDemo a.png b.png c.png --braille");
    AnsiConsole.MarkupLine("  ConsoleImage.SpectreDemo photo.jpg --matrix");
    return;
}

// Parse mode from args
var mode = AnimationMode.Ascii;
var files = new List<FileInfo>();

foreach (var arg in args)
    if (arg == "--ascii") mode = AnimationMode.Ascii;
    else if (arg == "--blocks") mode = AnimationMode.ColorBlock;
    else if (arg == "--braille") mode = AnimationMode.Braille;
    else if (arg == "--matrix") mode = AnimationMode.Matrix;
    else files.Add(new FileInfo(arg));

foreach (var f in files)
    if (!f.Exists)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {f.FullName}");
        return;
    }

var options = new RenderOptions
{
    MaxWidth = files.Count > 1 ? 40 : 80,
    MaxHeight = files.Count > 1 ? 20 : 40,
    UseColor = true,
    ContrastPower = 2.5f
};

var hasGifs = files.Any(f => f.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase));

if (hasGifs && files.Count > 1)
    // Side-by-side animation demo
    await PlaySideBySideAnimations(files, mode, options);
else if (hasGifs)
    // Single GIF animation
    await PlaySingleAnimation(files[0], mode, options);
else if (files.Count > 1)
    // Multiple static images side by side
    DisplaySideBySide(files, mode, options);
else
    // Single static image
    DisplaySingle(files[0], mode, options);

static void DisplaySingle(FileInfo file, AnimationMode mode, RenderOptions options)
{
    IRenderable image = mode switch
    {
        AnimationMode.ColorBlock => new ColorBlockImage(file.FullName, options),
        AnimationMode.Braille => new BrailleImage(file.FullName, options),
        AnimationMode.Matrix => new MatrixImage(file.FullName, options),
        _ => new AsciiImage(file.FullName, options)
    };

    var panel = new Panel(image)
        .Header($"[cyan]{Markup.Escape(file.Name)}[/]")
        .Border(BoxBorder.Rounded);

    AnsiConsole.Write(panel);
}

static void DisplaySideBySide(List<FileInfo> files, AnimationMode mode, RenderOptions options)
{
    var panels = new List<Panel>();

    foreach (var file in files)
    {
        IRenderable image = mode switch
        {
            AnimationMode.ColorBlock => new ColorBlockImage(file.FullName, options),
            AnimationMode.Braille => new BrailleImage(file.FullName, options),
            AnimationMode.Matrix => new MatrixImage(file.FullName, options),
            _ => new AsciiImage(file.FullName, options)
        };

        var panel = new Panel(image)
            .Header($"[cyan]{file.Name}[/]")
            .Border(BoxBorder.Rounded)
            .Expand();
        panels.Add(panel);
    }

    AnsiConsole.Write(new Columns(panels));
}

static async Task PlaySingleAnimation(FileInfo file, AnimationMode mode, RenderOptions options)
{
    AnsiConsole.MarkupLine($"[dim]Playing {file.Name} - Press Ctrl+C to stop[/]");
    AnsiConsole.WriteLine();

    var animation = new AnimatedImage(file.FullName, mode, options);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    await animation.PlayAsync(cts.Token);
}

static async Task PlaySideBySideAnimations(List<FileInfo> files, AnimationMode mode, RenderOptions options)
{
    AnsiConsole.MarkupLine($"[dim]Playing {files.Count} animations side by side - Press Ctrl+C to stop[/]");
    AnsiConsole.WriteLine();

    // Load all animations
    var animations = files.Select(f => new AnimatedImage(f.FullName, mode, options)).ToList();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    // Create initial layout
    IRenderable CreateLayout()
    {
        var panels = new List<Panel>();
        for (var i = 0; i < animations.Count; i++)
        {
            var header = animations[i].FrameCount > 1
                ? $"[cyan]{files[i].Name}[/] [dim]{animations[i].CurrentFrame + 1}/{animations[i].FrameCount}[/]"
                : $"[cyan]{files[i].Name}[/]";

            var panel = new Panel(animations[i])
                .Header(header)
                .Border(BoxBorder.Rounded)
                .Expand();
            panels.Add(panel);
        }

        return new Columns(panels);
    }

    await AnsiConsole.Live(CreateLayout())
        .AutoClear(false)
        .StartAsync(async ctx =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // Advance all animations
                foreach (var anim in animations)
                    anim.TryAdvanceFrame();

                ctx.UpdateTarget(CreateLayout());
                ctx.Refresh();

                try
                {
                    await Task.Delay(16, cts.Token); // ~60fps
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
}