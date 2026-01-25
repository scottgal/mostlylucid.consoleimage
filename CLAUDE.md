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
│   ├── AsciiAnimationPlayer.cs  # GIF playback with DECSET 2026
│   ├── ResizableAnimationPlayer.cs # Dynamic console resize support
│   ├── ConsoleImageDocument.cs  # JSON document format for saving/loading
│   ├── StreamingDocumentWriter.cs # NDJSON streaming writer for long videos
│   ├── DocumentPlayer.cs        # Playback of saved JSON documents
│   ├── AsciiFrame.cs            # Single frame data structure
│   ├── CharacterMap.cs          # Character shape analysis and matching
│   ├── RenderOptions.cs         # All configuration options
│   ├── ShapeVector.cs           # 6-element vector for shape matching
│   ├── Dithering.cs             # Floyd-Steinberg dithering
│   ├── EdgeDirection.cs         # Edge detection and directional chars
│   ├── CalibrationHelper.cs     # Aspect ratio calibration with circle test pattern
│   ├── ConsoleHelper.cs         # Windows ANSI support enabler
│   ├── StatusLine.cs            # Status display below rendered output
│   ├── UrlHelper.cs             # URL detection and download helpers
│   ├── YtdlpProvider.cs         # YouTube support via yt-dlp (auto-download)
│   ├── FrameHasher.cs           # Perceptual hashing for frame deduplication
│   ├── SmartFrameSampler.cs     # Intelligent frame skipping using perceptual hashing
│   └── Subtitles/
│       ├── SubtitleEntry.cs     # Single subtitle entry with timing
│       ├── SubtitleTrack.cs     # Collection of subtitle entries
│       ├── SubtitleParser.cs    # SRT/VTT file parser
│       ├── SubtitleRenderer.cs  # Console subtitle display with speaker colors
│       └── ILiveSubtitleProvider.cs  # Interface for streaming transcription
├── ConsoleImage/                # CLI tool for images/GIFs
│   ├── Program.cs               # Command-line interface
│   ├── CliOptions.cs            # CLI option definitions
│   ├── Handlers/
│   │   ├── SlideshowHandler.cs  # Directory/glob slideshow with keyboard control
│   │   ├── ImageHandler.cs      # Single image/GIF rendering
│   │   └── VideoHandler.cs      # Video playback via FFmpeg
│   └── calibration.json         # Saved aspect ratio calibration
├── ConsoleImage.Transcription/  # Whisper AI transcription library (v4.0)
│   ├── WhisperTranscriptionService.cs # Whisper.NET wrapper
│   ├── WhisperModelDownloader.cs # Model download with progress
│   ├── ChunkedTranscriber.cs    # Streaming transcription with buffering
│   ├── TranscriptSegment.cs     # Transcription result segment
│   ├── SrtFormatter.cs          # SRT output formatter
│   └── VttFormatter.cs          # VTT output formatter
├── ConsoleImage.Video.Core/     # Video playback library (FFmpeg-based)
│   ├── FFmpegService.cs         # FFmpeg process management
│   ├── VideoAnimationPlayer.cs  # Video streaming player with live subtitles
│   └── VideoRenderOptions.cs    # Video-specific options (incl. LiveSubtitleProvider)
├── ConsoleImage.Video/          # CLI tool for video files
│   ├── Program.cs               # Video CLI
│   └── calibration.json         # Saved aspect ratio calibration
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

# Show status line with progress, timing, file info
consoleimage animation.gif --status
consolevideo movie.mp4 -S -w 120

# Frame sampling for large GIFs
consoleimage big.gif --frame-sample 2  # Every 2nd frame (uniform skip)
consoleimage big.gif -f s              # Smart skip: perceptual hash-based dedup

# Output to GIF (auto-detected from .gif extension)
consolevideo movie.mp4 -o output.gif -w 100
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
consolevideo movie.mp4 -o gif:output.gif
consolevideo movie.mp4 -o json:movie.ndjson

# Play saved JSON/compressed document
consoleimage output.json
consoleimage output.cidz
consolevideo output.cidz
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
- `--gif-colors` - Max palette colors 16-256 (default: 64)

Note: GIFs loop infinitely by default. Use `-l 1` for single play.

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
- `-w, --width` - Output width in characters
- `-h, --height` - Output height in characters
- `--char-aspect` - Character aspect ratio (width/height)
- `-s, --speed` - Animation speed multiplier
- `-S, --status` - Show status line below output (progress, timing, file info)
- `-l, --loop` - Loop count (0 = infinite)
- `-f, --frame-step` - Frame step: 1 (every frame), 2 (every 2nd), s/smart (perceptual hash skip)
- `-a, --ascii` - Use classic ASCII characters (v2.x default)
- `-b, --blocks` - Use colored Unicode blocks
- `-B, --braille` - Use braille characters (DEFAULT - 2x4 dots per cell)
- `-M, --matrix` - Use Matrix digital rain effect
- `--monochrome, --mono` - Braille mode without color (compact, high-detail greyscale)
- `--matrix-color` - Matrix color: green, red, blue, amber, cyan, purple, or hex (#RRGGBB)
- `--matrix-fullcolor` - Use source image colors with Matrix lighting
- `--matrix-density` - Rain density (0.1-2.0, default 0.5)
- `--matrix-speed` - Rain speed multiplier (0.5-3.0, default 1.0)
- `-o, --output` - Output file (.gif, .json→.cidz, .cidz, raw:path.json)
- `--dejitter, --stabilize` - Enable temporal stability to reduce color flickering
- `--color-threshold` - Color stability threshold for de-jitter (0-255, default: 15)
- `--calibrate` - Show aspect ratio calibration pattern
- `--save` - Save calibration to calibration.json
- `--no-color` - Disable color output (greyscale for blocks/braille)
- `--no-animate` - Show first frame only

### Subtitle Options (Unified)

The `--subs` flag accepts different sources:
- `--subs <path>` - Load from SRT/VTT file
- `--subs auto` - Try YouTube subtitles, fall back to Whisper
- `--subs whisper` - Real-time Whisper transcription during playback
- `--subs off` - Disable subtitles

Additional options:
- `--sub-lang <lang>` - Preferred language (default: "en")
- `--whisper-model <size>` - Model: tiny, base (default), small, medium, large
- `--whisper-threads <n>` - CPU threads for transcription

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

**How it works:**
1. Detects YouTube URL patterns (youtube.com, youtu.be, shorts)
2. Uses yt-dlp to extract the direct video stream URL
3. Passes the stream URL to FFmpeg for playback
4. For ASCII rendering, requests lower resolution (480p) for efficiency

## Build

```bash
dotnet build
dotnet run --project ConsoleImage -- image.jpg
```

### AOT Publishing

Native AOT publishing requires Visual Studio C++ build tools and vswhere.exe in PATH.
Use the provided PowerShell scripts which set up the environment automatically:

```powershell
# Publish consolevideo with AOT
.\ConsoleImage.Video\build-aot.ps1

# Publish consoleimage with AOT
.\ConsoleImage\build-aot.ps1
```

The scripts:
1. Add vswhere.exe to PATH (`C:\Program Files (x86)\Microsoft Visual Studio\Installer`)
2. Launch VS Developer PowerShell to set up MSVC toolchain
3. Run `dotnet publish` with AOT

Output location: `bin\Release\net10.0\win-x64\publish\`

**Important**: Use `string?` for CLI arguments (not `FileInfo?`) for AOT compatibility.
System.CommandLine's `FileInfo` argument type can have issues resolving paths in AOT builds.

## Dependencies

- SixLabors.ImageSharp (image loading/processing)
- System.CommandLine (CLI parsing)
- System.Text.Json (JSON document format with source generation)
- Targets .NET 10.0

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
