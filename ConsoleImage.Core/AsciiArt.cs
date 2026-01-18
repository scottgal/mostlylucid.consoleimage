// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Convenience API for quick ASCII art conversion

namespace ConsoleImage.Core;

/// <summary>
/// Static convenience methods for quick ASCII art conversion.
/// For more control, use AsciiRenderer directly.
/// </summary>
public static class AsciiArt
{
    /// <summary>
    /// Convert an image file to ASCII art string
    /// </summary>
    public static string FromFile(string path, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        var frame = renderer.RenderFile(path);
        return options?.UseColor == true ? frame.ToAnsiString() : frame.ToString();
    }

    /// <summary>
    /// Convert an image stream to ASCII art string
    /// </summary>
    public static string FromStream(Stream stream, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        var frame = renderer.RenderStream(stream);
        return options?.UseColor == true ? frame.ToAnsiString() : frame.ToString();
    }

    /// <summary>
    /// Convert a byte array to ASCII art string
    /// </summary>
    public static string FromBytes(byte[] data, RenderOptions? options = null)
    {
        using var stream = new MemoryStream(data);
        return FromStream(stream, options);
    }

    /// <summary>
    /// Convert an image file to ASCII frame with character array
    /// </summary>
    public static AsciiFrame FrameFromFile(string path, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        return renderer.RenderFile(path);
    }

    /// <summary>
    /// Convert an animated GIF to a list of ASCII frames
    /// </summary>
    public static IReadOnlyList<AsciiFrame> GifFromFile(string path, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        return renderer.RenderGif(path);
    }

    /// <summary>
    /// Convert an animated GIF stream to a list of ASCII frames
    /// </summary>
    public static IReadOnlyList<AsciiFrame> GifFromStream(Stream stream, RenderOptions? options = null)
    {
        using var renderer = new AsciiRenderer(options);
        return renderer.RenderGifStream(stream);
    }
}
