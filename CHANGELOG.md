# Changelog

All notable changes to this project will be documented in this file.

## [2.0.0] - 2025-01-21

### Major Features

#### Video Package is Now a Complete Superset
- **`consolevideo` handles images AND videos** - One CLI for everything: JPG, PNG, GIF, WebP, BMP, TIFF, plus all video formats FFmpeg supports
- **GIF output for video** - Save video clips as animated ASCII GIFs with `-g output.gif`
- **Unified experience** - Same render modes, options, and quality across images and videos

#### URL Support
- **Load images from HTTP/HTTPS URLs** - `consoleimage https://example.com/photo.jpg`
- **Download progress reporting** - Shows download progress for large files
- **UrlHelper utility class** - Programmatic URL downloading with progress callbacks

#### Native Terminal Protocol Support
- **iTerm2 inline images** - Native image display in iTerm2, WezTerm, Mintty
- **Kitty graphics protocol** - Native rendering in Kitty terminal
- **Sixel graphics** - DEC Sixel support for xterm, mlterm, foot, WezTerm
- **Auto-detection** - `TerminalCapabilities.DetectBestProtocol()` selects optimal protocol
- **Render mode option** - `-m/--mode` to select: ascii, blocks, braille, iterm2, kitty, sixel

#### Dynamic Console Resize Support
- **ResizableAnimationPlayer** - Animations automatically re-render when console window is resized
- **Auto-detect console size** - `--max-width` and `--max-height` default to current console dimensions

#### Flicker-Free Animation
- **DECSET 2026 synchronized output** - Atomic frame rendering for supported terminals
- **Diff-based rendering** - Only changed lines are updated between frames
- **Overwrite without clear** - No more black flashes from line clearing
- **Per-line color reset** - Prevents color bleed between lines

#### Smooth Loop Transitions
- **Automatic interpolation** - Creates 1-4 transition frames between last and first frame
- **Progressive line updates** - Crossfade effect for seamless loops
- **Smart threshold detection** - Only applies when frames are 10-70% different

#### JSON Document Format
- **Self-contained document format** - Save rendered ASCII art to JSON for playback without original source
- **Streaming support** - Save JSON while displaying (`-o json:output.json`)
- **Load and play** - `consoleimage animation.json` plays saved documents
- **AOT compatible** - Uses System.Text.Json source generation
- **Settings preserved** - All render settings stored for reproducible output

```bash
# Save as JSON while displaying
consoleimage animation.gif -o json:movie.json

# Load and play saved document
consoleimage movie.json

# Works with all render modes
consoleimage photo.jpg --braille -o json:photo.json
```

```csharp
// Programmatic API
var doc = ConsoleImageDocument.FromAsciiFrames(frames, options, "source.gif");
await doc.SaveAsync("output.json");

// Load and play
var loaded = await ConsoleImageDocument.LoadAsync("output.json");
using var player = new DocumentPlayer(loaded);
await player.PlayAsync();
```

#### Streaming JSON for Long Videos (NDJSON)
- **Incremental frame writing** - Write frames as they're processed without holding all in memory
- **Stop anytime** - Document auto-finalizes on stop (Ctrl+C), always valid
- **JSON Lines format** - Each line is valid JSON-LD, `.json` or `.ndjson` extension
- **Auto-detect on load** - `ConsoleImageDocument.LoadAsync()` handles both formats

```bash
# Stream long video to NDJSON (frames written incrementally)
consolevideo long_movie.mp4 -o json:movie.ndjson

# Stop anytime with Ctrl+C - document is still valid and playable
# Play back the saved document
consolevideo movie.ndjson
```

```csharp
// Streaming write API for video processing
await using var writer = new StreamingDocumentWriter("output.ndjson", "ASCII", options, "source.mp4");
await writer.WriteHeaderAsync();

await foreach (var frame in videoFrames)
{
    await writer.WriteFrameAsync(frame.Content, frame.DelayMs);
}

await writer.FinalizeAsync();  // Or let dispose auto-finalize
```

### New Packages

#### Spectre.Console Integration (`mostlylucid.consoleimage.spectre`)
Native `IRenderable` implementations for Spectre.Console:
- `AsciiImage` - ASCII art in any Spectre layout
- `ColorBlockImage` - High-fidelity Unicode half-blocks
- `BrailleImage` - Ultra-high resolution braille
- `AnimatedImage` - Animated GIF with `AnsiConsole.Live()`

```csharp
// Single image in a panel
AnsiConsole.Write(new Panel(new AsciiImage("photo.png")));

// Side-by-side comparison
AnsiConsole.Write(new Columns(
    new ColorBlockImage("a.png"),
    new ColorBlockImage("b.png")
));
```

### Performance Improvements

- **Parallel BrailleRenderer** - Row rendering parallelized with pre-computed brightness buffers
- **Parallel ColorBlockRenderer** - Multi-threaded row processing
- **Pre-computed trigonometry** - Lookup tables eliminate ~216 trig calls per cell
- **Optimized bounds checking** - Branchless `(uint)x < (uint)width` pattern
- **Pre-sized StringBuilders** - Reduced memory allocations
- **Shared utility classes** - `BrightnessHelper` and `AnsiCodes` reduce code duplication

### Gamma Correction

- **Automatic brightness compensation** - Default gamma of 0.85 brightens output to compensate for character/dot density
- **Adjustable via CLI** - `--gamma` option (< 1.0 brightens, > 1.0 darkens)
- **Applied to all render modes** - ASCII, Braille colors are gamma-corrected

### New CLI Options

#### Image/GIF CLI (`consoleimage`)
- `-m, --mode <mode>` - Render mode: ascii, blocks, braille, iterm2, kitty, sixel
- `--mode list` - List all available render modes
- `-o, --output <format>` - Output format: `json`, `json:file.json`, or file path (saves JSON document)
- `-r, --framerate <fps>` - Fixed framerate override for GIFs
- `-j, --json` - JSON output for LLM tool calls
- `--dark-cutoff <threshold>` - Skip colors below brightness (default: 0.1)
- `--light-cutoff <threshold>` - Skip colors above brightness (default: 0.9)
- `--gamma <value>` - Gamma correction for brightness (default: 0.85, < 1.0 brightens)
- `--gif-scale <factor>` - GIF output scale (0.25-2.0)
- `--gif-colors <n>` - GIF palette size (16-256)
- `--gif-fps <n>` - GIF framerate
- `--gif-font-size <px>` - GIF text size
- `--gif-length <seconds>` - Max GIF duration
- `--gif-frames <n>` - Max GIF frames

#### Video CLI (`consolevideo`)
- `-g, --output-gif <file>` - Save as animated GIF instead of playing
- `-o, --output <format>` - Output format: `json`, `json:file.json` (saves JSON document)
- `--gif-font-size <px>` - GIF font size (default: 10)
- `--gif-scale <factor>` - GIF scale factor (default: 1.0)
- `--gif-colors <n>` - GIF palette size (default: 128)
- All image CLI options also work with video CLI

### API Improvements

#### Aspect Ratio Handling
- **Proper dimension calculation** - `-w 150` now correctly preserves aspect ratio in all modes
- **Per-mode pixel ratios** - ASCII (1x1), ColorBlocks (1x2), Braille (2x4) handled correctly
- **`RenderOptions.CalculateVisualDimensions()`** - Shared calculation across all renderers

#### New Utility Classes
```csharp
// Centralized brightness calculation (ITU BT.601)
float brightness = BrightnessHelper.GetBrightness(pixel);
bool skip = BrightnessHelper.ShouldSkipColor(brightness, darkThreshold, lightThreshold);

// ANSI escape code generation
string fg = AnsiCodes.Foreground(color);
AnsiCodes.AppendForeground(sb, color);  // StringBuilder-efficient
AnsiCodes.AppendForegroundAndBackground(sb, fg, bg);
```

#### Terminal Capabilities Detection
```csharp
var protocol = TerminalCapabilities.DetectBestProtocol();
bool hasSixel = TerminalCapabilities.SupportsSixel();
bool hasKitty = TerminalCapabilities.SupportsKitty();
bool hasITerm = TerminalCapabilities.SupportsITerm2();
```

### Bug Fixes

- **Algorithm normalization** - Character vectors now normalize each component independently per Alex Harri's article
- **Braille mode inversion** - Fixed inverted output on dark terminals
- **Braille UTF-8 encoding** - Proper Unicode support on Windows with `SetConsoleOutputCP(65001)`
- **Braille solid blocks** - Fixed threshold calculation (was 0.2, now 0.5) and added Floyd-Steinberg dithering for smoother gradients
- **Animation row flickering** - Fixed interleaved Console.Write calls
- **Color artifacts** - Fixed color bleeding between animation frames
- **Aspect ratio** - Width-only or height-only specifications now preserve aspect ratio

### Breaking Changes

- Minimum .NET version is now **.NET 10**
- `consolevideo` CLI now handles images (may affect scripts expecting video-only)

### Documentation

- Added notes clarifying GIF output vs terminal display differences
- Comprehensive CLI examples for all options
- Real-world usage examples with video files

---

## [1.0.0] - Previous Release

### Features
- Shape-matching ASCII art algorithm based on Alex Harri's approach
- 3×2 staggered sampling grid with 6 sampling circles
- K-D tree optimization for fast character matching
- Multiple render modes: ASCII, ColorBlock (▀▄), Braille (⣿)
- Animated GIF support with DECSET 2026 synchronized output
- Floyd-Steinberg dithering
- Edge-direction character detection
- Auto background detection and suppression
- AOT compatible
- Cross-platform (Windows, Linux, macOS)
