# ConsoleImage.Player.Spectre

Spectre.Console integration for ConsoleImage.Player. Display pre-rendered ASCII art documents as `IRenderable` objects within Spectre.Console layouts.

## Installation

```bash
dotnet add package mostlylucid.consoleimage.player.spectre
```

## Quick Start

### Display a static document

```csharp
using ConsoleImage.Player;
using ConsoleImage.Player.Spectre;
using Spectre.Console;

var doc = await PlayerDocument.LoadAsync("image.json");
var image = new DocumentImage(doc);

// Display in a panel
AnsiConsole.Write(new Panel(image).Header("My Image"));
```

### Play an animation

```csharp
using ConsoleImage.Player.Spectre;
using Spectre.Console;

var animation = await AnimatedDocument.FromFileAsync("animation.json");
await animation.PlayAsync(cancellationToken);
```

### Manual animation control

```csharp
var animation = await AnimatedDocument.FromFileAsync("animation.json");

await AnsiConsole.Live(animation).StartAsync(async ctx =>
{
    while (!token.IsCancellationRequested)
    {
        animation.TryAdvanceFrame();
        ctx.Refresh();
        await Task.Delay(16);
    }
});
```

### With progress display

```csharp
var animation = await AnimatedDocument.FromFileAsync("animation.json");
await animation.PlayWithProgressAsync(cancellationToken, loopCount: 3);
```

## Classes

### DocumentImage

Static `IRenderable` for displaying a single frame:

```csharp
var doc = await PlayerDocument.LoadAsync("file.json");

// Display first frame
var image = new DocumentImage(doc);

// Display specific frame
var image = new DocumentImage(doc, frameIndex: 5);

// Use in layouts
AnsiConsole.Write(new Panel(image));
AnsiConsole.Write(new Columns(image1, image2));
```

### DocumentFrame

`IRenderable` wrapper for a single `PlayerFrame`:

```csharp
var doc = await PlayerDocument.LoadAsync("file.json");
var frame = new DocumentFrame(doc.Frames[0]);
AnsiConsole.Write(frame);
```

### AnimatedDocument

Animated `IRenderable` for use with `AnsiConsole.Live()`:

```csharp
var animation = await AnimatedDocument.FromFileAsync("animation.json");

// Properties
animation.CurrentFrame   // Current frame index
animation.FrameCount     // Total frames
animation.IsAnimated     // true if multi-frame
animation.TargetFps      // Override frame timing

// Methods
animation.TryAdvanceFrame()  // Advance if time elapsed
animation.Reset()            // Back to frame 0
animation.SetFrame(n)        // Jump to frame n

// Play methods (extension)
await animation.PlayAsync(ct, loopCount: 3);
await animation.PlayWithProgressAsync(ct);
```

## Layout Integration

All renderables work with Spectre.Console layouts:

```csharp
var doc = await PlayerDocument.LoadAsync("cat.json");
var image = new DocumentImage(doc);

// In a table
var table = new Table();
table.AddColumn("Image");
table.AddRow(image);
AnsiConsole.Write(table);

// In columns
AnsiConsole.Write(new Columns(image, new Text("Description")));

// In a panel with border
AnsiConsole.Write(new Panel(image)
    .Header("ASCII Art")
    .Border(BoxBorder.Double));
```

## Creating Documents

Use the CLI tools to create JSON documents:

```bash
consoleimage photo.png -o image.json
consoleimage animation.gif -o animation.json
consolevideo movie.mp4 -o video.json -w 80
```

## License

Unlicense - Public Domain
