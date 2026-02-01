# ConsoleImage - ASCII Art Renderer

A high-quality ASCII art renderer for converting images and GIFs to terminal output.
Based on Alex Harri's shape-matching algorithm: https://alexharri.com/blog/ascii-rendering

## Project Structure

```
ConsoleImage/
├── ConsoleImage.Core/           # Core library (NuGet package)
│   ├── AsciiRenderer.cs         # Main ASCII rendering engine
│   ├── ColorBlockRenderer.cs    # Unicode block-based rendering (2x resolution)
│   ├── BrailleRenderer.cs       # Braille character rendering (2x4 dots per cell)
│   ├── MatrixRenderer.cs        # Matrix digital rain effect rendering
│   ├── UnifiedRenderer.cs       # Unified entry point for all render modes
│   ├── UnifiedPlayer.cs         # Unified animation player for all formats
│   ├── AsciiAnimationPlayer.cs  # GIF playback with DECSET 2026
│   ├── BrailleInterlacePlayer.cs # Temporal interlace player (experimental)
│   ├── ResizableAnimationPlayer.cs # Dynamic console resize support
│   ├── ConsoleImageDocument.cs  # JSON document format for saving/loading
│   ├── CompressedDocument.cs    # Compressed .cidz format with delta encoding
│   ├── StreamingDocumentWriter.cs # NDJSON streaming writer for long videos
│   ├── DocumentPlayer.cs        # Playback of saved JSON documents
│   ├── GifWriter.cs             # GIF output writer (text-to-image rendering)
│   ├── StreamingGifWriter.cs    # Streaming GIF writer for large animations
│   ├── AsciiFrame.cs            # Single frame data structure
│   ├── CellData.cs              # Cell data for temporal stability tracking
│   ├── CharacterMap.cs          # Character shape analysis and matching
│   ├── BrailleCharacterMap.cs   # Braille-specific character mapping
│   ├── RenderOptions.cs         # All configuration options
│   ├── TemplateSettings.cs      # JSON template for saving/loading render options
│   ├── ShapeVector.cs           # 6-element vector for shape matching
│   ├── RendererBuffers.cs       # Shared buffer management for renderers
│   ├── Dithering.cs             # Floyd-Steinberg dithering
│   ├── EdgeDirection.cs         # Edge detection and directional chars
│   ├── BrightnessHelper.cs     # Brightness calculation utilities
│   ├── KdTree.cs                # K-D tree for efficient color matching
│   ├── AnsiCodes.cs             # ANSI escape code constants and utilities
│   ├── LineUtils.cs             # Line manipulation and measurement utilities
│   ├── FrameTiming.cs           # Frame timing and delay calculations
│   ├── CalibrationHelper.cs     # Aspect ratio calibration with circle test pattern
│   ├── ConsoleHelper.cs         # ANSI support + cell aspect ratio auto-detection
│   ├── TerminalCapabilities.cs  # Terminal feature detection (color, Unicode, Sixel, etc.)
│   ├── TerminalProtocol.cs      # Terminal protocol enum (ANSI, Sixel, iTerm2, Kitty)
│   ├── StatusLine.cs            # Status display below rendered output
│   ├── SecurityHelper.cs        # Input validation (paths, URLs, browser names)
│   ├── UrlHelper.cs             # URL detection and download helpers
│   ├── YtdlpProvider.cs         # YouTube support via yt-dlp (auto-download)
│   ├── AsciiArt.cs              # Simple static API for one-call rendering
│   ├── FrameHasher.cs           # Perceptual hashing for frame deduplication
│   ├── SmartFrameSampler.cs     # Intelligent frame skipping using perceptual hashing
│   ├── MarkdownRenderer.cs      # Markdown/SVG/HTML export for ASCII art
│   └── Subtitles/
│       ├── SubtitleEntry.cs     # Single subtitle entry with timing
│       ├── SubtitleTrack.cs     # Collection of subtitle entries
│       ├── SubtitleTrackData.cs # Track data container with metadata
│       ├── SubtitleParser.cs    # SRT/VTT file parser
│       ├── SubtitleRenderer.cs  # Console subtitle display with speaker colors
│       ├── SubtitleResolver.cs  # Automatic subtitle source resolution
│       ├── SubtitleSplitter.cs  # Subtitle splitting and synchronization
│       └── ILiveSubtitleProvider.cs  # Interface for streaming transcription
├── ConsoleImage/                # CLI tool for images/GIFs
│   ├── Program.cs               # Command-line interface + subcommands
│   ├── CliOptions.cs            # CLI option definitions
│   ├── Handlers/
│   │   ├── SlideshowHandler.cs  # Directory/glob slideshow with keyboard control
│   │   ├── ImageHandler.cs      # Single image/GIF rendering
│   │   ├── VideoHandler.cs      # Video playback via FFmpeg
│   │   ├── DocumentHandler.cs   # Playback of saved JSON/cidz documents
│   │   ├── CalibrationHandler.cs # Aspect ratio calibration UI
│   │   └── TranscriptionHandler.cs # Transcript-only mode
│   ├── Utilities/
│   │   ├── RenderHelpers.cs     # Render mode resolution, aspect ratio chain, frame playback
│   │   └── TimeParser.cs        # Flexible time parsing (seconds, mm:ss, hh:mm:ss, decimal minutes)
│   └── calibration.json         # Saved aspect ratio calibration
├── ConsoleImage.Transcription/  # Whisper AI transcription library (v4.0)
│   ├── WhisperTranscriptionService.cs # Whisper.NET wrapper
│   ├── WhisperModelDownloader.cs # Model download with progress
│   ├── WhisperRuntimeDownloader.cs # Native runtime download
│   ├── ChunkedTranscriber.cs    # Streaming transcription with buffering
│   ├── TranscriptSegment.cs     # Transcription result segment
│   ├── SrtFormatter.cs          # SRT output formatter
│   └── VttFormatter.cs          # VTT output formatter
├── ConsoleImage.Video.Core/     # Video playback library (FFmpeg-based)
│   ├── FFmpegService.cs         # FFmpeg process management
│   ├── FFmpegProvider.cs        # FFmpeg binary discovery and download
│   ├── FFmpegGifWriter.cs       # FFmpeg-based GIF/video output
│   ├── VideoAnimationPlayer.cs  # Video streaming player with live subtitles
│   ├── VideoPlayer.cs           # Core video playback implementation
│   ├── VideoFrameSampler.cs     # Intelligent video frame sampling
│   ├── VideoRenderOptions.cs    # Video-specific options (incl. LiveSubtitleProvider)
│   ├── SmartKeyframeExtractor.cs # Scene detection for keyframe extraction
│   └── KeyframeDeduplicationService.cs # Duplicate keyframe detection
├── ConsoleImage.Mcp/            # MCP (Model Context Protocol) server
│   └── Program.cs               # MCP tool server for LLM integration
├── ConsoleImage.Player/         # Standalone document player library
│   ├── ConsolePlayer.cs         # Player implementation
│   └── PlayerDocument.cs        # Player document model
├── ConsoleImage.Video/          # CLI tool for video files
│   ├── Program.cs               # Video CLI
│   └── calibration.json         # Saved aspect ratio calibration
├── ConsoleImage.Core.Tests/     # Unit tests for core library
├── ConsoleImage.Video.Core.Tests/ # Unit tests for video library
├── ConsoleImage.Player.Tests/   # Unit tests for player library
├── ConsoleImage.DocTests/       # Documentation tests
├── ConsoleImage.Benchmarks/     # Performance benchmarks
└── docs/
    └── JSON-FORMAT.md           # JSON document format specification
```

## Key Classes

### AsciiRenderer
Main rendering engine. Converts images to ASCII by:
1. Resizing image to target dimensions
2. Sampling each cell with 6 circular sample points (staggered grid)
3. Matching sample vectors against pre-computed character shapes
4. Applying contrast enhancement and directional contrast

### ColorBlockRenderer
High-fidelity renderer using Unicode half-block characters (▀▄█).
Each character displays 2 pixels vertically with separate fg/bg colors.
Requires 24-bit color terminal support.

### MatrixRenderer
Renders images with the iconic "Matrix digital rain" falling code effect.
- Uses half-width katakana, numbers, and symbols (authentic Matrix film characters)
- Supports custom colors: green (default), red, blue, amber, cyan, purple, or hex (#RRGGBB)
- Full-color mode uses source image colors with Matrix-style lighting/fading
- Configurable density, speed, and trail length
- Source image brightness influences rain appearance and intensity
- **Continuous animation** - Matrix mode always animates, even on still images
- **Pure rain mode** - Run with just `-M` and no input for standalone rain effect

### AsciiAnimationPlayer
Plays GIF animations using DECSET 2026 synchronized output for flicker-free rendering.
Supports responsive cancellation and frame event callbacks.

### RenderOptions
All configuration in one class. Key properties:
- `Width/Height/MaxWidth/MaxHeight` - Output dimensions
- `CharacterAspectRatio` - Terminal font compensation (default 0.5)
- `ContrastPower` - Contrast enhancement (2.0-4.0 recommended)
- `UseColor` - Enable ANSI color codes (when false, outputs greyscale)
- `FrameSampleRate` - Skip frames for efficiency (1 = all, 2 = every 2nd, etc.)
- `EnableDithering/EnableEdgeDirectionChars` - Experimental features
- `EnableTemporalStability` - De-jitter for animations (reduces color flickering)
- `ColorStabilityThreshold` - Color similarity threshold for de-jitter (0-255, default: 15)

### ConsoleImageDocument
Self-contained JSON document format for saving/loading rendered ASCII art.
- Stores all frames with ANSI escape codes
- Preserves render settings for reproducibility
- JSON-LD compatible (`@context`, `@type` fields)
- AOT-compatible using System.Text.Json source generation
- Auto-detects standard JSON vs streaming NDJSON format on load
- **Compressed format (.cidz)** - GZip compressed with delta encoding for animations

### CompressedDocumentArchive
Optimized compressed format for animations with delta encoding.
- **Global color palette** - Colors stored once, referenced by index
- **Delta frames (P-frames)** - Only changed cells stored between keyframes
- **Keyframe interval** - Full frames stored every N frames (default: 30)
- **Temporal stability (de-jitter)** - Stabilizes similar colors between frames
- **Loop count in metadata** - Frames NOT duplicated for loops
- File extension: `.cidz` (or `.cid.7z`)

### StreamingDocumentWriter
Writes frames incrementally to NDJSON (JSON Lines) format for long videos.
- One JSON object per line - each line is valid JSON
- Auto-finalizes on dispose (Ctrl+C produces valid document)
- No memory buildup - frames written as processed

### DocumentPlayer
Plays back saved JSON documents with animation support.
- Handles both single-frame and animated documents
- Respects saved settings (speed, loop count)
- Can override settings at playback time

### StatusLine
Displays information below rendered output during playback.
- Shows filename, resolution (source → output), render mode
- Progress bar with frame count or time position
- Loop counter for animations
- Use `--status` or `-S` CLI flag to enable
- Status in GIF output only supported for ASCII mode (pixel-based modes can't mix text)

### SmartFrameSampler
Intelligent frame skipping using perceptual hashing for improved playback performance.
- **Perceptual hashing** - Resizes frame to 8x8, computes average brightness hash
- **LFU cache** - Caches rendered content, reuses for similar frames
- **Streaming optimized** - Hash computation runs ahead of rendering
- **No quality loss** - Only skips visually similar frames
- Enable with `-f s` or `-f smart` CLI option
- Maintains proper timing - frames are not sped up, just reuses cached content

### FrameHasher
Fast perceptual hashing for frame deduplication.
- **aHash algorithm** - Resize to 8x8, compare to average brightness
- **64-bit hash** - One bit per pixel (above/below average)
- **Hamming distance** - Count differing bits between hashes
- **Threshold 5** - Default similarity threshold (5 of 64 bits can differ)

### MarkdownRenderer
Converts ANSI-colored ASCII art to markdown-friendly formats for documentation.

- **Plain** - Text in code blocks, strips ANSI codes (universal compatibility)
- **HTML** - `<span>` elements with inline CSS colors (limited renderer support)
- **SVG** - Scalable Vector Graphics with colored `<tspan>` elements (GitHub, GitLab)
- **ANSI** - Preserves escape codes in `ansi` code blocks (terminal-rendered only)

```csharp
// Usage in code
var ansiContent = renderer.RenderImage(image);
var markdown = MarkdownRenderer.ToMarkdown(ansiContent, MarkdownFormat.Svg, "My Image");
await MarkdownRenderer.SaveMarkdownAsync(ansiContent, "output.md", MarkdownFormat.Svg);
await MarkdownRenderer.SaveSvgAsync(ansiContent, "output.svg");
```

### CalibrationHelper
Manages terminal font aspect ratio calibration. Each render mode (ASCII, Blocks, Braille)
maps pixels to characters differently and may need separate calibration.

- `CalibrationSettings` - Stores per-mode aspect ratios in `calibration.json`
- `GenerateCalibrationImage()` - Creates a circle test pattern using ImageSharp
- `RenderCalibrationPattern()` - Renders calibration through actual render pipeline
- `Load()/Save()` - JSON persistence with AOT-compatible source generation

**Calibration Format (calibration.json):**
```json
{
  "AsciiCharacterAspectRatio": 0.5,
  "BlocksCharacterAspectRatio": 0.5,
  "BrailleCharacterAspectRatio": 0.5
}
```

### UnifiedRenderer
Single entry point for all render modes. Selects the appropriate renderer (ASCII, Braille,
Blocks, Matrix) based on `RenderMode` and delegates rendering. Simplifies code that needs
to support all modes without switching logic.

### UnifiedPlayer
Unified animation player that handles all frame types (ASCII, Braille, Blocks, Matrix).
Wraps format-specific players with a common interface for playback.

### TerminalCapabilities
Detects terminal features at runtime:
- **Color support** - True color (24-bit), 256 color, or 16 color
- **Unicode support** - Braille characters, block elements
- **Image protocols** - Sixel, iTerm2 inline images, Kitty graphics
- Used by `TerminalProtocol` enum to select optimal output method

### ConsoleHelper
Enables ANSI escape sequences on Windows and provides cell aspect ratio auto-detection:
- `EnableAnsiSupport()` - Enable VT processing + UTF-8 on Windows consoles
- `DetectCellAspectRatio()` - Auto-detect character cell width/height ratio
  - Windows: `GetCurrentConsoleFontEx` P/Invoke for actual font metrics
  - Fallback: ANSI `CSI 16 t` query (Windows Terminal, iTerm2, kitty, xterm)
  - Returns `null` if detection fails; cached per session
  - Skipped when stdin/stdout redirected (piped/CI)

### GifWriter
Renders ANSI text frames to GIF images using ImageSharp.
- Text-to-image rendering with configurable font size and scale
- Palette quantization (4-256 colors)
- Supports ASCII text frames, Braille pixel frames, and ColorBlock pixel frames
- `AddImageFrameWithOverlays()` - Compose frame with subtitle and status overlays

### TemplateSettings
JSON template system for saving and loading render option presets.
- Save current CLI options to a `.json` template file
- Load template to restore all options at once
- Useful for consistent rendering across sessions

### TimeParser (CLI Utility)
Flexible time string parser supporting multiple formats:
- Seconds: `4.7`, `120`
- MM:SS: `6:47`
- HH:MM:SS: `1:30:00`
- Decimal minutes: `6.47` (via `--start-minutes`)

## Common Issues

### Variable Frame Height in GIFs
GIFs can use delta encoding where frames only contain changed regions (partial frames).
When using ImageSharp's `CloneFrame()`, these return smaller images.
**Fixed by:** Compositing partial frames onto full-size canvas before processing.

### Frame Positioning
ImageSharp's `ImageFrame.Bounds()` returns local coordinates (0,0), not canvas position.
`GifFrameMetadata` doesn't expose Left/Top offset properties.
**Workaround:** Draw partial frames at (0,0) on full canvas.

### Extra Rows in Output
If GIF has uniform background color filling bottom portion:
- Use `--auto-bg` to auto-detect and suppress background
- Use `--dark-bg-threshold 0.4` for dark backgrounds (like purple)
- Use `--bg-threshold 0.85` for light backgrounds

### Inter-line Color Artifacts
ANSI color codes can bleed between lines during animation.
**Fixed by:** Reset colors (`\x1b[0m`) at end of each line before newline.

### Animation Adds Extra Lines Per Frame
Off-by-one error in cursor positioning. After writing N lines without trailing newline,
cursor is ON line N-1, not below it.
**Fixed by:** Move up `maxHeight - 1` lines, not `maxHeight` lines.
Use `\x1b[{n}A` to move up n lines in one command.

### Braille Mode Solarization
Braille rendering can appear "solarized" (weird color mixing) if colors are averaged
from all pixels in a cell instead of just the visible dots.
**Fixed by:** Only sample colors from pixels where dots are actually displayed.
The dot pattern is determined by brightness threshold; colors should come from the
same pixels that contribute to the visible pattern.

## Quick Reference (Fast Paths)

### Video Playback
```bash
# Play video in terminal (braille mode, auto-scaling)
consoleimage video.mp4

# YouTube video
consoleimage "https://youtu.be/VIDEO_ID"

# Start at specific time, play for 30 seconds
consoleimage video.mp4 -ss 60 -t 30

# With status bar showing progress
consoleimage video.mp4 --status
```

### Video Playback Controls

During video playback, the following keyboard controls are available:

| Key | Action |
|-----|--------|
| `Space` | Pause/resume playback |
| `Left` | Seek backward (default 10s) |
| `Right` | Seek forward (default 10s) |
| `+` / `=` | Double seek step (max 60s) |
| `-` | Halve seek step (min 5s) |
| `R` | Save raw frame snapshot as PNG (with burned-in subtitles) |
| `S` | Save ANSI text rendering as PNG snapshot |
| `Q` / `Escape` | Quit playback |
| `A` | Show hint for ASCII mode flag |
| `b` / `B` | Show hint for Blocks/Braille mode flag |
| `m` / `M` | Show hint for Monochrome/Matrix mode flag |

**Seeking:** Arrow keys seek by 10 seconds by default. Use `+`/`-` to adjust the step size between 5-60 seconds. Seeking clears the screen and resumes from the new position. Subtitles and status bar update correctly after seeking.

**Snapshots:** Press `R` to save the current raw video frame as a PNG file, or `S` to save the current terminal rendering (braille/blocks/ASCII) as a PNG image. Both include burned-in subtitles when active. Snapshots are saved to the video's directory with sequential numbering: `{videoname}_{type}_{HH.mm.ss}_{NNN}.png`

### Subtitle Generation
```bash
# Auto-detect subtitles (local → embedded → YouTube → Whisper)
consoleimage video.mp4 --subs auto

# Force Whisper transcription
consoleimage video.mp4 --subs whisper

# Transcript only (no video rendering)
consoleimage video.mp4 --transcript

# Save subtitles to file
consoleimage transcribe video.mp4 -o output.vtt
```

### CI/CD Usage
```bash
# Extract thumbnail (read-only mode)
consoleimage video.mp4 --raw -ss 5 -o thumb.png --no-write

# Generate ASCII preview GIF
consoleimage video.mp4 -w 80 --ascii -o preview.gif -t 3 --no-write

# Smart keyframes (scene detection)
consoleimage video.mp4 --raw --smart -o keyframe.png --gif-frames 5 --no-write
```

### Output to GIF with Subtitles
```bash
# GIF with burned-in subtitles (auto-sized, WCAG compliant)
consoleimage video.mp4 --subs whisper --raw -o clip.gif -t 10

# ASCII art GIF with subtitles
consoleimage video.mp4 --subs movie.srt -o output.gif -w 80
```

### Markdown / SVG Export
```bash
# Simplest: auto-generates image.svg with full color (SVG is default format)
consoleimage image.jpg --md

# Specify output path
consoleimage image.jpg --md output.md

# Plain text in code block (works everywhere)
consoleimage image.jpg --md --md-format plain

# HTML spans with inline CSS (for compatible renderers)
consoleimage image.jpg --md --md-format html
```

---

## CLI Usage

```bash
# Basic rendering (braille mode is DEFAULT in v3.0+)
consoleimage image.jpg

# Classic ASCII mode (v2.x default)
consoleimage image.jpg --ascii

# With options
consoleimage animation.gif -w 80 --speed 1.5 --loop 3

# Color blocks mode (higher fidelity)
consoleimage photo.png --blocks

# Braille mode (ultra-high resolution) - DEFAULT
consoleimage photo.png --braille

# Monochrome mode (braille without color - compact, fast)
consoleimage photo.png --monochrome
consoleimage movie.mp4 --mono -w 80

# Matrix mode (digital rain effect)
consoleimage photo.png --matrix

# Matrix with custom color
consoleimage photo.png --matrix --matrix-color red
consoleimage photo.png --matrix --matrix-color "#FF6600"

# Matrix with source image colors (full-color mode)
consoleimage photo.png --matrix --matrix-fullcolor

# Matrix with adjusted density and speed
consoleimage photo.png --matrix --matrix-density 0.8 --matrix-speed 1.5

# Matrix mode always animates continuously on still images
# The rain keeps falling indefinitely until Ctrl+C
consoleimage photo.jpg --matrix

# Pure Matrix rain effect (no input image - just for fun!)
consoleimage --matrix
consoleimage -M --matrix-color amber

# Show status line with progress, timing, file info
consoleimage animation.gif --status
consoleimage movie.mp4 -S -w 120

# Frame sampling for large GIFs
consoleimage big.gif --frame-sample 2  # Every 2nd frame (uniform skip)
consoleimage big.gif -f s              # Smart skip: perceptual hash-based dedup

# Output to GIF (auto-detected from .gif extension)
consoleimage movie.mp4 -o output.gif -w 100
consoleimage animation.gif -o converted.gif

# Output to compressed JSON (default for .json extension)
consoleimage animation.gif -o output.json  # Actually saves as output.cidz

# Explicit compressed format
consoleimage animation.gif -o output.cidz
consoleimage animation.gif -o compressed

# Explicit uncompressed JSON (use raw: prefix)
consoleimage animation.gif -o raw:output.json

# Output with de-jitter (temporal stability for animations)
consoleimage animation.gif -o output.cidz --dejitter
consoleimage animation.gif -o output.cidz --dejitter --color-threshold 20

# Greyscale blocks mode (smaller JSON files)
consoleimage photo.png --blocks --no-color -o output.cidz

# Explicit format prefixes also work
consoleimage movie.mp4 -o gif:output.gif
consoleimage movie.mp4 -o json:movie.ndjson

# Play saved JSON/compressed document
consoleimage output.json
consoleimage output.cidz
consoleimage output.cidz
```

### Output Options

The `-o` / `--output` option auto-detects format from file extension:
- `.gif` → Animated GIF output (loops infinitely by default)
- `.json` → Compressed format (.cidz) by default (use `raw:path.json` for uncompressed)
- `.cidz` / `.cid.7z` → Compressed document with delta encoding
- `.ndjson` → Streaming JSON Lines format (uncompressed)

You can also use explicit prefixes: `gif:path`, `json:path`, `raw:path`, or `cidz:path`

GIF output settings:
- `--gif-font-size` - Font size for text rendering (default: 10)
- `--gif-scale` - Scale factor (default: 1.0)
- `-c, --colors` - Max palette colors 4-256 (default: 64 for GIF output)

Note: GIFs loop infinitely by default. Use `-l 1` for single play.

### Markdown / SVG Export

Export ASCII art to embeddable formats. SVG is the default — full-color, scalable vector output.

```bash
# Simplest: just add --md (auto-generates photo.svg with full color)
consoleimage photo.jpg --md

# Specify output path
consoleimage photo.jpg --md output.md

# Plain text in code block (universal compatibility)
consoleimage photo.jpg --md --md-format plain

# HTML with inline CSS (for renderers that support HTML)
consoleimage photo.jpg --md --md-format html

# ANSI codes preserved (for terminals that render ANSI in markdown)
consoleimage photo.jpg --md --md-format ansi
```

**Format Comparison:**

| Format | Colors | Compatibility | Use Case |
|--------|--------|---------------|----------|
| `svg` | Full | GitHub, GitLab, most | **Default** — embeddable colored vector art |
| `plain` | None | Universal | All markdown renderers |
| `html` | Full | Limited | Custom docs sites |
| `ansi` | Full | Terminals only | Terminal-rendered markdown |

**Example: Embed in README**
```markdown
## Preview

![ASCII Art](preview.svg)
```

Or inline the SVG directly for GitHub README badges/images.

### Calibration

The aspect ratio calibration ensures circles appear round, not stretched.
Each render mode (ASCII, Blocks, Braille) can be calibrated separately.

```bash
# Show calibration pattern (should be a circle)
consoleimage --calibrate

# Calibrate blocks mode
consoleimage --calibrate --blocks

# Adjust aspect ratio until circle looks correct
consoleimage --calibrate --aspect-ratio 0.45

# Save calibration once circle looks right
consoleimage --calibrate --aspect-ratio 0.48 --save

# Each mode has its own saved value
consoleimage --calibrate --braille --aspect-ratio 0.52 --save
```

**Suggested aspect ratios by platform:**
| Platform         | Typical Value |
|------------------|---------------|
| Windows Terminal | 0.5           |
| Windows Console  | 0.5           |
| macOS Terminal   | 0.5           |
| iTerm2           | 0.5           |
| Linux (gnome)    | 0.45-0.5      |
| VS Code Terminal | 0.5           |

Values may vary by font. Run `--calibrate` to find your ideal value.

### Key CLI Options

**Dimensions & Display:**
- `-w, --width` - Output width in characters
- `-h, --height` - Output height in characters
- `--max-width` - Maximum output width (default: terminal width - 1)
- `--max-height` - Maximum output height (default: terminal height - 2)
- `--char-aspect` - Character aspect ratio (width/height, auto-detected if not set)
- `-S, --status` - Show status line below output (progress, timing, file info)
- `--color-depth, --depth` - Terminal color depth: `true` (24-bit, default), `256`, `16`
- `--no-color` - Disable color output (greyscale for blocks/braille)

**Render Modes:**
- `-a, --ascii` - Use classic ASCII characters (v2.x default)
- `-b, --blocks` - Use colored Unicode blocks
- `-B, --braille` - Use braille characters (DEFAULT - 2x4 dots per cell)
- `-M, --matrix` - Use Matrix digital rain effect
- `--monochrome, --mono` - Braille mode without color (compact, high-detail greyscale)
- `-m, --mode` - Render mode by name: ascii, blocks, braille, mono, matrix
- `-p, --preset` - Character preset: extended, simple, block, classic
- `--charset` - Custom character set (light to dark)

**Matrix Mode:**
- `--matrix-color` - Matrix color: green, red, blue, amber, cyan, purple, or hex (#RRGGBB)
- `--matrix-fullcolor` - Use source image colors with Matrix lighting
- `--matrix-density` - Rain density (0.1-2.0, default 0.5)
- `--matrix-speed` - Rain speed multiplier (0.5-3.0, default 1.0)
- `--matrix-alphabet` - Custom character set for rain

**Playback:**
- `-s, --speed` - Animation speed multiplier
- `-l, --loop` - Loop count (0 = infinite)
- `-f, --frame-step` - Frame step: 1 (every frame), 2 (every 2nd), s/smart (perceptual hash skip)
- `-r, --fps` - Target framerate
- `--no-animate` - Show first frame only
- `--buffer` - Frames to buffer ahead (2-10, default: 3)

**Time Range (Video Seeking):**
- `-ss, --start` - Start time: seconds (4.7), mm:ss (6:47), or hh:mm:ss
- `-to, --end` - End time (same formats)
- `-t, --duration` - Duration (same formats)
- `-sm, --start-minutes` - Start in decimal minutes (6.47 = 6m 28.2s)
- `-em, --end-minutes` - End in decimal minutes
- `-dm, --duration-minutes` - Duration in decimal minutes
- `-sf, --start-frame` - Start at frame number
- `-ef, --end-frame` - End at frame number
- `-df, --duration-frames` - Number of frames to play

**Frame Sampling:**
- `--sampling` - Sampling strategy: uniform, keyframe, scene, adaptive
- `--scene-threshold` - Scene detection threshold (0.0-1.0, default: 0.4)

**Rendering Adjustments:**
- `--contrast` - Contrast enhancement (1.0 = none, default: 2.5)
- `-g, --gamma` - Gamma correction (< 1.0 = brighter; default: 0.5 braille, 0.65 others)
- `-c, --colors` - Max colors in palette (4, 16, 256)
- `--no-invert` - Don't invert brightness (for light terminal backgrounds)
- `-e, --edge` - Enable edge detection
- `--dark-cutoff` - Skip colors below this brightness
- `--light-cutoff` - Skip colors above this brightness
- `--auto-bg` - Auto-detect and suppress uniform background
- `--bg-threshold` - Light background suppression threshold
- `--dark-bg-threshold` - Dark background suppression threshold

**Temporal Stability:**
- `--dejitter, --stabilize` - Enable temporal stability to reduce color flickering
- `--color-threshold` - Color stability threshold for de-jitter (0-255, default: 15)

**Output:**
- `-o, --output` - Output file (.gif, .json→.cidz, .cidz, raw:path.json)
- `--md, --markdown` - Export to markdown/SVG (auto-names from input, or specify path)
- `--md-format` - Export format: svg (default), plain, html, ansi
- `-j, --json` - Output as JSON document
- `-i, --info` - Show file info and exit
- `--hash` - Output perceptual hash (for comparing visual fidelity)

**GIF Output:**
- `--gif-font-size` - Font size for GIF text rendering (default: 10)
- `--gif-scale` - Scale factor for GIF output (default: 1.0)
- `--gif-fps` - Target FPS for GIF (default: 15)
- `--gif-length` - Max GIF length in seconds
- `--gif-frames` - Max frames for GIF
- `--gif-width` / `--gif-height` - GIF output dimensions in characters

**Raw/Extract Mode:**
- `--raw, --extract` - Extract raw video frames as GIF (no ASCII rendering)
- `--raw-width` / `--raw-height` - Dimensions for raw output in pixels
- `--smart-keyframes, --smart` - Use smart scene detection for keyframes
- `-q, --quality` - Output quality 1-100 (for JPEG, WebP; default: 85)

**Calibration:**
- `--calibrate` - Show aspect ratio calibration pattern (Tab to switch aspect/gamma)
- `--save` - Save calibration to calibration.json

**Aspect Ratio Resolution Chain:**
CLI `--char-aspect` → saved calibration per mode → auto-detected from terminal font → 0.5 default

**Performance:**
- `--no-hwaccel` - Disable hardware acceleration
- `--no-alt-screen` - Disable alternate screen buffer
- `--no-parallel` - Disable parallel processing

**Template Support:**
- `--template <file>` - Load render options from a JSON template
- `--save-template <file>` - Save current options to a JSON template

**Miscellaneous:**
- `--no-write, --readonly` - Disable all caching and downloading (CI/CD mode)
- `--no-ffmpeg-download` - Don't auto-download FFmpeg
- `--no-enhance` - Disable FFmpeg audio preprocessing for Whisper transcription
- `--ee` - Easter egg animation demo
- `--debug` - Debug output for smart frame sampling

### Subtitle Options (Unified)

The `--subs` flag accepts different sources:
- `--subs <path>` - Load from SRT/VTT file
- `--subs auto` - Automatic resolution (see priority below)
- `--subs whisper` - Real-time Whisper transcription during playback
- `--subs yt` - YouTube subtitles only (fail if not available)
- `--subs off` - Disable subtitles

**Subtitle Priority (--subs auto):**
When using `--subs auto`, subtitles are resolved in this order:
1. **Local subtitle files** - Searches for matching .srt/.vtt files (video.srt, video.en.srt)
2. **Embedded subtitles** - Extracts text-based subtitles from video container (MKV, MP4)
3. **YouTube subtitles** - Downloads from YouTube (if YouTube URL)
4. **Whisper transcription** - Generates with local AI model (last resort)

Additional options:
- `--sub-lang <lang>` - Preferred language (default: "en")
- `--whisper-model <size>` - Model: tiny, base (default), small, medium, large
- `--whisper-threads <n>` - CPU threads for transcription
- `--force-subs` - Force re-transcription even if cached subtitles exist
- `--save-subs <path>` - Save generated subtitles to specified path (VTT format)
- `--no-sub-cache` - Don't cache subtitles (always re-transcribe)

### Transcript-Only Mode (v4.0)

Generate subtitles without video rendering. For YouTube, tries existing subtitles first:

```bash
# Auto mode (default): YouTube subs first, then Whisper if none
consoleimage https://youtu.be/VIDEO_ID --transcript
consoleimage https://youtu.be/VIDEO_ID --transcript --subs auto  # Explicit auto

# Force Whisper transcription (skip YouTube subs check)
consoleimage https://youtu.be/VIDEO_ID --transcript --subs whisper

# Force YouTube subs only (fail if not available)
consoleimage https://youtu.be/VIDEO_ID --transcript --subs yt

# Local files always use Whisper
consoleimage movie.mp4 --transcript

# Save VTT file using transcribe subcommand
consoleimage transcribe movie.mp4 -o output.vtt
consoleimage transcribe https://youtu.be/VIDEO_ID -o output.vtt

# Stream to stdout AND save file
consoleimage transcribe movie.mp4 -o output.vtt --stream

# Quiet mode (only output text, no progress)
consoleimage transcribe movie.mp4 -o output.vtt --stream --quiet
```

Output format for `--transcript` and `--stream`:
```
[00:00:01.500 --> 00:00:04.200] Hello, welcome to the video.
[00:00:04.500 --> 00:00:07.800] Today we'll be discussing...
```

Subtitles are displayed as 2 centered lines below the video frame, constrained to frame width.

**Supported formats:**
- **SRT** (SubRip) - Standard format with `.srt` extension
- **VTT** (WebVTT) - Web format with `.vtt` extension

**Live transcription features:**
- Audio extracted and transcribed in 15-second chunks
- Background transcription runs ahead of playback (30s buffer)
- Shows "⏳ Transcribing..." if playback catches up
- Subtitles auto-saved as `.vtt` files for instant replay
- Respects `--ss` start time for seeking

**Note:** Speaker diarization requires pyannote integration (not yet implemented).

```bash
# Local video with SRT file
consoleimage movie.mp4 --subs movie.srt

# Live transcription during playback
consoleimage movie.mp4 --subs whisper
consoleimage movie.mp4 --subs whisper --whisper-model small

# YouTube with Whisper transcription
consoleimage "https://youtu.be/VIDEO_ID" --subs whisper
consoleimage "https://youtu.be/VIDEO_ID" --subs whisper --ss 3600

# YouTube with auto subtitles
consoleimage "https://youtu.be/VIDEO_ID" --subs auto

# Subtitles are cached - subsequent plays use saved .vtt
consoleimage movie.mp4 --subs whisper  # First: transcribes
consoleimage movie.mp4 --subs whisper  # Second: uses cached movie.vtt

# Save to GIF with subtitles
consoleimage movie.mp4 --subs movie.srt -o output.gif

# Disable subtitles
consoleimage "https://youtu.be/VIDEO_ID" --subs off
```

**Notes:**
- Whisper models auto-download on first use (75MB - 3GB)
- HTML tags in subtitles are automatically stripped
- SRT files typically match video name: `movie.mkv` → `movie.srt`

### Subcommands

#### `transcribe` - Generate subtitles

```bash
consoleimage transcribe <input> [options]
```

Generates subtitles from video/audio using Whisper AI.

**Options:**
- `-o, --output` - Output file (default: input.vtt)
- `-m, --model` - Whisper model: tiny, base (default), small, medium, large
- `-l, --lang` - Language code: en, es, ja, etc. or 'auto' (default: "en")
- `-d, --diarize` - Enable speaker diarization
- `-t, --threads` - CPU threads (default: half available)
- `-s, --stream` - Stream transcript text to stdout as generated
- `-q, --quiet` - Suppress progress messages (only transcribed text)
- `--no-enhance` - Disable FFmpeg audio preprocessing filters

#### `tools` - Manage external tools

```bash
consoleimage tools [options]
```

Check status and manage required tools (FFmpeg, yt-dlp, Whisper runtime).

**Options:**
- `--verify` - Test download URLs are reachable
- `--clear-cache` - Clear all downloaded tool caches
- `--redownload` - Clear caches and re-download everything

### Slideshow Mode

Pass a directory or glob pattern to enter slideshow mode for browsing multiple images:

```bash
# Slideshow from directory (newest first by default)
consoleimage ./photos

# Slideshow with glob pattern
consoleimage "C:\Pictures\*.jpg"
consoleimage "./vacation/d*.png"

# Recursive (include subdirectories)
consoleimage ./photos -R

# Shuffle order
consoleimage ./photos --shuffle

# Sort options: name, date, size, random
consoleimage ./photos --sort name --asc

# Manual navigation only (no auto-advance)
consoleimage ./photos --slide-delay 0

# Custom delay between slides (default: 3 seconds)
consoleimage ./photos --slide-delay 5

# Output slideshow as GIF
consoleimage ./photos -o slideshow.gif --slide-delay 2

# Hide the [1/N] filename header
consoleimage ./photos --hide-info
```

**Slideshow Controls:**
- `Space` - Pause/resume auto-advance
- `Left/Right` arrows - Previous/next image
- `Up/Down` arrows - Previous/next image (alternate)
- `Home/End` - Jump to first/last image
- `N/P` - Next/previous (vim-style)
- `Q` or `Escape` - Quit slideshow

**Slideshow Options:**
- `-d, --slide-delay` - Seconds between slides (0 = manual only, default: 3)
- `--shuffle` - Randomize image order
- `-R, --recursive` - Include subdirectories
- `--sort` - Sort by: name, date, size, random (default: date)
- `--desc` - Sort descending (default for date = newest first)
- `--asc` - Sort ascending (oldest first)
- `--video-preview` - Max video preview duration in seconds (default: 30)
- `--gif-loop` - Loop GIFs continuously (default: play once)
- `--hide-info` - Hide the [1/N] filename header

### YouTube Support

Play YouTube videos directly by passing a URL:

```bash
# Play YouTube video (yt-dlp required)
consoleimage "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
consoleimage "https://youtu.be/dQw4w9WgXcQ"

# YouTube Shorts
consoleimage "https://youtube.com/shorts/abc123"

# With render options
consoleimage "https://youtu.be/xyz" -w 80 --blocks

# Output YouTube video to GIF
consoleimage "https://youtu.be/xyz" -o output.gif --duration 10

# Specify custom yt-dlp path
consoleimage "https://youtu.be/xyz" --ytdlp-path /path/to/yt-dlp

# Auto-confirm yt-dlp download
consoleimage "https://youtu.be/xyz" -y
```

**yt-dlp Requirement:**
YouTube support requires [yt-dlp](https://github.com/yt-dlp/yt-dlp) to extract video streams.

If yt-dlp is not found, ConsoleImage will offer to download it automatically (~10MB).
Install manually with:
- Windows: `winget install yt-dlp` or `pip install yt-dlp`
- macOS: `brew install yt-dlp`
- Linux: `pip install yt-dlp` or `sudo apt install yt-dlp`

**YouTube Options:**
- `--ytdlp-path` - Path to yt-dlp executable
- `-y, --yes` - Auto-confirm yt-dlp download (no prompt)
- `--cookies-from-browser <browser>` - Use cookies from browser (validated whitelist: brave, chrome, chromium, edge, firefox, opera, safari, vivaldi)
- `--cookies <file>` - Path to Netscape cookies.txt file

**YouTube Authentication:**
YouTube may require authentication to prevent bot access. If you see:
```
Sign in to confirm you're not a bot
```
Use one of these cookie options:
```bash
# Use cookies from your browser (recommended)
consoleimage "https://youtu.be/xyz" --cookies-from-browser chrome
consoleimage "https://youtu.be/xyz" --cookies-from-browser firefox

# Or export cookies to a file and use that
consoleimage "https://youtu.be/xyz" --cookies cookies.txt
```

See [yt-dlp FAQ](https://github.com/yt-dlp/yt-dlp/wiki/FAQ#how-do-i-pass-cookies-to-yt-dlp) for more details.

**How it works:**
1. Detects YouTube URL patterns (youtube.com, youtu.be, shorts)
2. Uses yt-dlp to extract the direct video stream URL
3. Streams immediately for fast start
4. Downloads in background for future replays (caching)
5. For ASCII rendering, requests lower resolution (480p) for efficiency

**Video Caching (v4.0):**
YouTube videos are cached locally by default for better user experience:
- First play: Streams immediately (no wait), downloads in background
- Subsequent plays: Uses cached file (instant start, zero bandwidth)
- Cache limit: 2GB (oldest files removed automatically)
- Location: `%LOCALAPPDATA%\consoleimage\videos`

```bash
# Default behavior: stream + cache for next time
consoleimage "https://youtu.be/xyz"

# Disable caching (always stream directly)
consoleimage "https://youtu.be/xyz" --no-cache

# Read-only mode (no caching or downloading at all)
consoleimage "https://youtu.be/xyz" --no-write
```

### Configuration (appsettings.json)

Create/edit `appsettings.json` in the application directory to set defaults:

```json
{
  "Global": {
    "NoWrite": false,           // Disable all caching/downloading
    "AutoDownloadTools": true   // Auto-download FFmpeg/yt-dlp
  },
  "Cache": {
    "EnableVideoCache": true,
    "MaxVideoCacheSizeMB": 2048,
    "CacheDirectory": null      // null = default location
  },
  "Render": {
    "DefaultMode": "braille",
    "CharacterAspectRatio": 0.5
  },
  "Subtitles": {
    "DefaultMode": "auto",
    "DefaultLanguage": "en",
    "WhisperModel": "base"
  }
}
```

**Read-Only Mode (`--no-write`):**
For locked-down environments (CI/CD, kiosks), use `--no-write` to:
- Disable all file caching (videos, subtitles)
- Disable auto-downloading of FFmpeg/yt-dlp
- Only use pre-installed tools and stream directly

```bash
# CI/CD mode - no writes to filesystem
consoleimage movie.mp4 --no-write

# Alias
consoleimage movie.mp4 --readonly
```

### CI/CD Usage

ConsoleImage can be used in CI/CD pipelines for automated image/video processing:

**Extract Thumbnails:**
```bash
# Single frame thumbnail at specific timestamp
consoleimage video.mp4 --raw -ss 5 -o thumbnail.png --no-write

# Multiple keyframes using scene detection
consoleimage video.mp4 --raw --smart-keyframes --gif-frames 5 -o keyframe.png --no-write
# Outputs: keyframe_001.png, keyframe_002.png, ...

# Extract first frame at specific size
consoleimage video.mp4 --raw -ss 0 --raw-width 320 -o preview.jpg --no-write
```

**Generate ASCII Preview Images:**
```bash
# Render ASCII art to GIF (for web previews)
consoleimage video.mp4 -w 80 --ascii -o preview.gif -t 3 --no-write

# Render single ASCII frame (braille mode)
consoleimage video.mp4 -w 60 --braille -ss 10 --no-animate -o frame.gif --no-write
```

**Video Processing:**
```bash
# Extract clip and re-encode
consoleimage video.mp4 --raw -ss 30 -t 10 -o clip.mp4 --no-write

# Convert to WebP animation
consoleimage video.mp4 --raw -t 5 -o animation.webp --no-write
```

**GitHub Actions Example:**
```yaml
jobs:
  generate-preview:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install FFmpeg
        run: sudo apt-get install -y ffmpeg

      - name: Generate thumbnail
        run: |
          dotnet run --project ConsoleImage -- \
            video.mp4 --raw -ss 5 -o thumbnail.png --no-write

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: thumbnails
          path: thumbnail.png
```

**Future: Playwright Visual Testing:**
ConsoleImage ASCII art can be rendered in a browser via Playwright for visual regression testing:
```bash
# Concept: Render to HTML, screenshot with Playwright
consoleimage video.mp4 -w 80 --ascii -o preview.html
npx playwright screenshot preview.html screenshot.png
```

### Experimental Features

#### Temporal Interlacing (Braille)
Rapidly cycles between different brightness thresholds to reveal more detail
than a single threshold allows. Known issues with black bars and clearing artifacts.

```bash
consoleimage image.png --interlace
consoleimage image.png --interlace --interlace-frames 4 --interlace-spread 0.06 --interlace-fps 20
```

- `--interlace` - Enable temporal interlacing
- `--interlace-frames` - Number of subframes per cycle (2-8, default: 4)
- `--interlace-spread` - Threshold spread (0.01-0.2, default: 0.06)
- `--interlace-fps` - Visible frame rate (default: 20)

### MCP Server (ConsoleImage.Mcp)

ConsoleImage includes an MCP (Model Context Protocol) server for LLM integration.
This allows AI assistants to render images and videos through tool calls.

```bash
# Run the MCP server
dotnet run --project ConsoleImage.Mcp
```

## Build

```bash
dotnet build
dotnet run --project ConsoleImage -- image.jpg
```

### AOT Publishing

Cross-platform AOT build scripts are provided for all supported platforms.

**Windows (requires Visual Studio Build Tools):**
```powershell
# Build for Windows x64 (default)
.\ConsoleImage\build-aot.ps1

# Build for Windows ARM64
.\ConsoleImage\build-aot.ps1 -RID win-arm64
```

**Linux/macOS:**
```bash
# Build for current platform (auto-detects architecture)
./ConsoleImage/build-aot.sh

# Prerequisites installed automatically on Linux (clang, zlib1g-dev)
```

**Supported Platforms:**
| Platform | RID | Build Script |
|----------|-----|--------------|
| Windows x64 | win-x64 | `build-aot.ps1` |
| Windows ARM64 | win-arm64 | `build-aot.ps1 -RID win-arm64` |
| Linux x64 | linux-x64 | `build-aot.sh` |
| Linux ARM64 | linux-arm64 | `build-aot.sh` |
| macOS Intel | osx-x64 | `build-aot.sh` |
| macOS Apple Silicon | osx-arm64 | `build-aot.sh` |

Output location: `bin/Release/net10.0/{rid}/publish/`

**Important**: Use `string?` for CLI arguments (not `FileInfo?`) for AOT compatibility.
System.CommandLine's `FileInfo` argument type can have issues resolving paths in AOT builds.

### Auto-Download and Cache Directories

ConsoleImage automatically downloads required tools (FFmpeg, yt-dlp, Whisper runtime) on first use.
Cache locations follow platform conventions:

| Platform | Cache Base Directory |
|----------|---------------------|
| Windows | `%LOCALAPPDATA%\consoleimage\` |
| macOS | `~/Library/Application Support/consoleimage/` |
| Linux | `~/.local/share/consoleimage/` |

**Cache Subdirectories:**
- `ffmpeg/` - FFmpeg binaries (~100MB)
- `ytdlp/` - yt-dlp binary (~10MB)
- `whisper/` - Whisper models (75MB-3GB) and runtime (~12MB)

**Disabling Auto-Download:**
```bash
# Use --no-write to prevent all automatic downloads
consoleimage movie.mp4 --no-write

# Or set in appsettings.json
{ "Global": { "AutoDownloadTools": false } }
```

Manually install tools:
- **FFmpeg**: `winget install FFmpeg` (Windows), `apt install ffmpeg` (Linux), `brew install ffmpeg` (macOS)
- **yt-dlp**: `pip install yt-dlp` or download from https://github.com/yt-dlp/yt-dlp
- **Whisper**: Models download automatically; runtime requires `dotnet add package Whisper.net.Runtime`

## Dependencies

- SixLabors.ImageSharp (image loading/processing)
- System.CommandLine (CLI parsing)
- System.Text.Json (JSON document format with source generation)
- Targets .NET 10.0

## Security

The codebase includes input validation to prevent command injection attacks:

- **Browser name validation** - `--cookies-from-browser` only accepts whitelisted browser names (brave, chrome, chromium, edge, firefox, opera, safari, vivaldi)
- **Path validation** - File paths passed to FFmpeg/yt-dlp are validated for shell metacharacters
- **URL validation** - Only http/https URLs are accepted for streaming
- **SecurityHelper class** - Shared utilities in `ConsoleImage.Core/SecurityHelper.cs`

## JSON Document Format

See [docs/JSON-FORMAT.md](docs/JSON-FORMAT.md) for the full specification.

**Three formats supported:**
- **Compressed (.cidz)** - GZip compressed with delta encoding (default for animations)
- **Standard JSON** - Single JSON object with all frames (images, short GIFs)
- **Streaming NDJSON** - JSON Lines format, one record per line (long videos)

**Key features:**
- JSON-LD compatible (`@context`, `@type` fields)
- Self-contained - no source file needed for playback
- Auto-detects format on load (by extension or magic bytes)
- Streaming format auto-finalizes on Ctrl+C
- Compressed format uses delta encoding (P-frames) for efficient storage
- Loop count stored in metadata - frames NOT duplicated

**Compressed format benefits:**
- ~7:1 compression ratio vs uncompressed JSON
- Delta encoding stores only changed cells between frames
- Global color palette reduces redundancy
- Optional temporal stability (de-jitter) reduces visual flickering

## LLM Integration

ConsoleImage serves as a powerful **video probe for LLMs**, enabling text-based AI models to perceive and extract content from videos without requiring vision APIs.

### Use Cases

**1. Visual Probe - "See" video content as text**
```bash
# Render video frames as ASCII/Braille text an LLM can read
consoleimage video.mp4 -ss 60 -t 5 -w 80

# Lower width for more compact output
consoleimage video.mp4 -ss 120 -t 10 -w 40 --mono
```

**2. Audio Probe - Extract spoken content**
```bash
# Stream transcript to stdout (pipe-friendly)
consoleimage video.mp4 --transcript -t 30

# Transcribe specific segment
consoleimage video.mp4 --transcript -ss 60 -t 15
```

**3. Combined Analysis - Visual + dialogue**
```bash
# Watch video with live subtitles
consoleimage video.mp4 -ss 120 -t 10 -w 60 --subs whisper
```

**4. GIF Extraction - Create clips with burned-in subtitles**
```bash
# Extract 10s GIF with auto-transcribed subtitles
consoleimage video.mp4 -ss 120 -t 10 --subs whisper --raw -o clip.gif

# From YouTube (requires cookies for some videos)
consoleimage "https://youtu.be/xyz" -t 15 --subs whisper --raw -o demo.gif

# With existing SRT file
consoleimage movie.mp4 -ss 300 -t 20 --srt movie.srt --raw -o scene.gif
```

### LLM Capabilities Enabled

| Capability | Command | Output |
|------------|---------|--------|
| Analyze video visually | `consoleimage video.mp4 -w 60` | ASCII frames to stdout |
| Extract dialogue | `--transcript` | Timestamped text |
| Search for scenes | Watch ASCII + transcript | Text patterns |
| Create captioned GIFs | `--raw --subs whisper -o out.gif` | GIF file with subtitles |
| Describe with context | `-w 60 --subs whisper` | Visual + audio as text |

### Key Options for LLM Use

- `-w 40-80` - Width in characters (smaller = less tokens)
- `--mono` - Monochrome braille (compact, high detail)
- `--transcript` - Output only the transcript (no video)
- `--subs whisper` - Auto-transcribe with Whisper AI
- `--raw -o file.gif` - Extract actual video frames to GIF
- `-ss N -t M` - Select time range (start at N seconds, M seconds duration)

### Benefits

- **No vision API required**: ASCII output is readable text
- **Automatic transcription**: Whisper AI generates captions
- **Self-contained**: FFmpeg and Whisper models auto-download
- **Accessible output**: Subtitles burned at WCAG-compliant sizes
- **Streaming**: Can process videos of any length
