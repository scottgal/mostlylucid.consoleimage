// Spectre.Console integration demo for ConsoleImage
// Shows how to render ASCII art and animations within Spectre's layout system

using ConsoleImage.Core;
using Spectre.Console;

// Ensure ANSI support is enabled
ConsoleHelper.EnableAnsiSupport();

if (args.Length == 0)
{
    AnsiConsole.MarkupLine("[yellow]Usage:[/] ConsoleImage.SpectreDemo <image1> [image2] [image3] ...");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[dim]Examples:[/]");
    AnsiConsole.MarkupLine("  ConsoleImage.SpectreDemo image.png              [dim]# Single image in panel[/]");
    AnsiConsole.MarkupLine("  ConsoleImage.SpectreDemo a.gif b.gif            [dim]# Side-by-side animations[/]");
    AnsiConsole.MarkupLine("  ConsoleImage.SpectreDemo a.png b.png c.png      [dim]# Three images in row[/]");
    return;
}

var files = args.Select(a => new FileInfo(a)).ToList();
foreach (var f in files)
{
    if (!f.Exists)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {f.FullName}");
        return;
    }
}

// Check if any are GIFs
bool hasGifs = files.Any(f => f.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase));

var options = new RenderOptions
{
    MaxWidth = 40,  // Keep individual images small for side-by-side
    MaxHeight = 20,
    UseColor = true,
    ContrastPower = 2.5f
};

if (hasGifs && files.Count > 1)
{
    // Side-by-side animation demo
    await PlaySideBySideAnimations(files, options);
}
else if (hasGifs)
{
    // Single GIF in panel
    await PlaySingleAnimation(files[0], options);
}
else
{
    // Static images side by side
    DisplayStaticImages(files, options);
}

static void DisplayStaticImages(List<FileInfo> files, RenderOptions options)
{
    var panels = new List<Panel>();

    foreach (var file in files)
    {
        using var renderer = new AsciiRenderer(options);
        var frame = renderer.RenderFile(file.FullName);
        var content = frame.ToAnsiString();

        var panel = new Panel(new Text(content))
            .Header($"[cyan]{file.Name}[/]")
            .Border(BoxBorder.Rounded)
            .Expand();
        panels.Add(panel);
    }

    var columns = new Columns(panels.ToArray());
    AnsiConsole.Write(columns);
}

static async Task PlaySingleAnimation(FileInfo file, RenderOptions options)
{
    using var renderer = new AsciiRenderer(options);
    var frames = renderer.RenderGif(file.FullName);

    if (frames.Count <= 1)
    {
        var panel = new Panel(new Text(frames[0].ToAnsiString()))
            .Header($"[cyan]{file.Name}[/]")
            .Border(BoxBorder.Rounded);
        AnsiConsole.Write(panel);
        return;
    }

    AnsiConsole.MarkupLine($"[dim]Playing {file.Name} ({frames.Count} frames) - Press Ctrl+C to stop[/]");
    AnsiConsole.WriteLine();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    await AnsiConsole.Live(new Text(""))
        .StartAsync(async ctx =>
        {
            int frameIndex = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                var frame = frames[frameIndex];
                var panel = new Panel(new Text(frame.ToAnsiString()))
                    .Header($"[cyan]{file.Name}[/] [dim]Frame {frameIndex + 1}/{frames.Count}[/]")
                    .Border(BoxBorder.Rounded);

                ctx.UpdateTarget(panel);
                ctx.Refresh();

                try
                {
                    await Task.Delay(frame.DelayMs, cts.Token);
                }
                catch (OperationCanceledException) { break; }

                frameIndex = (frameIndex + 1) % frames.Count;
            }
        });
}

static async Task PlaySideBySideAnimations(List<FileInfo> files, RenderOptions options)
{
    // Pre-render all frames for all GIFs
    var allFrames = new List<List<AsciiFrame>>();
    var frameIndices = new int[files.Count];

    foreach (var file in files)
    {
        if (file.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            using var renderer = new AsciiRenderer(options);
            allFrames.Add(renderer.RenderGif(file.FullName).ToList());
        }
        else
        {
            // Static image - just render once and wrap in list
            using var renderer = new AsciiRenderer(options);
            allFrames.Add(new List<AsciiFrame> { renderer.RenderFile(file.FullName) });
        }
    }

    AnsiConsole.MarkupLine($"[dim]Playing {files.Count} animations side by side - Press Ctrl+C to stop[/]");
    AnsiConsole.WriteLine();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    // Track timing per animation
    var lastFrameTime = new DateTime[files.Count];
    for (int i = 0; i < files.Count; i++)
        lastFrameTime[i] = DateTime.UtcNow;

    await AnsiConsole.Live(new Text(""))
        .AutoClear(false)
        .StartAsync(async ctx =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var panels = new List<Panel>();
                var now = DateTime.UtcNow;

                for (int i = 0; i < files.Count; i++)
                {
                    var frames = allFrames[i];
                    var frame = frames[frameIndices[i]];

                    // Check if it's time to advance this animation
                    if ((now - lastFrameTime[i]).TotalMilliseconds >= frame.DelayMs)
                    {
                        frameIndices[i] = (frameIndices[i] + 1) % frames.Count;
                        lastFrameTime[i] = now;
                    }

                    var currentFrame = frames[frameIndices[i]];
                    var header = frames.Count > 1
                        ? $"[cyan]{files[i].Name}[/] [dim]{frameIndices[i] + 1}/{frames.Count}[/]"
                        : $"[cyan]{files[i].Name}[/]";

                    var panel = new Panel(new Text(currentFrame.ToAnsiString()))
                        .Header(header)
                        .Border(BoxBorder.Rounded)
                        .Expand();
                    panels.Add(panel);
                }

                ctx.UpdateTarget(new Columns(panels.ToArray()));
                ctx.Refresh();

                try
                {
                    // Update at ~30fps for smooth animation
                    await Task.Delay(33, cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        });
}
