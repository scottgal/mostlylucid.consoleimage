# ConsoleImage.Player

A minimal, zero-dependency player for ConsoleImage documents (.cidz, .json). Play pre-rendered ASCII art animations in
any terminal without needing the full rendering library.

**Use case**: Embed document playback in your own applications without pulling in ImageSharp or FFmpeg dependencies.

## Features

- **Zero external dependencies** - Only System.Text.Json (built-in)
- **AOT compatible** - Works with Native AOT publishing
- **Tiny footprint** - Just the essentials for playback
- **All formats supported** - Compressed .cidz, standard JSON, and streaming NDJSON
- **Fast parsing** - ~100-300µs per frame, instant startup
- **Low allocation** - Designed for minimal GC pressure

## Installation

```bash
dotnet add package mostlylucid.consoleimage.player
```

## Quick Start

### Play from file

```csharp
using ConsoleImage.Player;

// Load and play (supports .cidz, .json, .ndjson)
var player = await ConsolePlayer.FromFileAsync("animation.cidz");
await player.PlayAsync();

// Or from uncompressed JSON
var player = await ConsolePlayer.FromFileAsync("animation.json");
await player.PlayAsync();
```

### Play from JSON string

```csharp
using ConsoleImage.Player;

string json = /* your JSON document */;
var player = ConsolePlayer.FromJson(json);
await player.PlayAsync();
```

### Access frames directly

```csharp
using ConsoleImage.Player;

var doc = await PlayerDocument.LoadAsync("animation.json");

// Iterate frames
foreach (var frame in doc.GetFrames())
{
    Console.Write(frame.Content);
    await Task.Delay(frame.DelayMs);
}
```

## API

### PlayerDocument

The main document class with frame access:

```csharp
// Load from file (auto-detects JSON vs NDJSON)
var doc = await PlayerDocument.LoadAsync("file.json");

// Load from string
var doc = PlayerDocument.FromJson(jsonString);

// Properties
doc.FrameCount      // Number of frames
doc.IsAnimated      // true if more than 1 frame
doc.TotalDurationMs // Total animation duration
doc.RenderMode      // "ASCII", "ColorBlocks", "Braille", or "Matrix"
doc.Settings        // Playback settings

// Iterate frames
foreach (var frame in doc.Frames) { ... }
```

### ConsolePlayer

High-level player with animation support:

```csharp
var player = new ConsolePlayer(doc, speedMultiplier: 1.5f, loopCount: 3);

// Events
player.OnFrameChanged += (current, total) => { ... };
player.OnLoopComplete += (loopNum) => { ... };

// Play with cancellation
using var cts = new CancellationTokenSource();
await player.PlayAsync(cts.Token);

// Or display single frame
player.Display();

// Get info
Console.WriteLine(player.GetInfo());
```

### PlayerFrame

Individual frame data:

```csharp
frame.Content  // ANSI-escaped string content
frame.DelayMs  // Delay before next frame
frame.Width    // Width in characters
frame.Height   // Height in lines
```

## JSON Format

This player reads the ConsoleImage JSON document format:

```json
{
  "@context": "https://schema.org/",
  "@type": "ConsoleImageDocument",
  "Version": "2.0",
  "RenderMode": "ASCII",
  "Settings": {
    "AnimationSpeedMultiplier": 1.0,
    "LoopCount": 0
  },
  "Frames": [
    {
      "Content": "...",
      "DelayMs": 100,
      "Width": 80,
      "Height": 24
    }
  ]
}
```

Also supports streaming NDJSON format (one JSON object per line).

## Creating Documents

Use the `consoleimage` CLI to create documents from any source:

```bash
# From image
consoleimage photo.png -o output.cidz --blocks

# From animated GIF
consoleimage animation.gif -o animation.cidz

# From video
consoleimage movie.mp4 -o movie.cidz -w 80

# Uncompressed JSON (larger files)
consoleimage animation.gif -o output.json
```

Or use the full `mostlylucid.consoleimage` library to create documents programmatically.

## Complete Example: Animated Startup Logo

This example shows how to add an animated ASCII logo to your console application using a pre-rendered `.cidz` document.
The animation plays once on startup before your app begins.

### Render Modes

| ASCII Mode                                                                                                                      | ColorBlocks Mode                                                                                                                  | Braille Mode                                                                                                                        |
|---------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/moviebill_ascii.gif" width="200" alt="ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/moviebill_blocks.gif" width="200" alt="Blocks"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/moviebill_braille.gif" width="200" alt="Braille"> |

### Step 1: Create the Document

First, render your logo or animation to a `.cidz` file using the CLI:

```bash
# Render animated GIF to compressed document
consoleimage logo.gif -w 60 -o logo.cidz

# Or with color blocks for higher fidelity
consoleimage logo.gif -w 60 --blocks -o logo.cidz

# Or braille for maximum resolution
consoleimage logo.gif -w 60 --braille -o logo.cidz
```

### Step 2: Add to Your Project

Add the `.cidz` file to your project and mark it as embedded resource or content:

```xml
<!-- In your .csproj -->
<ItemGroup>
  <Content Include="logo.cidz">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Or embed it as a resource:

```xml
<ItemGroup>
  <EmbeddedResource Include="logo.cidz" />
</ItemGroup>
```

### Step 3: Play on Startup

```csharp
using ConsoleImage.Player;

class Program
{
    static async Task Main(string[] args)
    {
        // Play animated logo once before app starts
        await PlayStartupLogoAsync();

        // Your app continues here...
        Console.WriteLine("\nWelcome to MyApp!");
    }

    static async Task PlayStartupLogoAsync()
    {
        try
        {
            // Load from file
            var player = await ConsolePlayer.FromFileAsync("logo.cidz");

            // Play once (loopCount: 1) at normal speed
            await player.PlayAsync(loopCount: 1);
        }
        catch (FileNotFoundException)
        {
            // Logo not found - continue without it
        }
    }
}
```

### Loading from Embedded Resource

If you embedded the `.cidz` as a resource:

```csharp
using System.Reflection;
using ConsoleImage.Player;

static async Task PlayEmbeddedLogoAsync()
{
    var assembly = Assembly.GetExecutingAssembly();
    var resourceName = "MyApp.logo.cidz"; // Namespace.filename

    await using var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream == null) return;

    // Read compressed data
    using var ms = new MemoryStream();
    await stream.CopyToAsync(ms);
    var data = ms.ToArray();

    // Load and play
    var doc = PlayerDocument.FromCompressedBytes(data);
    var player = new ConsolePlayer(doc, loopCount: 1);
    await player.PlayAsync();
}
```

### Advanced: Progress Display

Show a loading indicator while the animation plays:

```csharp
var player = await ConsolePlayer.FromFileAsync("logo.cidz");

player.OnFrameChanged += (current, total) =>
{
    // Update progress bar, etc.
    var percent = (current * 100) / total;
    Console.Title = $"Loading... {percent}%";
};

player.OnLoopComplete += (loop) =>
{
    Console.Title = "MyApp";
};

await player.PlayAsync(loopCount: 1);
```

### Why Use Pre-rendered Documents?

| Approach           | Dependencies         | Startup Time | File Size     |
|--------------------|----------------------|--------------|---------------|
| **Player + .cidz** | None (built-in JSON) | ~1-3ms       | Small (~50KB) |
| Full library       | ImageSharp (~2MB)    | ~50-100ms    | Large         |

The Player package is ideal for:

- **Splash screens** - Show animated logo during initialization
- **CLI tools** - Add visual flair without bloating your binary
- **AOT apps** - No reflection, works with Native AOT
- **Embedded systems** - Minimal memory footprint

## Performance

Benchmarked on typical animation documents:

| Document Size | Frames | Load Time | Per Frame |
|---------------|--------|-----------|-----------|
| 181 KB        | 9      | 1.1 ms    | ~120 µs   |
| 724 KB        | 31     | 3.1 ms    | ~100 µs   |
| 2 MB          | 59     | 9.1 ms    | ~155 µs   |

- First parse may be slower due to JIT warmup
- Subsequent parses are near-instant
- Memory efficient - frames stored as strings, no intermediate objects

## Threading

- `LoadAsync` and `PlayAsync` are async and support `CancellationToken`
- `FromJson` is synchronous (for small documents or when you already have the string)
- The player writes directly to `Console` - not thread-safe with concurrent console output

## Error Handling

```csharp
try
{
    var doc = await PlayerDocument.LoadAsync("file.json");
}
catch (FileNotFoundException)
{
    // File doesn't exist
}
catch (JsonException)
{
    // Invalid JSON format
}
```

## License

Unlicense - Public Domain
