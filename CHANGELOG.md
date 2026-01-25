# Changelog

All notable changes to this project will be documented in this file.

## [4.0.0] - 2025-01-25

### Major Features

#### 🎤 Live Transcription with Whisper AI
- **Real-time subtitle generation** - `--subs whisper` streams subtitles while watching videos
- **Chunked audio processing** - Audio extracted and transcribed in 15-second chunks
- **Background transcription** - Runs ahead of playback, buffers 30 seconds ahead
- **Sync-waiting** - Playback pauses briefly with "⏳ Transcribing..." if it catches up
- **Auto-caching** - Subtitles automatically saved as `.vtt` files for instant replay
- **Start time support** - `--ss` works with transcription, starts from that position

```bash
# Live transcription during playback
consoleimage movie.mp4 --subs whisper

# With start time (efficient - only transcribes from start position)
consoleimage movie.mp4 --subs whisper --ss 3600

# YouTube with live transcription
consoleimage "https://youtu.be/VIDEO_ID" --subs whisper

# Choose model size for speed vs accuracy
consoleimage movie.mp4 --subs whisper --whisper-model tiny   # Fastest
consoleimage movie.mp4 --subs whisper --whisper-model small  # Better accuracy
```

#### 📝 Transcript-Only Mode (No Video)
- **`--transcript` flag** - Generate subtitles without rendering video
- **Streaming output** - Streams text to stdout as it's transcribed (for piping)
- **`transcribe` subcommand** - Enhanced with `--stream` and `--quiet` options

```bash
# Stream transcript to stdout (for piping to other tools)
consoleimage movie.mp4 --transcript
consoleimage https://youtu.be/VIDEO_ID --transcript

# Save VTT file with transcribe subcommand
consoleimage transcribe movie.mp4 -o output.vtt

# Stream AND save
consoleimage transcribe movie.mp4 -o output.vtt --stream

# Quiet mode (only output text, no progress)
consoleimage transcribe movie.mp4 --stream --quiet
```

Output format for streaming:
```
[00:00:01.500 --> 00:00:04.200] Hello, welcome to the video.
[00:00:04.500 --> 00:00:07.800] Today we'll be discussing...
```

#### Unified Subtitle CLI
- **`--subs <source>`** - Unified flag for all subtitle sources:
  - `--subs <path>` - Load from SRT/VTT file
  - `--subs auto` - Try YouTube, fall back to Whisper
  - `--subs whisper` - Real-time Whisper transcription
  - `--subs off` - Disable subtitles

#### Speaker Diarization Support (Infrastructure)
- **Color-coded speakers** - Infrastructure ready for different speakers in different colors
- **8 distinct colors** - White, yellow, cyan, pink, green, purple, orange, blue
- **Speaker labels** - "Speaker 1:", "Speaker 2:" prefixes when diarization data available
- **Note:** Requires pyannote integration (not yet implemented) - `--diarize` flag reserved for future use

#### YouTube Start Time
- **Efficient seeking** - `--ss` with YouTube URLs seeks via FFmpeg
- **Works with transcription** - Transcription starts from `--ss` position

```bash
# Start 1 hour into a YouTube video
consoleimage "https://youtu.be/VIDEO_ID" --ss 3600 --subs whisper
```

### New Classes

- **`ChunkedTranscriber`** - Streaming transcription with ahead-of-playback buffering
- **`ILiveSubtitleProvider`** - Interface for live subtitle providers (decouples transcription from video)

### Improvements

#### Transcription Progress Feedback
- **Visual progress** - Shows "Extracting audio at 01:00:00..." and "Transcribing 01:00:00 - 01:00:15..."
- **No more "stuck"** - Clear feedback during initial buffering phase
- **OnProgress event** - Hook for custom progress UI

#### CIDZ Streaming Fix
- **Memory efficient** - Long videos no longer buffer all frames before compression
- **Stream-then-compress** - Frames written to temp NDJSON, then compressed to .cidz
- **Automatic cleanup** - Temp files removed after compression

### New CLI Options

| Option | Description | Default |
|--------|-------------|---------|
| `--whisper` | Enable live transcription during playback | OFF |
| `--whisper-model` | Model size: tiny, base, small, medium, large | base |
| `--whisper-threads` | CPU threads for transcription | Auto |

### Bug Fixes

- **Fixed initial buffer hang** - Reduced initial buffer from 90s to 15s (one chunk)
- **Fixed --ss with transcription** - Start time now properly passed to ChunkedTranscriber
- **Fixed speaker color assignment** - Colors now properly cycle through 8-color palette

---

## [3.1.0] - 2025-01-24

### Major Features

#### Slideshow Mode
- **Directory and glob pattern support** - Pass a directory or glob pattern to browse multiple images
- **Interactive keyboard controls** - Space (pause), arrows (navigate), Q (quit), Home/End (jump)
- **Background pre-rendering** - Next 2 slides cached for instant, silky-smooth transitions
- **Multiple sort options** - Sort by name, date, size, or random with `--sort`
- **Manual navigation mode** - `--slide-delay 0` disables auto-advance for manual-only browsing
- **Recursive scanning** - `-R` includes subdirectories
- **Shuffle mode** - `--shuffle` randomizes playback order
- **GIF output** - Export slideshow as animated GIF with `--output slideshow.gif`

```bash
# Browse photos (newest first by default)
consoleimage ./photos

# Glob pattern with shuffle
consoleimage "C:\Pictures\*.jpg" --shuffle

# Manual navigation only (no auto-advance)
consoleimage ./vacation --slide-delay 0

# Recursive with custom delay
consoleimage ./albums -R --slide-delay 5

# Export slideshow to GIF
consoleimage ./photos -o slideshow.gif --slide-delay 2
```

**Slideshow Controls:**
| Key | Action |
|-----|--------|
| `Space` | Pause/resume auto-advance |
| `←` `→` | Previous/next image |
| `↑` `↓` | Previous/next (alternate) |
| `Home` `End` | Jump to first/last |
| `N` `P` | Next/previous (vim-style) |
| `Q` `Esc` | Quit slideshow |

#### YouTube Support
- **Direct YouTube playback** - Pass any YouTube URL to play as ASCII art
- **yt-dlp auto-download** - Offers to download yt-dlp (~10MB) if not installed
- **Custom yt-dlp path** - `--ytdlp-path` to specify location
- **All URL formats supported** - youtube.com/watch, youtu.be, youtube.com/shorts
- **Direct stream extraction** - Prefers MP4 streams over HLS for reliable playback

```bash
# Play YouTube video
consoleimage "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
consoleimage "https://youtu.be/dQw4w9WgXcQ"

# YouTube Shorts
consoleimage "https://youtube.com/shorts/abc123"

# With render options
consoleimage "https://youtu.be/xyz" -w 80 --blocks

# Output YouTube to GIF
consoleimage "https://youtu.be/xyz" -o output.gif -t 10

# Specify yt-dlp path
consoleimage "https://youtu.be/xyz" --ytdlp-path /path/to/yt-dlp

# Auto-confirm yt-dlp download
consoleimage "https://youtu.be/xyz" -y
```

### New Classes

- **`SlideshowHandler`** - Full slideshow implementation with caching and keyboard control
- **`YtdlpProvider`** - YouTube support via yt-dlp with auto-download capability
- **`SlideshowOptions`** - Configuration for slideshow mode

### MCP Server Updates

New tools added to the MCP server for AI assistants:

| Tool | Description |
|------|-------------|
| `render_video` | Render video files to animated ASCII art GIFs |
| `check_youtube_url` | Check if a URL is a YouTube video |
| `get_youtube_stream` | Extract direct stream URL from YouTube videos |

### Improvements

#### FFmpeg Hardware Acceleration
- **Extended codec blocklist** - Added mjpeg, prores, dnxhd, huffyuv, ffv1, rawvideo to prevent CUDA errors
- **Better error handling** - Automatic fallback to software decoding when hardware fails

#### URL Handling
- **Fixed URL detection** - URLs with `?` (like YouTube) no longer incorrectly trigger slideshow mode

### New CLI Options

#### Slideshow Options
| Option | Description | Default |
|--------|-------------|---------|
| `-d, --slide-delay` | Seconds between slides (0 = manual only) | 3 |
| `--shuffle` | Randomize playback order | OFF |
| `-R, --recursive` | Include subdirectories | OFF |
| `--sort` | Sort by: name, date, size, random | date |
| `--desc` | Sort descending (newest first) | ON |
| `--asc` | Sort ascending (oldest first) | OFF |
| `--video-preview` | Max video preview duration (seconds) | 30 |
| `--gif-loop` | Loop GIFs continuously in slideshow | OFF |
| `--hide-info` | Hide [1/N] filename header | OFF |

#### YouTube Options
| Option | Description |
|--------|-------------|
| `--ytdlp-path` | Path to yt-dlp executable |
| `-y, --yes` | Auto-confirm yt-dlp download |

### Bug Fixes

- **Fixed YouTube URL detection** - URLs containing `?` no longer treated as glob patterns
- **Fixed HLS stream issues** - YouTube format selector now prefers direct MP4 over HLS manifests
- **Fixed CUDA decoder errors** - MJPEG and other problematic codecs fall back to software decoding

---

## [3.0.0] - 2025-01-24

### Breaking Changes

#### Braille is Now the Default Render Mode
- **Braille mode (`-B`) is now the default** - Highest resolution output (2×4 dots per cell = 8x detail)
- **`--ascii` / `-a` option added** - Use this for classic ASCII character rendering
- **Default video width is 50** for braille mode - Braille is more CPU intensive, so we default to a smaller width for smooth video playback

**Migration:**
```bash
# Old default (ASCII) - now requires -a flag
consoleimage photo.jpg -a

# New default (Braille) - just works
consoleimage photo.jpg

# Braille explicitly - same as before
consoleimage photo.jpg -B
```

### New Features

#### Mode Switching
- **`-a, --ascii`** - Switch to classic ASCII character rendering
- **Mutually exclusive modes** - `-a`, `-b`, `-B`, and `-M` now properly override each other
- **Smart defaults** - Braille for maximum detail, ASCII available when needed

#### Easter Egg
- **Star Wars ASCII animation** - Run `consoleimage` with no arguments to see a surprise!

#### Raw Frame Extraction
- **Multiple output formats** - `--raw` now supports GIF, WebP, PNG/JPG sequence, or video output
- **`-q, --quality`** - Output quality control (1-100, default: 85)
- **Smart keyframes** - `--smart-keyframes` uses scene detection for representative frames
- **Video re-encoding** - Output to MP4/WebM/MKV/AVI for re-encoded video clips

```bash
consoleimage movie.mp4 --raw -o frames.gif          # Animated GIF
consoleimage movie.mp4 --raw -o frames.webp -q 90   # Animated WebP
consoleimage movie.mp4 --raw -o clip.mp4 --ss 30 -t 5  # Video clip
consoleimage movie.mp4 --raw -o frame.png --gif-frames 10  # Image sequence
```

#### MCP Server
- **`extract_frames` tool** - Extract raw video frames to GIF via MCP API

### Improvements

#### Braille Rendering
- **Temporal stability (de-jitter)** - `--dejitter` reduces color flickering between frames
- **Color quantization** - `--color-threshold` fine-tunes stability (0-255, default: 15)
- **Cell-level delta rendering** - Only update changed cells for smoother video playback

#### Codec Compatibility
- **MPEG-4 Part 2 support** - Added to hardware acceleration blocklist for compatibility
- **AV1 support** - Added to hardware acceleration blocklist for compatibility
- **Better fallback** - Automatic software decoding when hardware acceleration fails

### Bug Fixes

- **Fixed hwdownload errors** - MPEG-4 and AV1 codecs now fall back to software decoding
- **Fixed British spelling** - `--colours` alias added for `--colors` option

### CLI Changes

| Change | Old | New |
|--------|-----|-----|
| Default mode | ASCII | Braille |
| ASCII mode | (default) | `-a, --ascii` |
| Video width (braille) | Console width | 50 |

---

## [2.7.0] - 2025-01-24

### Major Features

#### Unified CLI
- **Single CLI for everything** - `consoleimage` now handles images, GIFs, videos, AND document playback
- **No separate `consolevideo`** - All functionality merged into the unified CLI
- **FFmpeg-style time options** - Use `--ss` for start time, `-t` for duration on videos

```bash
# Images, GIFs, videos, documents - all with one CLI
consoleimage photo.jpg
consoleimage animation.gif
consoleimage movie.mp4 --ss 60 -t 30
consoleimage saved.cidz
```

#### Compressed Document Format (.cidz)
- **Delta encoding** - Only changed cells stored between frames (~7:1 compression)
- **Global color palette** - Colors stored once, referenced by index
- **Keyframe interval** - Full frames every N frames (default: 30) for seeking
- **Loop count in metadata** - Frames NOT duplicated for loops

```bash
# Save as compressed document
consoleimage animation.gif -o output.cidz
consoleimage movie.mp4 -o movie.cidz --blocks

# Play documents (no original source needed)
consoleimage output.cidz --speed 1.5

# Convert document to GIF
consoleimage movie.cidz -o movie.gif
```

#### Status Line
- **Progress indicator** - `-S, --status` shows real-time playback info
- **File info** - Displays filename, resolution (source → output), render mode
- **Frame counter** - Shows current frame / total frames for animations
- **Time position** - Shows elapsed time / total duration for videos

```bash
consoleimage animation.gif --status
consoleimage movie.mp4 -S -w 120
```

#### Temporal Stability (De-jitter)
- **Reduces color flickering** - `--dejitter` stabilizes similar colors between frames
- **Configurable threshold** - `--color-threshold` sets similarity (0-255, default: 15)
- **Better compression** - More stable colors = smaller delta frames

```bash
consoleimage animation.gif -o output.cidz --dejitter
consoleimage animation.gif -o output.cidz --dejitter --color-threshold 20
```

### New Packages

#### ConsoleImage.Player (`mostlylucid.consoleimage.player`)
- **Standalone playback** - Play .cidz documents without ImageSharp or FFmpeg
- **Perfect for embedding** - Add animated startup logos with minimal dependencies
- **Spectre.Console variant** - `mostlylucid.consoleimage.player.spectre` for native integration

```csharp
// One-liner document playback
var player = await ConsolePlayer.FromFileAsync("logo.cidz");
await player.PlayAsync(loopCount: 1);
```

### Improvements

#### Braille Rendering
- **Better full-color mode** - Improved hybrid algorithm for edge-preserving dot selection
- **Smart dot removal** - Removes darkest dots on dark terminals (blend with background)
- **Edge detail** - Shows edges by WHICH dots are removed, not by creating holes

#### Build & Release
- **Single release workflow** - Builds consoleimage and consoleimage-mcp for all platforms
- **Updated GitHub Actions** - Uses actions/checkout@v6, actions/setup-dotnet@v5
- **Fixed solution file** - Added Player projects, removed obsolete entries

### Bug Fixes

- **Fixed braille solarization** - Colors now sampled from visible dots only
- **Fixed animation extra lines** - Corrected cursor positioning math
- **Fixed color artifacts** - Proper ANSI reset at end of each line

### New CLI Options

| Option | Description | Default |
|--------|-------------|---------|
| `-S, --status` | Show status line with progress | OFF |
| `--dejitter, --stabilize` | Enable temporal stability | OFF |
| `--color-threshold` | De-jitter color threshold (0-255) | 15 |
| `--ss, --start` | Start time in seconds (videos) | 0 |
| `-t, --duration` | Duration to play (videos) | Full |

---

## [2.6.0] - 2025-01-22

### Major Features

#### MCP Server for AI Tool Integration
- **`consoleimage-mcp`** - Model Context Protocol server exposes rendering as AI tools
- **Works with Claude Desktop, VS Code** - Add to MCP config for instant image rendering
- **AOT compiled** - Fast startup, cross-platform binaries available
- **7 tools available**:
  - `render_image` - Render image/GIF to ASCII art (all 4 modes)
  - `render_to_gif` - Create animated GIF output
  - `get_gif_info` - Get GIF metadata (dimensions, frame count)
  - `get_video_info` - Get video file info via FFmpeg
  - `list_render_modes` - List available render modes with descriptions
  - `list_matrix_presets` - List Matrix digital rain color presets
  - `compare_render_modes` - Render same image in all modes for comparison

```json
// Claude Desktop config (claude_desktop_config.json)
{
  "mcpServers": {
    "consoleimage": {
      "command": "path/to/consoleimage-mcp"
    }
  }
}
```

### New Packages

#### MCP Server (`ConsoleImage.Mcp`)
- Cross-platform AOT binaries in GitHub releases
- Supports NuGet dependency mode for CI builds
- JSON source generation for AOT compatibility

### Spectre.Console Enhancements
- **`MatrixImage`** - Static Matrix rain effect renderable
- **`AnimatedMatrixImage`** - Animated Matrix rain with `AnsiConsole.Live()`
- **`ConsoleImageFactory`** - Factory for creating renderables by mode
- **`MultiAnimationPlayer`** - Play multiple animations side-by-side
- **`RenderModeComparison`** - Compare all render modes in a grid
- **`IAnimatedRenderable`** - Interface for animated renderables

### Documentation
- **VideoPlayer static class** - Simple one-liner video playback API
- **NuGet READMEs** - Proper package-specific documentation with working image links
- **DocTests project** - Verifies all documented APIs compile and work

### Bug Fixes
- Fixed CalibrationSettings test for Matrix mode (shared aspect ratio property)
- Fixed duplicate ConsoleImage.Video.Core entry in solution file

---

## [2.5.0] - 2025-01-22

### Major Features

#### Matrix Mode (Digital Rain Effect)
- **Iconic Matrix digital rain** - `-M/--matrix` option renders images with falling code effect
- **Authentic characters** - Half-width katakana, numbers, and symbols from The Matrix films
- **Color presets** - `--matrix-color green|red|blue|amber|cyan|purple` or hex `#RRGGBB`
- **Full color mode** - `--matrix-fullcolor` uses source image colors with Matrix lighting
- **Custom alphabets** - `--matrix-alphabet "01"` for binary rain, or any custom string
- **ASCII fallback** - `--matrix-ascii` for terminals without katakana support
- **Configurable density/speed** - `--matrix-density` and `--matrix-speed` options

```bash
# Classic green Matrix
consoleimage photo.jpg --matrix

# Full color from source image
consoleimage photo.jpg --matrix --matrix-fullcolor

# Binary rain
consoleimage photo.jpg --matrix --matrix-alphabet "01"

# Custom color
consoleimage photo.jpg --matrix --matrix-color "#FF6600"
```

#### Edge Detection Image Reveal
- **Sobel edge detection** - `--matrix-edge-detect` reveals image shape through rain
- **Brightness persistence** - `--matrix-bright-persist` makes bright areas glow longer
- **Horizontal edge priority** - Rain "collects" on shoulders/ledges like real rain
- **Phosphor glow effect** - Characters flash brightly when crossing edges
- **ImageReveal preset** - Optimized settings for clear image visibility

```bash
# Image reveal mode - see the shape through the rain
consoleimage portrait.jpg --matrix --matrix-edge-detect --matrix-bright-persist
```

#### FFmpeg Auto-Download
- **Zero setup required** - FFmpeg downloads automatically on first use (~100MB)
- **Interactive prompt** - Asks before downloading, respects `-y/--yes` for automation
- **Skip option** - `--no-ffmpeg-download` to prevent auto-download
- **Platform detection** - Downloads correct binary for Windows/Linux/macOS

#### Smart Keyframe Extraction
- **Scene detection** - `--smart-keyframes` uses histogram analysis to find scene changes
- **Representative frames** - Extracts visually distinct frames, not just uniform intervals
- **Memory efficient** - Streams frames directly to FFmpeg, only 1 frame in memory
- **Configurable limits** - `--gif-frames` and `--duration` control output

```bash
# Extract 20 representative keyframes from a video
consolevideo movie.mp4 --raw --smart-keyframes -o gif:keyframes.gif --gif-frames 20
```

### New Classes

- **`MatrixRenderer`** - Full Matrix digital rain effect renderer
- **`MatrixOptions`** - Configuration for Matrix mode (colors, density, edge detection)
- **`SceneDetectionService`** - Histogram-based scene change detection
- **`StreamingGifWriter`** - Memory-efficient GIF writer with frame buffering
- **`FFmpegGifWriter`** - Pipes raw frames to FFmpeg stdin (1 frame in memory)

### Performance Improvements

- **FFmpeg pipe streaming** - GIF encoding via stdin pipe, no temp files
- **Pre-allocated frame buffers** - Reuses memory for frame processing
- **Incremental GIF writing** - Frames flushed as processed, no memory buildup

### Bug Fixes

- **White pixel bleed in ASCII GIF** - Fixed color reset code using wrong foreground color
- **Matrix animation static** - Fixed missing `_state.Advance()` call for animation

### Documentation

- **Documentation matrix** - Root README now links to all project docs
- **CLI-focused READMEs** - Separate docs for CLI tools vs NuGet libraries
- **Matrix mode examples** - Sample GIFs showing different Matrix variations
- **GIF output documentation** - Added to Video CLI README
- **Keyframe extraction docs** - Scene detection and raw frame extraction

### New CLI Options

#### Matrix Mode Options
| Option | Description | Default |
|--------|-------------|---------|
| `-M, --matrix` | Enable Matrix digital rain effect | OFF |
| `--matrix-color` | Color: green, red, blue, amber, cyan, purple, or #RRGGBB | green |
| `--matrix-fullcolor` | Use source image colors | OFF |
| `--matrix-ascii` | ASCII only (no katakana) | OFF |
| `--matrix-alphabet` | Custom character set | - |
| `--matrix-density` | Rain density (0.1-2.0) | 0.5 |
| `--matrix-speed` | Animation speed | 1.0 |
| `--matrix-edge-detect` | Enable edge detection reveal | OFF |
| `--matrix-bright-persist` | Brightness persistence | OFF |

#### Video CLI Options
| Option | Description |
|--------|-------------|
| `--smart-keyframes` | Use scene detection for keyframe extraction |
| `--raw` | Extract raw video frames as GIF |
| `-y, --yes` | Auto-confirm prompts (for automation) |
| `--no-ffmpeg-download` | Prevent automatic FFmpeg download |

---

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
