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
}