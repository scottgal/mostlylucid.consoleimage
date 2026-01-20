# mostlylucid.consoleimage

High-quality ASCII art renderer for .NET 10 using shape-matching algorithm.

**Based on [Alex Harri's excellent article](https://alexharri.com/blog/ascii-rendering)** on ASCII rendering techniques.

<img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/wiggum_loop.gif" width="50%" alt="ConsoleImage Demo">

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)



## Features

- **Shape-matching algorithm**: Characters selected by visual shape similarity, not just brightness
- **3×2 staggered sampling grid**: 6 sampling circles arranged per Alex Harri's article for accurate shape matching
- **K-D tree optimization**: Fast nearest-neighbor search in 6D vector space
- **Contrast enhancement**: Global power function + directional contrast with 10 external sampling circles
- **Animated GIF support**: Smooth flicker-free playback with DECSET 2026 synchronized output
- **Multiple render modes**:
  - ANSI colored ASCII characters (extended 91-char set by default)
  - High-fidelity color blocks using Unicode half-blocks (▀▄)
  - Ultra-high resolution braille characters (2×4 dots per cell)
- **Auto background detection**: Automatically detects dark/light backgrounds
- **Floyd-Steinberg dithering**: Error diffusion for smoother gradient rendering
- **Adaptive thresholding**: Otsu's method for optimal braille binarization
- **Edge-direction characters**: Uses directional chars (/ \ | -) based on detected edges
- **AOT compatible**: Works with Native AOT compilation
- **Cross-platform**: Windows, Linux, macOS (x64 and ARM64)

## Installation

### NuGet Package (Library)

```bash
dotnet add package mostlylucid.consoleimage
```

### CLI Tool (Standalone Binaries)

Download from [GitHub Releases](https://github.com/scottgal/mostlylucid.consoleimage/releases):

| Platform | Download |
|----------|----------|
| Windows x64 | `consoleimage-win-x64.zip` |
| Linux x64 | `consoleimage-linux-x64.tar.gz` |
| Linux ARM64 | `consoleimage-linux-arm64.tar.gz` |
| macOS ARM64 | `consoleimage-osx-arm64.tar.gz` |

## Quick Start

```csharp
using ConsoleImage.Core;

// One line - just works with sensible defaults!
// (color ON, invert ON for dark terminals, auto background detection)
Console.WriteLine(AsciiArt.Render("photo.jpg"));
```

## CLI Usage

```bash
# Basic - color and animation ON by default
consoleimage photo.jpg

# Specify width
consoleimage photo.jpg -w 80

# Disable color (monochrome)
consoleimage photo.jpg --no-color

# High-fidelity color blocks (requires 24-bit color terminal)
consoleimage photo.jpg --blocks

# Ultra-high resolution braille mode (2x4 dots per cell)
consoleimage photo.jpg --braille

# Play animated GIF (animates by default)
consoleimage animation.gif

# Braille animation
consoleimage animation.gif --braille

# Don't animate, just show first frame
consoleimage animation.gif --no-animate

# Control animation loops (0 = infinite, default)
consoleimage animation.gif --loop 3

# Speed up animation
consoleimage animation.gif --speed 2.0

# For light terminal backgrounds
consoleimage photo.jpg --no-invert

# Edge detection for enhanced foreground
consoleimage photo.jpg --edge

# Manual background suppression
consoleimage photo.jpg --bg-threshold 0.85      # Suppress light backgrounds
consoleimage photo.jpg --dark-bg-threshold 0.15 # Suppress dark backgrounds

# Save to file
consoleimage photo.jpg -o output.txt

# Disable dithering (ON by default for smoother gradients)
consoleimage photo.jpg --no-dither

# Disable edge-direction characters (ON by default)
consoleimage photo.jpg --no-edge-chars

# Custom aspect ratio (default: 0.5, meaning chars are 2x taller than wide)
consoleimage photo.jpg --aspect-ratio 0.6

# Character set presets
consoleimage photo.jpg -p simple    # Minimal: .:-=+*#%@
consoleimage photo.jpg -p block     # Unicode blocks: ░▒▓█
consoleimage photo.jpg -p classic   # Original 71-char set
# (extended 91-char set is the default)
```

### CLI Options

| Option | Description | Default |
|--------|-------------|---------|
| `-w, --width` | Output width in characters | Auto |
| `-h, --height` | Output height in characters | Auto |
| `--max-width` | Maximum output width | 120 |
| `--max-height` | Maximum output height | 60 |
| `--no-color` | Disable colored output | Color ON |
| `--no-invert` | Don't invert (for light backgrounds) | Invert ON |
| `--contrast` | Contrast power (1.0 = none) | 2.5 |
| `--charset` | Custom character set | - |
| `-p, --preset` | Preset: extended, simple, block, classic | extended |
| `-o, --output` | Write to file | Console |
| `--no-animate` | Don't animate GIFs | Animate ON |
| `-s, --speed` | Animation speed multiplier | 1.0 |
| `-l, --loop` | Animation loop count (0 = infinite) | 0 |
| `-r, --framerate` | Fixed framerate in FPS (overrides GIF timing) | GIF timing |
| `-f, --frame-sample` | Frame sampling rate (skip frames) | 1 |
| `-e, --edge` | Enable edge detection | OFF |
| `--bg-threshold` | Light background threshold (0.0-1.0) | Auto |
| `--dark-bg-threshold` | Dark background threshold (0.0-1.0) | Auto |
| `--auto-bg` | Auto-detect background | ON |
| `-b, --blocks` | Use colored Unicode half-blocks | OFF |
| `-B, --braille` | Use braille characters (2×4 dots/cell) | OFF |
| `--no-alt-screen` | Keep animation in scrollback | Alt screen ON |
| `--no-parallel` | Disable parallel processing | Parallel ON |
| `-a, --aspect-ratio` | Character aspect ratio (width/height) | 0.5 |
| `--no-dither` | Disable Floyd-Steinberg dithering | Dither ON |
| `--no-edge-chars` | Disable edge-direction characters | Edge chars ON |
| `-j, --json` | Output as JSON (for LLM tool calls) | OFF |

## Library API

### Simple API

```csharp
using ConsoleImage.Core;

// Basic - just works
Console.WriteLine(AsciiArt.Render("photo.jpg"));

// With width
Console.WriteLine(AsciiArt.Render("photo.jpg", 80));

// Colored output
Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));

// For light terminal backgrounds
Console.WriteLine(AsciiArt.RenderForLightBackground("photo.jpg"));

// Play animated GIF
await AsciiArt.PlayGif("animation.gif");
```

### Full Options API

```csharp
using ConsoleImage.Core;

// Use presets
var options = RenderOptions.Default;           // Sensible defaults
var options = RenderOptions.HighDetail;        // Maximum detail
var options = RenderOptions.Monochrome;        // No color
var options = RenderOptions.ForLightBackground; // For light terminals
var options = RenderOptions.ForDarkBackground; // Enhanced for dark images
var options = RenderOptions.ForAnimation(loopCount: 3);

// Or customize everything
var options = new RenderOptions
{
    MaxWidth = 100,
    MaxHeight = 50,
    UseColor = true,
    Invert = true,                    // Dark terminals (default)
    ContrastPower = 3.0f,
    DirectionalContrastStrength = 0.3f,
    CharacterSetPreset = "extended",
    AutoBackgroundSuppression = true,
    UseParallelProcessing = true
};

using var renderer = new AsciiRenderer(options);
var frame = renderer.RenderFile("photo.jpg");
Console.WriteLine(frame.ToAnsiString()); // Colored
Console.WriteLine(frame.ToString());      // Plain
```

### High-Fidelity Color Blocks

```csharp
using ConsoleImage.Core;

// Enable ANSI support on Windows (call once at startup)
ConsoleHelper.EnableAnsiSupport();

// Uses Unicode half-blocks (▀▄) for 2x vertical resolution
using var renderer = new ColorBlockRenderer(options);
string output = renderer.RenderFile("photo.jpg");
Console.WriteLine(output);

// For animated GIFs
var frames = renderer.RenderGif("animation.gif");
foreach (var frame in frames)
{
    Console.WriteLine(frame.Content);
    Thread.Sleep(frame.DelayMs);
}
```

### ANSI Support on Windows

For colored output and animations to work correctly on Windows, you may need to enable virtual terminal processing:

```csharp
using ConsoleImage.Core;

// Call once at application startup
ConsoleHelper.EnableAnsiSupport();

// Now ANSI colors and cursor control will work
Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));
```

Modern terminals like Windows Terminal have this enabled by default, but older consoles (cmd.exe, older PowerShell) may need this call.

### Spectre.Console Integration

Install the dedicated Spectre.Console package for native `IRenderable` support:

```bash
dotnet add package mostlylucid.consoleimage.spectre
```

```csharp
using ConsoleImage.Core;
using ConsoleImage.Spectre;
using Spectre.Console;

// Static images as native Spectre renderables
AnsiConsole.Write(new AsciiImage("photo.png"));
AnsiConsole.Write(new ColorBlockImage("photo.png"));  // High-fidelity
AnsiConsole.Write(new BrailleImage("photo.png"));     // Ultra-high res

// Use in any Spectre layout
AnsiConsole.Write(new Panel(new AsciiImage("photo.png"))
    .Header("My Image")
    .Border(BoxBorder.Rounded));

// Side-by-side images
AnsiConsole.Write(new Columns(
    new Panel(new AsciiImage("a.png")).Header("Image A"),
    new Panel(new AsciiImage("b.png")).Header("Image B")
));

// Animated GIFs with Live display
var animation = new AnimatedImage("clip.gif", AnimationMode.ColorBlock);
await animation.PlayAsync(cancellationToken);

// Side-by-side animations
var anim1 = new AnimatedImage("a.gif");
var anim2 = new AnimatedImage("b.gif");
await AnsiConsole.Live(new Columns(anim1, anim2))
    .StartAsync(async ctx => {
        while (!token.IsCancellationRequested) {
            anim1.TryAdvanceFrame();
            anim2.TryAdvanceFrame();
            ctx.Refresh();
            await Task.Delay(16);
        }
    });
```

#### Without the Spectre package

The core library output is also compatible with Spectre.Console directly:

```csharp
using Spectre.Console;
using ConsoleImage.Core;

// Spectre.Console handles ANSI escape codes automatically
AnsiConsole.Write(new Text(AsciiArt.RenderColored("photo.jpg")));
```

### Animated GIFs

```csharp
using ConsoleImage.Core;

// Simple playback (infinite loop by default)
await AsciiArt.PlayGif("animation.gif");

// With options
var options = new RenderOptions
{
    LoopCount = 3,
    AnimationSpeedMultiplier = 1.5f,
    UseColor = true
};
await AsciiArt.PlayGif("animation.gif", options);

// Manual frame control
using var renderer = new AsciiRenderer(options);
var frames = renderer.RenderGif("animation.gif");

using var player = new AsciiAnimationPlayer(frames, useColor: true, loopCount: 0);
await player.PlayAsync(cancellationToken);
```

### Configuration from appsettings.json

```json
{
  "AsciiRenderer": {
    "MaxWidth": 120,
    "MaxHeight": 60,
    "ContrastPower": 2.5,
    "DirectionalContrastStrength": 0.3,
    "UseColor": true,
    "Invert": true,
    "AutoBackgroundSuppression": true,
    "CharacterSetPreset": "default"
  }
}
```

```csharp
var config = builder.Configuration.GetSection("AsciiRenderer").Get<RenderOptions>();
Console.WriteLine(AsciiArt.FromFile("photo.jpg", config));
```

## Character Set Presets

| Preset | Characters | Use Case |
|--------|------------|----------|
| `extended` | 91 ASCII chars | **Default** - Maximum detail |
| `simple` | ` .:-=+*#%@` | Quick renders |
| `block` | ` ░▒▓█` | High density blocks |
| `classic` | 71 ASCII chars | Original algorithm set |

## How It Works

This library implements [Alex Harri's shape-matching approach](https://alexharri.com/blog/ascii-rendering):

### 1. Character Analysis
Each ASCII character is rendered and analyzed using **6 sampling circles in a 3×2 staggered grid**:

```
[0]  [1]  [2]   ← Top row (staggered vertically)
[3]  [4]  [5]   ← Bottom row
```

Left circles are lowered, right circles are raised to minimize gaps while avoiding overlap. Each circle samples "ink coverage" to create a 6D shape vector.

### 2. Normalization
All character vectors are normalized by dividing by the maximum component value across ALL characters, ensuring comparable magnitudes.

### 3. Image Sampling
Input images are sampled the same way, creating shape vectors for each output cell.

### 4. Contrast Enhancement

**Global contrast** (power function):
```
value = (value / max)^power × max
```
This "crunches" lower values toward zero while preserving lighter areas.

**Directional contrast** (10 external circles):
External sampling circles reach outside each cell. For each internal component:
```
maxValue = max(internal, external)
result = applyContrast(maxValue)
```
This enhances edges where content meets empty space.

### 5. K-D Tree Matching
Fast nearest-neighbor search in 6D space finds the character whose shape vector best matches each image cell. Results are cached for repeated lookups.

### 6. Animation Optimization
For GIFs, frame differencing computes only changed pixels between frames, using ANSI cursor positioning to update efficiently.

## Performance

- **SIMD optimized**: Uses `Vector128`/`Vector256` for distance calculations
- **Parallel processing**: Multi-threaded rendering for large images
- **Caching**: Quantized vector lookups cached with 5-bit precision

### Animation Smoothness

Multiple techniques ensure flicker-free animation:

- **DECSET 2026 Synchronized Output**: Batches frame output for atomic rendering (supported by Windows Terminal, WezTerm, Ghostty, Alacritty, iTerm2)
- **Diff rendering**: Only updates changed pixels between frames using ANSI cursor positioning
- **Cursor hiding**: `\x1b[?25l` hides cursor during playback
- **Cursor save/restore**: `\x1b[s` / `\x1b[u` for consistent frame positioning
- **Pre-buffering**: All frames converted to strings before playback
- **Immediate flush**: `Console.Out.Flush()` after each frame

## Building from Source

```bash
git clone https://github.com/scottgal/mostlylucid.consoleimage.git
cd ConsoleImage
dotnet build
dotnet run --project ConsoleImage -- path/to/image.jpg
```

### Publishing AOT Binary

```bash
dotnet publish ConsoleImage/ConsoleImage.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishAot=true
```

## Credits

- Algorithm: [Alex Harri's ASCII Rendering article](https://alexharri.com/blog/ascii-rendering)
- Image processing: [SixLabors.ImageSharp](https://sixlabors.com/products/imagesharp/)

## License

This is free and unencumbered software released into the public domain. See [UNLICENSE](UNLICENSE) for details.
