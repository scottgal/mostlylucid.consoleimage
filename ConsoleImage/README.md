# ConsoleImage CLI

**Unified command-line tool** for rendering images, GIFs, videos, and playing cidz/json documents as ASCII art.

**Part of the [ConsoleImage](https://github.com/scottgal/mostlylucid.consoleimage) project.**

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

## Features

- **Images** - JPG, PNG, BMP, WebP, TIFF with shape-matched ASCII rendering
- **Animated GIFs** - Smooth flicker-free playback with DECSET 2026
- **Videos** - Any FFmpeg-supported format (MP4, MKV, AVI, WebM, etc.)
- **Document playback** - Play saved .cidz/.json documents without original source
- **Four render modes** - ASCII, ColorBlocks (▀▄), Braille, Matrix rain
- **GIF output** - Convert any input to animated GIF
- **Compressed documents** - Save rendered output as .cidz for sharing/playback

### Example Output

| ASCII Mode | ColorBlocks Mode | Braille Mode |
|------------|------------------|--------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_ascii.gif" width="200" alt="ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_blocks.gif" width="200" alt="Blocks"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_braille.gif" width="200" alt="Braille"> |

## Installation

Download from [GitHub Releases](https://github.com/scottgal/mostlylucid.consoleimage/releases):

| Platform | Download |
|----------|----------|
| Windows x64 | `consoleimage-win-x64.zip` |
| Linux x64 | `consoleimage-linux-x64.tar.gz` |
| Linux ARM64 | `consoleimage-linux-arm64.tar.gz` |
| macOS ARM64 | `consoleimage-osx-arm64.tar.gz` |

## Requirements

- **Terminal with ANSI support** - Windows Terminal, iTerm2, any modern terminal
- **FFmpeg** - Only needed for video files (auto-downloads on first video use)
  - Images, GIFs, and cidz documents work without FFmpeg

## Quick Start

```bash
# === IMAGES ===
consoleimage photo.jpg                    # Render image to terminal
consoleimage photo.png --blocks           # High-fidelity color blocks (▀▄)
consoleimage photo.png --braille          # Ultra-high resolution braille
consoleimage photo.png --matrix           # Matrix digital rain effect
consoleimage https://example.com/img.jpg  # Load from URL

# === ANIMATED GIFs ===
consoleimage animation.gif                # Play animation
consoleimage animation.gif --speed 1.5    # Speed up playback
consoleimage animation.gif --loop 3       # Play 3 times then stop

# === VIDEOS (FFmpeg auto-downloads on first use) ===
consoleimage movie.mp4                    # Play video as ASCII
consoleimage movie.mkv --blocks -w 120    # Color blocks, 120 chars wide
consoleimage movie.mp4 --ss 60 -t 30      # Start at 60s, play 30 seconds

# === SAVE TO DOCUMENT ===
consoleimage animation.gif -o output.cidz # Save as compressed document
consoleimage movie.mp4 -o movie.cidz -b   # Save video in blocks mode

# === PLAY DOCUMENTS ===
consoleimage output.cidz                  # Play saved document
consoleimage movie.cidz --speed 2.0       # Playback with 2x speed

# === CONVERT TO GIF ===
consoleimage animation.gif -o output.gif  # GIF to ASCII GIF
consoleimage movie.mp4 -o movie.gif       # Video to ASCII GIF
consoleimage movie.cidz -o movie.gif      # Document to GIF
```

## Render Modes

| Mode | CLI Option | Description | Best For |
|------|------------|-------------|----------|
| **ASCII** | (default) | Shape-matched ASCII characters | General use, widest compatibility |
| **ColorBlocks** | `-b, --blocks` | Unicode half-blocks (▀▄) | High fidelity, photos |
| **Braille** | `-B, --braille` | Braille patterns (2×4 dots/cell) | Maximum resolution |
| **Matrix** | `-M, --matrix` | Digital rain effect | Stylized output |

## CLI Options

### Input/Output

| Option | Description | Default |
|--------|-------------|---------|
| `input` | Image, GIF, video, or cidz/json document | Required |
| `-o, --output` | Output file (.gif, .cidz, .json) | Console |

### Dimensions

| Option | Description | Default |
|--------|-------------|---------|
| `-w, --width` | Output width in characters | Auto (console width) |
| `-h, --height` | Output height in characters | Auto |
| `--max-width` | Maximum output width | Console width |
| `--max-height` | Maximum output height | Console height |

### Render Mode

| Option | Description |
|--------|-------------|
| `-b, --blocks` | Use colored Unicode half-blocks |
| `-B, --braille` | Use braille characters |
| `-M, --matrix` | Use Matrix digital rain effect |
| `--no-color` | Disable color output |

### Playback Control

| Option | Description | Default |
|--------|-------------|---------|
| `-s, --speed` | Playback speed multiplier | 1.0 |
| `-l, --loop` | Loop count (0 = infinite) | 0 |
| `--no-animate` | Show first frame only | OFF |

### Video Options (requires FFmpeg)

| Option | Description | Default |
|--------|-------------|---------|
| `--start, --ss` | Start time in seconds | 0 |
| `-t, --duration` | Duration to play | Full video |
| `--end` | End time in seconds | Video end |
| `-r, --fps` | Target framerate | Video native |
| `--frame-step` | Skip frames (2 = every 2nd) | 1 |
| `--no-hw` | Disable hardware acceleration | HW enabled |
| `--ffmpeg-path` | Path to FFmpeg installation | Auto-download |

### GIF Output Options

| Option | Description | Default |
|--------|-------------|---------|
| `--gif-fps` | GIF framerate | 10 |
| `--gif-colors` | Palette size (16-256) | 64 |
| `--gif-font-size` | Font size in pixels | 10 |
| `--gif-scale` | Scale factor | 1.0 |

### Image Adjustment

| Option | Description | Default |
|--------|-------------|---------|
| `--contrast` | Contrast power (1.0 = none) | 2.5 |
| `--gamma` | Gamma correction (<1 brighter) | 0.85 |
| `-a, --aspect-ratio` | Character aspect ratio | 0.5 |
| `--colors` | Color palette size (4, 16, 256) | Full |

### Calibration

```bash
# Show calibration pattern (should appear as circle)
consoleimage --calibrate

# Adjust until circle looks round
consoleimage --calibrate --aspect-ratio 0.48

# Save calibration for this mode
consoleimage --calibrate --aspect-ratio 0.48 --save

# Calibrate different modes separately
consoleimage --calibrate --blocks --aspect-ratio 0.5 --save
consoleimage --calibrate --braille --aspect-ratio 0.52 --save
```

## Document Formats

### Compressed (.cidz) - Recommended

- GZip compressed with delta encoding
- ~7:1 compression ratio vs raw JSON
- Best for animations and videos
- Supports playback speed override

```bash
consoleimage animation.gif -o output.cidz
consoleimage output.cidz --speed 1.5
```

### JSON (.json)

- Uncompressed, human-readable
- Useful for debugging or small outputs

```bash
consoleimage image.png -o output.json
```

### Streaming NDJSON (.ndjson)

- JSON Lines format for long videos
- Auto-finalizes on Ctrl+C
- Frames written incrementally

```bash
consoleimage long_movie.mp4 -o movie.ndjson
```

## FFmpeg - Zero Setup Required

FFmpeg is **automatically downloaded** on first video use:

```
$ consoleimage video.mp4
FFmpeg not found. Downloading...
Cache location: C:\Users\you\AppData\Local\consoleimage\ffmpeg

Downloading FFmpeg...                              75%
```

### Custom FFmpeg Path

```bash
consoleimage video.mp4 --ffmpeg-path "C:\tools\ffmpeg\bin"
```

### Manual Installation (Optional)

```bash
# Windows
winget install FFmpeg

# macOS
brew install ffmpeg

# Linux
sudo apt install ffmpeg
```

## Examples

### Create ASCII Art GIF from Video

```bash
# Basic conversion
consoleimage movie.mp4 -o output.gif -w 80

# Specific section with color blocks
consoleimage movie.mp4 -o clip.gif --ss 30 -t 10 --blocks

# Braille mode, slower playback
consoleimage movie.mp4 -o output.gif --braille --speed 0.5
```

### Archive Video as Document

```bash
# Save video as compressed document
consoleimage movie.mp4 -o movie.cidz --blocks -w 100

# Play it back anytime without the original video
consoleimage movie.cidz

# Convert to GIF later
consoleimage movie.cidz -o movie.gif
```

### Matrix Rain Effect

```bash
# Classic green matrix
consoleimage photo.png --matrix

# Red matrix
consoleimage photo.png --matrix --matrix-color red

# Full color from source image
consoleimage photo.png --matrix --matrix-fullcolor
```

## Performance Tips

1. **Reduce resolution** - Use `-w 80` for faster rendering
2. **Skip frames** - Use `--frame-step 2` for videos on slow systems
3. **Use ColorBlocks** - Faster than ASCII for similar quality
4. **Hardware acceleration** - Enabled by default for videos

## Troubleshooting

### Choppy video playback
- Try `--no-hw` to disable hardware acceleration
- Reduce dimensions with `-w 80 -h 40`
- Skip frames with `--frame-step 2`

### Colors look wrong
- Ensure terminal supports 24-bit color
- Try `--blocks` mode for best color support

### Video stretched/squashed
- Run calibration: `consoleimage --calibrate --save`

### cidz file won't load
- Ensure it was created with a recent version
- Try uncompressed JSON: `-o output.json`

## License

This is free and unencumbered software released into the public domain. See [UNLICENSE](../UNLICENSE) for details.
