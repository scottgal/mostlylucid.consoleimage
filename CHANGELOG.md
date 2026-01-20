# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added

#### Dynamic Console Resize Support
- **ResizableAnimationPlayer**: New animation player that automatically re-renders frames when the console window is resized during playback
- **Auto-detect console size**: `--max-width` and `--max-height` now default to the current console window dimensions instead of fixed 120x60

#### Flicker-Free Animation
- **Diff-based rendering**: Subsequent frames now use line-by-line diffing - only changed lines are updated, eliminating flicker completely
- **Overwrite without clear**: Removed per-line `\x1b[2K` clear commands that caused visible black flashes; lines are now overwritten in place with space padding
- **Atomic frame rendering**: Entire frames are now pre-built as single strings and written with one `Console.Write()` call
- **Per-line color reset**: `\x1b[0m` reset added at end of each line to prevent color bleed between lines

#### Smooth Loop Transitions
- **Automatic loop interpolation**: When last and first frames differ, creates 1-4 interpolated transition frames for seamless looping
- **Progressive line updates**: Transition frames progressively update changed lines to create a crossfade effect
- **Smart threshold detection**: Only applies interpolation when frames are close (10-70% different); instant transition for very different frames

### Performance
- **Pre-computed trigonometry**: Circle sampling now uses lookup tables for sin/cos, eliminating ~216 trig calls per cell
- **Parallel ColorBlockRenderer**: Row rendering is now parallelized when `UseParallelProcessing` is enabled
- **Parallel BrailleRenderer**: Brightness buffer calculation is now parallelized
- **Optimized bounds checking**: Uses `(uint)x < (uint)width` pattern for branchless bounds checks
- **Pre-sized StringBuilders**: Row buffers pre-allocated to reduce reallocations

### Fixed
- **Algorithm normalization**: Character vector normalization now correctly normalizes each of the 6 components independently (per Alex Harri's article), rather than using a single global maximum
- **Braille mode inversion**: Fixed inverted output on dark terminals - bright pixels now correctly show as dots
- **Braille UTF-8 encoding**: Added `Console.OutputEncoding = UTF8` and `SetConsoleOutputCP(65001)` for proper Unicode support on Windows

#### New CLI Options
- `--framerate, -r <fps>`: Fixed framerate override - play GIFs at a specific FPS regardless of embedded timing
- `--json, -j`: JSON output mode for LLM tool calls and programmatic use
- `--dark-cutoff <threshold>`: Dark terminal optimization - skip colors below this brightness (0.0-1.0, default: 0.1)
- `--light-cutoff <threshold>`: Light terminal optimization - skip colors above this brightness (0.0-1.0, default: 0.9)

#### Terminal Background Optimization
- **Dark terminal mode**: Pixels below brightness threshold are rendered as plain spaces without color codes, blending naturally with dark terminal backgrounds and reducing output size
- **Light terminal mode**: Pixels above brightness threshold are similarly optimized for light backgrounds
- Thresholds are configurable via CLI (`--dark-cutoff`, `--light-cutoff`) or programmatically (`DarkTerminalBrightnessThreshold`, `LightTerminalBrightnessThreshold`)
- Optimization applies to all render modes: ASCII, ColorBlock, and Braille

#### Spectre.Console Integration Package
New package: `mostlylucid.consoleimage.spectre`

Provides native Spectre.Console `IRenderable` implementations:
- `AsciiImage` - Render images as ASCII art in any Spectre layout
- `ColorBlockImage` - High-fidelity Unicode half-block rendering
- `BrailleImage` - Ultra-high resolution braille rendering
- `AnimatedImage` - Animated GIF support with `AnsiConsole.Live()`

Example usage:
```csharp
using ConsoleImage.Spectre;
using Spectre.Console;

// Single image in a panel
AnsiConsole.Write(new Panel(new AsciiImage("photo.png")));

// Side-by-side images
AnsiConsole.Write(new Columns(
    new AsciiImage("a.png"),
    new AsciiImage("b.png")
));

// Animated GIF
var animation = new AnimatedImage("clip.gif", AnimationMode.ColorBlock);
await animation.PlayAsync(cancellationToken);
```

#### Demo Application
- `ConsoleImage.SpectreDemo` - Demonstrates Spectre.Console integration including side-by-side animations

### Changed
- Animation player now supports optional `targetFps` parameter for framerate override
- ColorBlock and Braille animation loops in CLI now use atomic frame buffering

### Fixed
- First few rows flickering during GIF playback due to interleaved Console.Write calls
- Color artifacts bleeding between animation frames

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
