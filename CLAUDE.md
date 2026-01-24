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
│   └── StatusLine.cs            # Status display below rendered output
├── ConsoleImage/                # CLI tool for images/GIFs
│   ├── Program.cs               # Command-line interface
│   └── calibration.json         # Saved aspect ratio calibration
├── ConsoleImage.Video.Core/     # Video playback library (FFmpeg-based)
│   ├── FFmpegService.cs         # FFmpeg process management
│   ├── VideoAnimationPlayer.cs  # Video streaming player
│   └── VideoRenderOptions.cs    # Video-specific options
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
consoleimage big.gif --frame-sample 2  # Every 2nd frame

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
- `-f, --frame-sample` - Frame sampling rate (skip frames)
- `-a, --ascii` - Use classic ASCII characters (v2.x default)
- `-b, --blocks` - Use colored Unicode blocks
- `-B, --braille` - Use braille characters (DEFAULT - 2x4 dots per cell)
- `-M, --matrix` - Use Matrix digital rain effect
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
