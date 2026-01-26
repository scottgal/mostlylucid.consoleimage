// ANSI escape code utilities
// Centralizes terminal color code generation for consistent output

using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
///     Utility class for generating ANSI escape sequences.
///     Provides efficient methods for terminal color output.
/// </summary>
public static class AnsiCodes
{
    /// <summary>
    ///     Reset all attributes to default.
    /// </summary>
    public const string Reset = "\x1b[0m";

    /// <summary>
    ///     Escape character for ANSI sequences.
    /// </summary>
    public const string Escape = "\x1b[";

    /// <summary>
    ///     Generate a 24-bit foreground color code.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Foreground(Rgba32 color)
    {
        return $"\x1b[38;2;{color.R};{color.G};{color.B}m";
    }

    /// <summary>
    ///     Generate a 24-bit foreground color code from RGB values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Foreground(int r, int g, int b)
    {
        return $"\x1b[38;2;{r};{g};{b}m";
    }

    /// <summary>
    ///     Generate a 24-bit foreground color code for Rgb24.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Foreground(Rgb24 color)
    {
        return $"\x1b[38;2;{color.R};{color.G};{color.B}m";
    }

    /// <summary>
    ///     Generate a 24-bit background color code.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Background(Rgba32 color)
    {
        return $"\x1b[48;2;{color.R};{color.G};{color.B}m";
    }

    /// <summary>
    ///     Generate combined foreground and background color codes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ForegroundAndBackground(Rgba32 foreground, Rgba32 background)
    {
        return
            $"\x1b[38;2;{foreground.R};{foreground.G};{foreground.B};48;2;{background.R};{background.G};{background.B}m";
    }

    /// <summary>
    ///     Append foreground color code to StringBuilder (more efficient for loops).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendForeground(StringBuilder sb, Rgba32 color)
    {
        sb.Append("\x1b[38;2;");
        sb.Append(color.R);
        sb.Append(';');
        sb.Append(color.G);
        sb.Append(';');
        sb.Append(color.B);
        sb.Append('m');
    }

    /// <summary>
    ///     Append foreground color code to StringBuilder from RGB values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendForeground(StringBuilder sb, int r, int g, int b)
    {
        sb.Append("\x1b[38;2;");
        sb.Append(r);
        sb.Append(';');
        sb.Append(g);
        sb.Append(';');
        sb.Append(b);
        sb.Append('m');
    }

    /// <summary>
    ///     Append foreground and background color codes to StringBuilder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendForegroundAndBackground(StringBuilder sb, Rgba32 foreground, Rgba32 background)
    {
        sb.Append("\x1b[38;2;");
        sb.Append(foreground.R);
        sb.Append(';');
        sb.Append(foreground.G);
        sb.Append(';');
        sb.Append(foreground.B);
        sb.Append(";48;2;");
        sb.Append(background.R);
        sb.Append(';');
        sb.Append(background.G);
        sb.Append(';');
        sb.Append(background.B);
        sb.Append('m');
    }

    /// <summary>
    ///     Append reset and then foreground color code to StringBuilder.
    ///     Used for blocks where we need clean color transitions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendResetAndForeground(StringBuilder sb, Rgba32 color)
    {
        sb.Append("\x1b[0m\x1b[38;2;");
        sb.Append(color.R);
        sb.Append(';');
        sb.Append(color.G);
        sb.Append(';');
        sb.Append(color.B);
        sb.Append('m');
    }

    /// <summary>
    ///     Append foreground color code to StringBuilder for Rgb24.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendForeground(StringBuilder sb, Rgb24 color)
    {
        sb.Append("\x1b[38;2;");
        sb.Append(color.R);
        sb.Append(';');
        sb.Append(color.G);
        sb.Append(';');
        sb.Append(color.B);
        sb.Append('m');
    }

    // ── 256-color and 16-color support ──

    // Standard 16-color ANSI palette (approximate RGB values)
    private static readonly (byte R, byte G, byte B)[] Ansi16Colors =
    {
        (0, 0, 0), (128, 0, 0), (0, 128, 0), (128, 128, 0),
        (0, 0, 128), (128, 0, 128), (0, 128, 128), (192, 192, 192),
        (128, 128, 128), (255, 0, 0), (0, 255, 0), (255, 255, 0),
        (0, 0, 255), (255, 0, 255), (0, 255, 255), (255, 255, 255)
    };

    /// <summary>
    ///     Convert RGB to nearest 256-color palette index.
    ///     Uses 6x6x6 color cube (indices 16-231) or grey ramp (232-255).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RgbTo256(byte r, byte g, byte b)
    {
        // Check if greyscale (use grey ramp for better precision)
        if (r == g && g == b)
        {
            if (r < 8) return 16; // black
            if (r > 248) return 231; // white
            return 232 + (int)Math.Round((r - 8) / 247.0 * 23);
        }

        // Map to 6x6x6 color cube
        var ri = (int)Math.Round(r / 255.0 * 5);
        var gi = (int)Math.Round(g / 255.0 * 5);
        var bi = (int)Math.Round(b / 255.0 * 5);
        return 16 + 36 * ri + 6 * gi + bi;
    }

    /// <summary>
    ///     Convert RGB to nearest 16-color ANSI index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RgbTo16(byte r, byte g, byte b)
    {
        var bestIdx = 0;
        var bestDist = int.MaxValue;
        for (var i = 0; i < 16; i++)
        {
            var dr = r - Ansi16Colors[i].R;
            var dg = g - Ansi16Colors[i].G;
            var db = b - Ansi16Colors[i].B;
            var dist = dr * dr + dg * dg + db * db;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    /// <summary>
    ///     Convert a brightness value (0-255) to a 256-color grey ramp index (232-255).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BrightnessToGrey256(byte brightness)
    {
        if (brightness < 8) return 232;
        if (brightness > 248) return 255;
        return 232 + (int)Math.Round((brightness - 8) / 247.0 * 23);
    }

    /// <summary>
    ///     Append foreground color using 256-color palette.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendForeground256(StringBuilder sb, byte r, byte g, byte b)
    {
        sb.Append("\x1b[38;5;");
        sb.Append(RgbTo256(r, g, b));
        sb.Append('m');
    }

    /// <summary>
    ///     Append foreground color using 16-color ANSI codes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendForeground16(StringBuilder sb, byte r, byte g, byte b)
    {
        var idx = RgbTo16(r, g, b);
        sb.Append(idx < 8 ? "\x1b[" : "\x1b[");
        sb.Append(idx < 8 ? 30 + idx : 82 + idx); // 30-37 or 90-97
        sb.Append('m');
    }

    /// <summary>
    ///     Append foreground color using the specified color depth.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendForegroundAdaptive(StringBuilder sb, byte r, byte g, byte b, ColorDepth depth)
    {
        switch (depth)
        {
            case ColorDepth.Palette256:
                AppendForeground256(sb, r, g, b);
                break;
            case ColorDepth.Palette16:
                AppendForeground16(sb, r, g, b);
                break;
            default:
                sb.Append("\x1b[38;2;");
                sb.Append(r);
                sb.Append(';');
                sb.Append(g);
                sb.Append(';');
                sb.Append(b);
                sb.Append('m');
                break;
        }
    }

    /// <summary>
    ///     Append foreground and background colors using the specified color depth.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendForegroundAndBackgroundAdaptive(
        StringBuilder sb, Rgba32 foreground, Rgba32 background, ColorDepth depth)
    {
        switch (depth)
        {
            case ColorDepth.Palette256:
                sb.Append("\x1b[38;5;");
                sb.Append(RgbTo256(foreground.R, foreground.G, foreground.B));
                sb.Append(";48;5;");
                sb.Append(RgbTo256(background.R, background.G, background.B));
                sb.Append('m');
                break;
            case ColorDepth.Palette16:
                var fgIdx = RgbTo16(foreground.R, foreground.G, foreground.B);
                var bgIdx = RgbTo16(background.R, background.G, background.B);
                sb.Append("\x1b[");
                sb.Append(fgIdx < 8 ? 30 + fgIdx : 82 + fgIdx);
                sb.Append(';');
                sb.Append(bgIdx < 8 ? 40 + bgIdx : 92 + bgIdx);
                sb.Append('m');
                break;
            default:
                AppendForegroundAndBackground(sb, foreground, background);
                break;
        }
    }

    /// <summary>
    ///     Append reset and foreground color using the specified color depth.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendResetAndForegroundAdaptive(
        StringBuilder sb, Rgba32 color, ColorDepth depth)
    {
        sb.Append(Reset);
        AppendForegroundAdaptive(sb, color.R, color.G, color.B, depth);
    }

    /// <summary>
    ///     Append foreground greyscale using 256-color grey ramp (232-255).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendForegroundGrey256(StringBuilder sb, byte brightness)
    {
        sb.Append("\x1b[38;5;");
        sb.Append(BrightnessToGrey256(brightness));
        sb.Append('m');
    }

    /// <summary>
    ///     Check if two Rgba32 colors are equal (for color change detection).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ColorsEqual(Rgba32 a, Rgba32 b)
    {
        return a.R == b.R && a.G == b.G && a.B == b.B;
    }

    /// <summary>
    ///     Check if two Rgb24 colors are equal (for color change detection).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ColorsEqual(Rgb24 a, Rgb24 b)
    {
        return a.R == b.R && a.G == b.G && a.B == b.B;
    }

    /// <summary>
    ///     Check if two Rgba32 colors are similar within a threshold (for temporal stability).
    ///     Used to prevent flickering between similar colors in animations.
    /// </summary>
    /// <param name="a">First color</param>
    /// <param name="b">Second color</param>
    /// <param name="threshold">Per-channel difference threshold (0-255). Default: 15</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ColorsSimilar(Rgba32 a, Rgba32 b, int threshold = 15)
    {
        return Math.Abs(a.R - b.R) <= threshold &&
               Math.Abs(a.G - b.G) <= threshold &&
               Math.Abs(a.B - b.B) <= threshold;
    }

    /// <summary>
    ///     Check if two Rgb24 colors are similar within a threshold (for temporal stability).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ColorsSimilar(Rgb24 a, Rgb24 b, int threshold = 15)
    {
        return Math.Abs(a.R - b.R) <= threshold &&
               Math.Abs(a.G - b.G) <= threshold &&
               Math.Abs(a.B - b.B) <= threshold;
    }

    /// <summary>
    ///     Snap color to previous if similar (for temporal stability).
    ///     Returns the previous color if similar, otherwise returns current.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba32 StabilizeColor(Rgba32 current, Rgba32 previous, int threshold)
    {
        return ColorsSimilar(current, previous, threshold) ? previous : current;
    }
}