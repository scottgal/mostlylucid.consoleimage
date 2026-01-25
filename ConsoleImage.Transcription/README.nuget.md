# ConsoleImage.Transcription

Whisper-based audio transcription for ConsoleImage. Auto-generates subtitles from video/audio files using OpenAI Whisper models.

## Features

- **Whisper.NET integration** - Local speech-to-text using OpenAI Whisper models
- **Auto model download** - Downloads Whisper models on first use
- **Multiple formats** - Outputs SRT or VTT subtitle files
- **Speaker diarization** - Distinguishes different speakers with color coding
- **Time-limited transcription** - Process specific portions of audio
- **Cross-platform** - Windows, Linux, macOS support

## Installation

```bash
# Install the transcription library
dotnet add package mostlylucid.consoleimage.transcription

# Install a Whisper runtime for your platform (choose one):
dotnet add package Whisper.net.AllRuntimes    # All platforms (~400MB)
dotnet add package Whisper.net.Runtime.Cpu    # CPU only (smaller)
dotnet add package Whisper.net.Runtime.Cuda   # NVIDIA GPU acceleration
dotnet add package Whisper.net.Runtime.CoreML # Apple Silicon acceleration
```

## Usage

```csharp
using ConsoleImage.Transcription;

// Create transcriber
var transcriber = new WhisperTranscriber();

// Transcribe audio file
var track = await transcriber.TranscribeAsync(
    "video.mp4",
    modelSize: "base",  // tiny, base, small, medium, large
    language: "en",
    ct: cancellationToken);

// Access subtitles
foreach (var entry in track.Entries)
{
    Console.WriteLine($"[{entry.StartTime} --> {entry.EndTime}] {entry.Text}");
}
```

## CLI Usage

When used with the `consoleimage` CLI:

```bash
# Auto-transcribe video with Whisper
consoleimage movie.mp4 --subs whisper

# Specify model size
consoleimage movie.mp4 --subs whisper --whisper-model small

# Transcribe specific time range
consoleimage movie.mp4 --subs whisper -ss 60 -t 30

# YouTube with auto-transcription
consoleimage "https://youtu.be/VIDEO_ID" --subs whisper
```

## Model Sizes

| Model | VRAM | Speed | Accuracy |
|-------|------|-------|----------|
| tiny | ~1GB | Fast | Basic |
| base | ~1GB | Fast | Good |
| small | ~2GB | Medium | Better |
| medium | ~5GB | Slow | Great |
| large | ~10GB | Slowest | Best |

Models are downloaded automatically on first use to `~/.cache/whisper/`.

## Requirements

- .NET 10.0+
- Whisper.NET native binaries (included via Whisper.net.AllRuntimes)
- NAudio for audio processing

## License

Unlicense - Public Domain
