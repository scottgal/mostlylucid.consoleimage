# mostlylucid.consoleimage.video

Real-time video to ASCII art rendering using FFmpeg for .NET 10.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.video.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage.video/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

| ASCII Mode | ColorBlocks Mode | Braille Mode |
|------------|------------------|--------------|
| <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/familyguy_ascii.gif" width="200" alt="ASCII"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/familyguy_blocks.gif" width="200" alt="Blocks"> | <img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/familyguy_braille.gif" width="200" alt="Braille"> |

## Features

- **Real-time video playback** in terminal
- **Zero setup** - FFmpeg auto-downloads on first use
- **Hardware acceleration** (CUDA, DXVA2, VideoToolbox, VAAPI)
- **URL streaming** - play from HTTP/HTTPS
- **Time range playback** - start/end times
- **Multiple render modes** - ASCII, ColorBlocks, Braille

## Quick Start

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

## FFmpeg Auto-Download

FFmpeg is automatically downloaded on first use:

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

## Render Modes

### ASCII Mode

<img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/familyguy_ascii.gif" width="350" alt="ASCII Video">

```csharp
var options = new VideoRenderOptions
{
    RenderMode = VideoRenderMode.Ascii
};
```

### ColorBlocks Mode

<img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/familyguy_blocks.gif" width="350" alt="ColorBlocks Video">

```csharp
var options = new VideoRenderOptions
{
    RenderMode = VideoRenderMode.ColorBlocks
};
```

### Braille Mode

<img src="https://github.com/scottgal/mostlylucid.consoleimage/raw/master/familyguy_braille.gif" width="350" alt="Braille Video">

```csharp
var options = new VideoRenderOptions
{
    RenderMode = VideoRenderMode.Braille
};
```

## VideoRenderOptions Reference

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

## Presets

```csharp
var options = VideoRenderOptions.Default;
var options = VideoRenderOptions.LowResource;   // Lower buffer, frame skip
var options = VideoRenderOptions.HighQuality;   // Full frames, larger buffer
var options = VideoRenderOptions.ForTimeRange(30, 60);  // Play 30-60 seconds
```

## Frame Sampling Strategies

```csharp
options.SamplingStrategy = FrameSamplingStrategy.Uniform;    // Even distribution
options.SamplingStrategy = FrameSamplingStrategy.Keyframe;   // Prefer I-frames
options.SamplingStrategy = FrameSamplingStrategy.SceneAware; // Around scene changes
options.SamplingStrategy = FrameSamplingStrategy.Adaptive;   // Based on motion
```

## Video Information

```csharp
using var ffmpeg = new FFmpegService();
var info = await ffmpeg.GetVideoInfoAsync("video.mp4");

Console.WriteLine($"Duration: {info.Duration}s");
Console.WriteLine($"Resolution: {info.Width}x{info.Height}");
Console.WriteLine($"FPS: {info.FrameRate}");
Console.WriteLine($"Codec: {info.VideoCodec}");
```

## Custom Player Control

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

## URL Streaming

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

## Hardware Acceleration

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

## Custom FFmpeg Path

```csharp
// Specify custom FFmpeg location
var customPath = await FFmpegProvider.GetFFmpegPathAsync(
    customPath: @"C:\tools\ffmpeg\bin");
```

## Dependencies

- [mostlylucid.consoleimage](https://www.nuget.org/packages/mostlylucid.consoleimage/) - Core rendering
- [FFmpeg](https://ffmpeg.org/) - Video decoding (auto-downloaded)

## License

Public domain - see [UNLICENSE](https://github.com/scottgal/mostlylucid.consoleimage/blob/master/UNLICENSE)
