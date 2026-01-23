// ConsoleImage.Player Demo - Benchmark and sample generation
// Demonstrates the minimal player library with real-world examples

using System.Diagnostics;
using ConsoleImage.Core;
using ConsoleImage.Player;

// Enable ANSI support on Windows
ConsoleHelper.EnableAnsiSupport();

var sampleDir = Path.Combine(AppContext.BaseDirectory, "samples");
Directory.CreateDirectory(sampleDir);

Console.WriteLine("ConsoleImage.Player Demo");
Console.WriteLine("========================\n");

// Check for command line args
if (args.Length > 0)
{
    if (args[0] == "generate")
    {
        await GenerateSamples(sampleDir);
        return 0;
    }
    else if (args[0] == "benchmark")
    {
        await RunBenchmark(sampleDir);
        return 0;
    }
    else if (args[0] == "play" && args.Length > 1)
    {
        await PlayDocument(args[1]);
        return 0;
    }
}

// Default: show menu
Console.WriteLine("Usage:");
Console.WriteLine("  player-demo generate   - Generate sample JSON documents from GIFs");
Console.WriteLine("  player-demo benchmark  - Run load/parse benchmark");
Console.WriteLine("  player-demo play <file.json> - Play a document");
Console.WriteLine();

// Quick demo with inline document
await QuickDemo();

return 0;

async Task QuickDemo()
{
    Console.WriteLine("Quick Demo - Loading inline document...\n");

    var json = """
    {
        "@context": "https://schema.org/",
        "@type": "ConsoleImageDocument",
        "Version": "2.0",
        "RenderMode": "ASCII",
        "Settings": {
            "MaxWidth": 40,
            "MaxHeight": 10,
            "UseColor": true,
            "AnimationSpeedMultiplier": 1.0,
            "LoopCount": 3
        },
        "Frames": [
            { "Content": "\u001b[32m  *  \u001b[0m\n\u001b[32m ***\u001b[0m\n\u001b[32m*****\u001b[0m\n  |  ", "DelayMs": 200, "Width": 5, "Height": 4 },
            { "Content": "\u001b[33m  *  \u001b[0m\n\u001b[33m ***\u001b[0m\n\u001b[33m*****\u001b[0m\n  |  ", "DelayMs": 200, "Width": 5, "Height": 4 },
            { "Content": "\u001b[31m  *  \u001b[0m\n\u001b[31m ***\u001b[0m\n\u001b[31m*****\u001b[0m\n  |  ", "DelayMs": 200, "Width": 5, "Height": 4 }
        ]
    }
    """;

    // Benchmark parsing
    var sw = Stopwatch.StartNew();
    var doc = PlayerDocument.FromJson(json);
    var parseTime = sw.Elapsed;

    Console.WriteLine($"Parsed in {parseTime.TotalMicroseconds:F0}µs ({doc.FrameCount} frames)");
    Console.WriteLine($"Document: {doc.RenderMode} mode, {doc.TotalDurationMs}ms duration\n");

    // Play it
    using var player = new ConsolePlayer(doc);
    Console.WriteLine(player.GetInfo());
    Console.WriteLine("\nPlaying animation (3 loops)...\n");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    await player.PlayAsync(cts.Token);

    Console.WriteLine("\n\nDone!");
}

async Task GenerateSamples(string outputDir)
{
    Console.WriteLine($"Generating samples to: {outputDir}\n");

    var gifPaths = new[]
    {
        @"F:\gifs\cat_wag.gif",
        @"F:\gifs\alanshrug_opt.gif",
        @"F:\gifs\boingball_10_80x80_256.gif"
    };

    foreach (var gifPath in gifPaths)
    {
        if (!File.Exists(gifPath))
        {
            Console.WriteLine($"Skipping (not found): {gifPath}");
            continue;
        }

        var baseName = Path.GetFileNameWithoutExtension(gifPath);
        Console.WriteLine($"Processing: {baseName}");

        var options = new RenderOptions
        {
            MaxWidth = 60,
            MaxHeight = 30,
            UseColor = true,
            Invert = true
        };

        // Generate ASCII version
        Console.Write("  ASCII... ");
        var sw = Stopwatch.StartNew();
        using (var renderer = new AsciiRenderer(options))
        {
            var frames = renderer.RenderGif(gifPath);
            var doc = ConsoleImageDocument.FromAsciiFrames(frames, options, baseName + ".gif");
            var jsonPath = Path.Combine(outputDir, $"{baseName}_ascii.json");
            await doc.SaveAsync(jsonPath);
            Console.WriteLine($"OK ({sw.ElapsedMilliseconds}ms, {doc.FrameCount} frames, {new FileInfo(jsonPath).Length / 1024}KB)");
        }

        // Generate Braille version (higher resolution)
        Console.Write("  Braille... ");
        sw.Restart();
        using (var renderer = new BrailleRenderer(options))
        {
            var frames = renderer.RenderGif(gifPath);
            var doc = ConsoleImageDocument.FromBrailleFrames(frames, options, baseName + ".gif");
            var jsonPath = Path.Combine(outputDir, $"{baseName}_braille.json");
            await doc.SaveAsync(jsonPath);
            Console.WriteLine($"OK ({sw.ElapsedMilliseconds}ms, {doc.FrameCount} frames, {new FileInfo(jsonPath).Length / 1024}KB)");
        }

        // Generate Blocks version
        Console.Write("  Blocks... ");
        sw.Restart();
        using (var renderer = new ColorBlockRenderer(options))
        {
            var frames = renderer.RenderGif(gifPath);
            var doc = ConsoleImageDocument.FromColorBlockFrames(frames, options, baseName + ".gif");
            var jsonPath = Path.Combine(outputDir, $"{baseName}_blocks.json");
            await doc.SaveAsync(jsonPath);
            Console.WriteLine($"OK ({sw.ElapsedMilliseconds}ms, {doc.FrameCount} frames, {new FileInfo(jsonPath).Length / 1024}KB)");
        }

        Console.WriteLine();
    }

    Console.WriteLine("Sample generation complete!");
    Console.WriteLine($"\nGenerated files in: {outputDir}");

    // List generated files
    foreach (var file in Directory.GetFiles(outputDir, "*.json"))
    {
        var fi = new FileInfo(file);
        Console.WriteLine($"  {fi.Name} ({fi.Length / 1024}KB)");
    }
}

async Task RunBenchmark(string sampleDir)
{
    Console.WriteLine("Player Benchmark");
    Console.WriteLine("================\n");

    // Find sample files
    var samples = Directory.GetFiles(sampleDir, "*.json");
    if (samples.Length == 0)
    {
        Console.WriteLine("No samples found. Run 'player-demo generate' first.");
        return;
    }

    Console.WriteLine($"Found {samples.Length} sample files\n");
    Console.WriteLine("| File | Size | Frames | Load Time | Parse/Frame |");
    Console.WriteLine("|------|------|--------|-----------|-------------|");

    foreach (var samplePath in samples.OrderBy(f => new FileInfo(f).Length))
    {
        var fi = new FileInfo(samplePath);
        var name = fi.Name;
        var sizeKb = fi.Length / 1024;

        // Cold load benchmark (includes file I/O)
        var sw = Stopwatch.StartNew();
        var doc = await PlayerDocument.LoadAsync(samplePath);
        var loadTime = sw.Elapsed;

        var perFrame = doc.FrameCount > 0
            ? loadTime.TotalMicroseconds / doc.FrameCount
            : 0;

        Console.WriteLine($"| {name,-30} | {sizeKb,4}KB | {doc.FrameCount,6} | {loadTime.TotalMilliseconds,7:F2}ms | {perFrame,9:F1}µs |");
    }

    Console.WriteLine();

    // Memory allocation benchmark
    Console.WriteLine("Memory Allocation Test (parsing same document 1000x):");
    var testFile = samples.First();
    var testJson = await File.ReadAllTextAsync(testFile);

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var memBefore = GC.GetTotalMemory(true);

    var sw2 = Stopwatch.StartNew();
    for (int i = 0; i < 1000; i++)
    {
        var _ = PlayerDocument.FromJson(testJson);
    }
    sw2.Stop();

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var memAfter = GC.GetTotalMemory(true);

    Console.WriteLine($"  File: {Path.GetFileName(testFile)}");
    Console.WriteLine($"  1000 parses in {sw2.ElapsedMilliseconds}ms ({sw2.ElapsedMilliseconds / 1000.0:F3}ms/parse)");
    Console.WriteLine($"  Memory delta: {(memAfter - memBefore) / 1024}KB");
}

async Task PlayDocument(string path)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"File not found: {path}");
        return;
    }

    Console.WriteLine($"Loading: {path}");
    var sw = Stopwatch.StartNew();
    var doc = await PlayerDocument.LoadAsync(path);
    Console.WriteLine($"Loaded in {sw.ElapsedMilliseconds}ms\n");

    using var player = new ConsolePlayer(doc);
    Console.WriteLine(player.GetInfo());

    if (doc.IsAnimated)
    {
        Console.WriteLine("\nPress Ctrl+C to stop\n");
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

        await player.PlayAsync(cts.Token);
        Console.WriteLine("\n");
    }
    else
    {
        player.Display();
    }
}
