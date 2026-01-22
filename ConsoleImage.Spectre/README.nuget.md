# mostlylucid.consoleimage.spectre

Spectre.Console integration for ConsoleImage - display ASCII art within Spectre layouts.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.consoleimage.spectre.svg)](https://www.nuget.org/packages/mostlylucid.consoleimage.spectre/)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](https://unlicense.org)

> **[Full documentation on GitHub](https://github.com/scottgal/mostlylucid.consoleimage)**

## Render Modes

|                                              ASCII                                               |                                            ColorBlocks                                             |                                               Braille                                                |                                                   Matrix                                                    |
|:------------------------------------------------------------------------------------------------:|:--------------------------------------------------------------------------------------------------:|:----------------------------------------------------------------------------------------------------:|:-----------------------------------------------------------------------------------------------------------:|
| ![ASCII](https://raw.githubusercontent.com/scottgal/ConsoleImage/master/samples/earth_ascii.gif) | ![Blocks](https://raw.githubusercontent.com/scottgal/ConsoleImage/master/samples/earth_blocks.gif) | ![Braille](https://raw.githubusercontent.com/scottgal/ConsoleImage/master/samples/earth_braille.gif) | ![Matrix](https://raw.githubusercontent.com/scottgal/ConsoleImage/master/samples/matrix_portrait_final.gif) |

## Features

- **IRenderable implementations** for all render modes
- **Animated GIF support** with Spectre's Live display
- **Matrix digital rain** effect with animations
- **Composable** with Spectre panels, tables, and layouts

## Quick Start

```csharp
using ConsoleImage.Spectre;
using Spectre.Console;

// Static images
AnsiConsole.Write(new AsciiImage("photo.jpg"));
AnsiConsole.Write(new ColorBlockImage("photo.jpg"));
AnsiConsole.Write(new BrailleImage("photo.jpg"));
AnsiConsole.Write(new MatrixImage("photo.jpg"));
```

## Renderable Classes

### AsciiImage

Shape-matched ASCII characters with optional color.

```csharp
using ConsoleImage.Core;
using ConsoleImage.Spectre;

var options = new RenderOptions { MaxWidth = 80, UseColor = true };
var image = new AsciiImage("photo.jpg", options);
AnsiConsole.Write(image);

// Or from pre-rendered frame
using var renderer = new AsciiRenderer(options);
var frame = renderer.RenderFile("photo.jpg");
AnsiConsole.Write(new AsciiImage(frame));
```

### ColorBlockImage

Unicode half-blocks for 2x vertical resolution.

```csharp
var image = new ColorBlockImage("photo.jpg", new RenderOptions { MaxWidth = 80 });
AnsiConsole.Write(image);
```

### BrailleImage

2x4 dot patterns for highest resolution.

```csharp
var image = new BrailleImage("photo.jpg", new RenderOptions { MaxWidth = 80 });
AnsiConsole.Write(image);
```

### MatrixImage

Digital rain effect overlay.

```csharp
var matrixOpts = new MatrixOptions
{
    BaseColor = new Rgba32(0, 255, 0, 255),  // Classic green
    Density = 0.5f
};
var image = new MatrixImage("photo.jpg", matrixOptions: matrixOpts);
AnsiConsole.Write(image);
```

## Animated GIFs

### AnimatedImage

Plays GIFs with Spectre's Live display.

```csharp
using ConsoleImage.Spectre;

// Play animated GIF (any mode)
var animation = new AnimatedImage("cat.gif", AnimationMode.Braille);
await animation.PlayAsync(cancellationToken);

// With loop control
await animation.PlayAsync(loopCount: 3);

// Manual frame control
await AnsiConsole.Live(animation)
    .StartAsync(async ctx =>
    {
        while (!token.IsCancellationRequested)
        {
            animation.TryAdvanceFrame();
            ctx.Refresh();
            await Task.Delay(16);
        }
    });
```

### AnimatedMatrixImage

Continuous Matrix rain animation.

```csharp
var animation = new AnimatedMatrixImage("photo.jpg", frameCount: 200);
await animation.PlayAsync(cancellationToken);
```

## Animation Modes

```csharp
public enum AnimationMode
{
    Ascii,       // Shape-matched characters
    ColorBlock,  // Unicode half-blocks
    Braille,     // 2x4 dot patterns
    Matrix       // Digital rain effect
}
```

## Composing with Spectre Layouts

```csharp
// In a panel
var panel = new Panel(new AsciiImage("photo.jpg"))
{
    Header = new PanelHeader("My Image"),
    Border = BoxBorder.Rounded
};
AnsiConsole.Write(panel);

// In a table
var table = new Table();
table.AddColumn("ASCII");
table.AddColumn("Blocks");
table.AddRow(
    new AsciiImage("photo.jpg", new RenderOptions { MaxWidth = 40 }),
    new ColorBlockImage("photo.jpg", new RenderOptions { MaxWidth = 40 })
);
AnsiConsole.Write(table);

// In columns
AnsiConsole.Write(new Columns(
    new AsciiImage("a.jpg"),
    new BrailleImage("b.jpg")
));
```

## RenderOptions

```csharp
var options = new RenderOptions
{
    MaxWidth = 80,           // Maximum width
    MaxHeight = 40,          // Maximum height
    UseColor = true,         // Enable ANSI colors
    CharacterAspectRatio = 0.5f,  // Terminal char width/height
    ContrastPower = 2.5f,    // Contrast enhancement
    Gamma = 0.65f            // Gamma correction
};
```

## Related Packages

- [mostlylucid.consoleimage](https://www.nuget.org/packages/mostlylucid.consoleimage/) - Core rendering (dependency)
- [mostlylucid.consoleimage.video](https://www.nuget.org/packages/mostlylucid.consoleimage.video/) - Video playback

## License

Public domain - [UNLICENSE](https://github.com/scottgal/mostlylucid.consoleimage/blob/master/UNLICENSE)
