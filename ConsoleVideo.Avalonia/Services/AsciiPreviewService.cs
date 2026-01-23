using ConsoleImage.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RenderMode = ConsoleVideo.Avalonia.Models.RenderMode;

namespace ConsoleVideo.Avalonia.Services;

/// <summary>
/// Service for rendering in-app ASCII previews of frames.
/// Converts images to ANSI-colored text for display.
/// </summary>
public class AsciiPreviewService
{
    /// <summary>
    /// Render an image to ANSI-escaped text.
    /// </summary>
    public string RenderToAnsi(Image<Rgba32> image, RenderMode mode, int maxWidth = 80, int maxHeight = 40)
    {
        var options = new RenderOptions
        {
            MaxWidth = maxWidth,
            MaxHeight = maxHeight,
            UseColor = true,
            ContrastPower = 2.5f
        };

        return mode switch
        {
            RenderMode.Blocks => RenderBlocks(image, options),
            RenderMode.Braille => RenderBraille(image, options),
            _ => RenderAscii(image, options)
        };
    }

    private static string RenderBlocks(Image<Rgba32> image, RenderOptions options)
    {
        using var renderer = new ColorBlockRenderer(options);
        return renderer.RenderImage(image);
    }

    private static string RenderBraille(Image<Rgba32> image, RenderOptions options)
    {
        using var renderer = new BrailleRenderer(options);
        return renderer.RenderImage(image);
    }

    private static string RenderAscii(Image<Rgba32> image, RenderOptions options)
    {
        using var renderer = new AsciiRenderer(options);
        var frame = renderer.RenderImage(image);
        return frame.ToAnsiString();
    }

    /// <summary>
    /// Parse ANSI text and convert to formatted segments for display.
    /// Each segment has text and a color.
    /// </summary>
    public List<AnsiSegment> ParseAnsiToSegments(string ansiText)
    {
        var segments = new List<AnsiSegment>();
        var currentColor = new AnsiColor(192, 192, 192); // Default gray
        var currentText = new System.Text.StringBuilder();
        var i = 0;

        while (i < ansiText.Length)
        {
            // Check for ANSI escape sequence
            if (i < ansiText.Length - 1 && ansiText[i] == '\x1b' && ansiText[i + 1] == '[')
            {
                // Flush current text
                if (currentText.Length > 0)
                {
                    segments.Add(new AnsiSegment(currentText.ToString(), currentColor));
                    currentText.Clear();
                }

                // Parse escape sequence
                var end = ansiText.IndexOf('m', i);
                if (end == -1)
                {
                    i++;
                    continue;
                }

                var sequence = ansiText.Substring(i + 2, end - i - 2);
                currentColor = ParseAnsiSequence(sequence, currentColor);
                i = end + 1;
            }
            else
            {
                currentText.Append(ansiText[i]);
                i++;
            }
        }

        // Flush remaining text
        if (currentText.Length > 0)
        {
            segments.Add(new AnsiSegment(currentText.ToString(), currentColor));
        }

        return segments;
    }

    private static AnsiColor ParseAnsiSequence(string sequence, AnsiColor current)
    {
        if (sequence == "0" || string.IsNullOrEmpty(sequence))
        {
            return new AnsiColor(192, 192, 192); // Reset to default
        }

        var parts = sequence.Split(';');

        // 24-bit color: 38;2;R;G;B (foreground) or 48;2;R;G;B (background)
        if (parts.Length >= 5 && parts[0] == "38" && parts[1] == "2")
        {
            if (byte.TryParse(parts[2], out var r) &&
                byte.TryParse(parts[3], out var g) &&
                byte.TryParse(parts[4], out var b))
            {
                return new AnsiColor(r, g, b);
            }
        }

        return current;
    }
}

/// <summary>
/// A segment of text with a color.
/// </summary>
public record AnsiSegment(string Text, AnsiColor Color);

/// <summary>
/// RGB color from ANSI sequence.
/// </summary>
public record AnsiColor(byte R, byte G, byte B)
{
    public uint ToUint32() => 0xFF000000u | ((uint)R << 16) | ((uint)G << 8) | B;
}
