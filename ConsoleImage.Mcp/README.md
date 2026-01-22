# ConsoleImage MCP Server

Model Context Protocol (MCP) server that exposes ConsoleImage rendering capabilities as AI tools.

## What is MCP?

[Model Context Protocol](https://modelcontextprotocol.io/) is an open standard that allows AI assistants (like Claude) to interact with external tools and services. This MCP server lets Claude render images as ASCII art, create animated GIFs, and analyze media files.

## Installation

### Option 1: Download Pre-built Binary (Recommended)

Download the latest release for your platform from [GitHub Releases](https://github.com/scottgal/ConsoleImage/releases):

| Platform | Download |
|----------|----------|
| Windows x64 | `consoleimage-win-x64-mcp.zip` |
| Linux x64 | `consoleimage-linux-x64-mcp.tar.gz` |
| Linux ARM64 | `consoleimage-linux-arm64-mcp.tar.gz` |
| macOS ARM64 | `consoleimage-osx-arm64-mcp.tar.gz` |

Extract to a directory of your choice, e.g.:
- Windows: `C:\Tools\consoleimage-mcp\`
- Linux/macOS: `~/tools/consoleimage-mcp/`

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/scottgal/ConsoleImage.git
cd ConsoleImage

# Build with AOT (produces native binary)
dotnet publish ConsoleImage.Mcp -c Release -r win-x64 --self-contained -p:PublishAot=true

# Or use the build script (Windows)
.\ConsoleImage.Mcp\build-aot.ps1
```

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

Add to your project's `.mcp.json` or global MCP config:

```json
{
  "mcpServers": {
    "consoleimage": {
      "command": "path/to/consoleimage-mcp"
    }
  }
}
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

## Usage Examples

Once configured, you can ask Claude things like:

- "Show me what this image looks like as ASCII art"
- "Render photo.jpg in Matrix style"
- "Compare all render modes for this image"
- "Convert this GIF to ASCII art and save it"
- "What are the dimensions of this video file?"

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
