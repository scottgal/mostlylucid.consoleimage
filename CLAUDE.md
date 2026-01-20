# ConsoleImage - ASCII Art Renderer

A high-quality ASCII art renderer for converting images and GIFs to terminal output.
Based on Alex Harri's shape-matching algorithm: https://alexharri.com/blog/ascii-rendering

## Project Structure

```
ConsoleImage/
├── ConsoleImage.Core/           # Core library (NuGet package)
│   ├── AsciiRenderer.cs         # Main ASCII rendering engine
│   ├── ColorBlockRenderer.cs    # Unicode block-based rendering (2x resolution)
│   ├── AsciiAnimationPlayer.cs  # GIF playback with DECSET 2026
│   ├── AsciiFrame.cs            # Single frame data structure
│   ├── CharacterMap.cs          # Character shape analysis and matching
│   ├── RenderOptions.cs         # All configuration options
│   ├── ShapeVector.cs           # 6-element vector for shape matching
│   ├── Dithering.cs             # Floyd-Steinberg dithering
│   ├── EdgeDirection.cs         # Edge detection and directional chars
│   └── ConsoleHelper.cs         # Windows ANSI support enabler
└── ConsoleImage/                # CLI tool
    └── Program.cs               # Command-line interface
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

### AsciiAnimationPlayer
Plays GIF animations using DECSET 2026 synchronized output for flicker-free rendering.
Supports responsive cancellation and frame event callbacks.

### RenderOptions
All configuration in one class. Key properties:
- `Width/Height/MaxWidth/MaxHeight` - Output dimensions
- `CharacterAspectRatio` - Terminal font compensation (default 0.5)
- `ContrastPower` - Contrast enhancement (2.0-4.0 recommended)
- `UseColor` - Enable ANSI color codes
- `FrameSampleRate` - Skip frames for efficiency (1 = all, 2 = every 2nd, etc.)
- `EnableDithering/EnableEdgeDirectionChars` - Experimental features

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

## CLI Usage

```bash
# Basic rendering
ascii-image image.jpg

# With options
ascii-image animation.gif -w 80 --speed 1.5 --loop 3

# Color blocks mode (higher fidelity)
ascii-image photo.png --blocks

# Frame sampling for large GIFs
ascii-image big.gif --frame-sample 2  # Every 2nd frame
```

### Key CLI Options
- `-w, --width` - Output width in characters
- `-h, --height` - Output height in characters
- `-s, --speed` - Animation speed multiplier
- `-l, --loop` - Loop count (0 = infinite)
- `-f, --frame-sample` - Frame sampling rate (skip frames)
- `-b, --blocks` - Use colored Unicode blocks
- `--no-color` - Disable color output
- `--no-animate` - Show first frame only

## Build

```bash
dotnet build
dotnet run --project ConsoleImage -- image.jpg
```

## Dependencies

- SixLabors.ImageSharp (image loading/processing)
- System.CommandLine (CLI parsing)
- Targets .NET 8.0 and .NET 10.0
