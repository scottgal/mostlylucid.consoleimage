# ConsoleImage.Player

A minimal, **zero-dependency** player for ConsoleImage documents. Play pre-rendered ASCII art and animations in any .NET terminal app  -  no ImageSharp, no FFmpeg, no external packages.

| Feature | Details |
|---|---|
| Dependencies | **None** (only built-in System.Text.Json) |
| Package size | ~50 KB |
| AOT compatible | Yes  -  source-generated JSON, no reflection |
| Formats | `.cidz` (compressed), `.json`, `.ndjson` (streaming) |
| Frame parse time | ~100–300 µs per frame |

## Install

```bash
dotnet add package mostlylucid.consoleimage.player
```

---

## Tutorial: Export, Embed, and Play a `.cidz` Animation

This walkthrough creates a self-contained CLI app that plays an animated logo on startup. By the end you will have a working project with an embedded `.cidz` file.

### 1. Install the CLI tool

You need the `consoleimage` CLI to render images/GIFs into `.cidz` documents. Install it as a .NET global tool:

```bash
dotnet tool install -g mostlylucid.consoleimage
```

Or build from source: `dotnet build` in the ConsoleImage directory.

### 2. Export a `.cidz` file

Render any image or GIF to a compressed document:

```bash
# Still image → single-frame document
consoleimage logo.png -w 60 --braille -o logo.cidz

# Animated GIF → multi-frame document (animation preserved)
consoleimage animation.gif -w 60 -o animation.cidz

# Color blocks mode (higher fidelity, uses Unicode half-blocks)
consoleimage photo.jpg -w 80 --blocks -o photo.cidz

# Classic ASCII characters
consoleimage banner.png -w 100 --ascii -o banner.cidz

# Video clip (first 5 seconds)
consoleimage intro.mp4 -w 60 -t 5 -o intro.cidz
```

> **What is `.cidz`?** A GZip-compressed JSON format with delta encoding. A 2 MB uncompressed JSON shrinks to ~300 KB. The Player handles decompression transparently.

### 3. Create a new console project

```bash
mkdir MyApp && cd MyApp
dotnet new console
dotnet add package mostlylucid.consoleimage.player
```

### 4. Add the `.cidz` file to your project

Copy `animation.cidz` into the project root, then add to `MyApp.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="animation.cidz" />
</ItemGroup>
```

### 5. Write the playback code

Replace `Program.cs` with:

```csharp
using System.Reflection;
using ConsoleImage.Player;

// Load the embedded .cidz resource
var assembly = Assembly.GetExecutingAssembly();

// Resource name = default namespace + filename (dots replace folder separators)
await using var stream = assembly.GetManifestResourceStream("MyApp.animation.cidz");

if (stream is null)
{
    Console.Error.WriteLine("Resource not found. Check the name matches your namespace.");
    return 1;
}

// Read the compressed bytes
using var ms = new MemoryStream();
await stream.CopyToAsync(ms);
var doc = PlayerDocument.FromCompressedBytes(ms.ToArray());

// Play once, then continue
using var player = new ConsolePlayer(doc, loopCount: 1);
await player.PlayAsync();

Console.WriteLine();
Console.WriteLine("Welcome to MyApp!");
return 0;
```

### 6. Run it

```bash
dotnet run
```

The animation plays through once, then your app continues normally. Press `Ctrl+C` at any time to cancel.

### 7. Publish as AOT (optional)

The Player is fully AOT-compatible:

```bash
dotnet publish -c Release -r win-x64 /p:PublishAot=true
```

---

## Quick Start

### Play from a file

```csharp
using ConsoleImage.Player;

// Auto-detects format: .cidz, .json, or .ndjson
using var player = await ConsolePlayer.FromFileAsync("animation.cidz");
await player.PlayAsync();
```

### Play from a JSON string

```csharp
using ConsoleImage.Player;

string json = File.ReadAllText("animation.json");
using var player = ConsolePlayer.FromJson(json);
await player.PlayAsync();
```

### Display a single frame (no animation)

```csharp
using var player = await ConsolePlayer.FromFileAsync("photo.cidz");
player.Display(); // Writes the first frame to stdout
```

### Access frames directly

```csharp
var doc = await PlayerDocument.LoadAsync("animation.cidz");

Console.WriteLine($"Frames: {doc.FrameCount}");
Console.WriteLine($"Duration: {doc.TotalDurationMs}ms");
Console.WriteLine($"Mode: {doc.RenderMode}"); // ASCII, Braille, ColorBlocks, Matrix

foreach (var frame in doc.Frames)
{
    Console.Write(frame.Content); // ANSI-escaped string
    await Task.Delay(frame.DelayMs);
}
```

---

## API Reference

### ConsolePlayer

The high-level player. Handles cursor management, synchronized output, and animation timing.

```csharp
// Constructor
new ConsolePlayer(
    PlayerDocument document,
    float? speedMultiplier = null,  // null = use document default
    int? loopCount = null,          // null = use document default, 0 = infinite
    string? subtitlePath = null     // optional SRT/VTT file
)

// Static factories (convenience)
await ConsolePlayer.FromFileAsync("file.cidz", speedMultiplier: 1.5f, loopCount: 3);
ConsolePlayer.FromJson(jsonString, loopCount: 1);

// Playback controls (can be set before or during playback)
player.SpeedMultiplier = 2.0f;         // Runtime speed change (> 1 = faster)
player.MaxDurationMs = 5000;           // Stop after 5s of content time
player.StartFrame = 10;               // Start at frame 10
player.EndFrame = 50;                 // Stop before frame 50
player.FrameStep = 2;                 // Play every other frame (downsampling)

// Playback
await player.PlayAsync(cancellationToken);  // Animated playback
player.Display();                           // First frame only
player.Display(showAllFrames: true);        // Dump all frames (debug)

// Info
string info = player.GetInfo();             // Formatted metadata
PlayerDocument doc = player.Document;       // Access underlying document

// Events
player.OnFrameChanged += (current, total) => { };
player.OnLoopComplete += (loopNumber) => { };

// Cleanup
player.Dispose();  // or use `using`
```

**Loop count values:**
- `0` = loop forever (until cancelled)
- `1` = play once
- `N` = play N times

**Playback control properties:**

| Property | Type | Default | Description |
|---|---|---|---|
| `SpeedMultiplier` | `float` | from doc | Playback speed (2.0 = 2x, 0.5 = half). Can change during playback. |
| `MaxDurationMs` | `int?` | `null` | Stop after N ms of content time. Null = no limit. |
| `StartFrame` | `int?` | `null` | First frame to play (0-based). Null = beginning. |
| `EndFrame` | `int?` | `null` | Frame to stop before (exclusive). Null = end. |
| `FrameStep` | `int` | `1` | Play every Nth frame. 2 = skip alternate frames. |

### PlayerDocument

Represents a loaded document with all frames. Load from any supported format:

```csharp
// From file (auto-detects format)
var doc = await PlayerDocument.LoadAsync("file.cidz", cancellationToken);

// From JSON string
var doc = PlayerDocument.FromJson(jsonString);

// From compressed byte array (e.g. embedded resource)
var doc = PlayerDocument.FromCompressedBytes(byteArray);

// From compressed stream
var doc = await PlayerDocument.FromCompressedStreamAsync(stream, cancellationToken);

// Properties
doc.FrameCount       // int  -  number of frames
doc.IsAnimated       // bool  -  true if more than 1 frame
doc.TotalDurationMs  // int  -  sum of all frame delays
doc.RenderMode       // string  -  "ASCII", "ColorBlocks", "Braille", "Matrix"
doc.Version          // string  -  format version
doc.Created          // DateTime  -  when the document was created
doc.SourceFile       // string?  -  original source file name
doc.Settings         // PlayerSettings  -  render settings
doc.Frames           // List<PlayerFrame>  -  all frames
```

### PlayerFrame

A single frame of content:

```csharp
frame.Content   // string  -  ANSI-escaped terminal content
frame.DelayMs   // int  -  milliseconds before next frame
frame.Width     // int  -  width in characters
frame.Height    // int  -  height in lines
```

### PlayerSettings

Render settings stored in the document:

```csharp
settings.MaxWidth                    // int (default 120)
settings.MaxHeight                   // int (default 60)
settings.CharacterAspectRatio        // float (default 0.5)
settings.UseColor                    // bool (default true)
settings.AnimationSpeedMultiplier    // float (default 1.0)
settings.LoopCount                   // int (default 0 = infinite)
```

### ConsoleHelper

Static utilities (called automatically, but available if needed):

```csharp
ConsoleHelper.EnableAnsiSupport();        // Enable ANSI on Windows
ConsoleHelper.IsAnsiSupported;            // Check ANSI support
ConsoleHelper.DetectCellAspectRatio();    // Auto-detect terminal font ratio
```

---

## Document Formats

The Player reads three formats. All are created by the `consoleimage` CLI or the `ConsoleImage.Core` library.

| Format | Extension | Best for | Size |
|---|---|---|---|
| **Compressed** | `.cidz` | Everything (default) | Smallest (~7:1 ratio) |
| **Standard JSON** | `.json` | Debugging, interop | Large |
| **Streaming NDJSON** | `.ndjson` | Long videos | Large (line-by-line) |

### Creating documents from the CLI

```bash
# Compressed (recommended)  -  auto-selected for .cidz extension
consoleimage input.gif -w 80 -o output.cidz

# Uncompressed JSON  -  use raw: prefix to force uncompressed
consoleimage input.gif -w 80 -o raw:output.json

# Streaming NDJSON  -  for very long videos, auto-finalizes on Ctrl+C
consoleimage input.mp4 -w 80 -o output.ndjson

# With de-jitter (reduces color flickering in animations)
consoleimage input.gif -w 80 -o output.cidz --dejitter
```

### Format auto-detection

`PlayerDocument.LoadAsync()` auto-detects format:

1. Checks for GZip magic bytes (`0x1F 0x8B`) → decompresses and parses
2. Checks first line for NDJSON header → streams line by line
3. Falls back to standard JSON

You never need to specify the format  -  just pass the file path.

---

## Loading from Embedded Resources

For self-contained apps, embed the `.cidz` as a resource.

### Step 1: Add to .csproj

```xml
<ItemGroup>
  <EmbeddedResource Include="assets/splash.cidz" />
</ItemGroup>
```

### Step 2: Load and play

```csharp
using System.Reflection;
using ConsoleImage.Player;

// Resource name format: {DefaultNamespace}.{folder}.{filename}
// Dots replace folder separators. Example:
//   Project namespace: MyApp
//   File path: assets/splash.cidz
//   Resource name: MyApp.assets.splash.cidz

var asm = Assembly.GetExecutingAssembly();
await using var stream = asm.GetManifestResourceStream("MyApp.assets.splash.cidz");

if (stream is null)
{
    // Debug: list all resource names to find the right one
    foreach (var name in asm.GetManifestResourceNames())
        Console.Error.WriteLine($"  Resource: {name}");
    return;
}

using var ms = new MemoryStream();
await stream.CopyToAsync(ms);
var doc = PlayerDocument.FromCompressedBytes(ms.ToArray());

using var player = new ConsolePlayer(doc, loopCount: 1);
await player.PlayAsync();
```

> **Common mistake:** The resource name uses dots, not slashes. `assets/splash.cidz` becomes `MyApp.assets.splash.cidz`. If loading fails, enumerate `GetManifestResourceNames()` to find the correct name.

---

## Cancellation

All async methods accept `CancellationToken`. Use `Ctrl+C` or cancel programmatically:

```csharp
using var cts = new CancellationTokenSource();

// Cancel after 10 seconds
cts.CancelAfter(TimeSpan.FromSeconds(10));

// Or cancel on keypress
_ = Task.Run(() => { Console.ReadKey(true); cts.Cancel(); });

using var player = await ConsolePlayer.FromFileAsync("animation.cidz");
await player.PlayAsync(cts.Token);
// Continues here after cancellation or completion
```

The player always restores cursor visibility and resets colors in its `finally` block, even when cancelled.

---

## Events and Progress

```csharp
using var player = await ConsolePlayer.FromFileAsync("animation.cidz");

player.OnFrameChanged += (current, total) =>
{
    var percent = (current * 100) / total;
    Console.Title = $"Playing... {percent}%";
};

player.OnLoopComplete += loopNumber =>
{
    Console.Title = $"Loop {loopNumber} complete";
};

await player.PlayAsync();
```

---

## Error Handling

```csharp
try
{
    var doc = await PlayerDocument.LoadAsync("file.cidz");
    using var player = new ConsolePlayer(doc);
    await player.PlayAsync();
}
catch (FileNotFoundException)
{
    Console.Error.WriteLine("Document file not found.");
}
catch (System.Text.Json.JsonException ex)
{
    Console.Error.WriteLine($"Invalid document format: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Failed to parse document: {ex.Message}");
}
```

---

## Performance

| Document Size | Frames | Load Time | Per Frame |
|---|---|---|---|
| 181 KB | 9 | 1.1 ms | ~120 µs |
| 724 KB | 31 | 3.1 ms | ~100 µs |
| 2 MB | 59 | 9.1 ms | ~155 µs |

- First parse may be slower due to JIT (sub-millisecond after warmup)
- Frames stored as strings  -  no intermediate objects, minimal GC pressure
- GZip decompression adds ~5–10% overhead

---

## Threading Notes

- `LoadAsync` and `PlayAsync` are fully async with `CancellationToken` support
- `FromJson` is synchronous  -  use for small documents or when you already have the string
- The player writes directly to `Console.Write`  -  do not write to the console from another thread during `PlayAsync`

---

## Troubleshooting

**Characters appear garbled (boxes, question marks)**
Your terminal doesn't support Unicode or ANSI escape codes. On Windows, use Windows Terminal (not the legacy `cmd.exe` window). The Player calls `ConsoleHelper.EnableAnsiSupport()` automatically, but legacy consoles may not support 24-bit color.

**Animation flickers**
Your terminal may not support DECSET 2026 (synchronized output). Windows Terminal and most modern Linux terminals support it. Legacy terminals will still work but may flicker between frames.

**Embedded resource returns null**
Resource names use dots as separators. Print all names to find yours:
```csharp
foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
    Console.WriteLine(name);
```

**File loads but shows nothing**
The document may have been rendered for a light terminal while you're on a dark one (or vice versa). Re-export with appropriate settings. Most documents use `Invert = true` (dark terminal default).

**`.json` file is huge**
Use `.cidz` instead  -  it's the same content with ~7:1 compression via delta encoding and GZip. The CLI defaults to `.cidz` when you specify a `.json` extension; use `raw:output.json` to force uncompressed.

---

## Complete Example: CLI App with Splash Screen

A production-ready example showing graceful fallback, cancellation, and AOT compatibility.

**MyApp.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <PublishAot>true</PublishAot>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="mostlylucid.consoleimage.player" Version="*" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="splash.cidz" />
  </ItemGroup>
</Project>
```

**Program.cs:**
```csharp
using System.Reflection;
using ConsoleImage.Player;

// Allow Ctrl+C to cancel the splash gracefully
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await PlaySplashAsync(cts.Token);

// App continues normally after splash
Console.WriteLine("App ready.");

static async Task PlaySplashAsync(CancellationToken ct)
{
    try
    {
        var asm = Assembly.GetExecutingAssembly();
        await using var stream = asm.GetManifestResourceStream("MyApp.splash.cidz");
        if (stream is null) return; // No splash  -  silently continue

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var doc = PlayerDocument.FromCompressedBytes(ms.ToArray());

        using var player = new ConsolePlayer(doc, loopCount: 1);
        await player.PlayAsync(ct);
    }
    catch (OperationCanceledException)
    {
        // User pressed Ctrl+C during splash  -  continue to app
    }
    catch (Exception ex)
    {
        // Splash failed  -  log and continue (never crash on splash)
        Console.Error.WriteLine($"Splash error: {ex.Message}");
    }
}
```

---

## License

Unlicense  -  Public Domain
