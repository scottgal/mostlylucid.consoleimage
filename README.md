# MostlyLucid.ConsoleImage

High-quality ASCII art renderer for .NET 10 using shape-matching algorithm.

**Based on [Alex Harri's excellent article](https://alexharri.com/blog/ascii-rendering)** on ASCII rendering techniques.

[![NuGet](https://img.shields.io/nuget/v/MostlyLucid.ConsoleImage.svg)](https://www.nuget.org/packages/MostlyLucid.ConsoleImage/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

- **Shape-matching algorithm**: Characters selected by visual shape similarity, not just brightness
- **3×2 staggered sampling grid**: 6 sampling circles arranged per Alex Harri's article for accurate shape matching
- **K-D tree optimization**: Fast nearest-neighbor search in 6D vector space
- **Contrast enhancement**: Global power function + directional contrast with 10 external sampling circles
- **Animated GIF support**: Smooth playback with diff rendering (only updates changed pixels)
- **Color modes**:
  - ANSI colored ASCII characters
  - High-fidelity color blocks using Unicode half-blocks (▀▄)
- **Auto background detection**: Automatically detects dark/light backgrounds
- **AOT compatible**: Works with Native AOT compilation
- **Cross-platform**: Windows, Linux, macOS (x64 and ARM64)

## Installation

### NuGet Package (Library)

```bash
dotnet add package MostlyLucid.ConsoleImage
```

### CLI Tool (Standalone Binaries)

Download from [GitHub Releases](https://github.com/scottgal/ConsoleImage/releases):

| Platform | Download |
|----------|----------|
| Windows x64 | `ascii-image-win-x64.zip` |
| Windows ARM64 | `ascii-image-win-arm64.zip` |
| Linux x64 | `ascii-image-linux-x64.tar.gz` |
| Linux ARM64 | `ascii-image-linux-arm64.tar.gz` |
| macOS x64 | `ascii-image-osx-x64.tar.gz` |
| macOS ARM64 | `ascii-image-osx-arm64.tar.gz` |

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
ascii-image photo.jpg

# Specify width
ascii-image photo.jpg -w 80

# Disable color (monochrome)
ascii-image photo.jpg --no-color

# High-fidelity color blocks (requires 24-bit color terminal)
ascii-image photo.jpg --blocks

# Play animated GIF (animates by default)
ascii-image animation.gif

# Don't animate, just show first frame
ascii-image animation.gif --no-animate

# Control animation loops (0 = infinite, default)
ascii-image animation.gif --loop 3

# Speed up animation
ascii-image animation.gif --speed 2.0

# For light terminal backgrounds
ascii-image photo.jpg --no-invert

# Edge detection for enhanced foreground
ascii-image photo.jpg --edge

# Manual background suppression
ascii-image photo.jpg --bg-threshold 0.85      # Suppress light backgrounds
ascii-image photo.jpg --dark-bg-threshold 0.15 # Suppress dark backgrounds

# Save to file
ascii-image photo.jpg -o output.txt
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
| `-p, --preset` | Preset: default, simple, block | default |
| `-o, --output` | Write to file | Console |
| `--no-animate` | Don't animate GIFs | Animate ON |
| `-s, --speed` | Animation speed multiplier | 1.0 |
| `-l, --loop` | Animation loop count (0 = infinite) | 0 |
| `-e, --edge` | Enable edge detection | OFF |
| `--bg-threshold` | Light background threshold (0.0-1.0) | Auto |
| `--dark-bg-threshold` | Dark background threshold (0.0-1.0) | Auto |
| `--auto-bg` | Auto-detect background | ON |
| `-b, --blocks` | Use colored Unicode blocks | OFF |
| `--no-parallel` | Disable parallel processing | Parallel ON |

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
Console.WriteLine(AsciiArt.RenderInverted("photo.jpg"));

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

// Uses Unicode half-blocks (▀▄) for 2x vertical resolution
using var renderer = new ColorBlockRenderer(options);
string output = renderer.RenderFile("photo.jpg");
Console.WriteLine(output);

// For animated GIFs
var frames = renderer.RenderGif("animation.gif");
// frames include diff-optimized content for smooth playback
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
| `default` | 70 ASCII chars | General purpose |
| `simple` | ` .:-=+*#%@` | Quick renders |
| `block` | ` ░▒▓█` | High density |
| `extended` | 95 characters | Maximum detail |

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
- **Diff rendering**: Animations only update changed pixels

## Building from Source

```bash
git clone https://github.com/scottgal/ConsoleImage.git
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

MIT License
