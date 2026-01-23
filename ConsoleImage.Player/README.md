# ConsoleImage.Player

A minimal, zero-dependency player for ConsoleImage JSON documents. Play pre-rendered ASCII art animations in any terminal.

## Features

- **Zero external dependencies** - Only System.Text.Json (built-in)
- **AOT compatible** - Works with Native AOT publishing
- **Tiny footprint** - Just the essentials for playback
- **Supports both formats** - Standard JSON and streaming NDJSON

## Installation

```bash
dotnet add package mostlylucid.consoleimage.player
```

## Quick Start

### Play from file

```csharp
using ConsoleImage.Player;

// Load and play
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

Use `consoleimage` or `consolevideo` CLI tools to create documents:

```bash
# From image/GIF
consoleimage animation.gif -o output.json

# From video
consolevideo movie.mp4 -o output.json -w 80
```

Or use the full `mostlylucid.consoleimage` library to create documents programmatically.

## License

Unlicense - Public Domain
