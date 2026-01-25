# mostlylucid.consoleimage

**Version 4.1** - High-quality ASCII art renderer for .NET 10 with live AI transcription.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

## Table of Contents

- [Quick Start](#quick-start)
- [CLI Guide](#cli-guide)
- [Features](#features)
- [Installation](#installation)
- [Requirements](#requirements)
- [CLI Cookbook](#cli-cookbook)
- [MCP Server](#mcp-server)
- [Library](#library)
- [CLI Reference](#cli-reference)
- [JSON Document Format](#json-document-format)
- [Library API](#library-api)
- [Documentation](#documentation)
- [Architecture](#architecture)
- [How It Works](#how-it-works)
- [Performance](#performance)
- [Building from Source](#building-from-source)
- [Credits](#credits)
- [License](#license)

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

# Live AI subtitles while watching (NEW in v4.0!)
consoleimage movie.mp4 --subs whisper
```

That's it! Colors and animation are enabled by default. **Braille mode is now the default** for maximum detail.

## Zero Setup - Everything Downloads Automatically

ConsoleImage requires **zero manual setup** for common tasks. Dependencies are downloaded automatically on first use:

| Component | When Downloaded | Size | Cache Location |
|-----------|----------------|------|----------------|
| **FFmpeg** | First video file playback | ~25MB | `~/.local/share/consoleimage/ffmpeg/` |
| **yt-dlp** | First YouTube URL | ~10MB | `~/.local/share/consoleimage/ytdlp/` |
| **Whisper Runtime** | First `--subs whisper` use | ~15MB | `~/.local/share/consoleimage/whisper/runtimes/` |
| **Whisper Models** | First transcription | 75MB-3GB | `~/.local/share/consoleimage/whisper/` |

### How Auto-Download Works

1. **FFmpeg & yt-dlp**: Downloads platform-specific binary, extracts to cache, adds to PATH
2. **Whisper Runtime**: Downloads NuGet package, extracts native libraries for your platform
3. **Whisper Models**: Downloads from Hugging Face, caches locally for reuse

### Skipping Downloads

Use `-y` / `--yes` to auto-confirm all downloads without prompts:

```bash
consoleimage movie.mp4 --subs whisper -y  # Auto-downloads everything needed
```

### Manual Installation (If Needed)

If auto-download fails (corporate firewall, etc.), install manually:

```bash
# FFmpeg
winget install ffmpeg              # Windows
brew install ffmpeg                # macOS
apt install ffmpeg                 # Linux

# yt-dlp
pip install yt-dlp
winget install yt-dlp              # Windows

# Whisper Runtime (via NuGet)
# Download: https://www.nuget.org/packages/Whisper.net.Runtime
# Extract and copy native libs to the cache location above
```

### Cache Cleanup

All downloads are cached in `~/.local/share/consoleimage/` (or `%LOCALAPPDATA%\consoleimage\` on Windows).

To clear cached downloads:
```bash
rm -rf ~/.local/share/consoleimage/  # Linux/macOS
rd /s "%LOCALAPPDATA%\consoleimage"  # Windows
```

Subtitle cache (auto-generated .vtt files) is stored in temp and auto-cleaned after 7 days.

## CLI Guide

### Live Transcription

Generate subtitles automatically while watching videos using local Whisper AI:

```bash
# Auto-generate subtitles while playing (streams in real-time!)
consoleimage movie.mp4 --subs whisper

# Works with YouTube too - skip ahead with --ss
consoleimage "https://youtu.be/dQw4w9WgXcQ" --subs whisper
consoleimage "https://youtu.be/dQw4w9WgXcQ" --subs whisper --ss 3600  # Start at 1 hour

# Use different model sizes for quality vs speed
consoleimage movie.mp4 --subs whisper --whisper-model tiny   # Fastest
consoleimage movie.mp4 --subs whisper --whisper-model small  # Better accuracy

# Subtitles are cached automatically - replay without re-transcribing
consoleimage movie.mp4 --subs whisper  # First time: transcribes
consoleimage movie.mp4 --subs whisper  # Second time: uses cached .vtt file
```

**Transcript-only mode** (no video - for piping to other tools):

```bash
# Stream transcript to stdout
consoleimage movie.mp4 --transcript
consoleimage https://youtu.be/VIDEO_ID --transcript

# Save to file
consoleimage transcribe movie.mp4 -o output.vtt
```

**How it works:**
- Audio is extracted and transcribed in 15-second chunks
- Transcription runs ahead of playback in the background
- If playback catches up, it briefly pauses showing "⏳ Transcribing..."
- Subtitles are auto-saved as `.vtt` files for instant replay

**Whisper models auto-download** on first use (~75MB-3GB depending on model).
> - YouTube videos with `--ss` may have audio extraction issues at certain positions. Try a different start time if transcription fails.

### Choosing a Render Mode

| Mode | Command | Resolution | Best For |
|------|---------|------------|----------|
| **Braille** | `consoleimage photo.jpg` | 8x (2×4 dots/cell) | **DEFAULT** - Maximum detail |
| **ASCII** | `consoleimage photo.jpg -a` | Standard | Widest compatibility |
| **Blocks** | `consoleimage photo.jpg -b` | 2x vertical | Photos, high fidelity |
| **Matrix** | `consoleimage photo.jpg -M` | Digital rain | Special effects |

**Braille mode** is now the default because it packs 8 dots into each character cell, giving you the highest resolution output:
- Maximum detail in limited terminal space
- Smallest `.cidz` document files (fewer characters = smaller files)
- Crisp rendering of photos, videos, or animations
- Output that needs to fit in narrow terminal windows

```bash
# Default braille at 80 chars wide
consoleimage photo.jpg -w 80

# Classic ASCII mode (v2.x default)
consoleimage photo.jpg -a

# Save video as compact braille document
consoleimage movie.mp4 -o movie.cidz
```

### Common Options

```bash
# Set output width
consoleimage photo.jpg -w 100

# Control animation speed and loops
consoleimage animation.gif --speed 1.5 --loop 3

# Video: start at 30s, play for 10s
consoleimage movie.mp4 --ss 30 -t 10

# Save as animated GIF
consoleimage animation.gif -o output.gif

# Save as compressed document (play back without source file)
consoleimage movie.mp4 -o movie.cidz
```

### Render Mode Comparison

| Braille (DEFAULT) | ASCII | ColorBlocks |
|-------------------|-------|-------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_braille.gif" width="250" alt="Braille Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_ascii.gif" width="250" alt="ASCII Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_blocks.gif" width="250" alt="ColorBlocks Mode"> |
| 2×4 dot patterns (8x resolution) | Shape-matched characters | Unicode half-blocks (▀▄) |

### Monochrome Braille: Compact & Fast

Monochrome braille (1-bit) is perfect for quick previews, SSH connections, and maximum detail in minimal file size:

| Monochrome Braille | Full Color Braille |
|--------------------|-------------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/boingball_mono.gif" width="250" alt="Monochrome"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/boingball_braille.gif" width="250" alt="Color Braille"> |
| ~265 KB (3-5x smaller) | ~884 KB |

```bash
consoleimage animation.gif --mono -w 120  # Fast, compact, high detail
```

### Landscape Example

| Braille | ASCII | ColorBlocks |
|---------|-------|-------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_braille.gif" width="250" alt="Landscape Braille"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_ascii.gif" width="250" alt="Landscape ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_blocks.gif" width="250" alt="Landscape Blocks"> |

```bash
consoleimage movie.mp4           # Braille by default
consoleimage movie.mp4 -a -w 120 # ASCII mode, wider
```

**Video Playback Controls:**
| Key | Action |
|-----|--------|
| `Space` | Pause/Resume playback |
| `Q` / `Esc` | Quit playback |

### Matrix Mode

| Classic Green | Full Color |
|---------------|------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_matrix.gif" width="250" alt="Matrix Classic"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_matrix_fullcolor.gif" width="250" alt="Matrix Full Color"> |

```bash
consoleimage photo.jpg --matrix              # Classic green
consoleimage photo.jpg --matrix --matrix-fullcolor  # Source colors
```

### Slideshow Mode (v3.1)

Browse directories of images with keyboard controls:

```bash
# Browse a folder of images
consoleimage ./photos

# Use glob patterns
consoleimage "*.jpg"
consoleimage "vacation/**/*.png"

# Manual advance only (no auto-advance)
consoleimage ./photos --slide-delay 0

# Auto-advance every 5 seconds
consoleimage ./photos --slide-delay 5000

# Shuffle order
consoleimage ./photos --shuffle
```

**Keyboard Controls:**
| Key | Action |
|-----|--------|
| `Space` | Pause/Resume auto-advance |
| `→` / `N` | Next image |
| `←` / `P` | Previous image |
| `R` | Toggle shuffle |
| `Q` / `Esc` | Quit |

### YouTube Support (v3.1)

Play YouTube videos directly in your terminal:

```bash
# Play a YouTube video
consoleimage "https://youtu.be/dQw4w9WgXcQ"
consoleimage "https://www.youtube.com/watch?v=dQw4w9WgXcQ"

# With render options
consoleimage "https://youtu.be/VIDEO_ID" --blocks -w 100

# Save as GIF
consoleimage "https://youtu.be/VIDEO_ID" -o output.gif -w 60

# Save as document
consoleimage "https://youtu.be/VIDEO_ID" -o video.cidz
```

**Requirements:**
- **yt-dlp** - Auto-downloads (~10MB) on first YouTube use, or install manually via `pip install yt-dlp`
- **FFmpeg** - Auto-downloads on first video use

Use `--ytdlp-path` to specify a custom yt-dlp location.

### Subtitles & Transcription

Display subtitles during video playback or generate them automatically with Whisper AI.

#### Subtitle Sources

| Source | Command | Description |
|--------|---------|-------------|
| **File** | `--subs movie.srt` | Load from SRT/VTT file |
| **YouTube** | `--subs auto` | Auto-download from YouTube |
| **Live Whisper** | `--subs whisper` | Real-time transcription during playback |

#### Live Transcription Mode (`--subs whisper`)

Stream subtitles in real-time while watching - no waiting for full transcription:

```bash
# Live transcription during playback
consoleimage movie.mp4 --subs whisper

# With start time (transcription starts from that position)
consoleimage "https://youtu.be/VIDEO_ID" --subs whisper --ss 3600

# Choose model size (tiny/base/small/medium/large)
consoleimage movie.mp4 --subs whisper --whisper-model small

# Subtitles are cached - second play uses saved .vtt file
consoleimage movie.mp4 --subs whisper  # Uses movie.vtt if it exists
```

**Features:**
- Audio transcribed in 15-second chunks, buffered 30s ahead
- Background transcription continues during playback
- Brief pause with "⏳ Transcribing..." if playback catches up
- Auto-saves to `.vtt` file for instant replay

#### Transcript-Only Mode (No Video)

Generate subtitles without video rendering - perfect for piping to other tools:

```bash
# Stream transcript to stdout (for piping)
consoleimage movie.mp4 --transcript
consoleimage https://youtu.be/VIDEO_ID --transcript

# Save VTT file using transcribe subcommand
consoleimage transcribe movie.mp4 -o movie.vtt
consoleimage transcribe https://youtu.be/VIDEO_ID -o output.vtt

# Stream to stdout AND save file
consoleimage transcribe movie.mp4 -o movie.vtt --stream

# Quiet mode (only output, no progress)
consoleimage transcribe movie.mp4 --stream --quiet
```

**Output format:**
```
[00:00:01.500 --> 00:00:04.200] Hello, welcome to the video.
[00:00:04.500 --> 00:00:07.800] Today we'll be discussing...
```

#### Playing with External Subtitles

```bash
# Load subtitles from SRT/VTT file
consoleimage movie.mp4 --subs movie.srt
consoleimage movie.mp4 --subs subtitles.vtt

# Auto-download YouTube subtitles
consoleimage "https://youtu.be/VIDEO_ID" --subs auto
consoleimage "https://youtu.be/VIDEO_ID" --subs auto --sub-lang es  # Spanish

# Disable subtitles
consoleimage movie.mp4 --subs off
```

#### Transcribe Subcommand

Generate VTT/SRT subtitle files from any video or audio:

```bash
# Generate WebVTT subtitles (default)
consoleimage transcribe movie.mp4 -o movie.vtt

# Generate SRT format
consoleimage transcribe movie.mp4 -o movie.srt

# Use a larger model for better accuracy
consoleimage transcribe movie.mp4 --model small -o movie.vtt
consoleimage transcribe movie.mp4 --model medium -o movie.vtt

# Specify language (or 'auto' for detection)
consoleimage transcribe movie.mp4 --lang ja -o movie.vtt  # Japanese
consoleimage transcribe movie.mp4 --lang auto -o movie.vtt

# Use a hosted Whisper API
consoleimage transcribe movie.mp4 --whisper-url https://api.example.com/transcribe
```

**Whisper Models:**

| Model | Size | Speed | Accuracy | Best For |
|-------|------|-------|----------|----------|
| `tiny` | 75MB | Fastest | Good | Quick previews |
| `base` | 142MB | Fast | Better | **Default** |
| `small` | 466MB | Medium | Great | General use |
| `medium` | 1.5GB | Slow | Excellent | Professional |
| `large` | 3GB | Slowest | Best | Maximum accuracy |

Models are automatically downloaded on first use (~30s-5min depending on size).

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
- **Atkinson dithering**: High-contrast error diffusion for crisp braille output
- **Adaptive thresholding**: Otsu's method for optimal braille binarization
- **Edge-direction characters**: Uses directional chars (/ \ | -) based on detected edges
- **AOT compatible**: Works with Native AOT compilation
- **Cross-platform**: Windows, Linux, macOS (x64 and ARM64)

<details>
<summary><strong>What's New in v3.0</strong></summary>

- **Braille is now the default** - Maximum resolution out of the box
- **`-a, --ascii` option** - Use for classic ASCII mode (previous default)
- **Video width defaults to 50** for braille (CPU intensive)
- **Easter egg** - Run with no arguments for a surprise!
- **MPEG-4 and AV1 codec support** - Hardware acceleration fallback

See [CHANGELOG.md](CHANGELOG.md) for full history.
</details>

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

#### Quick Install (Command Line)

**Windows (PowerShell):**
```powershell
# Download and extract to user bin folder
$version = "4.0.0"  # Check releases for latest
Invoke-WebRequest -Uri "https://github.com/scottgal/mostlylucid.consoleimage/releases/download/v$version/consoleimage-win-x64.zip" -OutFile "$env:TEMP\consoleimage.zip"
Expand-Archive -Path "$env:TEMP\consoleimage.zip" -DestinationPath "$env:LOCALAPPDATA\consoleimage" -Force
# Add to PATH (run once)
$env:PATH += ";$env:LOCALAPPDATA\consoleimage"
[Environment]::SetEnvironmentVariable("PATH", $env:PATH, "User")
```

**Linux x64:**
```bash
# Download and install to /usr/local/bin
VERSION="4.0.0"  # Check releases for latest
curl -L "https://github.com/scottgal/mostlylucid.consoleimage/releases/download/v${VERSION}/consoleimage-linux-x64.tar.gz" | sudo tar -xz -C /usr/local/bin
sudo chmod +x /usr/local/bin/consoleimage
```

**Linux ARM64 (Raspberry Pi, etc.):**
```bash
VERSION="4.0.0"
curl -L "https://github.com/scottgal/mostlylucid.consoleimage/releases/download/v${VERSION}/consoleimage-linux-arm64.tar.gz" | sudo tar -xz -C /usr/local/bin
sudo chmod +x /usr/local/bin/consoleimage
```

**macOS (Apple Silicon):**
```bash
VERSION="4.0.0"
curl -L "https://github.com/scottgal/mostlylucid.consoleimage/releases/download/v${VERSION}/consoleimage-osx-arm64.tar.gz" | tar -xz -C /usr/local/bin
chmod +x /usr/local/bin/consoleimage
# If blocked by Gatekeeper, run: xattr -d com.apple.quarantine /usr/local/bin/consoleimage
```

**Verify installation:**
```bash
consoleimage --version
```

## Requirements

- **.NET 10** runtime (or use standalone binaries)
- **Terminal with ANSI support** (Windows Terminal, iTerm2, any modern terminal)
- **24-bit color** recommended for `--mode blocks` and `--mode braille`
- **Unicode font** for braille mode (most terminals include this)
- **FFmpeg** - Only required for video files (auto-downloads on first video use)
  - Images, GIFs, and cidz/json documents work without FFmpeg
  - Manual install: `winget install FFmpeg` (Windows), `brew install ffmpeg` (macOS), `apt install ffmpeg` (Linux)

## CLI Cookbook

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
consoleimage movie.mp4                    # Play video (braille default)
consoleimage movie.mkv --blocks -w 120    # Color blocks mode
consoleimage movie.mp4 --ss 60 -t 30      # Start at 60s, play 30s

# === SUBTITLES ===
consoleimage movie.mp4 --subs movie.srt   # Play with SRT/VTT subtitles
consoleimage movie.mp4 --subs auto        # Auto-download YouTube subs
consoleimage movie.mp4 --subs whisper     # Generate with local Whisper AI

# === TRANSCRIPTION (Generate subtitles) ===
consoleimage transcribe movie.mp4 -o movie.vtt           # Generate VTT
consoleimage transcribe movie.mp4 --model small --diarize  # Better quality

# === SAVE & PLAYBACK ===
consoleimage animation.gif -o output.cidz # Save compressed document
consoleimage movie.mp4 -o movie.cidz      # Save video as document
consoleimage output.cidz                  # Play saved document
consoleimage movie.cidz --speed 2.0       # Playback with options
consoleimage movie.cidz -o movie.gif      # Convert document to GIF

# === RAW FRAME EXTRACTION (no ASCII, just video frames) ===
consoleimage movie.mp4 --raw -o frames.gif                    # Extract to GIF
consoleimage movie.mp4 --raw -o frames.webp -q 90             # Extract to WebP
consoleimage movie.mp4 --raw -o clip.mp4 --ss 30 -t 5         # Re-encode video clip
consoleimage movie.mp4 --raw --smart-keyframes -o scenes.gif  # Scene detection
consoleimage movie.mp4 --raw -o frame.png --gif-frames 10     # Image sequence

# === CALIBRATION ===
consoleimage --calibrate --aspect-ratio 0.5 --save
```

## MCP Server

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
| `render_video` | Render video to animated GIF (braille/ASCII/blocks) |
| `extract_frames` | Extract raw video frames to GIF (no ASCII, just frames) |
| `get_image_info` | Get detailed image metadata (format, dimensions, EXIF) |
| `get_gif_info` | Get GIF metadata (dimensions, frame count) |
| `get_video_info` | Get video file info via FFmpeg |
| `check_youtube_url` | Check if URL is a YouTube video |
| `get_youtube_stream` | Extract stream URL from YouTube |
| `list_render_modes` | List available render modes with descriptions |
| `list_matrix_presets` | List Matrix color presets |
| `compare_render_modes` | Render same image in all modes for comparison |

See [ConsoleImage.Mcp/README.md](ConsoleImage.Mcp/README.md) for full documentation.

## Library

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

## CLI Reference

### Render Modes

| Mode            | CLI Option                        | Description                                     | Best For                          |
|-----------------|-----------------------------------|-------------------------------------------------|-----------------------------------|
| **Braille**     | `--mode braille` or `-B` (default)| Braille patterns (2×4 dots per cell)            | **DEFAULT** - Maximum resolution  |
| **ASCII**       | `--mode ascii` or `-a`            | Shape-matched ASCII characters with ANSI colors | Widest compatibility              |
| **ColorBlocks** | `--mode blocks` or `-b`           | Unicode half-blocks (▀▄) with 24-bit color      | High fidelity, photos             |
| **iTerm2**      | `--mode iterm2`                   | Native inline image protocol                    | iTerm2, WezTerm                   |
| **Kitty**       | `--mode kitty`                    | Native graphics protocol                        | Kitty terminal                    |
| **Sixel**       | `--mode sixel`                    | DEC Sixel graphics                              | xterm, mlterm, foot               |

**Note:** Braille mode is now the default (v3.0+). Protocol modes (iTerm2, Kitty, Sixel) display true images in supported terminals. Use `--mode list` to see all
available modes.

### CLI Usage

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
| `-S, --status`        | Show status line with progress, timing, file info         | OFF            |
| `-r, --framerate`     | Fixed framerate in FPS (overrides GIF timing)             | GIF timing     |
| `-f, --frame-sample`  | Frame sampling rate (skip frames)                         | 1              |
| `--dejitter`          | Enable temporal stability to reduce color flickering      | OFF            |
| `--color-threshold`   | Color similarity threshold for dejitter (0-255)           | 15             |
| `-e, --edge`          | Enable edge detection                                     | OFF            |
| `--bg-threshold`      | Light background threshold (0.0-1.0)                      | Auto           |
| `--dark-bg-threshold` | Dark background threshold (0.0-1.0)                       | Auto           |
| `--auto-bg`           | Auto-detect background                                    | ON             |
| `-a, --ascii`         | Use classic ASCII characters                              | OFF            |
| `-b, --blocks`        | Use colored Unicode half-blocks                           | OFF            |
| `-B, --braille`       | Use braille characters (2×4 dots/cell)                    | **DEFAULT**    |
| `--no-alt-screen`     | Keep animation in scrollback                              | Alt screen ON  |
| `--no-parallel`       | Disable parallel processing                               | Parallel ON    |
| `-a, --aspect-ratio`  | Character aspect ratio (width/height)                     | 0.5            |
| `--no-dither`         | Disable Floyd-Steinberg dithering                         | Dither ON      |
| `--no-edge-chars`     | Disable edge-direction characters                         | Edge chars ON  |
| `-j, --json`          | Output as JSON (for LLM tool calls)                       | OFF            |
| `--dark-cutoff`       | Dark terminal: skip colors below brightness (0.0-1.0)     | 0.1            |
| `--light-cutoff`      | Light terminal: skip colors above brightness (0.0-1.0)    | 0.9            |
| `-m, --mode`          | Render mode: ascii, blocks, braille, iterm2, kitty, sixel | braille        |
| `--mode list`         | List all available render modes                           | -              |
| `--gif-scale`         | GIF output scale factor (0.25-2.0)                        | 1.0            |
| `--gif-colors`        | GIF palette size (16-256)                                 | 64             |
| `--gif-fps`           | GIF framerate                                             | 10             |
| `--gif-font-size`     | GIF font size in pixels                                   | 10             |
| `--gif-length`        | Max GIF length in seconds                                 | -              |
| `--gif-frames`        | Max GIF frames                                            | -              |
| **Slideshow**         |                                                           |                |
| `--slide-delay`       | Auto-advance delay in ms (0 = manual only)                | 3000           |
| `--shuffle`           | Randomize slideshow order                                 | OFF            |
| `--hide-info`         | Hide file info header in slideshow                        | OFF            |
| **YouTube**           |                                                           |                |
| `--ytdlp-path`        | Path to yt-dlp executable                                 | Auto-detect    |

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

### Document API (Library)

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

### Character Set Presets

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
| **Braille Rendering**              | How the 8x resolution braille mode works       | [docs/BRAILLE-RENDERING.md](docs/BRAILLE-RENDERING.md)                 |
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
