# ConsoleImage MCP Server

Model Context Protocol (MCP) server that exposes ConsoleImage rendering capabilities as AI tools.

## What is MCP?

[Model Context Protocol](https://modelcontextprotocol.io/) is an open standard that allows AI assistants (like Claude)
to interact with external tools and services. This MCP server lets Claude render images as ASCII art, create animated
GIFs, and analyze media files.

## Installation

### Option 1: Download Pre-built Binary (Recommended)

Download the latest release for your platform from [GitHub Releases](https://github.com/scottgal/mostlylucid.consoleimage/releases):

| Platform    | Download                              |
|-------------|---------------------------------------|
| Windows x64 | `consoleimage-win-x64-mcp.zip`        |
| Linux x64   | `consoleimage-linux-x64-mcp.tar.gz`   |
| Linux ARM64 | `consoleimage-linux-arm64-mcp.tar.gz` |
| macOS ARM64 | `consoleimage-osx-arm64-mcp.tar.gz`   |

Extract to a directory of your choice, e.g.:

- Windows: `C:\Tools\consoleimage-mcp\`
- Linux/macOS: `~/tools/consoleimage-mcp/`

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/scottgal/mostlylucid.consoleimage.git
cd ConsoleImage

# Build with AOT (produces native binary)
dotnet publish ConsoleImage.Mcp -c Release -r win-x64 --self-contained -p:PublishAot=true

# Or use the build script (Windows)
.\ConsoleImage.Mcp\build-aot.ps1
```

## Features

Provides the following MCP tools:

| Tool                   | Description                                                          |
|------------------------|----------------------------------------------------------------------|
| `render_image`         | Render image/GIF to ASCII art (ascii, blocks, braille, matrix modes) |
| `render_to_gif`        | Create animated GIF from image/GIF source                            |
| `render_video`         | Render video file to animated ASCII art GIF (NEW in v3.1)            |
| `extract_frames`       | Extract raw video frames to GIF (no ASCII rendering)                 |
| `get_image_info`       | Get detailed image metadata (format, dimensions, EXIF, color depth)  |
| `get_gif_info`         | Get GIF metadata (dimensions, frame count)                           |
| `get_video_info`       | Get video file info via FFmpeg                                       |
| `check_youtube_url`    | Check if URL is a YouTube video (NEW in v3.1)                        |
| `get_youtube_stream`   | Extract direct stream URL from YouTube (NEW in v3.1)                 |
| `list_render_modes`    | List available render modes with descriptions                        |
| `list_matrix_presets`  | List Matrix digital rain color presets                               |
| `compare_render_modes` | Render same image in all modes for comparison                        |

## Configuration

Once installed, configure your AI assistant to use the MCP server.

### Claude Desktop

Edit `claude_desktop_config.json`:

**Windows** (`%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "C:\\Tools\\consoleimage-mcp\\consoleimage-mcp.exe"
    }
  }
}
```

**macOS** (`~/Library/Application Support/Claude/claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "/Users/yourname/tools/consoleimage-mcp/consoleimage-mcp"
    }
  }
}
```

**Linux** (`~/.config/Claude/claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "/home/yourname/tools/consoleimage-mcp/consoleimage-mcp"
    }
  }
}
```

After editing, restart Claude Desktop. The tools will appear in Claude's tool list.

### Claude Code (CLI)

Create a `.mcp.json` file in your project root:

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "C:\\Tools\\consoleimage-mcp\\consoleimage-mcp.exe"
    }
  }
}
```

Or add to your global config at `~/.claude/mcp.json` (or `%USERPROFILE%\.claude\mcp.json` on Windows).

After adding, restart Claude Code. The tools will be available as `mcp__consoleimage__render_image`, etc.

**Quick test after setup:**

```
You: "Use the render_image tool to show me what photo.jpg looks like"
Claude: [calls mcp__consoleimage__render_image with path="photo.jpg"]
```

### VS Code (with Claude extension)

Add to VS Code settings (`settings.json`):

```json
{
  "claude.mcpServers": {
    "consoleimage": {
      "command": "path/to/consoleimage-mcp"
    }
  }
}
```

## Best Practices for LLM Context

ASCII art with ANSI color codes can generate large outputs that consume significant context window space. Here are
strategies to minimize context usage:

| Strategy               | How                                  | When to Use                                          |
|------------------------|--------------------------------------|------------------------------------------------------|
| **Save to file**       | Use `outputPath` parameter           | When you want to see the image but not waste context |
| **Braille + no color** | `mode: "braille"`, `useColor: false` | Quick preview with maximum detail, minimal output (DEFAULT mode) |
| **Reduce size**        | Lower `maxWidth`/`maxHeight`         | When rough preview is sufficient                     |
| **ASCII mode**         | Use `mode: "ascii"`                  | Widest compatibility, fewer bytes than blocks mode   |

**Quick preview (minimal context):**

```
"Quick preview of photo.jpg using braille without color at 60 wide"
```

**Full render without consuming context:**

```
"Render photo.jpg in blocks mode and save to render.txt"
```

Returns only:
`{"success": true, "outputPath": "render.txt", "mode": "blocks", "width": 80, "height": 40, "fileSizeKB": 12.5}`

## Usage Examples

Once configured, you can ask Claude things like:

- "Show me what this image looks like as ASCII art"
- "Render photo.jpg in Matrix style"
- "Compare all render modes for this image"
- "Convert this GIF to ASCII art and save it"
- "What are the dimensions of this video file?"
- "Quick preview of image.png using braille without color" (minimal context)
- "Render photo.jpg to output.txt" (saves to file, returns metadata only)
- "Show info for photo.jpg" (get detailed metadata: format, dimensions, EXIF, color depth)

## Tool Reference

### render_image

Renders an image or GIF frame to ASCII art and returns the result as text with ANSI color codes.

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | (required) | Path to image file (jpg, png, gif, bmp, webp) |
| `mode` | string | `"braille"` | Render mode: `braille` (default), `ascii`, `blocks`, or `matrix` |
| `maxWidth` | int | `80` | Maximum width in characters |
| `maxHeight` | int | `40` | Maximum height in characters |
| `useColor` | bool | `true` | Enable ANSI color codes in output |
| `frameIndex` | int | `0` | For GIFs, which frame to render (0-based) |
| `outputPath` | string | `null` | Save to file instead of returning inline (reduces context usage) |

**Example prompts:**

- "Render photo.jpg as ASCII art"
- "Show me image.png in braille mode at 120 characters wide"
- "Display frame 5 of animation.gif in matrix style"
- "Render photo.jpg and save to output.txt" (uses outputPath to avoid context bloat)

**Tips for reducing context usage:**

Large colored renders can consume significant context. To minimize this:

1. **Use `outputPath`** - Save output to a file instead of returning inline:
   ```
   "Render image.jpg to output.txt"
   ```
   Returns only metadata (success, dimensions, file size) instead of the full ANSI output.

2. **Use `braille` mode with `useColor: false`** - Fastest way to preview an image:
   ```
   "Quick preview of image.jpg using braille without color"
   ```
   Braille characters pack 8 pixels per character (2x4 dots), giving high detail with minimal output size. Without color
   codes, the response is very compact.

3. **Reduce dimensions** - Use smaller `maxWidth`/`maxHeight`:
   ```
   "Render image.jpg at 40 wide"
   ```

### render_to_gif

Creates an animated GIF file from an image or GIF source, rendered in the specified ASCII art style.

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `inputPath` | string | (required) | Source image or GIF path |
| `outputPath` | string | (required) | Output GIF file path |
| `mode` | string | `"braille"` | Render mode: `braille` (default), `ascii`, `blocks`, or `matrix` |
| `maxWidth` | int | `60` | Width in characters |
| `fontSize` | int | `10` | Font size for text rendering |
| `maxColors` | int | `64` | GIF palette size (16-256) |

**Example prompts:**

- "Convert animation.gif to ASCII art and save as output.gif"
- "Create a matrix-style GIF from photo.jpg"

### get_image_info

Returns comprehensive metadata about any image file. Useful for understanding an image before rendering.

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | (required) | Path to image file (jpg, png, gif, bmp, webp) |

**Returns:** JSON with detailed information:

```json
{
  "fileName": "photo.jpg",
  "fullPath": "/path/to/photo.jpg",
  "format": "JPEG",
  "width": 1920,
  "height": 1080,
  "aspectRatio": "16:9",
  "fileSizeBytes": 245760,
  "fileSizeFormatted": "240.0 KB",
  "frameCount": 1,
  "isAnimated": false,
  "bitsPerPixel": 24,
  "pixelFormat": "NoAlpha, 24bpp",
  "megaPixels": 2.07,
  "metadata": {
    "Make": "Canon",
    "Model": "EOS R5",
    "DateTimeOriginal": "2024:01:15 14:30:00",
    "FocalLength": "50/1"
  }
}
```

**Example prompts:**

- "Show info for photo.jpg"
- "What are the details of this image?"
- "Get metadata for screenshot.png"

### extract_frames

Extracts raw video frames to an animated GIF file. No ASCII rendering - preserves actual video frames. Useful for creating thumbnails, previews, or scene slideshows.

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `inputPath` | string | (required) | Path to video file (MP4, MKV, AVI, WebM, etc.) |
| `outputPath` | string | (required) | Path for the output GIF file |
| `width` | int | `320` | Output width in pixels |
| `maxFrames` | int | `8` | Maximum number of frames to extract |
| `maxLength` | double | `10` | Maximum duration in seconds to sample from |
| `startTime` | double | `0` | Start time in seconds |
| `smartKeyframes` | bool | `false` | Use scene detection instead of uniform sampling |
| `sceneThreshold` | double | `0.4` | Scene detection threshold (0.0-1.0, lower = more sensitive) |

**Example prompts:**

- "Extract 8 keyframes from video.mp4 as a GIF"
- "Create a thumbnail GIF from the first 5 seconds of movie.mp4"
- "Extract scene changes from video.mp4 using smart keyframe detection"

### render_video

Renders a video file to an animated ASCII art GIF. Requires FFmpeg (auto-downloads on first use).

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `inputPath` | string | (required) | Path to video file (MP4, MKV, AVI, WebM, etc.) |
| `outputPath` | string | (required) | Path for the output GIF file |
| `mode` | string | `"braille"` | Render mode: `braille` (default), `ascii`, `blocks`, or `matrix` |
| `width` | int | `60` | Width in characters |
| `startTime` | double | `0` | Start time in seconds |
| `duration` | double | `10` | Duration in seconds |
| `fps` | int | `10` | Target frames per second |
| `fontSize` | int | `10` | Font size for GIF rendering |
| `maxColors` | int | `64` | Max colors in GIF palette |

**Example prompts:**

- "Render movie.mp4 to ASCII art GIF"
- "Create a braille animation from video.mp4, first 5 seconds"
- "Convert video.mkv to matrix-style GIF at 80 characters wide"

### check_youtube_url

Check if a URL is a YouTube video and whether yt-dlp is available.

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | (required) | URL to check |

**Returns:** JSON with `isYouTubeUrl`, `ytdlpAvailable`, `status`

### get_youtube_stream

Extract the direct video stream URL from a YouTube video. Requires yt-dlp (auto-downloads on first use, ~10MB).

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | (required) | YouTube video URL |
| `maxHeight` | int | `null` | Maximum video height (e.g., 720, 1080) |

**Returns:** JSON with `videoUrl`, `title`, and status information

**Example prompts:**

- "Get the stream URL for this YouTube video"
- "Extract the 720p stream from https://youtu.be/..."

### get_gif_info

Returns metadata about a GIF file as JSON. (Use `get_image_info` for more detailed information.)

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | (required) | Path to GIF file |

**Returns:** JSON with `path`, `width`, `height`, `frameCount`, `isAnimated`

### get_video_info

Returns metadata about a video file via FFmpeg.

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | (required) | Path to video file |

**Returns:** JSON with `path`, `duration`, `durationFormatted`, `width`, `height`, `frameRate`, `videoCodec`

### list_render_modes

Lists all available render modes with descriptions. Takes no parameters.

**Returns:**

```json
{
  "modes": [
    {"name": "ascii", "description": "Classic ASCII characters..."},
    {"name": "blocks", "description": "Unicode half-blocks..."},
    {"name": "braille", "description": "Braille patterns..."},
    {"name": "matrix", "description": "Matrix digital rain..."}
  ]
}
```

### list_matrix_presets

Lists available Matrix color presets. Takes no parameters.

**Returns:** JSON array of preset names: `green`, `red`, `blue`, `amber`, `cyan`, `purple`

### compare_render_modes

Renders the same image in all four modes for side-by-side comparison.

**Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | string | (required) | Path to image file |
| `maxWidth` | int | `40` | Width for each render (smaller for comparison) |

**Example prompts:**

- "Compare all render modes for photo.jpg"
- "Show me how this image looks in each ASCII style"

## AOT Support

The server is configured for Native AOT compilation for fast startup. All JSON serialization uses source generators for
AOT compatibility.

Supported platforms:

- `win-x64` - Windows x64
- `linux-x64` - Linux x64
- `osx-x64` - macOS Intel
- `osx-arm64` - macOS Apple Silicon

## Troubleshooting

### Verify Installation

Test the MCP server directly from command line:

```bash
# Should output JSON-RPC response
echo '{"jsonrpc":"2.0","method":"tools/list","id":1}' | ./consoleimage-mcp
```

### Common Issues

**"Command not found"**

- Ensure the full path to the binary is correct in your config
- On Linux/macOS, ensure the binary is executable: `chmod +x consoleimage-mcp`

**"Tools not appearing in Claude"**

- Restart Claude Desktop after editing config
- Check Claude Desktop logs for MCP connection errors
- Verify JSON syntax in config file (use a JSON validator)

**"Video tools not working"**

- FFmpeg is required for video features
- FFmpeg will auto-download on first use, or install manually

## Dependencies

- ConsoleImage.Core - Image rendering
- ConsoleImage.Video.Core - Video/FFmpeg support
- ModelContextProtocol - MCP SDK

## License

MIT License - see repository root for details.
