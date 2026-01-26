# mostlylucid.consoleimage.video

**Version 2.0** - Real-time video to ASCII art rendering using FFmpeg for .NET 10.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.video.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage.video/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

**[Full documentation with animated examples on GitHub](https://github.com/scottgal/mostlylucid.consoleimage)**

![ASCII Mode](https://github.com/scottgal/mostlylucid.consoleimage/raw/master/samples/video_ascii_status.gif)

*Video to ASCII rendering - see [GitHub](https://github.com/scottgal/mostlylucid.consoleimage) for more examples*

## Features

- **Superset of consoleimage** - handles both videos AND images (JPG, PNG, GIF, etc.)
- **Real-time video playback** in terminal
- **Zero setup** - FFmpeg auto-downloads on first use
- **Hardware acceleration** (CUDA, DXVA2, VideoToolbox, VAAPI)
- **URL streaming** - play from HTTP/HTTPS
- **Time range playback** - start/end times
- **Multiple render modes** - ASCII, ColorBlocks, Braille
- **GIF output** - save video/image as animated ASCII GIF

## Quick Start

```csharp
using ConsoleImage.Video.Core;

// Play video - FFmpeg downloads automatically if needed
await VideoPlayer.PlayAsync("video.mp4");

// With render mode
var options = new VideoRenderOptions
{
    RenderMode = VideoRenderMode.ColorBlocks
};
await VideoPlayer.PlayAsync("video.mp4", options);
```

## CLI Examples

The `consolevideo` CLI handles **both videos and images** - it's a complete superset of `consoleimage`.

### Basic Usage

```bash
# Play a video file
consolevideo "Family Guy S18E01 Yacht Rocky.mkv"

# Display a still image
consolevideo photo.jpg

# Play an animated GIF
consolevideo wiggum.gif
```

### Render Modes

```bash
# Braille mode (default in v3.0+) - highest resolution using 2x4 dot patterns
consoleimage video.mp4
consoleimage video.mp4 -B

# ASCII mode (-a/--ascii) - character-based rendering (v2.x default)
consoleimage video.mp4 --ascii
consoleimage video.mp4 -a

# ColorBlocks mode (-b/--blocks) - Unicode half-blocks for 2x vertical resolution
consoleimage video.mp4 --blocks
consoleimage video.mp4 -b
```

### Dimension Control

```bash
# Set width (height auto-calculated to preserve aspect ratio)
consolevideo video.mp4 -w 120

# Set height (width auto-calculated)
consolevideo video.mp4 -h 40

# Set both (may distort if aspect ratio differs)
consolevideo video.mp4 -w 120 -h 50

# Set maximum bounds (fits within, preserves aspect ratio)
consolevideo video.mp4 --max-width 150 --max-height 60
```

### Playback Control

```bash
# Speed control (0.5 = half speed, 2.0 = double speed)
consolevideo video.mp4 -s 1.5

# Loop playback (0 = infinite)
consolevideo video.mp4 -l 3       # Play 3 times
consolevideo video.mp4 -l 0       # Loop forever

# Time range - play specific section
consolevideo video.mp4 -ss 30            # Start at 30 seconds
consolevideo video.mp4 -ss 60 -to 120    # Play from 1:00 to 2:00
consolevideo video.mp4 -ss 30 -t 60      # Start at 30s, play for 60s

# Target framerate
consolevideo video.mp4 -r 15             # Cap at 15 FPS
consolevideo video.mp4 -r 30             # Higher framerate for smooth playback

# Frame stepping (skip frames for performance)
consolevideo video.mp4 -f 2              # Every 2nd frame
consolevideo video.mp4 -f 3              # Every 3rd frame
```

### GIF Output

Save rendered output as animated GIF instead of playing:

```bash
# Basic GIF output
consolevideo video.mp4 -g output.gif

# GIF with specific render mode
consolevideo video.mp4 -g output.gif --blocks
consolevideo video.mp4 -g output.gif -B

# Control GIF quality and size
consolevideo video.mp4 -g output.gif --gif-font-size 8    # Smaller text = smaller file
consolevideo video.mp4 -g output.gif --gif-scale 0.5      # Half size output
consolevideo video.mp4 -g output.gif --gif-colors 64      # Fewer colors = smaller file

# Extract specific section to GIF
consolevideo video.mp4 -g clip.gif -ss 30 -t 10 --blocks

# Convert image to single-frame GIF
consolevideo photo.jpg -g photo_ascii.gif -B
```

### Image Handling

```bash
# Display still images (all common formats supported)
consolevideo photo.jpg
consolevideo screenshot.png
consolevideo artwork.bmp
consolevideo photo.webp

# Images with render modes
consolevideo photo.jpg -B                 # Braille - highest detail
consolevideo photo.jpg --blocks           # ColorBlocks - good color fidelity
consolevideo photo.jpg                    # ASCII - classic look

# Control image dimensions
consolevideo photo.jpg -w 100             # 100 chars wide, aspect preserved
consolevideo photo.jpg -h 50              # 50 chars tall, aspect preserved

# Animated GIF playback
consolevideo wiggum.gif                  # Play animated GIF
consolevideo wiggum.gif -l 0             # Loop forever
consolevideo wiggum.gif -s 0.5           # Half speed
```

### Video Information

```bash
# Show video metadata without playing
consolevideo video.mp4 -i
consolevideo video.mp4 --info
```

Output:

```
File: video.mp4
Duration: 00:22:45.123
Resolution: 1920x1080
Codec: h264
Frame Rate: 23.98 fps
Total Frames: ~32760
Bitrate: 5234 kbps
Hardware Accel: dxva2
```

### Aspect Ratio Calibration

Different terminals/fonts may need calibration for circles to appear round:

```bash
# Show calibration pattern (should be a circle)
consolevideo --calibrate
consolevideo --calibrate --blocks
consolevideo --calibrate --braille

# Adjust aspect ratio until circle looks correct
consolevideo --calibrate --char-aspect 0.45
consolevideo --calibrate --char-aspect 0.55

# Save calibration for future use
consolevideo --calibrate --char-aspect 0.48 --save
consolevideo --calibrate --blocks --char-aspect 0.50 --save
```

### Advanced Options

```bash
# Disable color output
consolevideo video.mp4 --no-color

# Adjust contrast (1.0 = none, higher = more contrast)
consolevideo video.mp4 --contrast 3.0

# Custom character set
consolevideo video.mp4 --charset " .:-=+*#%@"

# Character set presets
consolevideo video.mp4 -p simple      # Minimal character set
consolevideo video.mp4 -p extended    # Maximum detail (default)
consolevideo video.mp4 -p block       # Unicode density blocks
consolevideo video.mp4 -p classic     # Original algorithm set

# Frame sampling strategies
consolevideo video.mp4 --sampling uniform     # Even distribution (default)
consolevideo video.mp4 --sampling keyframe    # Prefer I-frames
consolevideo video.mp4 --sampling scene       # Around scene changes
consolevideo video.mp4 --sampling adaptive    # Based on motion

# Scene detection threshold (for scene-aware sampling)
consolevideo video.mp4 --sampling scene --scene-threshold 0.3  # More sensitive

# Frame buffering
consolevideo video.mp4 --buffer 5             # Buffer 5 frames ahead (default 3)

# Disable hardware acceleration (if causing issues)
consolevideo video.mp4 --no-hwaccel

# Disable alternate screen buffer
consolevideo video.mp4 --no-alt-screen

# Custom FFmpeg path
consolevideo video.mp4 --ffmpeg-path "C:\tools\ffmpeg\bin"
```

### Real-World Examples

```bash
# Watch a TV episode in ASCII art
consolevideo "Family Guy S18E01 Yacht Rocky.mkv" --blocks -w 120

# Create a GIF from a funny scene
consolevideo "Family Guy S18E01 Yacht Rocky.mkv" -g funny_scene.gif -ss 300 -t 5 --blocks

# Play at double speed for quick preview
consolevideo video.mp4 -s 2.0 -f 2

# Low-bandwidth/resource mode
consolevideo video.mp4 -w 80 -r 10 -f 2 --no-hwaccel

# High-quality braille render
consolevideo video.mp4 -B -w 150 --contrast 2.5

# Convert animated GIF to braille
consolevideo wiggum.gif -B -l 0

# Batch convert images to ASCII GIFs
for img in *.jpg; do consolevideo "$img" -g "${img%.jpg}_ascii.gif" -B; done
```

**Supported formats:**

- **Images:** JPG, JPEG, PNG, BMP, GIF (animated), WebP, TIFF
- **Videos:** MP4, MKV, AVI, MOV, WebM, and any format FFmpeg supports

## Library Usage

### Basic Playback

```csharp
using ConsoleImage.Video.Core;

// Simple playback - FFmpeg downloads automatically if needed
await VideoPlayer.PlayAsync("video.mp4");

// With render mode
var options = new VideoRenderOptions
{
    RenderMode = VideoRenderMode.ColorBlocks
};
await VideoPlayer.PlayAsync("video.mp4", options);
```

### FFmpeg Auto-Download

```csharp
using var ffmpeg = new FFmpegService();

// Shows download progress
await ffmpeg.InitializeAsync(new Progress<(string Status, double Progress)>(p =>
{
    Console.Write($"\r{p.Status} {p.Progress:P0}");
}));
```

**Download sources:**

- Windows/Linux: [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) (GitHub)
- macOS: [evermeet.cx/ffmpeg](https://evermeet.cx/ffmpeg/)

**Cache location:**

- Windows: `%LOCALAPPDATA%\consoleimage\ffmpeg`
- Linux/macOS: `~/.local/share/consoleimage/ffmpeg`

### VideoRenderOptions Reference

```csharp
var options = new VideoRenderOptions
{
    // Render mode
    RenderMode = VideoRenderMode.Ascii,  // Ascii, ColorBlocks, Braille
    RenderOptions = new RenderOptions(), // Base rendering options

    // Playback control
    SpeedMultiplier = 1.0f,   // 0.5 = half speed, 2.0 = double
    LoopCount = 1,            // 0 = infinite
    StartTime = 0,            // Start seconds
    EndTime = null,           // End seconds (null = video end)

    // Performance
    TargetFps = null,         // Override video FPS
    FrameStep = 1,            // 1 = all, 2 = skip every other
    BufferAheadFrames = 5,    // Frame look-ahead buffer
    UseHardwareAcceleration = true,

    // Display
    UseAltScreen = true,      // Alternate screen buffer
    SamplingStrategy = FrameSamplingStrategy.Uniform,
    SceneThreshold = 0.4f     // Scene detection sensitivity
};
```

### Presets

```csharp
var options = VideoRenderOptions.Default;
var options = VideoRenderOptions.LowResource;   // Lower buffer, frame skip
var options = VideoRenderOptions.HighQuality;   // Full frames, larger buffer
var options = VideoRenderOptions.ForTimeRange(30, 60);  // Play 30-60 seconds
```

### Video Information

```csharp
using var ffmpeg = new FFmpegService();
var info = await ffmpeg.GetVideoInfoAsync("video.mp4");

Console.WriteLine($"Duration: {info.Duration}s");
Console.WriteLine($"Resolution: {info.Width}x{info.Height}");
Console.WriteLine($"FPS: {info.FrameRate}");
Console.WriteLine($"Codec: {info.VideoCodec}");
```

### Custom Player Control

```csharp
using var player = new VideoAnimationPlayer(
    "video.mp4",
    options,
    width: 100,
    height: 40);

// With cancellation support
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

await player.PlayAsync(cts.Token);
```

### URL Streaming

```csharp
// FFmpeg handles URL streaming natively
await VideoPlayer.PlayAsync("https://example.com/video.mp4");

// With options
var options = new VideoRenderOptions
{
    RenderMode = VideoRenderMode.ColorBlocks,
    BufferAheadFrames = 10  // Larger buffer for network
};
await VideoPlayer.PlayAsync("https://example.com/video.mp4", options);
```

### Hardware Acceleration

Auto-detected per platform:

- **Windows**: DXVA2, CUDA (NVIDIA)
- **macOS**: VideoToolbox
- **Linux**: VAAPI, CUDA

```csharp
var options = new VideoRenderOptions
{
    UseHardwareAcceleration = true  // default
};

// Disable if causing issues
options.UseHardwareAcceleration = false;
```

## Dependencies

- [mostlylucid.consoleimage](https://www.nuget.org/packages/mostlylucid.consoleimage/) - Core rendering
- [FFmpeg](https://ffmpeg.org/) - Video decoding (auto-downloaded)

## License

Public domain - see [UNLICENSE](https://github.com/scottgal/mostlylucid.consoleimage/blob/master/UNLICENSE)
