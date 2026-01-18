# MostlyLucid.ConsoleImage

High-quality ASCII art renderer for .NET using shape-matching algorithm.

**Based on [Alex Harri's excellent article](https://alexharri.com/blog/ascii-rendering)** on ASCII rendering techniques.

## Features

- **Shape-matching algorithm**: Characters are selected based on visual shape similarity, not just brightness
- **6D shape vectors**: Each character and image region is analyzed using 6 sampling regions
- **K-D tree optimization**: Fast nearest-neighbor search for character matching
- **Contrast enhancement**: Global and directional contrast enhancement for sharper edges
- **Animated GIF support**: Render and play animated GIFs as ASCII art in the terminal
- **Color support**: Optional ANSI color output for terminals that support it
- **AOT compatible**: Designed for Native AOT compilation

## Installation

### NuGet Package

```bash
dotnet add package MostlyLucid.ConsoleImage
```

### CLI Tool

```bash
dotnet tool install --global MostlyLucid.ConsoleImage.Cli
```

## Usage

### Library Usage

```csharp
using ConsoleImage.Core;

// Simple conversion
string ascii = AsciiArt.FromFile("image.png");
Console.WriteLine(ascii);

// With options
var options = new RenderOptions
{
    MaxWidth = 80,
    MaxHeight = 40,
    UseColor = true,
    ContrastPower = 2.5f
};

string coloredAscii = AsciiArt.FromFile("image.png", options);
Console.WriteLine(coloredAscii);

// Animated GIF
var frames = AsciiArt.GifFromFile("animation.gif", options);
using var player = new AsciiAnimationPlayer(frames, useColor: true);
await player.PlayAsync();
```

### Using the Renderer Directly

```csharp
using ConsoleImage.Core;

var options = new RenderOptions
{
    MaxWidth = 100,
    ContrastPower = 3.0f,
    CharacterSet = " .:-=+*#%@"  // Custom character set
};

using var renderer = new AsciiRenderer(options);

// Render single image
var frame = renderer.RenderFile("photo.jpg");
Console.WriteLine(frame.ToString());

// Access character array directly
for (int y = 0; y < frame.Height; y++)
{
    for (int x = 0; x < frame.Width; x++)
    {
        char c = frame.Characters[y, x];
        // Process characters...
    }
}
```

### CLI Usage

```bash
# Basic conversion
ascii-image photo.jpg

# With options
ascii-image photo.jpg --width 80 --color

# Invert for dark terminals
ascii-image photo.jpg --invert

# Play animated GIF
ascii-image animation.gif --animate --loop 0

# Save to file
ascii-image photo.jpg --output result.txt

# Use block characters
ascii-image photo.jpg --preset block
```

### CLI Options

| Option | Short | Description |
|--------|-------|-------------|
| `--width` | `-w` | Output width in characters |
| `--height` | `-h` | Output height in characters |
| `--max-width` | | Maximum output width (default: 120) |
| `--max-height` | | Maximum output height (default: 60) |
| `--color` | `-c` | Enable ANSI color output |
| `--invert` | `-i` | Invert output (light on dark) |
| `--contrast` | | Contrast power (default: 2.5) |
| `--charset` | | Custom character set |
| `--preset` | `-p` | Character preset: default, simple, block |
| `--output` | `-o` | Write to file |
| `--animate` | `-a` | Play animated GIF |
| `--speed` | `-s` | Animation speed multiplier |
| `--loop` | `-l` | Animation loop count (0=infinite) |

## How It Works

Unlike simple brightness-based ASCII converters, this library uses **shape matching**:

1. **Character Analysis**: Each ASCII character is rendered and analyzed to create a 6-dimensional "shape vector" representing visual density in different regions (top-left, top-right, middle-left, middle-right, bottom-left, bottom-right).

2. **Image Sampling**: The input image is divided into cells, and each cell is sampled the same way to create a shape vector.

3. **K-D Tree Matching**: A k-d tree enables fast nearest-neighbor search to find the character whose shape vector is closest to each cell's vector.

4. **Contrast Enhancement**: Optional contrast enhancement sharpens edges by:
   - Global contrast: Applies a power function to emphasize differences
   - Directional contrast: Uses external sampling to detect edges with neighboring cells

This approach produces much sharper, more recognizable ASCII art compared to simple brightness mapping.

## Character Sets

Three built-in presets are available:

- **Default**: ` .'\`'^",:;!i><~+_-?][}{1)(|/\tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$`
- **Simple**: ` .'"\`^,:;Il!i><~+_-?][}{1)(|\/tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$`
- **Block**: ` ░▒▓█`

You can also provide a custom character set ordered from lightest to darkest.

## AOT Compilation

The library is designed for AOT compatibility. To publish as a native binary:

```bash
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r linux-x64
dotnet publish -c Release -r osx-x64
```

## Credits

- Algorithm based on [Alex Harri's ASCII Rendering article](https://alexharri.com/blog/ascii-rendering)
- Image processing powered by [ImageSharp](https://sixlabors.com/products/imagesharp/)

## License

MIT License - see LICENSE file for details.
