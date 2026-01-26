# mostlylucid.consoleimage.video

Real-time video to ASCII art rendering using FFmpeg for .NET 10.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.video.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage.video/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

> **[Full documentation and examples on GitHub](https://github.com/scottgal/mostlylucid.consoleimage)**

## Video Render Modes

|                                                     ASCII                                                     |                                                   ColorBlocks                                                   |                                                      Braille                                                      |
|:-------------------------------------------------------------------------------------------------------------:|:---------------------------------------------------------------------------------------------------------------:|:-----------------------------------------------------------------------------------------------------------------:|
| ![ASCII](https://raw.githubusercontent.com/scottgal/mostlylucid.consoleimage/master/samples/wiggum_ascii.gif) | ![Blocks](https://raw.githubusercontent.com/scottgal/mostlylucid.consoleimage/master/samples/wiggum_blocks.gif) | ![Braille](https://raw.githubusercontent.com/scottgal/mostlylucid.consoleimage/master/samples/wiggum_braille.gif) |
|                                           Shape-matched characters                                            |                                          Unicode half-blocks (2x res)                                           |                                          2x4 dot patterns (highest res)                                           |

## Features

- **Zero setup** - FFmpeg auto-downloads on first use
- **Hardware acceleration** - CUDA, DXVA2, VideoToolbox, VAAPI
- **URL streaming** - Play from HTTP/HTTPS sources
- **Time range playback** - Start/end time control
- **Multiple render modes** - ASCII, ColorBlocks, Braille
- **Handles images too** - JPG, PNG, GIF, WebP all supported

## Quick Start

```csharp
using ConsoleImage.Video.Core;

// Simple one-liner - FFmpeg auto-downloads if needed
await VideoPlayer.PlayAsync("video.mp4");

// With render mode
await VideoPlayer.PlayAsync("video.mp4", new VideoRenderOptions
{
    RenderMode = VideoRenderMode.ColorBlocks
});

// Stream from URL
await VideoPlayer.PlayAsync("https://example.com/video.mp4");

// With cancellation support
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
await VideoPlayer.PlayAsync("video.mp4", cancellationToken: cts.Token);
```

## VideoRenderOptions

```csharp
var options = new VideoRenderOptions
{
    // Render mode
    RenderMode = VideoRenderMode.Braille,  // Ascii, ColorBlocks, Braille

    // Playback control
    SpeedMultiplier = 1.5f,     // 0.5 = half, 2.0 = double speed
    LoopCount = 3,              // 0 = infinite
    StartTime = 30,             // Start at 30 seconds
    EndTime = 60,               // End at 60 seconds

    // Performance
    TargetFps = 24,             // Override video FPS
    FrameStep = 2,              // Skip every other frame
    UseHardwareAcceleration = true,
    BufferAheadFrames = 5,

    // Display
    ShowStatus = true,          // Show progress/info line
    UseAltScreen = true         // Use alternate screen buffer
};

await VideoPlayer.PlayAsync("video.mp4", options);
```

## Video Information

```csharp
var info = await VideoPlayer.GetInfoAsync("video.mp4");

Console.WriteLine($"Duration: {info.Duration}s");
Console.WriteLine($"Resolution: {info.Width}x{info.Height}");
Console.WriteLine($"FPS: {info.FrameRate}");
Console.WriteLine($"Codec: {info.VideoCodec}");
```

## Advanced: Direct Player Control

For more control, use `VideoAnimationPlayer` directly:

```csharp
using var player = new VideoAnimationPlayer("video.mp4", options);
await player.PlayAsync(cancellationToken);
```

## FFmpeg Auto-Download

FFmpeg downloads automatically on first use:

- **Windows/Linux**: From [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds)
- **macOS**: From [evermeet.cx/ffmpeg](https://evermeet.cx/ffmpeg/)

**Cache location:**

- Windows: `%LOCALAPPDATA%\consoleimage\ffmpeg`
- Linux/macOS: `~/.local/share/consoleimage/ffmpeg`

## Hardware Acceleration

Auto-detected per platform:

- **Windows**: DXVA2, CUDA (NVIDIA)
- **macOS**: VideoToolbox
- **Linux**: VAAPI, CUDA

## Supported Formats

- **Video**: MP4, MKV, AVI, MOV, WebM, and any FFmpeg-supported format
- **Images**: JPG, PNG, GIF (animated), WebP, BMP, TIFF

## Related Packages

- [mostlylucid.consoleimage](https://www.nuget.org/packages/mostlylucid.consoleimage/) - Core image rendering (
  dependency)
- [mostlylucid.consoleimage.spectre](https://www.nuget.org/packages/mostlylucid.consoleimage.spectre/) - Spectre.Console
  integration

## License

Public domain - [UNLICENSE](https://github.com/scottgal/mostlylucid.consoleimage/blob/master/UNLICENSE)
