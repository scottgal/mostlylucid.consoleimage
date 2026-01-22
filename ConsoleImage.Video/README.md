# ConsoleImage.Video

Real-time video to ASCII art renderer using FFmpeg for .NET 10.

**Part of the [ConsoleImage](https://github.com/scottgal/mostlylucid.consoleimage) project.**

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.video.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage.video/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

## Features

- **Real-time video playback** in terminal using ASCII, ColorBlocks, or Braille rendering
- **Zero setup** - FFmpeg auto-downloads on first use
- **Hardware acceleration** support (CUDA, DXVA2, VideoToolbox, VAAPI)
- **Dynamic console resize** - video re-renders when terminal size changes
- **URL streaming** - play videos from HTTP/HTTPS URLs
- **Time range playback** - specify start/end times
- **Speed control** - slow down or speed up playback
- **Frame buffering** - configurable look-ahead buffer for smooth playback
- **Multiple sampling strategies** - Uniform, Keyframe, SceneAware, Adaptive

### Example Output

| ASCII Mode | ColorBlocks Mode | Braille Mode |
|------------|------------------|--------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/familyguy_ascii.gif" width="200" alt="ASCII Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/familyguy_blocks.gif" width="200" alt="ColorBlocks Mode"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/familyguy_braille.gif" width="200" alt="Braille Mode"> |

**Real-time video rendering** in three modes with hardware-accelerated FFmpeg decoding

## FFmpeg - Zero Setup Required

FFmpeg is **automatically downloaded** on first use if not already installed.

```
$ consolevideo video.mp4
FFmpeg not found. Downloading...
Cache location: C:\Users\you\AppData\Local\consoleimage\ffmpeg

Downloading FFmpeg...                              75%
```

### Custom FFmpeg Path

If you have FFmpeg installed elsewhere, specify the path:

```bash
# Command line
consolevideo video.mp4 --ffmpeg-path "C:\tools\ffmpeg\bin"

# Or in appsettings.json
{
  "FFmpeg": {
    "Path": "C:\\tools\\ffmpeg\\bin"
  }
}
```

### Security & Download Sources

FFmpeg is downloaded from trusted sources:

| Platform | Source |
|----------|--------|
| Windows | [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) (GitHub) |
| Linux | [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) (GitHub) |
| macOS | [evermeet.cx/ffmpeg](https://evermeet.cx/ffmpeg/) (Official macOS builds) |

Downloads are cached to:
- **Windows**: `%LOCALAPPDATA%\consoleimage\ffmpeg`
- **Linux/macOS**: `~/.local/share/consoleimage/ffmpeg`

To disable auto-download and require manual installation, set `FFmpeg.Path` in appsettings.json to any value (even empty string prevents download attempts).

### Manual Installation (Optional)

If you prefer to install FFmpeg manually:

```bash
# Windows (winget)
winget install FFmpeg

# macOS (Homebrew)
brew install ffmpeg

# Linux (apt)
sudo apt install ffmpeg
```

## Installation

```bash
dotnet add package mostlylucid.consoleimage.video
```

Or download standalone CLI from [GitHub Releases](https://github.com/scottgal/mostlylucid.consoleimage/releases).

## CLI Usage

```bash
# Play video with default settings
consolevideo video.mp4

# Play from URL
consolevideo https://example.com/video.mp4

# ASCII mode (default)
consolevideo video.mp4

# High-fidelity color blocks
consolevideo video.mp4 --mode blocks

# Ultra-high resolution braille
consolevideo video.mp4 --mode braille

# Specify dimensions
consolevideo video.mp4 -w 120 -h 40

# Play specific time range
consolevideo video.mp4 --start 30 --end 60

# Speed control
consolevideo video.mp4 --speed 2.0

# Loop playback
consolevideo video.mp4 --loop 3   # 3 times
consolevideo video.mp4 --loop 0   # infinite

# Disable hardware acceleration
consolevideo video.mp4 --no-hw

# Custom aspect ratio calibration
consolevideo video.mp4 --aspect-ratio 0.45
```

### Render Modes

| Mode | Description |
|------|-------------|
| `ascii` | Shape-matched ASCII characters (default) |
| `blocks` | Unicode half-blocks (▀▄) - 2x vertical resolution |
| `braille` | Braille patterns - 2×4 dots per cell |
| `iterm2` | iTerm2 inline image protocol |
| `kitty` | Kitty graphics protocol |
| `sixel` | DEC Sixel graphics |

Use `--mode list` to see all available modes.

### CLI Options

| Option | Description | Default |
|--------|-------------|---------|
| `-w, --width` | Output width in characters | Console width |
| `-h, --height` | Output height in characters | Console height |
| `-m, --mode` | Render mode (see below) | ascii |
| `-a, --aspect-ratio` | Character aspect ratio | 0.5 |
| `--start` | Start time in seconds | 0 |
| `--end` | End time in seconds | Video end |
| `-s, --speed` | Playback speed multiplier | 1.0 |
| `-l, --loop` | Loop count (0 = infinite) | 1 |
| `-f, --fps` | Target framerate | Video native |
| `--frame-step` | Skip frames (1 = all, 2 = every 2nd) | 1 |
| `--buffer` | Frame buffer size | 5 |
| `--no-hw` | Disable hardware acceleration | HW enabled |
| `--no-alt-screen` | Keep in scrollback | Alt screen |
| `--ffmpeg-path` | Path to FFmpeg (file or directory) | Auto-detect/download |
| `--calibrate` | Show calibration pattern | OFF |
| `--save` | Save calibration | - |
| `-o, --output` | Output file (gif:path.gif or json:path.json) | - |
| `--raw` | Extract raw frames as GIF | - |
| `--smart-keyframes` | Use scene detection for keyframe extraction | - |

### GIF Output

Convert videos to animated ASCII GIFs:

```bash
# Basic video to GIF conversion
consolevideo video.mp4 -o gif:output.gif -w 80

# With render mode
consolevideo video.mp4 -o gif:output.gif --blocks
consolevideo video.mp4 -o gif:output.gif --braille

# Control GIF properties
consolevideo video.mp4 -o gif:output.gif --gif-fps 10 --gif-colors 64

# Limit output length
consolevideo video.mp4 -o gif:output.gif --gif-length 10   # 10 seconds max
consolevideo video.mp4 -o gif:output.gif --gif-frames 100  # 100 frames max

# Extract specific section
consolevideo video.mp4 -o gif:output.gif -ss 30 -t 10 --blocks
```

### GIF Output Options

| Option | Description | Default |
|--------|-------------|---------|
| `--gif-fps` | GIF framerate | 10 |
| `--gif-colors` | Palette size (16-256) | 64 |
| `--gif-font-size` | Font size in pixels | 10 |
| `--gif-scale` | Scale factor | 1.0 |
| `--gif-length` | Max length in seconds | - |
| `--gif-frames` | Max frame count | - |

### Raw Frame Extraction

Extract video frames as an animated GIF without ASCII rendering:

```bash
# Extract frames at uniform intervals
consolevideo video.mp4 --raw -o gif:frames.gif --fps 2 --duration 30

# Smart keyframe extraction (uses scene detection)
consolevideo video.mp4 --raw --smart-keyframes -o gif:keyframes.gif --gif-frames 20

# Extract keyframes from specific section
consolevideo video.mp4 --raw --smart-keyframes -o gif:keyframes.gif -ss 60 -t 60 --gif-frames 10
```

**Scene Detection**: The `--smart-keyframes` option uses histogram-based scene detection to identify visual changes and extract representative frames. This is useful for:
- Creating video thumbnails
- Extracting key moments from long videos
- Building preview animations

## Library API

### Basic Playback

```csharp
using ConsoleImage.Video.Core;

// Simple playback
await VideoPlayer.PlayAsync("video.mp4");

// With options
var options = new VideoRenderOptions
{
    RenderMode = VideoRenderMode.ColorBlocks,
    SpeedMultiplier = 1.5f,
    LoopCount = 0
};
await VideoPlayer.PlayAsync("video.mp4", options);
```

### Full Control

```csharp
using ConsoleImage.Video.Core;

// Get video info
using var ffmpeg = new FFmpegService();
var info = await ffmpeg.GetVideoInfoAsync("video.mp4");
Console.WriteLine($"Duration: {info.Duration}s, Resolution: {info.Width}x{info.Height}");

// Custom player
var options = VideoRenderOptions.Default.With(o =>
{
    o.RenderMode = VideoRenderMode.Braille;
    o.StartTime = 30;
    o.EndTime = 60;
    o.BufferAheadFrames = 10;
});

using var player = new VideoAnimationPlayer(
    "video.mp4",
    options,
    width: 100,
    height: 40);

await player.PlayAsync(cancellationToken);
```

### Streaming from URLs

```csharp
// FFmpeg handles URL streaming natively
await VideoPlayer.PlayAsync("https://example.com/video.mp4");
```

### Hardware Acceleration

Hardware acceleration is enabled by default and auto-detects:
- **Windows**: DXVA2, CUDA (NVIDIA)
- **macOS**: VideoToolbox
- **Linux**: VAAPI, CUDA

```csharp
var options = new VideoRenderOptions
{
    UseHardwareAcceleration = true  // default
};
```

### Video Render Options

```csharp
var options = new VideoRenderOptions
{
    // Core rendering
    RenderOptions = new RenderOptions { ... },  // Base image options
    RenderMode = VideoRenderMode.Ascii,         // Ascii, ColorBlocks, Braille

    // Playback control
    SpeedMultiplier = 1.0f,  // 0.5 = half speed, 2.0 = double speed
    LoopCount = 1,           // 0 = infinite
    StartTime = 0,           // Start seconds
    EndTime = null,          // End seconds (null = video end)

    // Performance
    TargetFps = null,        // Override video FPS
    FrameStep = 1,           // 1 = all frames, 2 = skip every other
    BufferAheadFrames = 5,   // Frame look-ahead buffer
    UseHardwareAcceleration = true,

    // Display
    UseAltScreen = true,     // Use alternate screen buffer
    SamplingStrategy = FrameSamplingStrategy.Uniform,
    SceneThreshold = 0.4     // Scene detection sensitivity
};

// Presets
var options = VideoRenderOptions.Default;
var options = VideoRenderOptions.LowResource;   // Lower buffer, frame skip
var options = VideoRenderOptions.HighQuality;   // Full frames, larger buffer
var options = VideoRenderOptions.ForTimeRange(30, 60);
```

### Frame Sampling Strategies

| Strategy | Description |
|----------|-------------|
| `Uniform` | Even frame distribution (default) |
| `Keyframe` | Prefer I-frames/keyframes |
| `SceneAware` | Sample around scene changes |
| `Adaptive` | Vary sampling based on motion |

## Calibration

For best results, calibrate the aspect ratio for your terminal:

```bash
# Show calibration pattern
consolevideo --calibrate

# Adjust until circle appears round
consolevideo --calibrate --aspect-ratio 0.45

# Save once correct
consolevideo --calibrate --aspect-ratio 0.45 --save
```

Different render modes may need different calibrations:

```bash
consolevideo --calibrate --mode blocks --aspect-ratio 0.48 --save
consolevideo --calibrate --mode braille --aspect-ratio 0.52 --save
```

## Configuration (appsettings.json)

Place `appsettings.json` next to the executable to configure defaults:

```json
{
  "FFmpeg": {
    "Path": "C:\\tools\\ffmpeg\\bin"
  },
  "Render": {
    "CharacterAspectRatio": 0.5,
    "UseColor": true,
    "ContrastPower": 2.5,
    "CharacterSetPreset": "extended"
  },
  "Playback": {
    "SpeedMultiplier": 1.0,
    "LoopCount": 1,
    "BufferAheadFrames": 3,
    "UseHardwareAcceleration": true
  }
}
```

## Performance Tips

1. **Use hardware acceleration** - enabled by default
2. **Reduce resolution** - smaller dimensions = faster rendering
3. **Skip frames** with `--frame-step 2` for slow systems
4. **Use ColorBlocks** instead of ASCII for faster rendering
5. **Reduce buffer** with `--buffer 2` to decrease memory usage

## Troubleshooting

### FFmpeg Download Issues
- If auto-download fails, manually download FFmpeg and use `--ffmpeg-path`
- Or set the path in `appsettings.json`

### Choppy playback
- Try `--no-hw` to disable hardware acceleration
- Reduce dimensions with `-w 80 -h 40`
- Skip frames with `--frame-step 2`

### Colors look wrong
- Ensure your terminal supports 24-bit color
- Try `--mode blocks` for best color support

### Video stretched or squashed
Run calibration and save: `--calibrate --aspect-ratio X --save`

## Dependencies

- [FFmpeg](https://ffmpeg.org/) - Video decoding
- [mostlylucid.consoleimage](https://www.nuget.org/packages/mostlylucid.consoleimage/) - Core rendering

## License

This is free and unencumbered software released into the public domain. See [UNLICENSE](../UNLICENSE) for details.
