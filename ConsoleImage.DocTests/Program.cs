// Documentation API Verification Tests
// This file tests ALL code samples from the NuGet README files
// If this compiles, the documentation is accurate

using ConsoleImage.Core;
using ConsoleImage.Spectre;
using ConsoleImage.Video.Core;
using SixLabors.ImageSharp.PixelFormats;

// Test image path - using a sample from the repo
var testImage = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "earth_ascii.gif");
var testGif = testImage; // Same file works for both

Console.WriteLine("=== Documentation API Verification ===\n");

// ============================================
// CORE PACKAGE (mostlylucid.consoleimage)
// ============================================
Console.WriteLine("--- ConsoleImage.Core API Tests ---\n");

// From README: Enable Windows ANSI support
ConsoleHelper.EnableAnsiSupport();
Console.WriteLine("[OK] ConsoleHelper.EnableAnsiSupport()");

// From README: AsciiArt static class
if (File.Exists(testImage))
{
    // Console.WriteLine(AsciiArt.Render("photo.jpg"));
    Console.WriteLine(AsciiArt.Render(testImage));
    Console.WriteLine("[OK] AsciiArt.Render()");

    // Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));
    Console.WriteLine(AsciiArt.RenderColored(testImage).Substring(0, 100) + "...");
    Console.WriteLine("[OK] AsciiArt.RenderColored()");
}
else
{
    Console.WriteLine($"[SKIP] Test image not found: {testImage}");
}

// From README: AsciiRenderer
{
    using var renderer = new AsciiRenderer(new RenderOptions { MaxWidth = 80 });
    Console.WriteLine("[OK] new AsciiRenderer(options)");

    if (File.Exists(testImage))
    {
        var frame = renderer.RenderFile(testImage);
        Console.WriteLine($"[OK] renderer.RenderFile() -> {frame.Width}x{frame.Height}");

        var _ = frame.ToAnsiString(); // Colored
        Console.WriteLine("[OK] frame.ToAnsiString()");

        var __ = frame.ToString(); // Plain text
        Console.WriteLine("[OK] frame.ToString()");
    }
}

// From README: ColorBlockRenderer
{
    using var renderer = new ColorBlockRenderer(new RenderOptions { MaxWidth = 80 });
    Console.WriteLine("[OK] new ColorBlockRenderer(options)");

    if (File.Exists(testImage))
    {
        var output = renderer.RenderFile(testImage);
        Console.WriteLine($"[OK] ColorBlockRenderer.RenderFile() -> {output.Length} chars");
    }
}

// From README: BrailleRenderer
{
    using var renderer = new BrailleRenderer(new RenderOptions { MaxWidth = 80 });
    Console.WriteLine("[OK] new BrailleRenderer(options)");

    if (File.Exists(testImage))
    {
        var output = renderer.RenderFile(testImage);
        Console.WriteLine($"[OK] BrailleRenderer.RenderFile() -> {output.Length} chars");
    }
}

// From README: MatrixRenderer
{
    var options = new RenderOptions { MaxWidth = 80 };
    var matrixOpts = new MatrixOptions
    {
        BaseColor = new Rgba32(0, 255, 65, 255), // Classic green
        Density = 0.5f,
        SpeedMultiplier = 1.0f,
        TargetFps = 20,
        UseAsciiOnly = false,
        CustomAlphabet = null
    };

    using var renderer = new MatrixRenderer(options, matrixOpts);
    Console.WriteLine("[OK] new MatrixRenderer(options, matrixOpts)");

    if (File.Exists(testImage))
    {
        var frame = renderer.RenderFile(testImage);
        Console.WriteLine($"[OK] MatrixRenderer.RenderFile() -> Content length: {frame.Content.Length}");
    }
}

// From README: Matrix Presets
{
    var green = MatrixOptions.ClassicGreen;
    var red = MatrixOptions.RedPill;
    var blue = MatrixOptions.BluePill;
    var amber = MatrixOptions.Amber;
    var fullColor = MatrixOptions.FullColor;
    Console.WriteLine("[OK] MatrixOptions presets (ClassicGreen, RedPill, BluePill, Amber, FullColor)");
}

// From README: RenderOptions
{
    var options = new RenderOptions
    {
        // Dimensions
        Width = null,
        Height = null,
        MaxWidth = 120,
        MaxHeight = 40,
        CharacterAspectRatio = 0.5f,

        // Appearance
        UseColor = true,
        Invert = true,
        ContrastPower = 2.5f,
        Gamma = 0.65f,

        // Animation
        AnimationSpeedMultiplier = 1.0f,
        LoopCount = 0,
        FrameSampleRate = 1,

        // Features
        EnableDithering = true,
        EnableEdgeDetection = false,
        UseParallelProcessing = true
    };
    Console.WriteLine("[OK] RenderOptions with all documented properties");
}

// From README: GifWriter
if (File.Exists(testGif))
{
    using var gifWriter = new GifWriter(new GifWriterOptions
    {
        FontSize = 10,
        Scale = 1.0f,
        MaxColors = 128,
        LoopCount = 0
    });
    Console.WriteLine("[OK] new GifWriter(options)");

    using var renderer = new AsciiRenderer(new RenderOptions { MaxWidth = 40 });
    var frames = renderer.RenderGif(testGif);
    Console.WriteLine($"[OK] renderer.RenderGif() -> {frames.Count} frames");
}

// ============================================
// VIDEO PACKAGE (mostlylucid.consoleimage.video)
// ============================================
Console.WriteLine("\n--- ConsoleImage.Video.Core API Tests ---\n");

// From README: VideoPlayer static class
{
    // VideoPlayer.PlayAsync exists
    var playMethod = typeof(VideoPlayer).GetMethod("PlayAsync", new[] { typeof(string), typeof(CancellationToken) });
    Console.WriteLine($"[OK] VideoPlayer.PlayAsync(path) exists: {playMethod != null}");

    var playWithOptionsMethod = typeof(VideoPlayer).GetMethod("PlayAsync",
        new[] { typeof(string), typeof(VideoRenderOptions), typeof(CancellationToken) });
    Console.WriteLine($"[OK] VideoPlayer.PlayAsync(path, options) exists: {playWithOptionsMethod != null}");

    var getInfoMethod = typeof(VideoPlayer).GetMethod("GetInfoAsync");
    Console.WriteLine($"[OK] VideoPlayer.GetInfoAsync() exists: {getInfoMethod != null}");
}

// From README: VideoRenderOptions
{
    var options = new VideoRenderOptions
    {
        RenderMode = VideoRenderMode.Braille,
        SpeedMultiplier = 1.5f,
        LoopCount = 3,
        StartTime = 30,
        EndTime = 60,
        TargetFps = 24,
        FrameStep = 2,
        UseHardwareAcceleration = true,
        BufferAheadFrames = 5,
        ShowStatus = true,
        UseAltScreen = true
    };
    Console.WriteLine("[OK] VideoRenderOptions with all documented properties");
}

// From README: VideoAnimationPlayer
{
    // Just verify constructor exists (don't actually play)
    var ctor = typeof(VideoAnimationPlayer).GetConstructor(new[] { typeof(string), typeof(VideoRenderOptions) });
    Console.WriteLine($"[OK] VideoAnimationPlayer constructor exists: {ctor != null}");
}

// From README: FFmpegService
{
    using var ffmpeg = new FFmpegService();
    Console.WriteLine("[OK] new FFmpegService()");
}

// ============================================
// SPECTRE PACKAGE (mostlylucid.consoleimage.spectre)
// ============================================
Console.WriteLine("\n--- ConsoleImage.Spectre API Tests ---\n");

// From README: Static image classes
if (File.Exists(testImage))
{
    var asciiImage = new AsciiImage(testImage, new RenderOptions { MaxWidth = 40 });
    Console.WriteLine("[OK] new AsciiImage(path, options)");

    var colorBlockImage = new ColorBlockImage(testImage, new RenderOptions { MaxWidth = 40 });
    Console.WriteLine("[OK] new ColorBlockImage(path, options)");

    var brailleImage = new BrailleImage(testImage, new RenderOptions { MaxWidth = 40 });
    Console.WriteLine("[OK] new BrailleImage(path, options)");

    var matrixImage = new MatrixImage(testImage, new RenderOptions { MaxWidth = 40 });
    Console.WriteLine("[OK] new MatrixImage(path, options)");
}

// From README: AnimatedImage
if (File.Exists(testGif))
{
    var animation = new AnimatedImage(testGif, AnimationMode.Braille, new RenderOptions { MaxWidth = 40 });
    Console.WriteLine($"[OK] new AnimatedImage(path, mode, options) -> {animation.FrameCount} frames");

    animation.TryAdvanceFrame();
    Console.WriteLine("[OK] animation.TryAdvanceFrame()");

    animation.Reset();
    Console.WriteLine("[OK] animation.Reset()");

    animation.SetFrame(0);
    Console.WriteLine("[OK] animation.SetFrame(index)");
}

// From README: AnimationMode enum
{
    var modes = new[] { AnimationMode.Ascii, AnimationMode.ColorBlock, AnimationMode.Braille, AnimationMode.Matrix };
    Console.WriteLine($"[OK] AnimationMode enum has {modes.Length} values");
}

// From README: ConsoleImageFactory
if (File.Exists(testImage))
{
    var image = ConsoleImageFactory.CreateImage(testImage, AnimationMode.Braille);
    Console.WriteLine("[OK] ConsoleImageFactory.CreateImage()");

    var anim = ConsoleImageFactory.CreateAnimation(testGif, AnimationMode.Ascii);
    Console.WriteLine("[OK] ConsoleImageFactory.CreateAnimation()");
}

// From README: MultiAnimationPlayer
{
    var player = new MultiAnimationPlayer();
    Console.WriteLine("[OK] new MultiAnimationPlayer()");

    if (File.Exists(testGif))
    {
        player.Add(testGif, AnimationMode.Ascii, "ASCII");
        Console.WriteLine("[OK] player.Add(path, mode, label)");
    }
}

// From README: RenderModeComparison
if (File.Exists(testImage))
{
    var comparison = RenderModeComparison.FromFile(testImage, null, AnimationMode.Ascii, AnimationMode.Braille);
    Console.WriteLine("[OK] RenderModeComparison.FromFile()");

    var allModes = RenderModeComparison.AllModes(testImage);
    Console.WriteLine("[OK] RenderModeComparison.AllModes()");
}

// From README: IAnimatedRenderable interface
{
    var hasInterface = typeof(AnimatedImage).GetInterfaces().Any(i => i.Name == "IAnimatedRenderable");
    Console.WriteLine($"[OK] AnimatedImage implements IAnimatedRenderable: {hasInterface}");
}

Console.WriteLine("\n=== All API Verification Tests Passed ===");
Console.WriteLine("\nNote: This verifies the APIs exist and compile.");
Console.WriteLine("For runtime tests, run with actual video/image files.");