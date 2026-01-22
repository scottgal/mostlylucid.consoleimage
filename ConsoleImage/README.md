# ConsoleImage

High-quality ASCII art renderer for images and animated GIFs.

**Part of the [ConsoleImage](https://github.com/scottgal/mostlylucid.consoleimage) project.**

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

## Features

- **Shape-matched ASCII** - Characters selected by visual similarity, not just brightness
- **Multiple render modes** - ASCII, ColorBlocks (▀▄), Braille (2×4 dots), Matrix rain
- **Animated GIF playback** - Flicker-free animation with DECSET 2026
- **GIF output** - Save rendered output as animated GIF files
- **JSON documents** - Save/load portable ASCII art documents
- **URL support** - Load images directly from HTTP/HTTPS
- **Native terminal protocols** - iTerm2, Kitty, Sixel auto-detection
- **Native AOT** - Fully compatible with ahead-of-time compilation

### Example Output

| ASCII Mode | ColorBlocks Mode | Braille Mode |
|------------|------------------|--------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_ascii.gif" width="200" alt="ASCII Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_blocks.gif" width="200" alt="ColorBlocks Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_braille.gif" width="200" alt="Braille Mode"> |

**Three rendering modes** using shape-matched ASCII, Unicode half-blocks, or braille patterns.

> **Note:** These images are rendered using the tool's GIF output with a consistent monospace font. Actual terminal display varies by terminal emulator, font, and color support.

## Installation

### Standalone Binaries

Download from [GitHub Releases](https://github.com/scottgal/mostlylucid.consoleimage/releases):

| Platform | Download |
|----------|----------|
| Windows x64 | `consoleimage-win-x64.zip` |
| Linux x64 | `consoleimage-linux-x64.tar.gz` |
| Linux ARM64 | `consoleimage-linux-arm64.tar.gz` |
| macOS ARM64 | `consoleimage-osx-arm64.tar.gz` |

### .NET Tool

```bash
dotnet tool install -g mostlylucid.consoleimage.cli
```

## CLI Usage

### Basic Usage

```bash
# Render an image
consoleimage photo.jpg

# Play animated GIF
consoleimage animation.gif

# Load from URL
consoleimage https://example.com/photo.jpg
```

### Render Modes

```bash
# ASCII mode (default) - shape-matched characters
consoleimage photo.jpg

# ColorBlocks mode (-b/--blocks) - 2x vertical resolution
consoleimage photo.jpg --blocks
consoleimage photo.jpg -b

# Braille mode (-B/--braille) - 2x4 dots per cell
consoleimage photo.jpg --braille
consoleimage photo.jpg -B

# Matrix mode (--matrix) - digital rain effect
consoleimage photo.jpg --matrix
consoleimage animation.gif --matrix
```

### Dimension Control

```bash
# Set width (height auto-calculated)
consoleimage photo.jpg -w 100

# Set height (width auto-calculated)
consoleimage photo.jpg -h 40

# Set maximum bounds (preserves aspect ratio)
consoleimage photo.jpg --max-width 120 --max-height 50
```

### Animation Control

```bash
# Speed control (2.0 = double speed)
consoleimage animation.gif -s 2.0

# Loop count (0 = infinite)
consoleimage animation.gif -l 3       # Play 3 times
consoleimage animation.gif -l 0       # Loop forever

# Show first frame only
consoleimage animation.gif --no-animate

# Frame sampling (skip frames for performance)
consoleimage animation.gif -f 2       # Every 2nd frame
```

### Output Options

```bash
# Save as animated GIF
consoleimage animation.gif -o gif:output.gif
consoleimage photo.jpg -o gif:photo.gif

# GIF quality options
consoleimage animation.gif -o gif:output.gif --gif-font-size 8
consoleimage animation.gif -o gif:output.gif --gif-scale 0.5
consoleimage animation.gif -o gif:output.gif --gif-colors 64
consoleimage animation.gif -o gif:output.gif --gif-length 10  # Max 10 seconds

# Save as JSON document (portable, no source needed)
consoleimage animation.gif -o json:output.json

# Play saved JSON document
consoleimage output.json

# Save to text file
consoleimage photo.jpg -o output.txt
```

### Matrix Mode Options

```bash
# Classic green Matrix
consoleimage photo.jpg --matrix

# Color variations
consoleimage photo.jpg --matrix --matrix-color red
consoleimage photo.jpg --matrix --matrix-color blue
consoleimage photo.jpg --matrix --matrix-color amber
consoleimage photo.jpg --matrix --matrix-color purple

# Full color (uses source image colors)
consoleimage photo.jpg --matrix --matrix-fullcolor

# Image reveal (edge detection + brightness persistence)
consoleimage photo.jpg --matrix --matrix-edge-detect --matrix-bright-persist

# ASCII only (no katakana)
consoleimage photo.jpg --matrix --matrix-ascii

# Custom alphabet
consoleimage photo.jpg --matrix --matrix-alphabet "01"        # Binary
consoleimage photo.jpg --matrix --matrix-alphabet "HELLO"     # Custom
```

### Appearance Options

```bash
# Disable color
consoleimage photo.jpg --no-color

# Adjust contrast (higher = more contrast)
consoleimage photo.jpg --contrast 3.0

# Gamma correction (< 1.0 brightens, > 1.0 darkens)
consoleimage photo.jpg --gamma 0.7

# Edge detection for enhanced foreground
consoleimage photo.jpg --edge

# Character set presets
consoleimage photo.jpg -p simple      # Minimal: .:-=+*#%@
consoleimage photo.jpg -p block       # Unicode: ░▒▓█
consoleimage photo.jpg -p extended    # 91 chars (default)
consoleimage photo.jpg -p classic     # Original algorithm

# Custom character set
consoleimage photo.jpg --charset " .:-=+*#%@"
```

### Background Handling

```bash
# Light background threshold (suppress above brightness)
consoleimage photo.jpg --bg-threshold 0.85

# Dark background threshold (suppress below brightness)
consoleimage photo.jpg --dark-bg-threshold 0.15
```

### Calibration

Different terminals/fonts may need calibration for circles to appear round:

```bash
# Show calibration pattern
consoleimage --calibrate

# Adjust aspect ratio until circle looks correct
consoleimage --calibrate --aspect-ratio 0.45

# Save calibration
consoleimage --calibrate --aspect-ratio 0.48 --save

# Each render mode can have separate calibration
consoleimage --calibrate --blocks --aspect-ratio 0.50 --save
consoleimage --calibrate --braille --aspect-ratio 0.52 --save
```

### Status Line

Show playback information below the rendered output:

```bash
# Show status line with progress, timing, file info
consoleimage animation.gif --status
consoleimage animation.gif -S
```

### CLI Options Reference

| Option | Description | Default |
|--------|-------------|---------|
| `-w, --width` | Output width in characters | Auto |
| `-h, --height` | Output height in characters | Auto |
| `--max-width` | Maximum width constraint | Console width |
| `--max-height` | Maximum height constraint | Console height |
| `-b, --blocks` | Use Unicode half-blocks | OFF |
| `-B, --braille` | Use braille characters | OFF |
| `--matrix` | Matrix digital rain effect | OFF |
| `-m, --mode` | Render mode (ascii, blocks, braille, iterm2, kitty, sixel) | ascii |
| `-o, --output` | Output: file, `gif:file.gif`, `json:file.json` | Console |
| `-s, --speed` | Animation speed multiplier | 1.0 |
| `-l, --loop` | Loop count (0 = infinite) | 0 |
| `-f, --frame-sample` | Skip frames (2 = every 2nd) | 1 |
| `-S, --status` | Show status line | OFF |
| `--no-color` | Disable color output | Color ON |
| `--no-animate` | Show first frame only | Animate ON |
| `--contrast` | Contrast power (1.0-4.0) | 2.5 |
| `--gamma` | Gamma correction | 0.85 |
| `-a, --aspect-ratio` | Character aspect ratio | 0.5 |
| `-p, --preset` | Character preset | extended |
| `-e, --edge` | Enable edge detection | OFF |
| `--no-dither` | Disable dithering | Dither ON |
| `--calibrate` | Show calibration pattern | OFF |
| `--save` | Save calibration | - |

### GIF Output Options

| Option | Description | Default |
|--------|-------------|---------|
| `--gif-font-size` | Font size in pixels | 10 |
| `--gif-scale` | Output scale factor | 1.0 |
| `--gif-colors` | Palette size (16-256) | 64 |
| `--gif-fps` | Framerate | 10 |
| `--gif-length` | Max length in seconds | - |
| `--gif-frames` | Max frame count | - |

### Matrix Options

| Option | Description | Default |
|--------|-------------|---------|
| `--matrix-color` | Color: green, red, blue, amber, purple | green |
| `--matrix-fullcolor` | Use source image colors | OFF |
| `--matrix-ascii` | ASCII only (no katakana) | OFF |
| `--matrix-alphabet` | Custom character set | - |
| `--matrix-density` | Rain density (0.1-2.0) | 0.5 |
| `--matrix-speed` | Animation speed | 1.0 |
| `--matrix-edge-detect` | Enable edge detection reveal | OFF |
| `--matrix-bright-persist` | Brightness persistence | OFF |

## Examples

```bash
# Quick preview of an image
consoleimage photo.jpg -w 80

# High-quality braille render
consoleimage photo.jpg -B --contrast 2.5

# Create animated GIF from source GIF
consoleimage animation.gif -o gif:output.gif --blocks

# Matrix effect on an image
consoleimage portrait.jpg --matrix --matrix-edge-detect -w 60

# Low-resource mode for large GIFs
consoleimage big.gif -w 60 -f 2 --no-dither

# Save and replay
consoleimage animation.gif -o json:saved.json
consoleimage saved.json
```

## Requirements

- **Terminal with ANSI support** (Windows Terminal, iTerm2, any modern terminal)
- **24-bit color** recommended for ColorBlocks and Braille modes
- **Unicode font** for braille mode (most terminals include this)

## For Video Support

Use [consolevideo](https://github.com/scottgal/mostlylucid.consoleimage/tree/master/ConsoleImage.Video) for video file playback with FFmpeg.

## Library Usage

For programmatic usage, see [mostlylucid.consoleimage](https://www.nuget.org/packages/mostlylucid.consoleimage/) NuGet package.

```csharp
using ConsoleImage.Core;

// One line - just works!
Console.WriteLine(AsciiArt.Render("photo.jpg"));

// Colored output
Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));

// Play animated GIF
await AsciiArt.PlayGif("animation.gif");
```

## License

This is free and unencumbered software released into the public domain. See [UNLICENSE](../UNLICENSE) for details.
