// Render helper utilities for the CLI

using ConsoleImage.Core;
using ConsoleImage.Player;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Cli.Utilities;

/// <summary>
///     Simple IAnimationFrame implementation for generic frames.
/// </summary>
public record SimpleFrame(string Content, int DelayMs) : IAnimationFrame;

/// <summary>
///     Helper methods for rendering operations.
/// </summary>
public static class RenderHelpers
{
    /// <summary>
    ///     Build MatrixOptions from CLI parameters.
    /// </summary>
    public static MatrixOptions BuildMatrixOptions(
        string? colorName,
        bool fullColor,
        float? density,
        float? speed,
        string? alphabet)
    {
        var opts = new MatrixOptions();

        if (fullColor)
        {
            opts.UseFullColor = true;
            opts.BaseColor = null;
        }
        else if (!string.IsNullOrEmpty(colorName))
        {
            opts.BaseColor = colorName.ToLowerInvariant() switch
            {
                "green" => new Rgba32(0, 255, 0, 255),
                "red" => new Rgba32(255, 0, 0, 255),
                "blue" => new Rgba32(0, 100, 255, 255),
                "amber" => new Rgba32(255, 191, 0, 255),
                "cyan" => new Rgba32(0, 255, 255, 255),
                "purple" => new Rgba32(200, 0, 255, 255),
                _ when colorName.StartsWith("#") && colorName.Length == 7 =>
                    new Rgba32(
                        Convert.ToByte(colorName.Substring(1, 2), 16),
                        Convert.ToByte(colorName.Substring(3, 2), 16),
                        Convert.ToByte(colorName.Substring(5, 2), 16),
                        255),
                _ => new Rgba32(0, 255, 0, 255) // default green
            };
        }

        if (density.HasValue)
            opts.Density = Math.Clamp(density.Value, 0.1f, 2.0f);

        if (speed.HasValue)
            opts.SpeedMultiplier = Math.Clamp(speed.Value, 0.5f, 3.0f);

        if (!string.IsNullOrEmpty(alphabet))
            opts.CustomAlphabet = alphabet;

        return opts;
    }

    /// <summary>
    ///     Frame-by-frame animation playback with synchronized output.
    ///     Uses DECSET 2026 for flicker-free rendering and proper line clearing.
    /// </summary>
    public static async Task PlayFramesAsync(
        List<IAnimationFrame> frames,
        int loopCount,
        float speed,
        CancellationToken ct)
    {
        if (frames.Count == 0) return;

        // Pre-compute line counts for all frames (avoids iterating large ANSI strings every frame)
        var lineCounts = new int[frames.Count];
        var maxLineCount = 0;
        for (var f = 0; f < frames.Count; f++)
        {
            var count = 1;
            var content = frames[f].Content;
            for (var i = 0; i < content.Length; i++)
                if (content[i] == '\n') count++;
            lineCounts[f] = count;
            if (count > maxLineCount) maxLineCount = count;
        }

        // Enter alternate screen buffer
        Console.Write("\x1b[?1049h"); // Enter alt screen
        Console.Write("\x1b[?25l"); // Hide cursor
        Console.Write("\x1b[2J"); // Clear screen

        try
        {
            var loops = 0;
            var prevLineCount = 0;

            while (!ct.IsCancellationRequested && (loopCount == 0 || loops < loopCount))
            {
                for (var fi = 0; fi < frames.Count; fi++)
                {
                    if (ct.IsCancellationRequested) break;

                    var frame = frames[fi];

                    // Begin synchronized output (atomic frame render)
                    Console.Write("\x1b[?2026h");

                    // Move to top-left
                    Console.Write("\x1b[H");

                    // Write entire frame at once — no per-line clearing needed.
                    Console.Write(frame.Content);
                    Console.Write("\x1b[0m"); // Reset colors after frame

                    // Clear any remaining lines from a previous taller frame
                    var lineCount = lineCounts[fi];
                    for (var i = lineCount; i < prevLineCount; i++)
                    {
                        Console.Write('\n');
                        Console.Write("\x1b[2K");
                    }

                    prevLineCount = lineCount;

                    // End synchronized output (render atomically)
                    Console.Write("\x1b[?2026l");

                    // Wait for frame delay (GIF default is 100ms if 0/10ms specified)
                    var baseDelay = frame.DelayMs <= 10 ? 100 : frame.DelayMs;
                    var delayMs = Math.Max(16, (int)(baseDelay / speed));
                    await Task.Delay(delayMs, ct);
                }

                loops++;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Console.Write("\x1b[?25h"); // Show cursor
            Console.Write("\x1b[?1049l"); // Exit alt screen
        }
    }

    /// <summary>
    ///     Get effective aspect ratio from explicit value, saved calibration, or default.
    /// </summary>
    public static float GetEffectiveAspectRatio(
        float? explicitAspect,
        CalibrationSettings? savedCalibration,
        RenderMode mode)
    {
        return explicitAspect
               ?? savedCalibration?.GetAspectRatio(mode)
               ?? ConsoleHelper.DetectCellAspectRatio()
               ?? 0.5f;
    }

    /// <summary>
    ///     Determine render mode from boolean CLI flags.
    ///     Priority: Matrix > Braille > Blocks > ASCII (default)
    /// </summary>
    public static RenderMode GetRenderMode(bool useBraille, bool useBlocks, bool useMatrix)
    {
        if (useMatrix) return RenderMode.Matrix;
        if (useBraille) return RenderMode.Braille;
        if (useBlocks) return RenderMode.ColorBlocks;
        return RenderMode.Ascii;
    }

    /// <summary>
    ///     Get display name for render mode.
    /// </summary>
    public static string GetRenderModeName(RenderMode mode)
    {
        return mode switch
        {
            RenderMode.Braille => "Braille",
            RenderMode.ColorBlocks => "Blocks",
            RenderMode.Matrix => "Matrix",
            _ => "ASCII"
        };
    }

    /// <summary>
    ///     Get effective gamma from explicit value, saved calibration, or default.
    ///     Braille mode defaults to 0.5 (brighter) to compensate for dot density.
    /// </summary>
    public static float GetEffectiveGamma(
        float? explicitGamma,
        CalibrationSettings? savedCalibration,
        RenderMode mode)
    {
        return explicitGamma
               ?? savedCalibration?.GetGamma(mode)
               ?? (mode == RenderMode.Braille ? 0.5f : 0.65f);
    }
}