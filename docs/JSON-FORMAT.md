# ConsoleImage JSON Document Format

This document describes the JSON file formats used by ConsoleImage for saving and loading rendered ASCII art.

## Overview

ConsoleImage supports two JSON formats:

1. **Standard JSON** - A single JSON object containing all frames (good for images and short animations)
2. **Streaming NDJSON** - JSON Lines format with one record per line (good for long videos)

Both formats use JSON-LD conventions (`@context`, `@type`) for semantic structure and can be loaded with `ConsoleImageDocument.LoadAsync()`, which auto-detects the format.

## Standard JSON Format

Used for images and short animations. The entire document is a single JSON object.

### Structure

```json
{
  "@context": "https://schema.org/",
  "@type": "ConsoleImageDocument",
  "Version": "2.0",
  "Created": "2026-01-21T15:59:09.9747165Z",
  "SourceFile": "animation.gif",
  "RenderMode": "ASCII",
  "Settings": {
    "Width": 80,
    "MaxWidth": 120,
    "MaxHeight": 40,
    "CharacterAspectRatio": 0.5,
    "ContrastPower": 2.5,
    "Gamma": 0.85,
    "UseColor": true,
    "Invert": true,
    "AnimationSpeedMultiplier": 1.0,
    "LoopCount": 0
  },
  "Frames": [
    {
      "Content": "\u001B[38;2;98;115;144mn\u001B[38;2;100;116;146mn...",
      "DelayMs": 100,
      "Width": 80,
      "Height": 24
    }
  ],
  "FrameCount": 1,
  "IsAnimated": false,
  "TotalDurationMs": 0
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `@context` | string | JSON-LD context URL (always `"https://schema.org/"`) |
| `@type` | string | Document type (always `"ConsoleImageDocument"`) |
| `Version` | string | Format version (currently `"2.0"`) |
| `Created` | string | ISO 8601 timestamp of when document was created |
| `SourceFile` | string? | Original source filename (optional) |
| `RenderMode` | string | Render mode: `"ASCII"`, `"ColorBlocks"`, or `"Braille"` |
| `Settings` | object | Render settings used (see Settings section) |
| `Frames` | array | Array of frame objects (see Frame section) |
| `FrameCount` | int | Number of frames (computed) |
| `IsAnimated` | bool | True if more than one frame (computed) |
| `TotalDurationMs` | int | Total duration in milliseconds (computed) |

### Settings Object

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Width` | int? | null | Explicit width (null = auto) |
| `Height` | int? | null | Explicit height (null = auto) |
| `MaxWidth` | int | 120 | Maximum output width |
| `MaxHeight` | int | 60 | Maximum output height |
| `CharacterAspectRatio` | float | 0.5 | Character aspect ratio (width/height) |
| `ContrastPower` | float | 2.5 | Contrast enhancement power |
| `Gamma` | float | 0.85 | Gamma correction |
| `UseColor` | bool | true | Use ANSI color codes |
| `Invert` | bool | true | Invert for dark terminals |
| `CharacterSetPreset` | string? | null | Character set preset name |
| `AnimationSpeedMultiplier` | float | 1.0 | Animation speed multiplier |
| `LoopCount` | int | 0 | Loop count (0 = infinite) |

### Frame Object

| Field | Type | Description |
|-------|------|-------------|
| `Content` | string | The rendered content (ANSI-escaped string with `\r\n` line endings) |
| `DelayMs` | int | Frame delay in milliseconds (for animations) |
| `Width` | int | Width of this frame in characters |
| `Height` | int | Height of this frame in lines |

### Content Encoding

The `Content` field contains the rendered ASCII art as a string:

- **ANSI escape codes** are included for color (e.g., `\u001B[38;2;R;G;Bm` for 24-bit foreground)
- **Line endings** are `\r\n` (CRLF)
- **Unicode characters** are UTF-8 encoded (important for braille mode)
- **Reset code** (`\u001B[0m`) at end of each frame

## Streaming NDJSON Format

Used for long videos where frames are written incrementally. Each line is a complete JSON object (JSON Lines / NDJSON format).

### Structure

```
{"@context":"https://schema.org/","@type":"ConsoleImageDocumentHeader","Version":"2.0","Created":"2026-01-21T16:00:00Z","SourceFile":"video.mp4","RenderMode":"ASCII","Settings":{...}}
{"@type":"Frame","Index":0,"Content":"...","DelayMs":33,"Width":80,"Height":24}
{"@type":"Frame","Index":1,"Content":"...","DelayMs":33,"Width":80,"Height":24}
{"@type":"Frame","Index":2,"Content":"...","DelayMs":33,"Width":80,"Height":24}
{"@type":"ConsoleImageDocumentFooter","FrameCount":3,"TotalDurationMs":99,"Completed":"2026-01-21T16:00:10Z","IsComplete":true}
```

### Line Types

#### Header (Line 1)

```json
{
  "@context": "https://schema.org/",
  "@type": "ConsoleImageDocumentHeader",
  "Version": "2.0",
  "Created": "2026-01-21T16:00:00Z",
  "SourceFile": "video.mp4",
  "RenderMode": "ASCII",
  "Settings": { ... }
}
```

Same fields as standard format, but `@type` is `"ConsoleImageDocumentHeader"`.

#### Frame (Lines 2-N)

```json
{
  "@type": "Frame",
  "Index": 0,
  "Content": "...",
  "DelayMs": 33,
  "Width": 80,
  "Height": 24
}
```

| Field | Type | Description |
|-------|------|-------------|
| `@type` | string | Always `"Frame"` |
| `Index` | int | Zero-based frame index |
| `Content` | string | Rendered content |
| `DelayMs` | int | Frame delay in milliseconds |
| `Width` | int | Frame width in characters |
| `Height` | int | Frame height in lines |

#### Footer (Last Line)

```json
{
  "@type": "ConsoleImageDocumentFooter",
  "FrameCount": 100,
  "TotalDurationMs": 3300,
  "Completed": "2026-01-21T16:00:10Z",
  "IsComplete": true
}
```

| Field | Type | Description |
|-------|------|-------------|
| `@type` | string | Always `"ConsoleImageDocumentFooter"` |
| `FrameCount` | int | Total number of frames written |
| `TotalDurationMs` | int | Sum of all frame delays |
| `Completed` | string | ISO 8601 completion timestamp |
| `IsComplete` | bool | True if processing completed normally, false if stopped early |

### Benefits of NDJSON Format

1. **Incremental writing** - Frames written as processed, no memory buildup
2. **Stop anytime** - Ctrl+C produces a valid document (auto-finalized on dispose)
3. **Streaming read** - Can process frames without loading entire file
4. **Append-friendly** - Easy to add frames to existing file
5. **Line-by-line parsing** - Each line is valid JSON

## File Extensions

Both `.json` and `.ndjson` extensions are supported:

| Extension | Recommended For | Notes |
|-----------|-----------------|-------|
| `.json` | Images, short GIFs | Can be either format (auto-detected) |
| `.ndjson` | Long videos | Explicitly indicates streaming format |

## API Usage

### Reading Documents

```csharp
using ConsoleImage.Core;

// Auto-detects format (standard or streaming)
var doc = await ConsoleImageDocument.LoadAsync("file.json");

// Check document properties
Console.WriteLine($"Mode: {doc.RenderMode}");
Console.WriteLine($"Frames: {doc.FrameCount}");
Console.WriteLine($"Animated: {doc.IsAnimated}");

// Play back
using var player = new DocumentPlayer(doc);
await player.PlayAsync();
```

### Writing Standard Format

```csharp
using ConsoleImage.Core;

// Create from rendered frames
var doc = ConsoleImageDocument.FromAsciiFrames(frames, options, "source.gif");

// Or from other frame types
var doc = ConsoleImageDocument.FromColorBlockFrames(blockFrames, options);
var doc = ConsoleImageDocument.FromBrailleFrames(brailleFrames, options);

// Save
await doc.SaveAsync("output.json");
```

### Writing Streaming Format

```csharp
using ConsoleImage.Core;

// Create streaming writer
await using var writer = new StreamingDocumentWriter(
    "output.ndjson",
    "ASCII",          // RenderMode
    options,          // RenderOptions
    "source.mp4");    // SourceFile (optional)

// Write header
await writer.WriteHeaderAsync();

// Write frames as they're processed
foreach (var frame in frames)
{
    await writer.WriteFrameAsync(frame.Content, frame.DelayMs);
    // or: await writer.WriteFrameAsync(asciiFrame, options);
}

// Finalize (or let dispose handle it)
await writer.FinalizeAsync();
```

### Streaming Read

```csharp
using ConsoleImage.Core;

// Stream frames without loading all into memory
await foreach (var (header, frame, footer) in StreamingDocumentReader.StreamAsync("file.ndjson"))
{
    if (header != null)
        Console.WriteLine($"Processing {header.SourceFile}");
    else if (frame != null)
        Console.WriteLine(frame.Content);
    else if (footer != null)
        Console.WriteLine($"Complete: {footer.FrameCount} frames");
}
```

## CLI Usage

```bash
# Save standard JSON
consoleimage animation.gif -o json:output.json

# Save streaming NDJSON (for video)
consolevideo long_movie.mp4 -o json:output.ndjson

# Play back either format
consoleimage output.json
consolevideo output.ndjson
```

## Compatibility

- **Version 2.0** is the current format version
- Documents are forward-compatible (unknown fields ignored)
- Settings can be overridden at playback time
- UTF-8 encoding required (BOM optional)

## Example Files

### Static Image (Standard JSON)

```json
{
  "@context": "https://schema.org/",
  "@type": "ConsoleImageDocument",
  "Version": "2.0",
  "Created": "2026-01-21T12:00:00Z",
  "SourceFile": "photo.jpg",
  "RenderMode": "ColorBlocks",
  "Settings": {
    "Width": 80,
    "MaxWidth": 120,
    "MaxHeight": 40,
    "CharacterAspectRatio": 0.5,
    "ContrastPower": 2.5,
    "Gamma": 0.85,
    "UseColor": true,
    "Invert": true
  },
  "Frames": [
    {
      "Content": "\u001B[48;2;30;40;50m\u001B[38;2;100;120;140m\u2580...",
      "DelayMs": 0,
      "Width": 80,
      "Height": 40
    }
  ],
  "FrameCount": 1,
  "IsAnimated": false,
  "TotalDurationMs": 0
}
```

### Animation (Streaming NDJSON)

```
{"@context":"https://schema.org/","@type":"ConsoleImageDocumentHeader","Version":"2.0","Created":"2026-01-21T12:00:00Z","SourceFile":"video.mp4","RenderMode":"Braille","Settings":{"Width":120,"CharacterAspectRatio":0.5,"UseColor":true,"Invert":true}}
{"@type":"Frame","Index":0,"Content":"\u001B[38;2;255;255;255m\u28FF...","DelayMs":33,"Width":120,"Height":60}
{"@type":"Frame","Index":1,"Content":"\u001B[38;2;255;255;255m\u28FF...","DelayMs":33,"Width":120,"Height":60}
{"@type":"ConsoleImageDocumentFooter","FrameCount":2,"TotalDurationMs":66,"Completed":"2026-01-21T12:00:05Z","IsComplete":true}
```
