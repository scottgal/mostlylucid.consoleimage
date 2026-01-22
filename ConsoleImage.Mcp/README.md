# ConsoleImage MCP Server

Model Context Protocol (MCP) server that exposes ConsoleImage rendering capabilities as AI tools.

## Features

Provides the following MCP tools:

| Tool | Description |
|------|-------------|
| `render_image` | Render image/GIF to ASCII art (ascii, blocks, braille, matrix modes) |
| `render_to_gif` | Create animated GIF from image/GIF source |
| `get_gif_info` | Get GIF metadata (dimensions, frame count) |
| `get_video_info` | Get video file info via FFmpeg |
| `list_render_modes` | List available render modes with descriptions |
| `list_matrix_presets` | List Matrix digital rain color presets |
| `compare_render_modes` | Render same image in all modes for comparison |

## Building

```bash
# Standard build
dotnet build

# AOT publish (Windows)
.\build-aot.ps1

# AOT publish for specific platform
.\build-aot.ps1 -Runtime linux-x64
.\build-aot.ps1 -Runtime osx-arm64
```

## Configuration

### Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "path/to/consoleimage-mcp.exe"
    }
  }
}
```

### VS Code (with Claude extension)

Add to settings:

```json
{
  "claude.mcpServers": {
    "consoleimage": {
      "command": "path/to/consoleimage-mcp.exe"
    }
  }
}
```

## Tool Examples

### render_image

```
Render photo.jpg to ASCII art at 80 columns wide
```

Parameters:
- `path` (required): Image file path
- `mode`: ascii, blocks, braille, or matrix (default: ascii)
- `maxWidth`: Max width in characters (default: 80)
- `maxHeight`: Max height in characters (default: 40)
- `useColor`: Enable ANSI colors (default: true)
- `frameIndex`: For GIFs, which frame to render (default: 0)

### render_to_gif

```
Convert animation.gif to ASCII art GIF
```

Parameters:
- `inputPath` (required): Source image/GIF path
- `outputPath` (required): Output GIF path
- `mode`: ascii, blocks, braille, or matrix (default: ascii)
- `maxWidth`: Max width in characters (default: 60)
- `fontSize`: Font size for rendering (default: 10)
- `maxColors`: GIF palette size (default: 64)

### get_video_info

```
Get duration and resolution of movie.mp4
```

Returns JSON with duration, resolution, frame rate, and codec info.

## AOT Support

The server is configured for Native AOT compilation for fast startup. All JSON serialization uses source generators for AOT compatibility.

Supported platforms:
- `win-x64` - Windows x64
- `linux-x64` - Linux x64
- `osx-x64` - macOS Intel
- `osx-arm64` - macOS Apple Silicon

## Dependencies

- ConsoleImage.Core - Image rendering
- ConsoleImage.Video.Core - Video/FFmpeg support
- ModelContextProtocol - MCP SDK
