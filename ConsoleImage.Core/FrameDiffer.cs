// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Frame differencing for efficient animation - only updates changed pixels

using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
///     Computes differences between frames and generates efficient ANSI update sequences
///     that only redraw changed pixels.
/// </summary>
public static class FrameDiffer
{
    /// <summary>
    ///     Pre-compute diff sequences for a list of frames.
    ///     Returns an array where index 0 is the full first frame,
    ///     and subsequent indices contain only the changes from the previous frame.
    /// </summary>
    public static string[] ComputeDiffs(IReadOnlyList<AsciiFrame> frames, bool useColor)
    {
        if (frames.Count == 0) return Array.Empty<string>();

        var diffs = new string[frames.Count];

        // First frame is always full render
        diffs[0] = useColor ? frames[0].ToAnsiString() : frames[0].ToString();

        // Subsequent frames are diffs from previous
        for (var i = 1; i < frames.Count; i++) diffs[i] = ComputeFrameDiff(frames[i - 1], frames[i], useColor);

        return diffs;
    }

    /// <summary>
    ///     Compute the diff between two frames, returning an ANSI string
    ///     that updates only changed cells.
    /// </summary>
    public static string ComputeFrameDiff(AsciiFrame prev, AsciiFrame curr, bool useColor)
    {
        if (prev.Width != curr.Width || prev.Height != curr.Height)
            // Dimensions changed - full redraw needed
            return useColor ? curr.ToAnsiString() : curr.ToString();

        var sb = new StringBuilder();
        var changedCells = 0;
        var totalCells = curr.Width * curr.Height;

        Rgb24? lastColor = null;

        for (var y = 0; y < curr.Height; y++)
        for (var x = 0; x < curr.Width; x++)
        {
            var prevChar = prev.Characters[y, x];
            var currChar = curr.Characters[y, x];

            var prevColor = prev.Colors?[y, x];
            var currColor = curr.Colors?[y, x];

            var charChanged = prevChar != currChar;
            var colorChanged = useColor && !ColorsEqual(prevColor, currColor);

            if (charChanged || colorChanged)
            {
                changedCells++;

                // Move cursor to this position (1-indexed)
                sb.Append($"\x1b[{y + 1};{x + 1}H");

                // Set color if needed
                if (useColor && currColor.HasValue)
                {
                    var c = currColor.Value;
                    if (lastColor == null || !lastColor.Value.Equals(c))
                    {
                        sb.Append($"\x1b[38;2;{c.R};{c.G};{c.B}m");
                        lastColor = c;
                    }
                }

                sb.Append(currChar);
            }
        }

        // If more than 60% changed, just do a full redraw (more efficient)
        if (changedCells > totalCells * 0.6) return useColor ? curr.ToAnsiString() : curr.ToString();

        // Reset color at end
        if (useColor && sb.Length > 0) sb.Append("\x1b[0m");

        return sb.ToString();
    }

    /// <summary>
    ///     Compute diffs for color block frames (string-based).
    ///     Since color blocks use complex ANSI sequences, we do line-by-line comparison.
    /// </summary>
    public static string[] ComputeColorBlockDiffs(IReadOnlyList<ColorBlockFrame> frames)
    {
        if (frames.Count == 0) return Array.Empty<string>();

        var diffs = new string[frames.Count];

        // First frame is always full
        diffs[0] = frames[0].Content;

        // For color blocks, compare line by line
        for (var i = 1; i < frames.Count; i++)
            diffs[i] = ComputeColorBlockDiff(frames[i - 1].Content, frames[i].Content);

        return diffs;
    }

    private static string ComputeColorBlockDiff(string prev, string curr)
    {
        var prevLines = prev.Split('\n');
        var currLines = curr.Split('\n');

        // If line count differs, full redraw
        if (prevLines.Length != currLines.Length) return curr;

        var sb = new StringBuilder();
        var changedLines = 0;

        for (var y = 0; y < currLines.Length; y++)
            if (prevLines[y] != currLines[y])
            {
                changedLines++;
                // Move to line start (1-indexed) and clear line before writing
                sb.Append($"\x1b[{y + 1};1H\x1b[2K");
                sb.Append(currLines[y]);
            }

        // If more than 50% of lines changed, full redraw
        if (changedLines > currLines.Length * 0.5) return curr;

        return sb.ToString();
    }

    private static bool ColorsEqual(Rgb24? a, Rgb24? b)
    {
        if (!a.HasValue && !b.HasValue) return true;
        if (!a.HasValue || !b.HasValue) return false;
        return a.Value.R == b.Value.R && a.Value.G == b.Value.G && a.Value.B == b.Value.B;
    }
}