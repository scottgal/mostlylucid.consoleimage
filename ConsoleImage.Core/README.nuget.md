# mostlylucid.consoleimage

High-quality ASCII art renderer for .NET 10 using shape-matching algorithm.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

**[Full documentation with examples and demos on GitHub](https://github.com/scottgal/mostlylucid.consoleimage)**

## Features

- **ASCII Mode** - Shape-matched characters using 6-point sampling grid
- **ColorBlocks Mode** - 2x vertical resolution using Unicode half-blocks (▀▄█)
- **Braille Mode** - 2x4 dots per cell for ultra-high resolution
- **Matrix Mode** - Digital rain effect with authentic color scheme
- **GIF Animation** - Render and play animated GIFs
- **GIF Output** - Save rendered output as animated GIF files
- **Native AOT** - Fully compatible with ahead-of-time compilation

## Quick Start

```csharp
using ConsoleImage.Core;

// Enable Windows ANSI support (call once at startup)
ConsoleHelper.EnableAnsiSupport();

// One line - just works!
Console.WriteLine(AsciiArt.Render("photo.jpg"));

// Colored output
Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));

// Play animated GIF
await AsciiArt.PlayGif("animation.gif");
```

## Render Modes

### ASCII (Shape-Matched Characters)

Characters selected by visual shape similarity using 6-point sampling grid.

```csharp
using var renderer = new AsciiRenderer(new RenderOptions { MaxWidth = 80 });
var frame = renderer.RenderFile("photo.jpg");
Console.WriteLine(frame.ToAnsiString()); // Colored
Console.WriteLine(frame.ToString());      // Plain text
```

### ColorBlocks (Unicode Half-Blocks)

2x vertical resolution using `▀▄█` characters with 24-bit color.

```csharp
using var renderer = new ColorBlockRenderer(new RenderOptions { MaxWidth = 80 });
string output = renderer.RenderFile("photo.jpg");
Console.WriteLine(output);
```

### Braille (Ultra-High Resolution)

2x4 dots per character cell with autocontrast and selective dithering.

```csharp
using var renderer = new BrailleRenderer(new RenderOptions { MaxWidth = 80 });
string output = renderer.RenderFile("photo.jpg");
Console.WriteLine(output);
```

### Matrix (Digital Rain Effect)

Iconic falling code effect with authentic color scheme (white heads fading to green).

```csharp
var options = new RenderOptions { MaxWidth = 80 };
var matrixOpts = new MatrixOptions
{
    BaseColor = new Rgba32(0, 255, 65, 255),  // Classic green
    Density = 0.5f,
    SpeedMultiplier = 1.0f,
    TargetFps = 20,
    UseAsciiOnly = false,      // Set true for ASCII-only (no katakana)
    CustomAlphabet = null      // Or "01" for binary, "HELLO" for custom
};

using var renderer = new MatrixRenderer(options, matrixOpts);

// Static image with Matrix overlay
var frame = renderer.RenderFile("photo.jpg");
Console.WriteLine(frame.Content);

// Animated GIF with Matrix effect
var frames = renderer.RenderGif("animation.gif");
```

**Matrix Presets:**
```csharp
var green = MatrixOptions.ClassicGreen;    // Default green
var red = MatrixOptions.RedPill;           // Red tint
var blue = MatrixOptions.BluePill;         // Blue tint
var amber = MatrixOptions.Amber;           // Retro amber
var fullColor = MatrixOptions.FullColor;   // Source image colors
```

**Custom Alphabets:**
- Default: Half-width katakana + numbers + symbols
- `UseAsciiOnly = true`: ASCII letters only
- `CustomAlphabet = "01"`: Binary rain
- `CustomAlphabet = "THEMATRIX"`: Custom characters

## GIF Animation Playback

```csharp
// Simple playback
await AsciiArt.PlayGif("animation.gif");

// With options
var options = RenderOptions.ForAnimation(loopCount: 3);
options.AnimationSpeedMultiplier = 1.5f;

using var renderer = new AsciiRenderer(options);
var frames = renderer.RenderGif("animation.gif");
using var player = new AsciiAnimationPlayer(frames, useColor: true);
await player.PlayAsync(cancellationToken);
```

## GIF Output

Save rendered output as animated GIF file:

```csharp
using var gifWriter = new GifWriter(new GifWriterOptions
{
    FontSize = 10,
    Scale = 1.0f,
    MaxColors = 128,
    LoopCount = 0  // 0 = infinite
});

using var renderer = new AsciiRenderer(options);
foreach (var frame in renderer.RenderGif("input.gif"))
{
    gifWriter.AddFrame(frame, frame.DelayMs);
}

await gifWriter.SaveAsync("output.gif");

// For Matrix mode
using var matrixRenderer = new MatrixRenderer(options, matrixOpts);
foreach (var frame in matrixRenderer.RenderGif("input.gif"))
{
    gifWriter.AddMatrixFrame(frame, frame.DelayMs, useBlockMode: false);
}
```

## RenderOptions Reference

```csharp
var options = new RenderOptions
{
    // Dimensions
    Width = null,                    // Exact width (null = auto)
    Height = null,                   // Exact height (null = auto)
    MaxWidth = 120,                  // Maximum width constraint
    MaxHeight = 40,                  // Maximum height constraint
    CharacterAspectRatio = 0.5f,     // Terminal char width/height ratio

    // Appearance
    UseColor = true,                 // Enable ANSI colors
    Invert = true,                   // For dark terminals (default)
    ContrastPower = 2.5f,            // Contrast enhancement (1.0-4.0)
    Gamma = 0.65f,                   // Gamma correction

    // Animation
    AnimationSpeedMultiplier = 1.0f,
    LoopCount = 0,                   // 0 = infinite
    FrameSampleRate = 1,             // Skip frames (2 = every 2nd)

    // Features
    EnableDithering = true,          // Floyd-Steinberg dithering
    EnableEdgeDetection = false,     // Sobel edge detection
    UseParallelProcessing = true     // Multi-threaded rendering
};
```

## Terminal Protocol Support

For terminals with native image protocols:

```csharp
using var renderer = new UnifiedRenderer(new RenderOptions { MaxWidth = 80 });

// Auto-detect best protocol
string output = renderer.RenderFile("photo.jpg", TerminalProtocol.Auto);

// Or specify explicitly
string sixel = renderer.RenderFile("photo.jpg", TerminalProtocol.Sixel);
string iterm = renderer.RenderFile("photo.jpg", TerminalProtocol.ITerm2);
string kitty = renderer.RenderFile("photo.jpg", TerminalProtocol.Kitty);

// Check capabilities
var protocol = TerminalCapabilities.DetectBestProtocol();
bool hasSixel = TerminalCapabilities.SupportsSixel();
```

## Performance

- SIMD optimized (Vector128/256/512)
- Parallel processing for multi-core rendering
- Pre-computed lookup tables
- K-D tree for fast character matching
- Result caching with quantized lookups

## Related Packages

- [mostlylucid.consoleimage.video](https://www.nuget.org/packages/mostlylucid.consoleimage.video/) - FFmpeg video playback

## Attribution

- **ASCII shape-matching**: Based on [Alex Harri's approach](https://alexharri.com/blog/ascii-rendering)
- **Braille autocontrast**: Inspired by [img2braille](https://github.com/TheFel0x/img2braille)
- **Matrix color scheme**: Based on [digital rain analysis](https://carlnewton.github.io/digital-rain-analysis/)

## License

Public domain - [UNLICENSE](https://github.com/scottgal/mostlylucid.consoleimage/blob/master/UNLICENSE)
