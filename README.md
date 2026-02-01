# mostlylucid.consoleimage

**Version 4.5** - High-quality ASCII art renderer for .NET 10 with SIMD-accelerated braille vectorization, live AI transcription, and temporal super-resolution.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

## Table of Contents

- [Quick Start](#quick-start)
- [Zero Setup](#zero-setup---everything-downloads-automatically)
- [Render Modes](#render-modes)
- [Features](#features)
- [Installation](#installation)
- [Requirements](#requirements)
- [MCP Server](#mcp-server)
- [Library](#library)
- [Documentation](#documentation)
- [Architecture](#architecture)
- [Building from Source](#building-from-source)
- [Braille Shape Vector Matching](#braille-shape-vector-matching-v45)

## Quick Start

```bash
# Render an image to your terminal (braille mode by default!)
consoleimage photo.jpg

# Play a video (braille mode, FFmpeg auto-downloads on first use)
consoleimage movie.mp4

# Play an animated GIF
consoleimage animation.gif

# Browse photos in slideshow mode
consoleimage ./photos

# Play YouTube videos directly
consoleimage "https://youtu.be/dQw4w9WgXcQ"

# Live AI subtitles while watching (requires whisper variant)
consoleimage movie.mp4 --subs whisper
```

That's it! Colors and animation are enabled by default. **Braille mode is now the default** for maximum detail.

## Zero Setup - Everything Downloads Automatically

ConsoleImage requires **zero manual setup** for common tasks. Dependencies are downloaded automatically on first use:

| Component          | When Downloaded           | Size     | Cache Location                        |
|--------------------|---------------------------|----------|---------------------------------------|
| **FFmpeg**         | First video file playback | ~25MB    | `~/.local/share/consoleimage/ffmpeg/` |
| **yt-dlp**         | First YouTube URL         | ~10MB    | `~/.local/share/consoleimage/ytdlp/`  |
| **Whisper Models** | First transcription       | 75MB-3GB | `~/.local/share/consoleimage/whisper/` |

### Whisper Transcription (`--subs whisper`)

Whisper AI transcription requires native libraries that are **only included in the `consoleimage-whisper` variant**. The default `consoleimage` download handles images, GIFs, videos, embedded subtitles, and YouTube subtitles — but does not include Whisper.

To use `--subs whisper`:
1. Download the **`consoleimage-*-whisper`** archive from [Releases](https://github.com/scottgal/mostlylucid.consoleimage/releases)
2. Extract and use in place of the default binary (same executable name, just includes native Whisper libraries alongside it)
3. Whisper **models** auto-download on first use (~75MB-3GB depending on model size)

```bash
# Using the whisper variant - just works
consoleimage movie.mp4 --subs whisper              # Auto-downloads base model (~142MB)
consoleimage movie.mp4 --subs whisper -wm small     # Use small model (~466MB)
consoleimage movie.mp4 --subs whisper -wm tiny      # Fastest, smallest (~75MB)
```

> **Don't need Whisper?** Most subtitle workflows don't require it. Use `--subs auto` to automatically find embedded subtitles, external .srt/.vtt files, or YouTube captions — no Whisper needed.

### Manual Installation (If Needed)

If auto-download fails (corporate firewall, etc.), install manually:

```bash
# FFmpeg
winget install ffmpeg              # Windows
brew install ffmpeg                # macOS
apt install ffmpeg                 # Linux

# yt-dlp
pip install yt-dlp
```

> **Whisper** does not require separate installation — download the `consoleimage-*-whisper` variant which bundles the native libraries. Whisper models auto-download from Hugging Face on first use.

Use `-y` / `--yes` to auto-confirm all downloads without prompts. All downloads are cached in `~/.local/share/consoleimage/` (or `%LOCALAPPDATA%\consoleimage\` on Windows).

## Render Modes

| Mode        | Command                     | Resolution         | Best For                     |
|-------------|-----------------------------|--------------------|------------------------------|
| **Braille** | `consoleimage photo.jpg`    | 8x (2x4 dots/cell) | **DEFAULT** - Maximum detail |
| **ASCII**   | `consoleimage photo.jpg -a` | Standard           | Widest compatibility         |
| **Blocks**  | `consoleimage photo.jpg -b` | 2x vertical        | Photos, high fidelity        |
| **Matrix**  | `consoleimage photo.jpg -M` | Digital rain       | Special effects              |

### Comparison

| Braille (DEFAULT) | ASCII | ColorBlocks |
|---|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_braille.gif" width="250" alt="Braille Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_ascii.gif" width="250" alt="ASCII Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_blocks.gif" width="250" alt="ColorBlocks Mode"> |
| 2x4 dot patterns (8x resolution) | Shape-matched characters | Unicode half-blocks |

### Monochrome Braille: Compact & Fast

| Monochrome Braille | Full Color Braille |
|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/boingball_mono.gif" width="250" alt="Monochrome"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/boingball_braille.gif" width="250" alt="Color Braille"> |
| ~265 KB (3-5x smaller) | ~884 KB |

### Landscape

| Braille | ASCII | ColorBlocks |
|---|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_braille.gif" width="250" alt="Landscape Braille"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_ascii.gif" width="250" alt="Landscape ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_blocks.gif" width="250" alt="Landscape Blocks"> |

### Matrix

| Classic Green | Full Color |
|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_matrix.gif" width="250" alt="Matrix Classic"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_matrix_fullcolor.gif" width="250" alt="Matrix Full Color"> |

For the complete CLI guide covering all modes, subtitles, YouTube, slideshow, export, and every option: **[CLI Guide](docs/CLI-GUIDE.md)**

## Features

- **Shape-matching algorithm**: Characters selected by visual shape similarity, not just brightness
- **SIMD braille vectorization**: 8D shape vectors for all 256 braille patterns with `Vector256<float>` hardware acceleration
- **Expanded ASCII character sets**: Full 95 printable ASCII characters with disk-cached shape vectors
- **Braille interlace mode** (experimental): Temporal super-resolution via rapid frame cycling (FRC-inspired)
- **Multiple render modes**: ASCII, ColorBlocks, Braille, Matrix, iTerm2, Kitty, Sixel
- **Animated GIF/video support**: Flicker-free DECSET 2026 synchronized output
- **Dynamic resize**: Animations re-render when you resize the console window
- **Live AI subtitles**: Real-time Whisper transcription during video playback
- **YouTube integration**: Play YouTube videos with auto-downloaded yt-dlp
- **Hardware acceleration**: CUDA/D3D11VA/VAAPI with automatic software fallback
- **Slideshow mode**: Browse directories with keyboard navigation
- **Markdown/SVG export**: Embeddable output for documentation and READMEs
- **Document format**: Save/load rendered art as `.cidz` compressed documents
- **AOT compiled**: Native binaries, no .NET runtime required
- **Cross-platform**: Windows, Linux, macOS (x64 and ARM64)

<details>
<summary><strong>What's New in v4.5</strong></summary>

### Braille Shape Vector Matching

The braille renderer now uses **SIMD-accelerated shape vector matching** instead of simple brightness thresholding. Each of the 256 Unicode braille patterns is represented as an 8D vector (one component per dot in the 2x4 grid), and the renderer finds the best-matching pattern using hardware-accelerated distance calculations.

**How it works:**
1. For each character cell, 8 dot positions are sampled using concentric ring sampling (13 samples per dot, 104 total per cell)
2. Brightness values are converted to coverage vectors (0.0-1.0 per dot)
3. The coverage vector is matched against all 256 braille patterns via SIMD brute force
4. A quantized cache (8 components x 4 bits = 32-bit key) prevents redundant calculations

With only 256 vectors, SIMD brute force outperforms tree-based search. Benchmarks show ~6 us per 1000 lookups with zero allocations.

### Braille Interlace Mode (Experimental)

> **Status: Experimental** — Known issues: black horizontal bars appear between frames due to a screen clearing/cursor positioning bug. The underlying frame generation works, but the playback player has visual artifacts.

Inspired by LCD FRC (Frame Rate Control) and DLP temporal dithering, interlace mode generates multiple braille subframes with slightly different dithering thresholds. When cycled rapidly, the human visual system integrates the frames, perceiving more tonal detail than any single braille frame can display.

```bash
# Interlace mode for still images (experimental)
consoleimage photo.jpg --interlace

# Configure interlace parameters
consoleimage photo.jpg --interlace --interlace-frames 6 --interlace-fps 30
```

### Expanded ASCII Character Sets

ASCII shape matching now uses **all 95 printable ASCII characters** by default (up from 70). More characters means more shape options for the matching algorithm, improving output quality with no performance cost.

- **Full (default)**: 95 printable ASCII chars (space through ~)
- **Classic**: Original 70-char curated set from Alex Harri's article
- **Extended**: 93-char set with additional gradations

Shape vectors are **cached to disk** (`%LOCALAPPDATA%\consoleimage\shapevectors\`) to avoid re-rendering fonts on every startup.

### Hardware Acceleration with Auto-Fallback

FFmpeg hardware acceleration (CUDA, D3D11VA, VAAPI) now automatically falls back to software decoding when GPU decoding fails. HEVC/H.265 and other problematic codecs are proactively blocklisted to avoid known compatibility issues.

### Greyscale ANSI Mode

256-color greyscale output using ANSI palette indices 232-255 for terminals without full RGB support.

### Other Improvements

- **ConsoleHelper auto-init**: Renderers auto-enable Windows ANSI support (no manual setup for NuGet consumers)
- **Edge direction fix**: Corrected gradient-to-edge rotation (PI/2 offset) for sharper directional characters
- **Symmetric sampling grid**: Fixed asymmetric stagger in ASCII sampling that caused `\` where `/` should appear

See [CHANGELOG.md](CHANGELOG.md) for full history.
</details>

<details>
<summary><strong>What's New in v3.0</strong></summary>

- **Braille is now the default** - Maximum resolution out of the box
- **`-a, --ascii` option** - Use for classic ASCII mode (previous default)
- **Video width defaults to 50** for braille (CPU intensive)
- **Easter egg** - Run with no arguments for a surprise!

See [CHANGELOG.md](CHANGELOG.md) for full history.
</details>

## Installation

### NuGet Package (Library)

```bash
dotnet add package mostlylucid.consoleimage
```

### CLI Tool (Standalone Binaries)

Download from [GitHub Releases](https://github.com/scottgal/mostlylucid.consoleimage/releases):

| Platform      | CLI                                 | + Whisper                                   | MCP Server                            |
|---------------|-------------------------------------|---------------------------------------------|---------------------------------------|
| Windows x64   | `consoleimage-win-x64.zip`          | `consoleimage-win-x64-whisper.zip`          | `consoleimage-win-x64-mcp.zip`        |
| Windows ARM64 | `consoleimage-win-arm64.zip`        | `consoleimage-win-arm64-whisper.zip`        | `consoleimage-win-arm64-mcp.zip`      |
| Linux x64     | `consoleimage-linux-x64.tar.gz`     | `consoleimage-linux-x64-whisper.tar.gz`     | `consoleimage-linux-x64-mcp.tar.gz`   |
| Linux ARM64   | `consoleimage-linux-arm64.tar.gz`   | `consoleimage-linux-arm64-whisper.tar.gz`   | `consoleimage-linux-arm64-mcp.tar.gz` |
| macOS ARM64   | `consoleimage-osx-arm64.tar.gz`     | `consoleimage-osx-arm64-whisper.tar.gz`     | `consoleimage-osx-arm64-mcp.tar.gz`   |

> **Which download do I need?**
> - **consoleimage** — Core CLI for images, GIFs, videos, subtitles from files/embedded/YouTube, and document playback.
> - **consoleimage + whisper** — Same binary + Whisper native libraries for local AI transcription (`--subs whisper`).
> - **consoleimage-mcp** — MCP server for AI assistants (Claude Desktop, Claude Code). See [MCP Server](#mcp-server).

### Quick Install (Command Line)

**Windows (PowerShell):**

```powershell
$version = "4.5.0"  # Check releases for latest
Invoke-WebRequest -Uri "https://github.com/scottgal/mostlylucid.consoleimage/releases/download/consoleimage-v$version/consoleimage-win-x64-whisper.zip" -OutFile "$env:TEMP\consoleimage.zip"
Expand-Archive -Path "$env:TEMP\consoleimage.zip" -DestinationPath "$env:LOCALAPPDATA\consoleimage" -Force
$env:PATH += ";$env:LOCALAPPDATA\consoleimage"
[Environment]::SetEnvironmentVariable("PATH", $env:PATH, "User")
```

**Linux / macOS:**

```bash
VERSION="4.5.0"  # Check releases for latest
# Linux x64:
curl -L "https://github.com/scottgal/mostlylucid.consoleimage/releases/download/consoleimage-v${VERSION}/consoleimage-linux-x64-whisper.tar.gz" | sudo tar -xz -C /usr/local/bin
# macOS ARM64:
curl -L "https://github.com/scottgal/mostlylucid.consoleimage/releases/download/consoleimage-v${VERSION}/consoleimage-osx-arm64-whisper.tar.gz" | tar -xz -C /usr/local/bin
```

Verify: `consoleimage --version`

## Requirements

- **.NET 10** runtime (or use standalone binaries)
- **Terminal with ANSI support** (Windows Terminal, iTerm2, any modern terminal)
- **24-bit color** recommended for `--mode blocks` and `--mode braille`
- **Unicode font** for braille mode (most terminals include this)
- **FFmpeg** - Only required for video files (auto-downloads on first video use)

## MCP Server

The MCP server exposes ConsoleImage as tools for AI assistants. Once configured, you can simply ask Claude to "render this image as ASCII art" and it will use the tools automatically.

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

**21 tools** across 6 categories:

| Category | Tools | What You Can Do |
|----------|-------|-----------------|
| **Image Rendering** | `render_image`, `render_to_gif`, `compare_render_modes`, `list_render_modes`, `list_matrix_presets` | See any image as text, create GIFs, compare styles |
| **Video Analysis** | `get_video_info`, `render_video`, `render_video_frame`, `extract_frames`, `detect_scenes` | Inspect videos, render frames, find scene cuts |
| **Subtitles** | `get_subtitle_streams`, `extract_subtitles`, `parse_subtitles`, `get_youtube_subtitles` | Read dialogue from any video or YouTube URL |
| **YouTube** | `check_youtube_url`, `get_youtube_stream` | Access YouTube video streams and captions |
| **Export** | `export_to_svg`, `export_to_markdown` | Create embeddable SVG art and markdown docs |
| **Documents & System** | `get_document_info`, `get_image_info`, `get_gif_info`, `check_dependencies` | Inspect metadata and verify tool availability |

See [ConsoleImage.Mcp/README.md](ConsoleImage.Mcp/README.md) for full documentation including AI-optimized usage patterns.

## Library

```csharp
using ConsoleImage.Core;

// One line - just works with sensible defaults!
Console.WriteLine(AsciiArt.Render("photo.jpg"));

// Colored output
Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));

// Play animated GIF
await AsciiArt.PlayGif("animation.gif");
```

```
::::::::::::::::::::::::::::::::::::::vhhhh_:::::::
:::::::::::::::::::::::::::::::::::::Q&&&MQ:::::::
:::::::::::::::::::::::::::::::::::::Q@@@MQ:::::::
::::::vhhhhh:::_hhhhhh::::::_hhhhhh::Q@@@MQ:::::::
::::::K&&&MQKhnM&Q@@&&O\:KhnO&Q@@&&\\Q@@@MQ:::::::
:::::KM@@@@hMQMzy@@@@@@\KMQgyy@@@@@@QQ@@@MQ:::::::
```

For presets, full options, Spectre.Console integration, GIF control, and configuration: **[Library API](docs/LIBRARY-API.md)**

## Documentation

| Document | Description |
|----------|-------------|
| **[CLI Guide](docs/CLI-GUIDE.md)** | Complete CLI reference: render modes, subtitles, YouTube, slideshow, export, all options |
| **[Library API](docs/LIBRARY-API.md)** | .NET library usage: simple API, full options, Spectre.Console, GIFs, configuration |
| **[How It Works](docs/HOW-IT-WORKS.md)** | Shape-matching algorithm explanation and performance details |
| **[JSON Format](docs/JSON-FORMAT.md)** | Document format specification (`.cidz`, `.json`, `.ndjson`) |
| **[Braille Rendering](docs/BRAILLE-RENDERING.md)** | Technical deep-dive into the 8x resolution braille mode |
| **[Changelog](CHANGELOG.md)** | Version history |

### Component Documentation

| Component | Description | Documentation |
|-----------|-------------|---------------|
| **consoleimage** | Unified CLI (images, GIFs, videos, documents) | [ConsoleImage/README.md](ConsoleImage/README.md) |
| **consoleimage-mcp** | MCP server for AI assistants (21 tools) | [ConsoleImage.Mcp/README.md](ConsoleImage.Mcp/README.md) |
| **mostlylucid.consoleimage** | Core rendering library (NuGet) | [ConsoleImage.Core/README.md](ConsoleImage.Core/README.md) |
| **mostlylucid.consoleimage.video** | Video support library (NuGet) | [ConsoleImage.Video.Core/README.md](ConsoleImage.Video.Core/README.md) |
| **mostlylucid.consoleimage.player** | Document playback library (NuGet) | [ConsoleImage.Player/README.md](ConsoleImage.Player/README.md) |
| **mostlylucid.consoleimage.spectre** | Spectre.Console integration (NuGet) | [ConsoleImage.Spectre/README.nuget.md](ConsoleImage.Spectre/README.nuget.md) |

## Architecture

```
ConsoleImage.Core              # Core library (NuGet: mostlylucid.consoleimage)
├── AsciiRenderer              # Shape-matching ASCII renderer (95-char default set)
├── ColorBlockRenderer         # Unicode half-block renderer
├── BrailleRenderer            # SIMD shape-vector braille renderer (2x4 dots per cell)
├── BrailleCharacterMap        # 8D vector generation + SIMD matching for 256 patterns
├── BrailleInterlacePlayer     # [EXPERIMENTAL] Temporal super-resolution playback (FRC-inspired)
├── CharacterMap               # Font shape analysis with disk-cached vectors
├── MatrixRenderer             # Digital rain effect
├── MarkdownRenderer           # SVG/HTML/Markdown export
├── Protocol renderers         # iTerm2, Kitty, Sixel support
├── AsciiAnimationPlayer       # Flicker-free GIF playback
├── ConsoleImageDocument       # JSON/CIDZ document format
├── DocumentPlayer             # Document playback
├── GifWriter                  # Animated GIF output
├── Subtitles/                 # SRT/VTT parsing, rendering, embedded extraction
└── ConsoleHelper              # Windows ANSI support (auto-initialized by renderers)

ConsoleImage                   # Unified CLI (images, GIFs, videos, documents)
ConsoleImage.Video.Core        # FFmpeg video decoding with hwaccel auto-fallback
ConsoleImage.Transcription     # Whisper AI transcription (optional, for --subs whisper)
ConsoleImage.Mcp               # MCP server for AI assistants (21 tools)
ConsoleImage.Player            # Standalone document playback (NuGet)
ConsoleImage.Spectre           # Spectre.Console integration
ConsoleImage.Benchmarks        # BenchmarkDotNet performance suite
```

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

## Braille Shape Vector Matching (v4.5)

The v4.5 braille renderer replaces simple brightness thresholding with **SIMD-accelerated shape vector matching** across all 256 Unicode braille patterns. This produces dramatically cleaner edges, better detail preservation, and more accurate representation of the source image.

### How It Works

Each Unicode braille character encodes 8 dots in a 2x4 grid. The renderer treats each character as an **8-dimensional vector** where each component represents dot on/off state:

```
Braille dot layout:        Vector mapping:
  ● ○     dot1 dot4        [1.0, 0.0, 1.0, 1.0, 0.0, 0.0, 1.0, 0.0]
  ● ●     dot2 dot5         ↑              ↑
  ○ ○     dot3 dot6         dot1=ON        dot4=OFF
  ● ○     dot7 dot8
  = U+28D5 (⣕)
```

**Sampling**: For each character cell, the renderer samples 8 dot positions using concentric ring sampling (center + 4 inner + 8 outer = 13 samples per dot, 104 total per cell). This captures smooth gradients more accurately than single-point sampling.

**Matching**: The 8-float coverage vector is matched against all 256 pre-computed braille vectors using `Vector256<float>` SIMD instructions. With only 256 candidates, brute force with hardware acceleration outperforms any tree or graph-based search.

**Caching**: A quantized cache maps each input to a 32-bit key (8 components x 4 bits), preventing redundant distance calculations for similar inputs. This also provides natural temporal stability for animations.

### Performance

Benchmarked on AMD Ryzen 9 9950X (.NET 10.0, AVX-512):

| Operation | Time (per 1000 lookups) | Allocations |
|-----------|------------------------|-------------|
| Cached random vectors | 6.3 us | 0 B |
| Brute force (no cache) | 6.2 us | 0 B |
| Sparse vectors (edges) | 6.2 us | 0 B |

The cache and brute force paths are essentially identical at 256 vectors -- SIMD is so fast that dictionary overhead cancels any caching benefit. Both paths are allocation-free.

### ASCII Character Set Expansion

The ASCII renderer also benefits from expanded shape matching. v4.5 uses all 95 printable ASCII characters by default, up from the original 70-character curated set. Shape vectors are cached to disk to avoid re-rendering fonts on startup.

| Character Set | Chars | Cached Lookup (per 1000) | Brute Force (per 1000) |
|---------------|-------|--------------------------|------------------------|
| Classic (v4.1) | 70 | 6.1 us | 94.1 us |
| Full (v4.5 default) | 95 | 6.0 us | 133.5 us |
| Extended | 93 | 6.0 us | N/A |

Cached lookups are character-count-independent (quantized cache hit). Brute force scales linearly but is only used on cache miss.

## Credits

- Algorithm: [Alex Harri's ASCII Rendering article](https://alexharri.com/blog/ascii-rendering)
- Image processing: [SixLabors.ImageSharp](https://sixlabors.com/products/imagesharp/)

## License

This is free and unencumbered software released into the public domain. See [UNLICENSE](UNLICENSE) for details.
