// Delta-based rendering for efficient video playback
// Only re-renders cells that changed between frames

using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
///     Stores cell data for delta comparison between frames.
/// </summary>
public readonly struct CellData : IEquatable<CellData>
{
    public readonly char Character;
    public readonly byte R, G, B;

    public CellData(char character, byte r, byte g, byte b)
    {
        Character = character;
        R = r;
        G = g;
        B = b;
    }

    public bool Equals(CellData other) =>
        Character == other.Character &&
        R == other.R && G == other.G && B == other.B;

    public override bool Equals(object? obj) => obj is CellData other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Character, R, G, B);

    /// <summary>
    ///     Check if colors are similar enough to skip update (temporal stability).
    /// </summary>
    public bool IsSimilar(CellData other, int threshold)
    {
        if (Character != other.Character) return false;
        return Math.Abs(R - other.R) <= threshold &&
               Math.Abs(G - other.G) <= threshold &&
               Math.Abs(B - other.B) <= threshold;
    }
}

/// <summary>
///     Delta renderer that tracks frame state and only outputs changed cells.
///     Dramatically reduces output for videos with static backgrounds.
/// </summary>
public class DeltaRenderer
{
    private CellData[,]? _previousFrame;
    private int _width;
    private int _height;
    private readonly int _colorThreshold;

    /// <summary>
    ///     Create a delta renderer with optional color stability threshold.
    /// </summary>
    /// <param name="colorThreshold">Colors within this threshold are considered unchanged (0-255)</param>
    public DeltaRenderer(int colorThreshold = 8)
    {
        _colorThreshold = colorThreshold;
    }

    /// <summary>
    ///     Reset frame state (call when starting new video or seeking).
    /// </summary>
    public void Reset()
    {
        _previousFrame = null;
    }

    /// <summary>
    ///     Render a frame with delta optimization.
    ///     Returns ANSI string that updates only changed cells.
    /// </summary>
    public string RenderDelta(CellData[,] currentFrame, bool forceFullRedraw = false)
    {
        var height = currentFrame.GetLength(0);
        var width = currentFrame.GetLength(1);

        // First frame or dimension change - full redraw
        if (forceFullRedraw || _previousFrame == null ||
            _width != width || _height != height)
        {
            _previousFrame = (CellData[,])currentFrame.Clone();
            _width = width;
            _height = height;
            return RenderFullFrame(currentFrame);
        }

        // Delta render - only changed cells
        var sb = new StringBuilder();
        Rgba32? lastColor = null;

        for (var y = 0; y < height; y++)
        {
            var rowHasChanges = false;
            var rowStart = -1;

            for (var x = 0; x < width; x++)
            {
                var current = currentFrame[y, x];
                var previous = _previousFrame[y, x];

                // Skip if cell hasn't changed (with threshold tolerance)
                if (current.IsSimilar(previous, _colorThreshold))
                    continue;

                // Cell changed - need to update
                if (!rowHasChanges)
                {
                    rowHasChanges = true;
                    rowStart = x;
                    // Position cursor at this cell (1-indexed)
                    sb.Append($"\x1b[{y + 1};{x + 1}H");
                }
                else if (x > rowStart + 1)
                {
                    // Gap in changes - reposition cursor
                    sb.Append($"\x1b[{y + 1};{x + 1}H");
                }

                // Output color if changed
                var newColor = new Rgba32(current.R, current.G, current.B, 255);
                if (lastColor == null || !AnsiCodes.ColorsEqual(lastColor.Value, newColor))
                {
                    sb.Append($"\x1b[38;2;{current.R};{current.G};{current.B}m");
                    lastColor = newColor;
                }

                sb.Append(current.Character);
                _previousFrame[y, x] = current;
            }
        }

        // Reset color at end
        if (lastColor != null)
            sb.Append("\x1b[0m");

        return sb.ToString();
    }

    /// <summary>
    ///     Render full frame (no delta optimization).
    /// </summary>
    private static string RenderFullFrame(CellData[,] frame)
    {
        var height = frame.GetLength(0);
        var width = frame.GetLength(1);
        var sb = new StringBuilder(width * height * 25);

        // Home cursor
        sb.Append("\x1b[H");

        Rgba32? lastColor = null;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = frame[y, x];
                var newColor = new Rgba32(cell.R, cell.G, cell.B, 255);

                if (lastColor == null || !AnsiCodes.ColorsEqual(lastColor.Value, newColor))
                {
                    sb.Append($"\x1b[38;2;{cell.R};{cell.G};{cell.B}m");
                    lastColor = newColor;
                }

                sb.Append(cell.Character);
            }

            if (y < height - 1)
            {
                sb.Append("\x1b[0m\n");
                lastColor = null;
            }
        }

        sb.Append("\x1b[0m");
        return sb.ToString();
    }

    /// <summary>
    ///     Get statistics about last delta render.
    /// </summary>
    public (int totalCells, int changedCells, float changeRatio) GetLastFrameStats()
    {
        if (_previousFrame == null) return (0, 0, 1.0f);
        var total = _width * _height;
        // Stats would need to be tracked during render - placeholder
        return (total, 0, 0);
    }
}

/// <summary>
///     Realtime rendering configuration.
/// </summary>
public class RealtimeOptions
{
    /// <summary>
    ///     Target frames per second (default: 30).
    /// </summary>
    public int TargetFps { get; set; } = 30;

    /// <summary>
    ///     Enable delta rendering (only update changed cells).
    /// </summary>
    public bool UseDeltaRendering { get; set; } = true;

    /// <summary>
    ///     Color threshold for delta detection (0-255, higher = more stability).
    /// </summary>
    public int DeltaColorThreshold { get; set; } = 8;

    /// <summary>
    ///     Automatically reduce quality if falling behind target FPS.
    /// </summary>
    public bool AdaptiveQuality { get; set; } = true;

    /// <summary>
    ///     Minimum width when using adaptive quality.
    /// </summary>
    public int MinWidth { get; set; } = 40;

    /// <summary>
    ///     Skip frames if render time exceeds frame budget.
    /// </summary>
    public bool AllowFrameSkip { get; set; } = true;

    /// <summary>
    ///     Maximum frames to skip in a row before forcing render.
    /// </summary>
    public int MaxConsecutiveSkips { get; set; } = 2;
}
