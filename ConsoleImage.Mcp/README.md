# ConsoleImage MCP Server

Model Context Protocol server that gives AI assistants the ability to see, analyze, and transform images, videos, GIFs, and documents.

## Capabilities at a Glance

**21 tools** across 6 categories:

| Category | Tools | What You Can Do |
|----------|-------|-----------------|
| **Image Rendering** | `render_image`, `render_to_gif`, `compare_render_modes`, `list_render_modes`, `list_matrix_presets` | See any image as text, create GIFs, compare styles |
| **Video Analysis** | `get_video_info`, `render_video`, `render_video_frame`, `extract_frames`, `detect_scenes` | Inspect videos, render frames, find scene cuts |
| **Subtitles** | `get_subtitle_streams`, `extract_subtitles`, `parse_subtitles`, `get_youtube_subtitles` | Read dialogue from any video or YouTube URL |
| **YouTube** | `check_youtube_url`, `get_youtube_stream`, `get_youtube_subtitles` | Access YouTube video streams and captions |
| **Export** | `export_to_svg`, `export_to_markdown` | Create embeddable SVG art and markdown docs |
| **Documents** | `get_document_info`, `get_image_info`, `get_gif_info` | Inspect saved documents and image metadata |
| **System** | `check_dependencies` | Verify FFmpeg/yt-dlp availability |

## Quick Start

### Installation

Download the latest binary from [GitHub Releases](https://github.com/scottgal/mostlylucid.consoleimage/releases):

| Platform | Download |
|----------|----------|
| Windows x64 | `consoleimage-win-x64-mcp.zip` |
| Windows ARM64 | `consoleimage-win-arm64-mcp.zip` |
| Linux x64 | `consoleimage-linux-x64-mcp.tar.gz` |
| Linux ARM64 | `consoleimage-linux-arm64-mcp.tar.gz` |
| macOS ARM64 | `consoleimage-osx-arm64-mcp.tar.gz` |

### Configure Claude Desktop

Edit `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "C:\\Tools\\consoleimage-mcp\\consoleimage-mcp.exe"
    }
  }
}
```

Config locations:
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Linux**: `~/.config/Claude/claude_desktop_config.json`

### Configure Claude Code

Add to `.mcp.json` in your project root or `~/.claude/mcp.json` globally:

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "C:\\Tools\\consoleimage-mcp\\consoleimage-mcp.exe"
    }
  }
}
```

---

## AI-Optimized Usage Guide

### Best Mode for AI "Vision"

| Goal | Mode | Color | Why |
|------|------|-------|-----|
| **Quick preview** (compact) | `braille` | `false` | 8 pixels per character, no ANSI codes. Smallest output. |
| **Readable ASCII** | `ascii` | `false` | Familiar characters, easy to reason about shapes |
| **Full fidelity** (save to file) | `blocks` | `true` | Best color accuracy, use `outputPath` to avoid context bloat |
| **Stylized** | `matrix` | `true` | Matrix digital rain effect |

**Context-efficient patterns:**

```
# Minimal context - braille without color (best for AI understanding)
render_image(path="photo.jpg", mode="braille", useColor=false, maxWidth=60)

# Zero context - save to file, get metadata only
render_image(path="photo.jpg", mode="blocks", outputPath="render.txt")
# Returns: {"success": true, "width": 80, "height": 40, "fileSizeKB": 12.5}

# Quick video peek - single frame, no GIF needed
render_video_frame(path="movie.mp4", timestamp=30.0, mode="braille", useColor=false)
```

### Common Workflows

**"What's in this video?"** (full analysis pipeline):
1. `get_video_info` → duration, resolution, codec, embedded subtitles
2. `detect_scenes` → find key moments/cuts
3. `render_video_frame` at each scene timestamp → see what happens
4. `extract_subtitles` or `get_youtube_subtitles` → read the dialogue

**"Show me this image"**:
1. `render_image` with `mode="braille"`, `useColor=false` → compact preview
2. Or `export_to_svg` → embeddable colored version

**"Get YouTube video content"**:
1. `check_youtube_url` → verify it's valid
2. `get_youtube_subtitles` → read all dialogue (no transcription needed)
3. `get_youtube_stream` → get direct stream URL for further processing

---

## Tool Reference

### Image Rendering

#### `render_image`
Render any image to ASCII art text.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Image file path (JPG, PNG, GIF, WebP, BMP) |
| `mode` | string | `"ascii"` | `ascii`, `blocks`, `braille`, or `matrix` |
| `maxWidth` | int | `80` | Max width in characters |
| `maxHeight` | int | `40` | Max height in characters |
| `useColor` | bool | `true` | ANSI color codes (false = compact monochrome) |
| `frameIndex` | int | `0` | GIF frame index |
| `outputPath` | string | null | Save to file (returns metadata only) |

#### `render_to_gif`
Create animated GIF from image/GIF source.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `inputPath` | string | required | Source image or GIF |
| `outputPath` | string | required | Output GIF path |
| `mode` | string | `"ascii"` | Render mode |
| `maxWidth` | int | `60` | Width in characters |
| `fontSize` | int | `10` | Font size for rendering |
| `maxColors` | int | `64` | GIF palette size (4-256) |

#### `compare_render_modes`
Render same image in all 4 modes for comparison. Returns `{"ascii": "...", "blocks": "...", "braille": "...", "matrix": "..."}`.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Image file path |
| `maxWidth` | int | `40` | Width per render |

#### `list_render_modes`
List all modes with descriptions. No parameters.

#### `list_matrix_presets`
List Matrix color presets (ClassicGreen, RedPill, BluePill, Amber, FullColor). No parameters.

---

### Video Analysis

#### `get_video_info`
Comprehensive video metadata including embedded subtitle streams.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Video file path |

Returns:
```json
{
  "fileName": "movie.mkv",
  "duration": 7200.5,
  "durationFormatted": "02:00:00.500",
  "width": 1920,
  "height": 1080,
  "aspectRatio": "16:9",
  "frameRate": 23.976,
  "totalFrames": 172836,
  "bitRate": 5000000,
  "videoCodec": "h264",
  "fileSizeBytes": 4500000000,
  "fileSizeFormatted": "4.19 GB",
  "subtitleStreams": [
    {"index": 0, "codec": "subrip", "language": "eng", "title": "English", "isTextBased": true},
    {"index": 1, "codec": "subrip", "language": "spa", "title": "Spanish", "isTextBased": true}
  ]
}
```

#### `render_video_frame`
Render a single video frame at a specific timestamp. Much faster than `render_video`.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Video file path |
| `timestamp` | double | `0` | Time in seconds |
| `mode` | string | `"braille"` | Render mode |
| `maxWidth` | int | `80` | Width in characters |
| `useColor` | bool | `true` | Enable color |
| `outputPath` | string | null | Save to file |

#### `render_video`
Render video segment to animated ASCII art GIF.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `inputPath` | string | required | Video file path |
| `outputPath` | string | required | Output GIF path |
| `mode` | string | `"braille"` | Render mode |
| `width` | int | `60` | Width in characters |
| `startTime` | double | `0` | Start time (seconds) |
| `duration` | double | `10` | Duration (seconds) |
| `fps` | int | `10` | Target FPS |
| `fontSize` | int | `10` | GIF font size |
| `maxColors` | int | `64` | GIF palette size |

#### `extract_frames`
Extract raw video frames to GIF (no ASCII rendering). Supports smart scene detection.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `inputPath` | string | required | Video file path |
| `outputPath` | string | required | Output GIF path |
| `width` | int | `320` | Output width (pixels) |
| `maxFrames` | int | `8` | Max frames to extract |
| `maxLength` | double | `10` | Duration to sample (seconds) |
| `startTime` | double | `0` | Start time (seconds) |
| `smartKeyframes` | bool | `false` | Use scene detection |
| `sceneThreshold` | double | `0.4` | Detection sensitivity (0-1) |

#### `detect_scenes`
Find scene changes/cuts in a video. Returns timestamps.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Video file path |
| `threshold` | double | `0.4` | Sensitivity (0-1, lower = more sensitive) |
| `startTime` | double | `0` | Start time (seconds) |
| `endTime` | double | null | End time (null = entire video) |

Returns:
```json
{
  "sceneCount": 12,
  "timestamps": [5.2, 12.8, 23.1, ...],
  "timestampsFormatted": ["00:00:05.200", "00:00:12.800", ...],
  "duration": 120.0
}
```

---

### Subtitle Tools

#### `get_subtitle_streams`
List embedded subtitle tracks in a video file.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Video file path |

Returns array of streams with index, codec, language, title, and whether they're text-extractable.

#### `extract_subtitles`
Extract embedded subtitles from video to SRT file.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Video file path |
| `outputPath` | string | required | Output SRT path |
| `streamIndex` | int | null | Stream index (auto-selects if omitted) |
| `language` | string | null | Preferred language code |

#### `parse_subtitles`
Parse an existing SRT/VTT file. Returns timestamped entries.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Subtitle file (.srt or .vtt) |
| `maxEntries` | int | null | Limit output entries |

Returns:
```json
{
  "success": true,
  "entryCount": 342,
  "totalDuration": "01:45:30",
  "entries": [
    {"index": 1, "startTime": "00:00:01.500", "endTime": "00:00:04.200", "startSeconds": 1.5, "endSeconds": 4.2, "text": "Hello, world."},
    ...
  ]
}
```

#### `get_youtube_subtitles`
Download captions from YouTube (manual or auto-generated, no Whisper needed).

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | required | YouTube URL |
| `outputDirectory` | string | required | Where to save the SRT file |
| `language` | string | `"en"` | Language code |
| `maxEntries` | int | null | Limit output entries |

Returns parsed subtitle entries (same format as `parse_subtitles`).

---

### YouTube Tools

#### `check_youtube_url`
Check if a URL is YouTube and whether yt-dlp is available.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | required | URL to check |

#### `get_youtube_stream`
Extract direct video stream URL from YouTube.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | required | YouTube URL |
| `maxHeight` | int | null | Max video height (720, 1080, etc.) |

---

### Export Tools

#### `export_to_svg`
Render image to colored SVG. Embeddable in web pages, GitHub READMEs, docs.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `inputPath` | string | required | Source image |
| `outputPath` | string | required | Output SVG path |
| `mode` | string | `"braille"` | Render mode |
| `maxWidth` | int | `60` | Width in characters |
| `fontSize` | int | `14` | SVG font size |

#### `export_to_markdown`
Render image to markdown file with embedded ASCII art.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `inputPath` | string | required | Source image |
| `outputPath` | string | required | Output .md path |
| `mode` | string | `"braille"` | Render mode |
| `format` | string | `"svg"` | `plain`, `html`, `svg`, or `ansi` |
| `maxWidth` | int | `60` | Width in characters |
| `title` | string | null | Document title |

---

### Document & System Tools

#### `get_document_info`
Inspect a saved ConsoleImage document (.cidz, .json, .ndjson).

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Document file path |

Returns: type, version, frame count, duration, render mode, settings, subtitle info, file size.

#### `get_image_info`
Detailed image metadata including EXIF data.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | required | Image file path |

Returns: format, dimensions, aspect ratio, color depth, megapixels, EXIF metadata.

#### `check_dependencies`
Check what external tools are available.

Returns: FFmpeg status, yt-dlp status, supported formats list, available render modes.

---

## Supported Formats

| Type | Formats |
|------|---------|
| **Images** | JPG, PNG, GIF, WebP, BMP, TIFF |
| **Video** | MP4, MKV, AVI, WebM, MOV, FLV, WMV (requires FFmpeg) |
| **Subtitles** | SRT, VTT |
| **Documents** | CIDZ (compressed), JSON, NDJSON |
| **Export** | GIF, SVG, Markdown (plain/html/svg/ansi) |

## External Dependencies

| Tool | Required For | Auto-downloads? |
|------|-------------|-----------------|
| **FFmpeg** | Video processing, frame extraction, subtitle extraction | Yes, on first use |
| **yt-dlp** | YouTube stream extraction and subtitle download | Yes, on first use |

Use `check_dependencies` to verify what's installed.

## Build from Source

```bash
git clone https://github.com/scottgal/mostlylucid.consoleimage.git
cd ConsoleImage
dotnet publish ConsoleImage.Mcp -c Release -r win-x64 --self-contained -p:PublishAot=true
```

Native AOT compiled - fast startup, no .NET runtime required.

## License

MIT License - see repository root for details.
