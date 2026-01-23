# mostlylucid.consoleimage

**Version 2.6** - High-quality ASCII art renderer for .NET 10 using shape-matching algorithm.

**Based on [Alex Harri's excellent article](https://alexharri.com/blog/ascii-rendering)** on ASCII rendering techniques.

## What's New in 2.6

- **MCP Server** - `consoleimage-mcp` exposes rendering as AI tools for Claude Desktop, VS Code, etc.
- **Spectre.Console Enhancements** - `MatrixImage`, `MultiAnimationPlayer`, `RenderModeComparison`
- **VideoPlayer API** - Simple one-liner video playback: `await VideoPlayer.PlayAsync("video.mp4")`

## What's New in 2.5

- **Matrix Mode** - `--matrix` digital rain effect with color presets, full color mode, custom alphabets
- **Edge Detection Reveal** - `--matrix-edge-detect` reveals image shape through rain
- **FFmpeg Auto-Download** - Zero setup, FFmpeg downloads automatically on first use
- **Smart Keyframes** - Scene detection for representative frame extraction
- **Memory Efficient** - Streaming GIF output, only 1 frame in memory

## What's New in 2.7

- **Unified CLI** - `consoleimage` now handles images, GIFs, videos, AND document playback
- **Compressed documents (.cidz)** - Save rendered output as compressed documents with delta encoding
- **Document-to-GIF conversion** - Convert cidz/json documents directly to animated GIFs
- **FFmpeg-style options** - Use `--ss` for start time, `-t` for duration

## What's New in 2.0

- **URL support** - Load images directly from HTTP/HTTPS URLs
- **Native terminal protocols** - iTerm2, Kitty, Sixel auto-detection
- **JSON document format** - Save/load rendered output as portable JSON-LD documents
- **Streaming JSON** - Write frames incrementally for long videos (NDJSON format)
- **GIF output** - Save rendered output as animated GIF files
- **Dynamic resize** - Animations re-render when you resize the console
- **Flicker-free** - DECSET 2026 synchronized output with diff-based rendering
- **Performance** - Parallel rendering, pre-computed lookup tables

See [CHANGELOG.md](CHANGELOG.md) for full details.

| ASCII Mode                                                                                                                        | ColorBlocks Mode                                                                                                                         | Braille Mode                                                                                                                          |
|-----------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_ascii.gif" width="250" alt="ASCII Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_blocks.gif" width="250" alt="ColorBlocks Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_braille.gif" width="250" alt="Braille Mode"> |

**Original GIF → Three rendering modes** (shape-matched ASCII, Unicode half-blocks, braille dots)

> **Note:** These example images are rendered to GIF using the tool's `-o gif:` output option with a consistent
> monospace font. Actual terminal display may vary depending on your terminal emulator, font choice, and color support.
> GIF output typically produces cleaner results than live terminal display.

### Video to ASCII

| ASCII                                                                                                                                 | ColorBlocks                                                                                                                             | Braille                                                                                                                                   |
|---------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/familyguy_ascii.gif" width="250" alt="Video ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/familyguy_blocks.gif" width="250" alt="Video Blocks"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/familyguy_braille.gif" width="250" alt="Video Braille"> |

**Video playback** - `consoleimage movie.mp4` (FFmpeg-powered, hardware accelerated)

### Still Images

| Landscape                                                                                                                                         | Portrait                                                                                                                                |
|---------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_mountain_blocks.gif" width="350" alt="Mountain Landscape"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_portrait_blocks.gif" width="250" alt="Portrait"> |

**High-fidelity ColorBlocks mode** - 2x vertical resolution with 24-bit color

### Edge Detection

| Standard                                                                                                                               | With Edge Detection                                                                                                                         |
|----------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_portrait_ascii.gif" width="250" alt="Standard"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_portrait_edge.gif" width="250" alt="Edge Detection"> |

**Enhanced foreground visibility** - `--edge` option uses Sobel edge detection

### Character Set Presets

| Extended (default)                                                                                                                     | Simple                                                                                                                                | Block                                                                                                                               |
|----------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_portrait_ascii.gif" width="200" alt="Extended"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_portrait_simple.gif" width="200" alt="Simple"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_portrait_block.gif" width="200" alt="Block"> |

**Multiple presets** - `-p simple`, `-p block`, `-p classic`, or `-p extended` (default)

### Gamma Correction (Brightness)

| No Gamma (1.0)                                                                                                                     | Default (0.85)                                                                                                                       | Brighter (0.7)                                                                                                                     |
|------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_gamma_1.0.gif" width="200" alt="Gamma 1.0"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_gamma_0.85.gif" width="200" alt="Gamma 0.85"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/demo_gamma_0.7.gif" width="200" alt="Gamma 0.7"> |

**Automatic brightness compensation** - `--gamma` adjusts output brightness. Values < 1.0 brighten, > 1.0 darken.
Default 0.85 compensates for character/dot density.

### Animated GIF - Earth Rotation

| ASCII                                                                                                                             | ColorBlocks                                                                                                                         | Braille                                                                                                                               |
|-----------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/earth_ascii.gif" width="200" alt="Earth ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/earth_blocks.gif" width="200" alt="Earth Blocks"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/earth_braille.gif" width="200" alt="Earth Braille"> |

**Smooth animation** - DECSET 2026 synchronized output with diff-based rendering

### Matrix Mode (Digital Rain)

| Classic Green                                                                                                                                  | Full Color                                                                                                                                            | Edge Reveal                                                                                                                                     |
|------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/matrix_portrait_final.gif" width="200" alt="Matrix Classic"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/matrix_mountain_fullcolor.gif" width="200" alt="Matrix Full Color"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/matrix_edge_reveal.gif" width="200" alt="Matrix Edge Reveal"> |

**The Matrix digital rain effect** - `--matrix` option with color presets (green, red, blue, amber, purple) or full
color from source image.

**Edge Detection Reveal** - `--matrix-edge-detect --matrix-bright-persist` makes rain "flash" brightly when crossing
image edges, revealing the shape through the rain. Characters slow down and collect on horizontal edges like rain on
shoulders.

| Binary Rain                                                                                                                           | Custom Alphabet             |
|---------------------------------------------------------------------------------------------------------------------------------------|-----------------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/matrix_binary.gif" width="200" alt="Matrix Binary"> | `--matrix-alphabet "HELLO"` |

**Custom alphabets** - `--matrix-alphabet "01"` for binary rain, or any custom string like `"HELLO"`.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

## Features

- **Shape-matching algorithm**: Characters selected by visual shape similarity, not just brightness
- **3×2 staggered sampling grid**: 6 sampling circles arranged per Alex Harri's article for accurate shape matching
- **K-D tree optimization**: Fast nearest-neighbor search in 6D vector space
- **Contrast enhancement**: Global power function + directional contrast with 10 external sampling circles
- **Animated GIF support**: Smooth flicker-free playback with DECSET 2026 synchronized output and diff-based rendering
- **Dynamic resize**: Animations automatically re-render when you resize the console window
- **URL support**: Load images directly from HTTP/HTTPS URLs with download progress
- **Multiple render modes**:
    - ANSI colored ASCII characters (extended 91-char set by default)
    - High-fidelity color blocks using Unicode half-blocks (▀▄)
    - Ultra-high resolution braille characters (2×4 dots per cell, UTF-8 auto-enabled)
    - Native terminal protocols: iTerm2, Kitty, Sixel (auto-detected)
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

| Platform    | CLI                               | MCP Server                            |
|-------------|-----------------------------------|---------------------------------------|
| Windows x64 | `consoleimage-win-x64.zip`        | `consoleimage-win-x64-mcp.zip`        |
| Linux x64   | `consoleimage-linux-x64.tar.gz`   | `consoleimage-linux-x64-mcp.tar.gz`   |
| Linux ARM64 | `consoleimage-linux-arm64.tar.gz` | `consoleimage-linux-arm64-mcp.tar.gz` |
| macOS ARM64 | `consoleimage-osx-arm64.tar.gz`   | `consoleimage-osx-arm64-mcp.tar.gz`   |

## Requirements

- **.NET 10** runtime (or use standalone binaries)
- **Terminal with ANSI support** (Windows Terminal, iTerm2, any modern terminal)
- **24-bit color** recommended for `--mode blocks` and `--mode braille`
- **Unicode font** for braille mode (most terminals include this)
- **FFmpeg** - Only required for video files (auto-downloads on first video use)
  - Images, GIFs, and cidz/json documents work without FFmpeg
  - Manual install: `winget install FFmpeg` (Windows), `brew install ffmpeg` (macOS), `apt install ffmpeg` (Linux)

## Quick Start

### CLI

```bash
# === IMAGES ===
consoleimage photo.jpg                    # Render to terminal
consoleimage photo.png --blocks           # High-fidelity color blocks (▀▄)
consoleimage photo.png --braille          # Ultra-high resolution braille
consoleimage photo.png --matrix           # Matrix digital rain effect
consoleimage https://example.com/photo.jpg # Load from URL

# === ANIMATED GIFs ===
consoleimage animation.gif                # Play animation
consoleimage animation.gif --speed 1.5    # Speed up playback
consoleimage animation.gif --loop 3       # Play 3 times

# === VIDEOS (FFmpeg auto-downloads on first use) ===
consoleimage movie.mp4                    # Play video as ASCII
consoleimage movie.mkv --blocks -w 120    # Color blocks mode
consoleimage movie.mp4 --ss 60 -t 30      # Start at 60s, play 30s

# === SAVE & PLAYBACK ===
consoleimage animation.gif -o output.cidz # Save compressed document
consoleimage movie.mp4 -o movie.cidz      # Save video as document
consoleimage output.cidz                  # Play saved document
consoleimage movie.cidz --speed 2.0       # Playback with options
consoleimage movie.cidz -o movie.gif      # Convert document to GIF

# === CALIBRATION ===
consoleimage --calibrate --aspect-ratio 0.5 --save
```

### MCP Server (AI Tool Integration)

The MCP server exposes ConsoleImage as tools for AI assistants. Once configured, you can simply ask Claude to "render
this image as ASCII art" and it will use the tools automatically.

**Claude Desktop** - Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "C:\\Tools\\consoleimage-mcp\\consoleimage-mcp.exe"
    }
  }
}
```

**Claude Code** - Create `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "path/to/consoleimage-mcp"
    }
  }
}
```

**Available tools:**
| Tool | Description |
|------|-------------|
| `render_image` | Render image/GIF to ASCII art (ascii, blocks, braille, matrix) |
| `render_to_gif` | Create animated GIF output |
| `get_gif_info` | Get GIF metadata (dimensions, frame count) |
| `get_video_info` | Get video file info via FFmpeg |
| `list_render_modes` | List available render modes with descriptions |
| `list_matrix_presets` | List Matrix color presets |
| `compare_render_modes` | Render same image in all modes for comparison |

See [ConsoleImage.Mcp/README.md](ConsoleImage.Mcp/README.md) for full documentation.

### Library

```csharp
using ConsoleImage.Core;

// One line - just works with sensible defaults!
Console.WriteLine(AsciiArt.Render("photo.jpg"));
```

### Example Output

```
::::::::::::::::::::::::::::::::::::::vhhhh_:::::::
:::::::::::::::::::::::::::::::::::::Q&&&MQ:::::::
:::::::::::::::::::::::::::::::::::::Q@@@MQ:::::::
::::::vhhhhh:::_hhhhhh::::::_hhhhhh::Q@@@MQ:::::::
::::::K&&&MQKhnM&Q@@&&O\:KhnO&Q@@&&\\Q@@@MQ:::::::
:::::KM@@@@hMQMzy@@@@@@\KMQgyy@@@@@@QQ@@@MQ:::::::
:::::K@@@@@MKh*:|\@@@@@@MKh*:|\@@@@@QQ@@@MQ:::::::
::::vM@@@@MK:::::QM@@@@KK:::::QM@@@@QQ@@@MQ:::::::
::::QM@@@MQ::::::K@@@@MK::::::K@@@@M7Q@@@MQ:::::::
:::^K@@@@K^::::: G@@@@K^:::::KM@@@@K:Q@@@MQ:::::::
:::QM@@@MQ::::::QM@@@MQ::::::K@@@@MK:Q@@@MQ:::::::
:::K@@@@M7:::::^K@@@@K^:::::*G@@@@K^:Q@@@MQ:::::::
::KM@@@@K::::::QM@@@@Q::::::QM@@@MQ::Q@@@@\hhh\:::
::QMQ%%MQ::::::K%Q%%M7:::::^K%Q%%K^::.\yQ@@@QQE\::
```

## Render Modes

| Mode            | CLI Option               | Description                                     | Best For                          |
|-----------------|--------------------------|-------------------------------------------------|-----------------------------------|
| **ASCII**       | `--mode ascii` (default) | Shape-matched ASCII characters with ANSI colors | General use, widest compatibility |
| **ColorBlocks** | `--mode blocks` or `-b`  | Unicode half-blocks (▀▄) with 24-bit color      | High fidelity, photos             |
| **Braille**     | `--mode braille` or `-B` | Braille patterns (2×4 dots per cell)            | Maximum resolution                |
| **iTerm2**      | `--mode iterm2`          | Native inline image protocol                    | iTerm2, WezTerm                   |
| **Kitty**       | `--mode kitty`           | Native graphics protocol                        | Kitty terminal                    |
| **Sixel**       | `--mode sixel`           | DEC Sixel graphics                              | xterm, mlterm, foot               |

**Note:** Protocol modes (iTerm2, Kitty, Sixel) display true images in supported terminals. Use `--mode list` to see all
available modes.

## CLI Usage

```bash
# Basic - color and animation ON by default
consoleimage photo.jpg

# Load image from URL
consoleimage https://example.com/photo.jpg

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

# Save to text file
consoleimage photo.jpg -o output.txt

# Save as animated GIF
consoleimage animation.gif -o gif:output.gif

# Save as JSON document (self-contained, portable)
consoleimage animation.gif -o json:output.json

# Play saved JSON document
consoleimage output.json

# GIF with compression options
consoleimage animation.gif -o gif:output.gif --gif-scale 0.5 --gif-colors 32

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

| Option                | Description                                               | Default        |
|-----------------------|-----------------------------------------------------------|----------------|
| `-w, --width`         | Output width in characters                                | Auto           |
| `-h, --height`        | Output height in characters                               | Auto           |
| `--max-width`         | Maximum output width                                      | Console width  |
| `--max-height`        | Maximum output height                                     | Console height |
| `--no-color`          | Disable colored output                                    | Color ON       |
| `--no-invert`         | Don't invert (for light backgrounds)                      | Invert ON      |
| `--contrast`          | Contrast power (1.0 = none)                               | 2.5            |
| `--gamma`             | Gamma correction (< 1.0 brightens, > 1.0 darkens)         | 0.85           |
| `--charset`           | Custom character set                                      | -              |
| `-p, --preset`        | Preset: extended, simple, block, classic                  | extended       |
| `-o, --output`        | Output: file, `gif:file.gif`, `json:file.json`            | Console        |
| `--no-animate`        | Don't animate GIFs                                        | Animate ON     |
| `-s, --speed`         | Animation speed multiplier                                | 1.0            |
| `-l, --loop`          | Animation loop count (0 = infinite)                       | 0              |
| `-r, --framerate`     | Fixed framerate in FPS (overrides GIF timing)             | GIF timing     |
| `-f, --frame-sample`  | Frame sampling rate (skip frames)                         | 1              |
| `-e, --edge`          | Enable edge detection                                     | OFF            |
| `--bg-threshold`      | Light background threshold (0.0-1.0)                      | Auto           |
| `--dark-bg-threshold` | Dark background threshold (0.0-1.0)                       | Auto           |
| `--auto-bg`           | Auto-detect background                                    | ON             |
| `-b, --blocks`        | Use colored Unicode half-blocks                           | OFF            |
| `-B, --braille`       | Use braille characters (2×4 dots/cell)                    | OFF            |
| `--no-alt-screen`     | Keep animation in scrollback                              | Alt screen ON  |
| `--no-parallel`       | Disable parallel processing                               | Parallel ON    |
| `-a, --aspect-ratio`  | Character aspect ratio (width/height)                     | 0.5            |
| `--no-dither`         | Disable Floyd-Steinberg dithering                         | Dither ON      |
| `--no-edge-chars`     | Disable edge-direction characters                         | Edge chars ON  |
| `-j, --json`          | Output as JSON (for LLM tool calls)                       | OFF            |
| `--dark-cutoff`       | Dark terminal: skip colors below brightness (0.0-1.0)     | 0.1            |
| `--light-cutoff`      | Light terminal: skip colors above brightness (0.0-1.0)    | 0.9            |
| `-m, --mode`          | Render mode: ascii, blocks, braille, iterm2, kitty, sixel | ascii          |
| `--mode list`         | List all available render modes                           | -              |
| `--gif-scale`         | GIF output scale factor (0.25-2.0)                        | 1.0            |
| `--gif-colors`        | GIF palette size (16-256)                                 | 64             |
| `--gif-fps`           | GIF framerate                                             | 10             |
| `--gif-font-size`     | GIF font size in pixels                                   | 10             |
| `--gif-length`        | Max GIF length in seconds                                 | -              |
| `--gif-frames`        | Max GIF frames                                            | -              |

## JSON Document Format

Save rendered ASCII art to self-contained JSON documents that can be played back without the original source file.
See [docs/JSON-FORMAT.md](docs/JSON-FORMAT.md) for the full specification.

### Quick Usage

```bash
# Save to compressed document (.cidz) - recommended for animations
consoleimage animation.gif -o output.cidz
consoleimage movie.mp4 -o movie.cidz --blocks

# Save to uncompressed JSON
consoleimage animation.gif -o output.json

# Play back saved documents
consoleimage output.cidz
consoleimage movie.cidz --speed 2.0

# Convert document to GIF
consoleimage movie.cidz -o movie.gif

# Stream long video to JSON (frames written incrementally)
consoleimage long_movie.mp4 -o movie.ndjson
```

### Library API

```csharp
using ConsoleImage.Core;

// Save rendered frames to document
var doc = ConsoleImageDocument.FromAsciiFrames(frames, options, "source.gif");
await doc.SaveAsync("output.json");

// Load and play
var loaded = await ConsoleImageDocument.LoadAsync("output.json");
using var player = new DocumentPlayer(loaded);
await player.PlayAsync();

// Streaming write for long videos (NDJSON format)
await using var writer = new StreamingDocumentWriter("output.ndjson", "ASCII", options, "source.mp4");
await writer.WriteHeaderAsync();

foreach (var frame in frames)
{
    await writer.WriteFrameAsync(frame.Content, frame.DelayMs);
}

await writer.FinalizeAsync();  // Or let dispose auto-finalize
```

### Format Features

- **JSON-LD compatible** - Uses `@context` and `@type` for semantic structure
- **Self-contained** - All render settings preserved for reproducible output
- **Two formats supported**:
    - **Standard JSON** (`.json`) - Single JSON object with all frames
    - **Streaming NDJSON** (`.json` or `.ndjson`) - JSON Lines format, one record per line
- **Auto-detection** - `LoadAsync()` automatically detects which format
- **Stop anytime** - Streaming format auto-finalizes on Ctrl+C, always valid

### Embedding in Applications

Use the lightweight **[ConsoleImage.Player](ConsoleImage.Player/README.md)** package to play `.cidz` documents without any dependencies on ImageSharp or FFmpeg. Perfect for animated startup logos:

| ASCII | ColorBlocks | Braille |
|-------|-------------|---------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/moviebill_ascii.gif" width="150" alt="ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/moviebill_blocks.gif" width="150" alt="Blocks"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/moviebill_braille.gif" width="150" alt="Braille"> |

```bash
# Create document at build time
consoleimage logo.gif -w 60 -o logo.cidz
```

```csharp
// Play on startup (no ImageSharp, no FFmpeg - just JSON parsing)
var player = await ConsolePlayer.FromFileAsync("logo.cidz");
await player.PlayAsync(loopCount: 1);
```

See [ConsoleImage.Player/README.md](ConsoleImage.Player/README.md) for the complete example.

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

Modern terminals like Windows Terminal have this enabled by default, but older consoles (cmd.exe, older PowerShell) may
need this call.

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
    "CharacterSetPreset": "default"
  }
}
```

```csharp
var config = builder.Configuration.GetSection("AsciiRenderer").Get<RenderOptions>();
Console.WriteLine(AsciiArt.FromFile("photo.jpg", config));
```

## Character Set Presets

| Preset     | Characters     | Use Case                     |
|------------|----------------|------------------------------|
| `extended` | 91 ASCII chars | **Default** - Maximum detail |
| `simple`   | ` .:-=+*#%@`   | Quick renders                |
| `block`    | ` ░▒▓█`        | High density blocks          |
| `classic`  | 71 ASCII chars | Original algorithm set       |

## Documentation

| Component                          | Description                                    | Documentation                                                          |
|------------------------------------|------------------------------------------------|------------------------------------------------------------------------|
| **consoleimage**                   | Unified CLI for images, GIFs, videos, cidz     | [ConsoleImage/README.md](ConsoleImage/README.md)                       |
| **mostlylucid.consoleimage**       | Core rendering library (NuGet)                 | [ConsoleImage.Core/README.md](ConsoleImage.Core/README.md)             |
| **mostlylucid.consoleimage.video** | Video support library (NuGet)                  | [ConsoleImage.Video.Core/README.md](ConsoleImage.Video.Core/README.md) |
| **mostlylucid.consoleimage.player**| Document playback library (NuGet)              | [ConsoleImage.Player/README.md](ConsoleImage.Player/README.md)         |
| **JSON/CIDZ Format**               | Document format specification                  | [docs/JSON-FORMAT.md](docs/JSON-FORMAT.md)                             |
| **Changelog**                      | Version history                                | [CHANGELOG.md](CHANGELOG.md)                                           |

## Architecture

```
ConsoleImage.Core              # Core library (NuGet: mostlylucid.consoleimage)
├── AsciiRenderer              # Shape-matching ASCII renderer
├── ColorBlockRenderer         # Unicode half-block renderer
├── BrailleRenderer            # 2×4 dot braille renderer
├── MatrixRenderer             # Digital rain effect
├── Protocol renderers         # iTerm2, Kitty, Sixel support
├── AsciiAnimationPlayer       # Flicker-free GIF playback
├── ConsoleImageDocument       # JSON/CIDZ document format
├── DocumentPlayer             # Document playback
├── GifWriter                  # Animated GIF output
└── ConsoleHelper              # Windows ANSI support

ConsoleImage                   # Unified CLI (images, GIFs, videos, documents)
ConsoleImage.Video.Core        # FFmpeg video decoding (optional, for video files)
ConsoleImage.Player            # Standalone document playback (NuGet)
ConsoleImage.Spectre           # Spectre.Console integration
```

## How It Works

This library implements [Alex Harri's shape-matching approach](https://alexharri.com/blog/ascii-rendering):

### 1. Character Analysis

Each ASCII character is rendered and analyzed using **6 sampling circles in a 3×2 staggered grid**:

```
[0]  [1]  [2]   ← Top row (staggered vertically)
[3]  [4]  [5]   ← Bottom row
```

Left circles are lowered, right circles are raised to minimize gaps while avoiding overlap. Each circle samples "ink
coverage" to create a 6D shape vector.

### 2. Normalization

All character vectors are normalized by dividing by the maximum component value across ALL characters, ensuring
comparable magnitudes.

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

Fast nearest-neighbor search in 6D space finds the character whose shape vector best matches each image cell. Results
are cached for repeated lookups.

### 6. Animation Optimization

For GIFs, frame differencing computes only changed pixels between frames, using ANSI cursor positioning to update
efficiently.

## Performance

- **SIMD optimized**: Uses `Vector128`/`Vector256`/`Vector512` for distance calculations
- **Parallel processing**: Multi-threaded rendering for ASCII, ColorBlock, and Braille modes
- **Pre-computed trigonometry**: Circle sampling uses lookup tables (eliminates ~216 trig calls per cell)
- **Caching**: Quantized vector lookups cached with 5-bit precision
- **Optimized bounds checking**: Branchless `(uint)x < (uint)width` pattern

### Animation Smoothness

Multiple techniques ensure flicker-free animation:

- **DECSET 2026 Synchronized Output**: Batches frame output for atomic rendering (supported by Windows Terminal,
  WezTerm, Ghostty, Alacritty, iTerm2)
- **Diff-based rendering**: Only updates changed lines between frames - no per-line clearing that causes black flashes
- **Overwrite with padding**: Lines are overwritten in place with space padding, eliminating flicker completely
- **Dynamic resize**: Animations automatically re-render when you resize the console window
- **Smooth loop transitions**: Automatic interpolation between last and first frames creates seamless loops
- **Cursor hiding**: `\x1b[?25l` hides cursor during playback
- **Pre-buffering**: All frames, diffs, and transitions converted to strings before playback
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
