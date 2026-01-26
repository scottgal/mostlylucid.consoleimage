# Library API

.NET library API reference for `mostlylucid.consoleimage`.

**See also:** [README](../README.md) | [CLI Guide](CLI-GUIDE.md) | [JSON Format](JSON-FORMAT.md) | [Core Library README](../ConsoleImage.Core/README.md)

## Installation

```bash
dotnet add package mostlylucid.consoleimage
```

Additional packages:

```bash
dotnet add package mostlylucid.consoleimage.video      # Video support (FFmpeg)
dotnet add package mostlylucid.consoleimage.player     # Standalone document playback
dotnet add package mostlylucid.consoleimage.spectre    # Spectre.Console integration
dotnet add package mostlylucid.consoleimage.transcription  # Whisper AI transcription
```

---

## Simple API

```csharp
using ConsoleImage.Core;

// Basic - just works
Console.WriteLine(AsciiArt.Render("photo.jpg"));

// With width
Console.WriteLine(AsciiArt.Render("photo.jpg", 80));

// Colored output
Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));

// For light terminal backgrounds
Console.WriteLine(AsciiArt.RenderForLightBackground("photo.jpg"));

// Play animated GIF
await AsciiArt.PlayGif("animation.gif");
```

---

## Full Options API

```csharp
using ConsoleImage.Core;

// Use presets
var options = RenderOptions.Default;           // Sensible defaults
var options = RenderOptions.HighDetail;        // Maximum detail
var options = RenderOptions.Monochrome;        // No color
var options = RenderOptions.ForLightBackground; // For light terminals
var options = RenderOptions.ForDarkBackground; // Enhanced for dark images
var options = RenderOptions.ForAnimation(loopCount: 3);

// Or customize everything
var options = new RenderOptions
{
    MaxWidth = 100,
    MaxHeight = 50,
    UseColor = true,
    Invert = true,                    // Dark terminals (default)
    ContrastPower = 3.0f,
    DirectionalContrastStrength = 0.3f,
    CharacterSetPreset = "extended",
    UseParallelProcessing = true
};

using var renderer = new AsciiRenderer(options);
var frame = renderer.RenderFile("photo.jpg");
Console.WriteLine(frame.ToAnsiString()); // Colored
Console.WriteLine(frame.ToString());      // Plain
```

---

## High-Fidelity Color Blocks

```csharp
using ConsoleImage.Core;

// Enable ANSI support on Windows (call once at startup)
ConsoleHelper.EnableAnsiSupport();

// Uses Unicode half-blocks for 2x vertical resolution
using var renderer = new ColorBlockRenderer(options);
string output = renderer.RenderFile("photo.jpg");
Console.WriteLine(output);

// For animated GIFs
var frames = renderer.RenderGif("animation.gif");
foreach (var frame in frames)
{
    Console.WriteLine(frame.Content);
    Thread.Sleep(frame.DelayMs);
}
```

---

## ANSI Support on Windows

For colored output and animations to work correctly on Windows, you may need to enable virtual terminal processing:

```csharp
using ConsoleImage.Core;

// Call once at application startup
ConsoleHelper.EnableAnsiSupport();

// Now ANSI colors and cursor control will work
Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));
```

Modern terminals like Windows Terminal have this enabled by default, but older consoles (cmd.exe, older PowerShell) may need this call.

---

## Spectre.Console Integration

Install the dedicated Spectre.Console package for native `IRenderable` support:

```bash
dotnet add package mostlylucid.consoleimage.spectre
```

```csharp
using ConsoleImage.Core;
using ConsoleImage.Spectre;
using Spectre.Console;

// Static images as native Spectre renderables
AnsiConsole.Write(new AsciiImage("photo.png"));
AnsiConsole.Write(new ColorBlockImage("photo.png"));  // High-fidelity
AnsiConsole.Write(new BrailleImage("photo.png"));     // Ultra-high res

// Use in any Spectre layout
AnsiConsole.Write(new Panel(new AsciiImage("photo.png"))
    .Header("My Image")
    .Border(BoxBorder.Rounded));

// Side-by-side images
AnsiConsole.Write(new Columns(
    new Panel(new AsciiImage("a.png")).Header("Image A"),
    new Panel(new AsciiImage("b.png")).Header("Image B")
));

// Animated GIFs with Live display
var animation = new AnimatedImage("clip.gif", AnimationMode.ColorBlock);
await animation.PlayAsync(cancellationToken);

// Side-by-side animations
var anim1 = new AnimatedImage("a.gif");
var anim2 = new AnimatedImage("b.gif");
await AnsiConsole.Live(new Columns(anim1, anim2))
    .StartAsync(async ctx => {
        while (!token.IsCancellationRequested) {
            anim1.TryAdvanceFrame();
            anim2.TryAdvanceFrame();
            ctx.Refresh();
            await Task.Delay(16);
        }
    });
```

### Without the Spectre package

The core library output is also compatible with Spectre.Console directly:

```csharp
using Spectre.Console;
using ConsoleImage.Core;

// Spectre.Console handles ANSI escape codes automatically
AnsiConsole.Write(new Text(AsciiArt.RenderColored("photo.jpg")));
```

---

## Animated GIFs

```csharp
using ConsoleImage.Core;

// Simple playback (infinite loop by default)
await AsciiArt.PlayGif("animation.gif");

// With options
var options = new RenderOptions
{
    LoopCount = 3,
    AnimationSpeedMultiplier = 1.5f,
    UseColor = true
};
await AsciiArt.PlayGif("animation.gif", options);

// Manual frame control
using var renderer = new AsciiRenderer(options);
var frames = renderer.RenderGif("animation.gif");

using var player = new AsciiAnimationPlayer(frames, useColor: true, loopCount: 0);
await player.PlayAsync(cancellationToken);
```

---

## Configuration from appsettings.json

```json
{
  "AsciiRenderer": {
    "MaxWidth": 120,
    "MaxHeight": 60,
    "ContrastPower": 2.5,
    "DirectionalContrastStrength": 0.3,
    "UseColor": true,
    "Invert": true,
    "CharacterSetPreset": "default"
  }
}
```

```csharp
var config = builder.Configuration.GetSection("AsciiRenderer").Get<RenderOptions>();
Console.WriteLine(AsciiArt.FromFile("photo.jpg", config));
```

---

## Video Playback

```bash
dotnet add package mostlylucid.consoleimage.video
```

### Simple Playback

```csharp
using ConsoleImage.Video.Core;

// One-liner: play a video in your terminal
await VideoPlayer.PlayAsync("movie.mp4");

// With options
var options = new VideoRenderOptions
{
    RenderOptions = new RenderOptions { MaxWidth = 80, UseColor = true },
    StartTime = 60.0,      // Start at 1 minute
    EndTime = 90.0,        // Play 30 seconds
    SpeedMultiplier = 1.5f, // 1.5x speed
    LoopCount = 2,
    ShowStatus = true
};
await VideoPlayer.PlayAsync("movie.mp4", options);
```

### Video Information

```csharp
var info = await VideoPlayer.GetInfoAsync("movie.mp4");
Console.WriteLine($"{info.Width}x{info.Height} @ {info.FrameRate}fps, {info.Duration:F1}s");
Console.WriteLine($"Codec: {info.VideoCodec}, Bitrate: {info.BitRate}");
```

### FFmpeg Service (Advanced)

For direct access to frame extraction, scene detection, and subtitle streams:

```csharp
using ConsoleImage.Video.Core;

var ffmpeg = new FFmpegService();
await ffmpeg.InitializeAsync(); // Downloads FFmpeg if needed

// Extract a single frame
var frame = await ffmpeg.ExtractFrameAsync("movie.mp4", timestamp: 30.0, width: 320);

// Stream frames (efficient pipe-based, no temp files)
await foreach (var image in ffmpeg.StreamFramesAsync("movie.mp4", 320, 240, startTime: 10))
{
    // Process each frame as ImageSharp Image<Rgba32>
}

// Detect scene changes
var scenes = await ffmpeg.DetectSceneChangesAsync("movie.mp4", threshold: 0.4);

// Get keyframes (very fast, codec metadata only)
var keyframes = await ffmpeg.GetKeyframesAsync("movie.mp4");

// Extract embedded subtitles
var streams = await ffmpeg.GetSubtitleStreamsAsync("movie.mp4");
foreach (var sub in streams.Where(s => s.IsTextBased))
    Console.WriteLine($"  [{sub.Index}] {sub.Language} ({sub.Codec})");

await ffmpeg.ExtractSubtitlesAsync("movie.mp4", "subs.srt", streamIndex: 0);
```

### VideoRenderOptions Presets

```csharp
var opts = VideoRenderOptions.Default;       // Sensible defaults
var opts = VideoRenderOptions.LowResource;   // Minimal CPU usage
var opts = VideoRenderOptions.HighQuality;   // Maximum fidelity
var opts = VideoRenderOptions.ForTimeRange(60, 90); // Play 60s-90s

// Fluent modification
var custom = VideoRenderOptions.Default.With(o => {
    o.SpeedMultiplier = 2.0f;
    o.ShowStatus = true;
    o.RenderOptions.MaxWidth = 120;
});
```

### Video with Subtitles

```csharp
using ConsoleImage.Core.Subtitles;
using ConsoleImage.Video.Core;

// Load subtitles from file
var track = SubtitleParser.ParseFile("movie.srt");

var options = new VideoRenderOptions
{
    Subtitles = track,
    ShowStatus = true
};
await VideoPlayer.PlayAsync("movie.mp4", options);
```

---

## Whisper AI Transcription

```bash
dotnet add package mostlylucid.consoleimage.transcription
```

### Batch Transcription

Transcribe an entire audio/video file at once:

```csharp
using ConsoleImage.Transcription;

var whisper = new WhisperTranscriptionService()
    .WithModel("base")     // tiny, base, small, medium, large
    .WithLanguage("en")    // or "auto" for detection
    .WithThreads(4);

// Initialize (downloads model + runtime on first use)
await whisper.InitializeAsync(new Progress<(long downloaded, long total, string status)>(p =>
    Console.Write($"\r{p.status} {p.downloaded}/{p.total}")
));

// Transcribe
var result = await whisper.TranscribeFileAsync("audio.wav");

Console.WriteLine($"Language: {result.Language}");
Console.WriteLine($"Duration: {result.AudioDurationSeconds:F1}s");
Console.WriteLine($"Segments: {result.Segments.Count}");
Console.WriteLine(result.FullText);

// Access individual segments
foreach (var seg in result.Segments)
    Console.WriteLine($"[{seg.StartTime:mm\\:ss} --> {seg.EndTime:mm\\:ss}] {seg.Text}");
```

### Save as SRT/VTT

```csharp
using ConsoleImage.Transcription;

// Save to subtitle file
await SrtFormatter.WriteAsync("output.srt", result.Segments);
await VttFormatter.WriteAsync("output.vtt", result.Segments);

// Or get as string
string srt = SrtFormatter.Format(result.Segments);
string vtt = VttFormatter.Format(result.Segments);
```

### Streaming Transcription (Live Subtitles)

For real-time subtitle generation during video playback:

```csharp
using ConsoleImage.Transcription;
using ConsoleImage.Video.Core;

// ChunkedTranscriber processes video audio in chunks
// and implements ILiveSubtitleProvider for integration with VideoAnimationPlayer
var transcriber = new ChunkedTranscriber(
    inputPath: "movie.mp4",
    modelSize: "base",
    language: "en",
    chunkDurationSeconds: 30.0,
    bufferAheadSeconds: 60.0,
    enhanceAudio: true  // FFmpeg noise reduction + normalization
);

// Listen for new subtitles
transcriber.OnSubtitleReady += entry =>
    Console.WriteLine($"[{entry.Start:mm\\:ss}] {entry.Text}");

// Start (downloads model if needed, begins background transcription)
await transcriber.StartAsync();
transcriber.StartBackgroundTranscription();

// Use with video player
var options = new VideoRenderOptions
{
    LiveSubtitleProvider = transcriber, // Subtitles appear as they're transcribed
    ShowStatus = true
};
await VideoPlayer.PlayAsync("movie.mp4", options);

// Or transcribe everything at once (batch mode)
await transcriber.TranscribeAllAsync();
string vtt = transcriber.ToVtt();
await transcriber.SaveAsync("movie.vtt");

await transcriber.DisposeAsync();
```

### Model Management

```csharp
using ConsoleImage.Transcription;

// Check if transcription is available
if (!WhisperTranscriptionService.IsAvailable())
    Console.WriteLine("Whisper not available");

// Check if model is cached
bool cached = WhisperModelDownloader.IsModelCached("base");

// Get model info (for download prompts)
var (fileName, sizeMB) = WhisperModelDownloader.GetModelInfo("small");
Console.WriteLine($"Model: {fileName} ({sizeMB}MB)");

// Pre-download model
string modelPath = await WhisperModelDownloader.EnsureModelAsync("base");
```

### Whisper Models

| Model | Size | Speed | Accuracy | Best For |
|-------|------|-------|----------|----------|
| `tiny` | 75MB | Fastest | Basic | Quick previews, testing |
| `base` | 142MB | Fast | Good | **Default** - General use |
| `small` | 466MB | Medium | Better | Important transcriptions |
| `medium` | 1.5GB | Slow | High | Professional quality |
| `large` | 3GB | Slowest | Best | Maximum accuracy |

---

## Character Set Presets

| Preset | Characters | Use Case |
|--------|------------|----------|
| `extended` | 91 ASCII chars | **Default** - Maximum detail |
| `simple` | ` .:-=+*#%@` | Quick renders |
| `block` | ` ` | High density blocks |
| `classic` | 71 ASCII chars | Original algorithm set |
