# MostlyLucid.ConsoleImage

High-quality ASCII art renderer for .NET using shape-matching algorithm.

**Based on [Alex Harri's excellent article](https://alexharri.com/blog/ascii-rendering)** on ASCII rendering techniques.

## Quick Start

```csharp
using ConsoleImage.Core;

// One line - just works!
Console.WriteLine(AsciiArt.Render("photo.jpg"));
```

## Installation

```bash
dotnet add package MostlyLucid.ConsoleImage
```

## Features

- **Shape-matching algorithm**: Characters selected by visual shape similarity, not just brightness
- **6D shape vectors**: Staggered sampling circles for accurate shape matching
- **K-D tree optimization**: Fast nearest-neighbor search
- **Contrast enhancement**: Global and directional enhancement for sharp edges
- **Animated GIF support**: Render and play animated GIFs in the terminal
- **Color support**: ANSI color codes for modern terminals
- **AOT compatible**: Works with Native AOT compilation
- **Configuration**: Supports `appsettings.json` binding

## Simple API

```csharp
using ConsoleImage.Core;

// Basic - just works
Console.WriteLine(AsciiArt.Render("photo.jpg"));

// With width
Console.WriteLine(AsciiArt.Render("photo.jpg", 80));

// Colored output
Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));

// For dark terminals
Console.WriteLine(AsciiArt.RenderInverted("photo.jpg"));

// Play animated GIF
await AsciiArt.PlayGif("animation.gif");
```

## Full Options API

```csharp
using ConsoleImage.Core;

// Use presets
var options = RenderOptions.HighDetail;
var options = RenderOptions.Colored;
var options = RenderOptions.ForDarkTerminal;
var options = RenderOptions.ForAnimation(loopCount: 3);

// Or customize everything
var options = new RenderOptions
{
    MaxWidth = 100,
    MaxHeight = 50,
    UseColor = true,
    ContrastPower = 3.0f,
    CharacterSetPreset = "extended"
};

Console.WriteLine(AsciiArt.FromFile("photo.jpg", options));
```

## Configuration from appsettings.json

```json
{
  "AsciiRenderer": {
    "MaxWidth": 120,
    "MaxHeight": 60,
    "ContrastPower": 2.5,
    "UseColor": true,
    "CharacterSetPreset": "default"
  }
}
```

```csharp
var config = builder.Configuration.GetSection("AsciiRenderer").Get<RenderOptions>();
Console.WriteLine(AsciiArt.FromFile("photo.jpg", config));
```

## Animated GIFs

```csharp
// Simple playback
await AsciiArt.PlayGif("animation.gif");

// With options
var options = RenderOptions.ForAnimation(loopCount: 0); // Infinite
options.UseColor = true;
await AsciiArt.PlayGif("animation.gif", options);

// Manual frame control
var frames = AsciiArt.GifFromFile("animation.gif");
foreach (var frame in frames)
{
    Console.Clear();
    Console.WriteLine(frame.ToString());
    await Task.Delay(frame.DelayMs);
}
```

## Reusing Renderer for Performance

```csharp
// Create once, use many times
using var renderer = AsciiArt.CreateRenderer(RenderOptions.HighDetail);

foreach (var imagePath in imagePaths)
{
    Console.WriteLine(renderer.RenderFile(imagePath).ToString());
}
```

## Character Set Presets

| Preset | Characters | Use Case |
|--------|------------|----------|
| `default` | Standard ASCII | General purpose |
| `simple` | ` .:-=+*#%@` | Quick renders |
| `block` | ` ░▒▓█` | High density |
| `extended` | 95 characters | Maximum detail |

## CLI Tool

```bash
# Basic
ascii-image photo.jpg

# With options
ascii-image photo.jpg -w 80 -c --invert

# Animated GIF
ascii-image animation.gif -a --loop 0
```

## How It Works

This library implements Alex Harri's shape-matching approach:

1. **Character Analysis**: Each ASCII character is rendered and analyzed using 6 staggered sampling circles to create a shape vector

2. **Image Sampling**: Input images are sampled the same way, creating shape vectors for each cell

3. **K-D Tree Matching**: Fast nearest-neighbor search finds the character whose shape best matches each cell

4. **Contrast Enhancement**:
   - Global: Power function emphasizes differences
   - Directional: 9 external sampling circles detect and enhance edges

## Credits

- Algorithm: [Alex Harri's ASCII Rendering article](https://alexharri.com/blog/ascii-rendering)
- Image processing: [ImageSharp](https://sixlabors.com/products/imagesharp/)

## License

MIT License
