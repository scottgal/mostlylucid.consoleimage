// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Simple API for quick ASCII art conversion

namespace ConsoleImage.Core;

/// <summary>
/// Simple, static API for converting images to ASCII art.
/// For the simplest usage, just call AsciiArt.Render(imagePath).
/// For more control, use AsciiRenderer directly.
/// </summary>
public static class AsciiArt
{
    #region Super Simple API

    /// <summary>
    /// Convert an image file to ASCII art string. Just works with sensible defaults.
    /// </summary>
    /// <param name="path">Path to the image file (PNG, JPG, GIF, etc.)</param>
    /// <returns>ASCII art string ready to print</returns>
    /// <example>
    /// Console.WriteLine(AsciiArt.Render("photo.jpg"));
    /// </example>
    public static string Render(string path)
    {
        using var renderer = new AsciiRenderer();
        return renderer.RenderFile(path).ToString();
    }

    /// <summary>
    /// Convert an image file to ASCII art with specified width.
    /// </summary>
    /// <param name="path">Path to the image file</param>
    /// <param name="width">Output width in characters</param>
    /// <returns>ASCII art string</returns>
    /// <example>
    /// Console.WriteLine(AsciiArt.Render("photo.jpg", 80));
    /// </example>
    public static string Render(string path, int width)
    {
        var options = new RenderOptions { MaxWidth = width, MaxHeight = width * 2 };
        using var renderer = new AsciiRenderer(options);
        return renderer.RenderFile(path).ToString();
    }

    /// <summary>
    /// Convert an image file to colored ASCII art (for terminals supporting ANSI codes).
    /// </summary>
    /// <param name="path">Path to the image file</param>
    /// <returns>ASCII art string with ANSI color codes</returns>
    /// <example>
    /// Console.WriteLine(AsciiArt.RenderColored("photo.jpg"));
    /// </example>
    public static string RenderColored(string path)
    {
        using var renderer = new AsciiRenderer();
        return renderer.RenderFile(path).ToAnsiString();
    }

    /// <summary>
    /// Convert an image file to inverted ASCII art (for dark terminals).
    /// </summary>
    /// <param name="path">Path to the image file</param>
    /// <returns>ASCII art string (inverted)</returns>
    public static string RenderInverted(string path)
    {
        using var renderer = new AsciiRenderer();
        return renderer.RenderFile(path).ToString();
    }

    #endregion

    #region Stream-based API

    /// <summary>
    /// Convert an image stream to ASCII art string.
    /// </summary>
    public static string FromStream(Stream stream, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        var frame = renderer.RenderStream(stream);
        return options?.UseColor == true ? frame.ToAnsiString() : frame.ToString();
    }

    /// <summary>
    /// Convert a byte array to ASCII art string.
    /// </summary>
    public static string FromBytes(byte[] data, RenderOptions? options = null)
    {
        using var stream = new MemoryStream(data);
        return FromStream(stream, options);
    }

    #endregion

    #region Full Options API

    /// <summary>
    /// Convert an image file to ASCII art string with full options.
    /// </summary>
    /// <param name="path">Path to the image file</param>
    /// <param name="options">Rendering options</param>
    /// <returns>ASCII art string</returns>
    /// <example>
    /// var options = new RenderOptions { MaxWidth = 100, UseColor = true };
    /// Console.WriteLine(AsciiArt.FromFile("photo.jpg", options));
    /// </example>
    public static string FromFile(string path, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        var frame = renderer.RenderFile(path);
        return options?.UseColor == true ? frame.ToAnsiString() : frame.ToString();
    }

    /// <summary>
    /// Convert an image file to an AsciiFrame with character array access.
    /// </summary>
    public static AsciiFrame FrameFromFile(string path, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        return renderer.RenderFile(path);
    }

    #endregion

    #region Animated GIF API

    /// <summary>
    /// Convert an animated GIF to a list of ASCII frames.
    /// </summary>
    /// <param name="path">Path to the GIF file</param>
    /// <param name="options">Rendering options</param>
    /// <returns>List of ASCII frames with timing information</returns>
    /// <example>
    /// var frames = AsciiArt.GifFromFile("animation.gif");
    /// foreach (var frame in frames)
    /// {
    ///     Console.Clear();
    ///     Console.WriteLine(frame.ToString());
    ///     Thread.Sleep(frame.DelayMs);
    /// }
    /// </example>
    public static IReadOnlyList<AsciiFrame> GifFromFile(string path, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        return renderer.RenderGif(path);
    }

    /// <summary>
    /// Convert an animated GIF stream to a list of ASCII frames.
    /// </summary>
    public static IReadOnlyList<AsciiFrame> GifFromStream(Stream stream, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        return renderer.RenderGifStream(stream);
    }

    /// <summary>
    /// Play an animated GIF in the console.
    /// </summary>
    /// <param name="path">Path to the GIF file</param>
    /// <param name="options">Rendering options (LoopCount controls playback)</param>
    /// <param name="cancellationToken">Token to cancel playback</param>
    /// <example>
    /// await AsciiArt.PlayGif("animation.gif");
    /// </example>
    public static async Task PlayGif(string path, RenderOptions? options = null,
                                      CancellationToken cancellationToken = default)
    {
        options ??= RenderOptions.ForAnimation();
        var frames = GifFromFile(path, options);

        using var player = new AsciiAnimationPlayer(frames, options.UseColor, options.LoopCount);
        await player.PlayAsync(cancellationToken);
    }

    #endregion

    #region Color Block Rendering

    /// <summary>
    /// Render image as colored blocks using Unicode half-block characters.
    /// Each character cell displays 2 vertical pixels for higher resolution.
    /// Requires a terminal that supports 24-bit ANSI colors.
    /// </summary>
    /// <param name="path">Path to the image file</param>
    /// <returns>String with ANSI color codes and Unicode blocks</returns>
    /// <example>
    /// Console.WriteLine(AsciiArt.RenderColorBlocks("photo.jpg"));
    /// </example>
    public static string RenderColorBlocks(string path)
    {
        using var renderer = new ColorBlockRenderer();
        return renderer.RenderFile(path);
    }

    /// <summary>
    /// Render image as colored blocks with specified options.
    /// </summary>
    public static string RenderColorBlocks(string path, RenderOptions? options)
    {
        using var renderer = new ColorBlockRenderer(options);
        return renderer.RenderFile(path);
    }

    /// <summary>
    /// Play animated GIF using colored block rendering.
    /// </summary>
    public static async Task PlayColorBlockGif(string path, RenderOptions? options = null,
                                                CancellationToken cancellationToken = default)
    {
        options ??= RenderOptions.ForAnimation();
        using var renderer = new ColorBlockRenderer(options);
        var frames = renderer.RenderGif(path);

        int startRow = Console.CursorTop;
        int loops = 0;
        int loopCount = options.LoopCount;

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var frame in frames)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    Console.SetCursorPosition(0, startRow);
                }
                catch
                {
                    // Ignore position errors
                }

                Console.Write(frame.Content);
                Console.Write("\x1b[0m"); // Reset colors

                if (frame.DelayMs > 0)
                {
                    await Task.Delay(frame.DelayMs, cancellationToken);
                }
            }

            loops++;
            if (loopCount > 0 && loops >= loopCount)
                break;
        }
    }

    #endregion

    #region Configuration Helpers

    /// <summary>
    /// Create a pre-configured renderer that can be reused for multiple images.
    /// More efficient when rendering many images with the same settings.
    /// </summary>
    /// <param name="options">Rendering options</param>
    /// <returns>Configured renderer (remember to dispose)</returns>
    /// <example>
    /// using var renderer = AsciiArt.CreateRenderer(RenderOptions.HighDetail);
    /// foreach (var imagePath in imagePaths)
    /// {
    ///     Console.WriteLine(renderer.RenderFile(imagePath).ToString());
    /// }
    /// </example>
    public static AsciiRenderer CreateRenderer(RenderOptions? options = null)
    {
        return new AsciiRenderer(options);
    }

    /// <summary>
    /// Create a color block renderer for high-fidelity colored output.
    /// </summary>
    public static ColorBlockRenderer CreateColorBlockRenderer(RenderOptions? options = null)
    {
        return new ColorBlockRenderer(options);
    }

    #endregion
}
