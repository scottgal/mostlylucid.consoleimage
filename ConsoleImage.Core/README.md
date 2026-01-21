# mostlylucid.consoleimage

**Version 2.0** - High-quality ASCII art renderer for .NET 10 using shape-matching algorithm.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

### Animation

| ASCII Mode | ColorBlocks Mode | Braille Mode |
|------------|------------------|--------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_ascii.gif" width="200" alt="ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_blocks.gif" width="200" alt="Blocks"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_braille.gif" width="200" alt="Braille"> |

> **Note:** These images are rendered using the tool's GIF output with a consistent monospace font. Actual terminal display varies by terminal emulator, font, and color support.

### Still Images

| Landscape | Portrait |
|-----------|----------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_mountain_blocks.gif" width="280" alt="Mountain"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_portrait_blocks.gif" width="200" alt="Portrait"> |

## Quick Start

```csharp
using ConsoleImage.Core;

// One line - just works!
Console.WriteLine(AsciiArt.Render("photo.jpg"));

// Colored output
Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));

// Play animated GIF
await AsciiArt.PlayGif("animation.gif");
```

## Render Modes

### ASCII (Shape-Matched Characters)

<img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_ascii.gif" width="300" alt="ASCII Mode">

Characters selected by visual shape similarity using 6-point sampling grid.

```csharp
using var renderer = new AsciiRenderer(new RenderOptions { MaxWidth = 80 });
var frame = renderer.RenderFile("photo.jpg");
Console.WriteLine(frame.ToAnsiString()); // Colored
Console.WriteLine(frame.ToString());      // Plain text
```

### ColorBlocks (Unicode Half-Blocks)

<img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_blocks.gif" width="300" alt="ColorBlocks Mode">

2x vertical resolution using `▀▄█` characters with 24-bit color.

```csharp
using var renderer = new ColorBlockRenderer(new RenderOptions { MaxWidth = 80 });
string output = renderer.RenderFile("photo.jpg");
Console.WriteLine(output);
```

### Braille (Ultra-High Resolution)

<img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_braille.gif" width="300" alt="Braille Mode">

2x4 dots per character cell (8 dots total) for maximum detail. Uses advanced rendering techniques:

- **Autocontrast**: Automatically adjusts threshold based on image's actual brightness range
  (inspired by [img2braille](https://github.com/TheFel0x/img2braille))
- **Selective Floyd-Steinberg dithering**: Error diffusion only in mid-tone regions for smooth
  gradients while keeping dark/bright areas clean
- **24-bit color support**: Each braille cell colored with averaged pixel color

```csharp
using var renderer = new BrailleRenderer(new RenderOptions { MaxWidth = 80 });
string output = renderer.RenderFile("photo.jpg");
Console.WriteLine(output);
```

Best results with: photographs, gradients, detailed artwork. For line art, consider disabling
dithering (`EnableDithering = false`) for sharper edges.

### Terminal Protocols (Native Images)

For terminals that support native image protocols:

```csharp
using var renderer = new UnifiedRenderer(new RenderOptions { MaxWidth = 80 });

// Auto-detect best protocol
string output = renderer.RenderFile("photo.jpg", TerminalProtocol.Auto);

// Or specify explicitly
string sixel = renderer.RenderFile("photo.jpg", TerminalProtocol.Sixel);
string iterm = renderer.RenderFile("photo.jpg", TerminalProtocol.ITerm2);
string kitty = renderer.RenderFile("photo.jpg", TerminalProtocol.Kitty);
```

## Edge Detection

Enhance foreground visibility with Sobel edge detection:

```csharp
var options = new RenderOptions
{
    EnableEdgeDetection = true,  // Sobel edge detection
    EnableEdgeDirectionChars = true  // Use /\|- for edge directions
};
using var renderer = new AsciiRenderer(options);
var frame = renderer.RenderFile("photo.jpg");
```

Edge detection is particularly effective for:
- Images with complex backgrounds
- Line art and diagrams
- Photos where subject separation is important

## Contrast and Dithering

### Contrast Enhancement

Control contrast with power curve (S-curve):

```csharp
var options = new RenderOptions
{
    ContrastPower = 2.5f,  // Default. Higher = more contrast
    DirectionalContrastStrength = 0.3f  // Edge emphasis
};
```

| Value | Effect |
|-------|--------|
| 1.0 | No enhancement |
| 2.0 | Moderate contrast |
| 2.5 | Default - good balance |
| 3.0+ | High contrast, dramatic |

### Floyd-Steinberg Dithering

Error-diffusion dithering for smooth gradients:

```csharp
var options = new RenderOptions
{
    EnableDithering = true  // Default. Smooth gradients
};
```

Dithering is particularly effective for:
- Photographs with subtle gradients
- Skin tones and natural imagery
- ColorBlocks mode for maximum quality

## Background Handling

### Automatic Background Suppression

Detect and suppress uniform backgrounds:

```csharp
var options = new RenderOptions
{
    AutoBackgroundSuppression = true  // Auto-detect background
};
```

### Manual Threshold Control

Fine-tune for specific images:

```csharp
var options = new RenderOptions
{
    // For light backgrounds (white, light gray)
    BackgroundThreshold = 0.85f,  // Suppress above this brightness

    // For dark backgrounds (black, dark gray)
    DarkBackgroundThreshold = 0.15f,  // Suppress below this brightness

    // Terminal optimization (skip near-black/white for blending)
    DarkTerminalBrightnessThreshold = 0.1f,
    LightTerminalBrightnessThreshold = 0.9f
};
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
    CharacterAspectRatio = 0.5f,     // Width/height of terminal chars

    // Appearance
    UseColor = true,                 // Enable ANSI colors
    Invert = true,                   // For dark terminals (default)
    ContrastPower = 2.5f,            // Contrast enhancement (1.0-4.0)
    DirectionalContrastStrength = 0.3f,

    // Character sets
    CharacterSet = null,             // Custom chars (light to dark)
    CharacterSetPreset = "extended", // extended|simple|block|classic

    // Animation
    AnimationSpeedMultiplier = 1.0f,
    LoopCount = 0,                   // 0 = infinite
    FrameSampleRate = 1,             // Skip frames (2 = every 2nd)

    // Features
    EnableDithering = true,          // Floyd-Steinberg dithering
    EnableEdgeDirectionChars = true, // Use /\|- for edges
    EnableEdgeDetection = false,     // Sobel edge detection
    UseParallelProcessing = true,    // Multi-threaded rendering

    // Background handling
    AutoBackgroundSuppression = true,
    BackgroundThreshold = null,      // Light bg threshold (0-1)
    DarkBackgroundThreshold = null,  // Dark bg threshold (0-1)

    // Terminal optimization
    DarkTerminalBrightnessThreshold = 0.1f,  // Skip very dark colors
    LightTerminalBrightnessThreshold = 0.9f  // Skip very bright colors
};
```

## Presets

```csharp
var options = RenderOptions.Default;            // Sensible defaults
var options = RenderOptions.HighDetail;         // Maximum quality
var options = RenderOptions.Monochrome;         // No color
var options = RenderOptions.ForLightBackground; // Light terminals
var options = RenderOptions.ForDarkBackground;  // Dark image enhancement
var options = RenderOptions.ForAnimation(loopCount: 3);
```

## Animated GIFs

```csharp
// Simple playback
await AsciiArt.PlayGif("animation.gif");

// With options
var options = RenderOptions.ForAnimation(loopCount: 3);
options.AnimationSpeedMultiplier = 1.5f;
await AsciiArt.PlayGif("animation.gif", options);

// Manual control
using var renderer = new AsciiRenderer(options);
var frames = renderer.RenderGif("animation.gif");
using var player = new AsciiAnimationPlayer(frames, useColor: true);
await player.PlayAsync(cancellationToken);

// ColorBlocks animation
using var blockRenderer = new ColorBlockRenderer(options);
var blockFrames = blockRenderer.RenderGif("animation.gif");
// frames implement IAnimationFrame for unified handling
```

## GIF Output

Save rendered output as animated GIF:

```csharp
using var gifWriter = new GifWriter(new GifWriterOptions
{
    FontSize = 10,        // Text size in pixels
    Scale = 1.0f,         // Output scale factor
    MaxColors = 128,      // GIF palette size (16-256)
    LoopCount = 0,        // 0 = infinite loop
    MaxFrames = null,     // Limit frames
    MaxLengthSeconds = null
});

using var renderer = new AsciiRenderer(options);
foreach (var frame in renderer.RenderGif("input.gif"))
{
    gifWriter.AddFrame(frame, frame.DelayMs);
}

await gifWriter.SaveAsync("output.gif");
```

## Loading from URLs

```csharp
// Download and render
using var stream = await UrlHelper.DownloadAsync(
    "https://example.com/image.jpg",
    (downloaded, total) => Console.Write($"\r{downloaded}/{total}"));

using var renderer = new AsciiRenderer(options);
var frame = renderer.RenderStream(stream);
```

## Windows ANSI Support

```csharp
// Call once at startup for Windows console ANSI support
ConsoleHelper.EnableAnsiSupport();
```

## Character Set Presets

| Preset | Characters | Description |
|--------|------------|-------------|
| `extended` | 91 chars | Default - maximum detail |
| `simple` | ` .:-=+*#%@` | Fast, minimal |
| `block` | ` ░▒▓█` | Unicode density blocks |
| `classic` | 71 chars | Original algorithm set |

## Terminal Protocol Support

| Protocol | Terminals | Detection |
|----------|-----------|-----------|
| Sixel | xterm, mlterm, foot, WezTerm | `TERM` + query |
| iTerm2 | iTerm2, WezTerm, Mintty | `ITERM_SESSION_ID` |
| Kitty | Kitty | `KITTY_WINDOW_ID` |

```csharp
// Check what's supported
var protocol = TerminalCapabilities.DetectBestProtocol();
bool hasSixel = TerminalCapabilities.SupportsSixel();
bool hasKitty = TerminalCapabilities.SupportsKitty();
bool hasITerm = TerminalCapabilities.SupportsITerm2();
```

## Performance

- **SIMD optimized**: Vector128/256/512 for distance calculations
- **Parallel processing**: Multi-threaded rendering
- **Pre-computed tables**: Eliminates trig calls in sampling
- **K-D tree matching**: Fast nearest-neighbor in 6D space
- **Result caching**: Quantized lookups cached

## Related Packages

- [mostlylucid.consoleimage.video](https://www.nuget.org/packages/mostlylucid.consoleimage.video/) - FFmpeg video playback
- [mostlylucid.consoleimage.spectre](https://www.nuget.org/packages/mostlylucid.consoleimage.spectre/) - Spectre.Console integration

## Attribution

This library implements techniques and algorithms from various sources:

- **ASCII shape-matching**: Based on [Alex Harri's ASCII rendering approach](https://alexharri.com/blog/ascii-rendering)
- **Braille autocontrast**: Inspired by [img2braille](https://github.com/TheFel0x/img2braille)
- **Floyd-Steinberg dithering**: Classic error-diffusion algorithm for smooth gradients
- **Braille Unicode mapping**: Standard Unicode Braille Patterns block (U+2800-U+28FF)

## License

Public domain - see [UNLICENSE](https://github.com/scottgal/mostlylucid.consoleimage/blob/master/UNLICENSE)
