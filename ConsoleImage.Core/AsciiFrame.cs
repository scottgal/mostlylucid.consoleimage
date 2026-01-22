// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering

using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
///     Represents a single frame of ASCII art, optionally with color information
/// </summary>
public class AsciiFrame
{
    public AsciiFrame(char[,] characters, Rgb24[,]? colors = null, int delayMs = 0)
    {
        Characters = characters;
        Colors = colors;
        DelayMs = delayMs;
    }

    /// <summary>
    ///     The ASCII characters for this frame, row by row
    /// </summary>
    public char[,] Characters { get; }

    /// <summary>
    ///     Optional color information for each character (RGB)
    /// </summary>
    public Rgb24[,]? Colors { get; }

    /// <summary>
    ///     Frame delay in milliseconds (for animations)
    /// </summary>
    public int DelayMs { get; }

    /// <summary>
    ///     Width of the frame in characters
    /// </summary>
    public int Width => Characters.GetLength(1);

    /// <summary>
    ///     Height of the frame in characters
    /// </summary>
    public int Height => Characters.GetLength(0);

    /// <summary>
    ///     Convert to plain text string
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++) sb.Append(Characters[y, x]);
            if (y < Height - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Convert to string with ANSI color codes
    /// </summary>
    /// <param name="darkThreshold">Optional: skip colors for pixels below this brightness (for dark terminals). 0.0-1.0</param>
    /// <param name="lightThreshold">Optional: skip colors for pixels above this brightness (for light terminals). 0.0-1.0</param>
    public string ToAnsiString(float? darkThreshold = null, float? lightThreshold = null)
    {
        if (Colors == null)
            return ToString();

        var sb = new StringBuilder();
        Rgb24? lastColor = null;
        var lastWasSkipped = false;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var color = Colors[y, x];
                var brightness = BrightnessHelper.GetBrightness(color);

                // Skip colors that match terminal background
                var skipColor = BrightnessHelper.ShouldSkipColor(brightness, darkThreshold, lightThreshold);

                if (skipColor)
                {
                    // Output space without color code (blends with terminal background)
                    if (!lastWasSkipped && lastColor != null)
                        sb.Append(AnsiCodes.Reset); // Reset before outputting plain space
                    sb.Append(' ');
                    lastWasSkipped = true;
                    lastColor = null;
                }
                else
                {
                    if (lastColor == null || !AnsiCodes.ColorsEqual(lastColor.Value, color))
                    {
                        sb.Append(AnsiCodes.Foreground(color));
                        lastColor = color;
                    }

                    sb.Append(Characters[y, x]);
                    lastWasSkipped = false;
                }
            }

            if (y < Height - 1)
                sb.AppendLine();
        }

        sb.Append(AnsiCodes.Reset);
        return sb.ToString();
    }

    /// <summary>
    ///     Get a single row as a string
    /// </summary>
    public string GetRow(int row)
    {
        if (row < 0 || row >= Height)
            throw new ArgumentOutOfRangeException(nameof(row));

        var chars = new char[Width];
        for (var x = 0; x < Width; x++) chars[x] = Characters[row, x];
        return new string(chars);
    }

    /// <summary>
    ///     Get a single row with ANSI colors
    /// </summary>
    /// <param name="row">Row index</param>
    /// <param name="darkThreshold">Optional: skip colors for pixels below this brightness (for dark terminals). 0.0-1.0</param>
    /// <param name="lightThreshold">Optional: skip colors for pixels above this brightness (for light terminals). 0.0-1.0</param>
    public string GetRowAnsi(int row, float? darkThreshold = null, float? lightThreshold = null)
    {
        if (row < 0 || row >= Height)
            throw new ArgumentOutOfRangeException(nameof(row));

        if (Colors == null)
            return GetRow(row);

        var sb = new StringBuilder();
        Rgb24? lastColor = null;
        var lastWasSkipped = false;

        for (var x = 0; x < Width; x++)
        {
            var color = Colors[row, x];
            var brightness = BrightnessHelper.GetBrightness(color);

            // Skip colors that match terminal background
            var skipColor = BrightnessHelper.ShouldSkipColor(brightness, darkThreshold, lightThreshold);

            if (skipColor)
            {
                if (!lastWasSkipped && lastColor != null) sb.Append(AnsiCodes.Reset);
                sb.Append(' ');
                lastWasSkipped = true;
                lastColor = null;
            }
            else
            {
                if (lastColor == null || !AnsiCodes.ColorsEqual(lastColor.Value, color))
                {
                    sb.Append(AnsiCodes.Foreground(color));
                    lastColor = color;
                }

                sb.Append(Characters[row, x]);
                lastWasSkipped = false;
            }
        }

        sb.Append(AnsiCodes.Reset);
        return sb.ToString();
    }
}