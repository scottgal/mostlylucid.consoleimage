# CLI Guide

Complete reference for the `consoleimage` command-line tool.

**See also:** [README](../README.md) | [Library API](LIBRARY-API.md) | [JSON Format](JSON-FORMAT.md) | [How It Works](HOW-IT-WORKS.md)

## Table of Contents

- [Render Modes](#render-modes)
- [Common Options](#common-options)
- [Matrix Mode](#matrix-mode)
- [Monochrome Braille](#monochrome-braille)
- [Slideshow Mode](#slideshow-mode)
- [YouTube Support](#youtube-support)
- [Subtitles & Transcription](#subtitles--transcription)
- [Markdown & SVG Export](#markdown--svg-export)
- [JSON Document Format](#json-document-format)
- [Raw Frame Extraction](#raw-frame-extraction)
- [Calibration](#calibration)
- [CLI Cookbook](#cli-cookbook)
- [CLI Reference](#cli-reference)

---

## Render Modes

| Mode        | Command                     | Resolution         | Best For                     |
|-------------|-----------------------------|--------------------|------------------------------|
| **Braille** | `consoleimage photo.jpg`    | 8x (2x4 dots/cell) | **DEFAULT** - Maximum detail |
| **ASCII**   | `consoleimage photo.jpg -a` | Standard           | Widest compatibility         |
| **Blocks**  | `consoleimage photo.jpg -b` | 2x vertical        | Photos, high fidelity        |
| **Matrix**  | `consoleimage photo.jpg -M` | Digital rain       | Special effects              |

**Braille mode** is the default because it packs 8 dots into each character cell, giving you the highest resolution output:

- Maximum detail in limited terminal space
- Smallest `.cidz` document files (fewer characters = smaller files)
- Crisp rendering of photos, videos, or animations

```bash
# Default braille at 80 chars wide
consoleimage photo.jpg -w 80

# Classic ASCII mode (v2.x default)
consoleimage photo.jpg -a

# Save video as compact braille document
consoleimage movie.mp4 -o movie.cidz
```

### Render Mode Comparison

| Braille (DEFAULT) | ASCII | ColorBlocks |
|---|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_braille.gif" width="250" alt="Braille Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_ascii.gif" width="250" alt="ASCII Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/wiggum_blocks.gif" width="250" alt="ColorBlocks Mode"> |
| 2x4 dot patterns (8x resolution) | Shape-matched characters | Unicode half-blocks |

### Landscape Example

| Braille | ASCII | ColorBlocks |
|---|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_braille.gif" width="250" alt="Landscape Braille"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_ascii.gif" width="250" alt="Landscape ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_blocks.gif" width="250" alt="Landscape Blocks"> |

```bash
consoleimage movie.mp4           # Braille by default
consoleimage movie.mp4 -a -w 120 # ASCII mode, wider
```

### All Render Modes

| Mode            | CLI Option                         | Description                                     | Best For                         |
|-----------------|------------------------------------|-------------------------------------------------|----------------------------------|
| **Braille**     | `--mode braille` or `-B` (default) | Braille patterns (2x4 dots per cell)            | **DEFAULT** - Maximum resolution |
| **ASCII**       | `--mode ascii` or `-a`             | Shape-matched ASCII characters with ANSI colors | Widest compatibility             |
| **ColorBlocks** | `--mode blocks` or `-b`            | Unicode half-blocks with 24-bit color           | High fidelity, photos            |
| **iTerm2**      | `--mode iterm2`                    | Native inline image protocol                    | iTerm2, WezTerm                  |
| **Kitty**       | `--mode kitty`                     | Native graphics protocol                        | Kitty terminal                   |
| **Sixel**       | `--mode sixel`                     | DEC Sixel graphics                              | xterm, mlterm, foot              |

Protocol modes (iTerm2, Kitty, Sixel) display true images in supported terminals. Use `--mode list` to see all available modes.

---

## Common Options

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

**Video Playback Controls:**

| Key | Action |
|-----|--------|
| `Space` | Pause/Resume playback |
| `Q` / `Esc` | Quit playback |

---

## Matrix Mode

| Classic Green | Full Color |
|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_matrix.gif" width="250" alt="Matrix Classic"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/landscape_matrix_fullcolor.gif" width="250" alt="Matrix Full Color"> |

```bash
consoleimage photo.jpg --matrix                        # Classic green
consoleimage photo.jpg --matrix --matrix-fullcolor     # Source colors
consoleimage photo.jpg --matrix --matrix-color red     # Custom color
consoleimage photo.jpg --matrix --matrix-color "#FF6600"  # Hex color
consoleimage --matrix                                  # Pure rain (no image)
```

Matrix mode always animates continuously, even on still images.

**Matrix Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--matrix-color` | Color: green, red, blue, amber, cyan, purple, or hex | green |
| `--matrix-fullcolor` | Use source image colors | OFF |
| `--matrix-density` | Rain density (0.1-2.0) | 0.5 |
| `--matrix-speed` | Rain speed multiplier (0.5-3.0) | 1.0 |

---

## Monochrome Braille

Monochrome braille (1-bit) is perfect for quick previews, SSH connections, and maximum detail in minimal file size:

| Monochrome Braille | Full Color Braille |
|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/boingball_mono.gif" width="250" alt="Monochrome"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/boingball_braille.gif" width="250" alt="Color Braille"> |
| ~265 KB (3-5x smaller) | ~884 KB |

```bash
consoleimage animation.gif --mono -w 120  # Fast, compact, high detail
```

---

## Slideshow Mode

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
| `->` / `N` | Next image |
| `<-` / `P` | Previous image |
| `R` | Toggle shuffle |
| `Q` / `Esc` | Quit |

**Slideshow Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--slide-delay` | Auto-advance delay in seconds (0 = manual only) | 3 |
| `--shuffle` | Randomize order | OFF |
| `-R, --recursive` | Include subdirectories | OFF |
| `--sort` | Sort by: name, date, size, random | date |
| `--hide-info` | Hide file info header | OFF |

---

## YouTube Support

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

**YouTube Authentication:**

YouTube may require authentication to prevent bot access. If you see "Sign in to confirm you're not a bot":

```bash
# Use cookies from your browser (recommended)
consoleimage "https://youtu.be/xyz" --cookies-from-browser chrome
consoleimage "https://youtu.be/xyz" --cookies-from-browser firefox

# Or use a cookies.txt file
consoleimage "https://youtu.be/xyz" --cookies cookies.txt
```

---

## Subtitles & Transcription

Display subtitles during video playback or generate them automatically with Whisper AI.

### Subtitle Sources

| Source | Command | Description |
|--------|---------|-------------|
| **Auto** | `--subs auto` | Embedded -> external file -> YouTube -> Whisper |
| **File** | `--subs movie.srt` | Load from SRT/VTT file |
| **Embedded** | `--subs auto` | Extract text subtitles from MKV/MP4 container |
| **YouTube** | `--subs yt` | Download YouTube captions |
| **Live Whisper** | `--subs whisper` | Real-time AI transcription (requires whisper variant) |

### Live Transcription (`--subs whisper`)

Generate subtitles automatically while watching videos using local Whisper AI. Requires the **`consoleimage-whisper`** download variant (includes bundled native Whisper libraries).

```bash
# Auto-generate subtitles while playing (streams in real-time!)
consoleimage movie.mp4 --subs whisper

# Works with YouTube too - skip ahead with --ss
consoleimage "https://youtu.be/dQw4w9WgXcQ" --subs whisper
consoleimage "https://youtu.be/dQw4w9WgXcQ" --subs whisper --ss 3600

# Use different model sizes for quality vs speed
consoleimage movie.mp4 --subs whisper --whisper-model tiny   # Fastest
consoleimage movie.mp4 --subs whisper --whisper-model small  # Better accuracy

# Subtitles are cached automatically - replay without re-transcribing
consoleimage movie.mp4 --subs whisper  # First time: transcribes
consoleimage movie.mp4 --subs whisper  # Second time: uses cached .vtt file
```

**How it works:**

- Audio is extracted and transcribed in 15-second chunks
- Transcription runs ahead of playback in the background
- If playback catches up, it briefly pauses showing "hourglass Transcribing..."
- Subtitles are auto-saved as `.vtt` files for instant replay

**Whisper Models:**

| Model | Size | Speed | Accuracy | Best For |
|-------|------|-------|----------|----------|
| `tiny` | 75MB | Fastest | Good | Quick previews |
| `base` | 142MB | Fast | Better | **Default** |
| `small` | 466MB | Medium | Great | General use |
| `medium` | 1.5GB | Slow | Excellent | Professional |
| `large` | 3GB | Slowest | Best | Maximum accuracy |

Models are automatically downloaded on first use.

### Transcript-Only Mode (No Video)

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

### Playing with External Subtitles

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

### Transcribe Subcommand

Generate VTT/SRT subtitle files from any video or audio:

```bash
# Generate WebVTT subtitles (default)
consoleimage transcribe movie.mp4 -o movie.vtt

# Generate SRT format
consoleimage transcribe movie.mp4 -o movie.srt

# Use a larger model for better accuracy
consoleimage transcribe movie.mp4 --model small -o movie.vtt

# Specify language (or 'auto' for detection)
consoleimage transcribe movie.mp4 --lang ja -o movie.vtt  # Japanese

# Use a hosted Whisper API
consoleimage transcribe movie.mp4 --whisper-url https://api.example.com/transcribe
```

---

## Markdown & SVG Export

Export ASCII art to embeddable formats for documentation, READMEs, and wikis. The default format is SVG - full-color, scalable vector output that works in GitHub, GitLab, and most markdown renderers.

```bash
# Just add --md - auto-generates photo.svg with full color (simplest!)
consoleimage photo.jpg --md

# Specify a custom output path
consoleimage photo.jpg --md output.md

# Choose a different format
consoleimage photo.jpg --md --md-format plain    # Text in code block (universal)
consoleimage photo.jpg --md --md-format html     # HTML spans with CSS colors
consoleimage photo.jpg --md --md-format ansi     # Preserve ANSI codes
```

| Format | Colors | Compatibility | Use Case |
|--------|--------|---------------|----------|
| `svg` | Full | GitHub, GitLab, most | **Default** - embeddable colored vector art |
| `plain` | None | Universal | All markdown renderers |
| `html` | Full | Limited | Custom docs sites |
| `ansi` | Full | Terminals only | Terminal-rendered markdown |

> **Note:** Markdown and SVG exports are static (single frame). For animated output, use GIF export (`-o output.gif`).

---

## JSON Document Format

Save rendered ASCII art to self-contained JSON documents that can be played back without the original source file. See [JSON-FORMAT.md](JSON-FORMAT.md) for the full specification.

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
```

### Format Features

- **JSON-LD compatible** - Uses `@context` and `@type` for semantic structure
- **Self-contained** - All render settings preserved for reproducible output
- **Two formats**: Standard JSON (`.json`) and Streaming NDJSON (`.ndjson`)
- **Auto-detection** - `LoadAsync()` automatically detects which format
- **Stop anytime** - Streaming format auto-finalizes on Ctrl+C, always valid

### Embedding in Applications

Use the lightweight **[ConsoleImage.Player](../ConsoleImage.Player/README.md)** package to play `.cidz` documents without any dependencies on ImageSharp or FFmpeg. Perfect for animated startup logos:

| ASCII | ColorBlocks | Braille |
|---|---|---|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/moviebill_ascii.gif" width="150" alt="ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/moviebill_blocks.gif" width="150" alt="Blocks"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/moviebill_braille.gif" width="150" alt="Braille"> |

```csharp
// Play on startup (no ImageSharp, no FFmpeg - just JSON parsing)
var player = await ConsolePlayer.FromFileAsync("logo.cidz");
await player.PlayAsync(loopCount: 1);
```

See [ConsoleImage.Player/README.md](../ConsoleImage.Player/README.md) for the complete example.

---

## Raw Frame Extraction

Extract actual video frames (not ASCII art) as images or video clips:

```bash
consoleimage movie.mp4 --raw -o frames.gif                    # Extract to GIF
consoleimage movie.mp4 --raw -o frames.webp -q 90             # Extract to WebP
consoleimage movie.mp4 --raw -o clip.mp4 --ss 30 -t 5         # Re-encode video clip
consoleimage movie.mp4 --raw --smart-keyframes -o scenes.gif  # Scene detection
consoleimage movie.mp4 --raw -o frame.png --gif-frames 10     # Image sequence
```

---

## Calibration

Ensure circles render as circles (not ovals) by calibrating the character aspect ratio:

```bash
# Show calibration pattern (should be a circle)
consoleimage --calibrate

# Adjust aspect ratio until circle looks correct
consoleimage --calibrate --aspect-ratio 0.48

# Save calibration once correct
consoleimage --calibrate --aspect-ratio 0.48 --save

# Each mode has its own saved value
consoleimage --calibrate --braille --aspect-ratio 0.52 --save
```

---

## CLI Cookbook

```bash
# === IMAGES ===
consoleimage photo.jpg                    # Render to terminal (braille default)
consoleimage photo.png -a                 # Classic ASCII mode (--ascii)
consoleimage photo.png --blocks           # High-fidelity color blocks
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
consoleimage movie.cidz -o movie.gif      # Convert document to GIF

# === CALIBRATION ===
consoleimage --calibrate --aspect-ratio 0.5 --save
```

---

## CLI Reference

### Usage

```bash
consoleimage <input> [options]
consoleimage transcribe <input> [options]
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| **Input/Output** | | |
| `<input>` | Image, GIF, video, URL, directory, or saved document | - |
| `-o, --output` | Output: file, `gif:file.gif`, `json:file.json` | Console |
| `-j, --json` | Output as JSON (for LLM tool calls) | OFF |
| **Dimensions** | | |
| `-w, --width` | Output width in characters | Auto |
| `-h, --height` | Output height in characters | Auto |
| `--max-width` | Maximum output width | Console width |
| `--max-height` | Maximum output height | Console height |
| **Render Mode** | | |
| `-m, --mode` | Render mode: ascii, blocks, braille, iterm2, kitty, sixel | braille |
| `-a, --ascii` | Use classic ASCII characters | OFF |
| `-b, --blocks` | Use colored Unicode half-blocks | OFF |
| `-B, --braille` | Use braille characters (2x4 dots/cell) | **DEFAULT** |
| `-M, --matrix` | Matrix digital rain effect | OFF |
| `--monochrome, --mono` | Braille mode without color | OFF |
| **Playback** | | |
| `-s, --speed` | Animation speed multiplier | 1.0 |
| `-l, --loop` | Animation loop count (0 = infinite) | 0 |
| `-r, --framerate` | Fixed framerate in FPS | Source timing |
| `-f, --frame-sample` | Frame sampling: 1 (all), 2 (skip), s/smart | 1 |
| `--no-animate` | Show first frame only | Animate ON |
| `-S, --status` | Show status line with progress | OFF |
| `--dejitter` | Enable temporal stability | OFF |
| `--color-threshold` | Color similarity threshold for dejitter (0-255) | 15 |
| `--no-alt-screen` | Keep animation in scrollback | Alt screen ON |
| **Image Adjustment** | | |
| `--no-color` | Disable colored output | Color ON |
| `--no-invert` | Don't invert (for light backgrounds) | Invert ON |
| `--contrast` | Contrast power (1.0 = none) | 2.5 |
| `--gamma` | Gamma correction (< 1.0 brightens) | 0.85 |
| `-a, --aspect-ratio` | Character aspect ratio (width/height) | 0.5 |
| `--no-dither` | Disable Floyd-Steinberg dithering | Dither ON |
| `--no-edge-chars` | Disable edge-direction characters | Edge chars ON |
| `-e, --edge` | Enable edge detection | OFF |
| `--bg-threshold` | Light background threshold (0.0-1.0) | Auto |
| `--dark-bg-threshold` | Dark background threshold (0.0-1.0) | Auto |
| `--dark-cutoff` | Skip colors below brightness (0.0-1.0) | 0.1 |
| `--light-cutoff` | Skip colors above brightness (0.0-1.0) | 0.9 |
| `-p, --preset` | Preset: extended, simple, block, classic | extended |
| `--charset` | Custom character set | - |
| **GIF Output** | | |
| `--gif-scale` | GIF output scale factor (0.25-2.0) | 1.0 |
| `--gif-colors` | GIF palette size (16-256) | 64 |
| `--gif-fps` | GIF framerate | 10 |
| `--gif-font-size` | GIF font size in pixels | 10 |
| `--gif-length` | Max GIF length in seconds | - |
| `--gif-frames` | Max GIF frames | - |
| **Export** | | |
| `--md` | Export to markdown/SVG (auto-names from input) | - |
| `--md-format` | Format: svg, plain, html, ansi | svg |
| **Subtitles** | | |
| `--subs` | Subtitle source: auto, off, `<path>`, yt, whisper | - |
| `--sub-lang` | Subtitle language code | en |
| `--whisper-model` | Whisper model: tiny, base, small, medium, large | base |
| `--whisper-threads` | CPU threads for Whisper | Half available |
| `--transcript` | Generate subtitles only (no video rendering) | OFF |
| `--no-enhance` | Disable FFmpeg audio preprocessing for Whisper | Enhance ON |
| **Slideshow** | | |
| `--slide-delay` | Auto-advance delay in seconds (0 = manual only) | 3 |
| `--shuffle` | Randomize slideshow order | OFF |
| `--hide-info` | Hide file info header in slideshow | OFF |
| **YouTube** | | |
| `--ytdlp-path` | Path to yt-dlp executable | Auto-detect |
| `--cookies-from-browser` | Use cookies from browser (chrome, firefox, edge, etc.) | - |
| `--cookies` | Path to Netscape cookies.txt file | - |
| `--no-cache` | Don't cache YouTube videos locally | Cache ON |
| **System** | | |
| `--no-write` | Disable all caching and downloads (read-only mode) | OFF |
| `-y, --yes` | Auto-confirm tool downloads (FFmpeg, yt-dlp) | Prompt |
