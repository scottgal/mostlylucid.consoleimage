using System.Text.Json;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Path = System.IO.Path;

namespace ConsoleImage.Core;

/// <summary>
///     Settings for calibration, saved to calibration.json.
///     Each render mode has its own character aspect ratio and gamma since they map pixels differently.
/// </summary>
public record CalibrationSettings
{
    // === Aspect Ratio Settings ===

    /// <summary>Character aspect ratio for ASCII mode (1 char = 1 pixel)</summary>
    public float AsciiCharacterAspectRatio { get; init; } = 0.5f;

    /// <summary>Character aspect ratio for ColorBlocks mode (1 char = 1x2 pixels)</summary>
    public float BlocksCharacterAspectRatio { get; init; } = 0.5f;

    /// <summary>Character aspect ratio for Braille mode (1 char = 2x4 pixels)</summary>
    public float BrailleCharacterAspectRatio { get; init; } = 0.5f;

    // === Gamma Settings (color calibration) ===

    /// <summary>Gamma correction for ASCII mode (1.0 = neutral, &lt;1.0 = brighter, &gt;1.0 = darker)</summary>
    public float AsciiGamma { get; init; } = 0.65f;

    /// <summary>Gamma correction for ColorBlocks mode</summary>
    public float BlocksGamma { get; init; } = 0.65f;

    /// <summary>Gamma correction for Braille mode (brighter default to compensate for dot density)</summary>
    public float BrailleGamma { get; init; } = 0.5f;

    /// <summary>Get the character aspect ratio for a specific render mode</summary>
    public float GetAspectRatio(RenderMode mode)
    {
        return mode switch
        {
            RenderMode.ColorBlocks => BlocksCharacterAspectRatio,
            RenderMode.Braille => BrailleCharacterAspectRatio,
            RenderMode.Matrix => AsciiCharacterAspectRatio, // Matrix uses same 1x1 cell as ASCII
            _ => AsciiCharacterAspectRatio
        };
    }

    /// <summary>Get the gamma correction for a specific render mode</summary>
    public float GetGamma(RenderMode mode)
    {
        // Get the stored gamma value
        var gamma = mode switch
        {
            RenderMode.ColorBlocks => BlocksGamma,
            RenderMode.Braille => BrailleGamma,
            RenderMode.Matrix => AsciiGamma, // Matrix uses same gamma as ASCII
            _ => AsciiGamma
        };

        // Return mode-specific default if gamma is 0 (unset - old calibration files without gamma)
        // System.Text.Json deserializes missing float properties as 0, not the init default
        if (gamma <= 0f)
            return mode == RenderMode.Braille ? 0.5f : 0.65f;
        return gamma;
    }

    /// <summary>Create a new settings with updated aspect ratio for a specific mode</summary>
    public CalibrationSettings WithAspectRatio(RenderMode mode, float aspectRatio)
    {
        return mode switch
        {
            RenderMode.ColorBlocks => this with { BlocksCharacterAspectRatio = aspectRatio },
            RenderMode.Braille => this with { BrailleCharacterAspectRatio = aspectRatio },
            RenderMode.Matrix => this with { AsciiCharacterAspectRatio = aspectRatio }, // Shares with ASCII
            _ => this with { AsciiCharacterAspectRatio = aspectRatio }
        };
    }

    /// <summary>Create a new settings with updated gamma for a specific mode</summary>
    public CalibrationSettings WithGamma(RenderMode mode, float gamma)
    {
        return mode switch
        {
            RenderMode.ColorBlocks => this with { BlocksGamma = gamma },
            RenderMode.Braille => this with { BrailleGamma = gamma },
            RenderMode.Matrix => this with { AsciiGamma = gamma }, // Shares with ASCII
            _ => this with { AsciiGamma = gamma }
        };
    }
}

/// <summary>
///     JSON serialization context for AOT compatibility
/// </summary>
[JsonSerializable(typeof(CalibrationSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class CalibrationJsonContext : JsonSerializerContext
{
}

/// <summary>
///     Helper for generating and managing calibration patterns
/// </summary>
public static class CalibrationHelper
{
    /// <summary>
    ///     Default calibration file name
    /// </summary>
    public const string DefaultFileName = "calibration.json";

    /// <summary>
    ///     Get the default calibration file path (next to the executable)
    /// </summary>
    public static string GetDefaultPath()
    {
        return Path.Combine(AppContext.BaseDirectory, DefaultFileName);
    }

    /// <summary>
    ///     Load calibration settings from file
    /// </summary>
    public static CalibrationSettings? Load(string? path = null)
    {
        path ??= GetDefaultPath();
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.CalibrationSettings);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Save calibration settings to file
    /// </summary>
    public static void Save(CalibrationSettings settings, string? path = null)
    {
        path ??= GetDefaultPath();
        var json = JsonSerializer.Serialize(settings, CalibrationJsonContext.Default.CalibrationSettings);
        File.WriteAllText(path, json);
    }

    /// <summary>
    ///     Generate a calibration test image with a circle and crosshairs
    /// </summary>
    public static Image<Rgba32> GenerateCalibrationImage(int size = 200)
    {
        var image = new Image<Rgba32>(size, size, Color.Black);

        var center = new PointF(size / 2f, size / 2f);
        var radius = size * 0.4f;
        var strokeWidth = size * 0.05f;

        image.Mutate(ctx =>
        {
            // Draw circle outline
            ctx.Draw(Color.White, strokeWidth, new EllipsePolygon(center, radius));

            // Draw crosshairs for additional reference
            var penThin = new SolidPen(Color.Gray, 2);
            ctx.DrawLine(penThin, new PointF(size / 2f, 0), new PointF(size / 2f, size));
            ctx.DrawLine(penThin, new PointF(0, size / 2f), new PointF(size, size / 2f));
        });

        return image;
    }

    /// <summary>
    ///     Generate a SMPTE-style color test card for terminal calibration.
    ///     Includes color bars, grayscale ramp, and PLUGE bars for gamma adjustment.
    /// </summary>
    public static Image<Rgba32> GenerateColorCalibrationImage(int width = 320, int height = 240)
    {
        var image = new Image<Rgba32>(width, height, Color.Black);

        // SMPTE color bar layout (top 2/3)
        // 75% color bars: Gray, Yellow, Cyan, Green, Magenta, Red, Blue
        var topHeight = height * 2 / 3;
        var colorBars = new[]
        {
            new Rgba32(191, 191, 191, 255), // 75% Gray
            new Rgba32(191, 191, 0, 255), // 75% Yellow
            new Rgba32(0, 191, 191, 255), // 75% Cyan
            new Rgba32(0, 191, 0, 255), // 75% Green
            new Rgba32(191, 0, 191, 255), // 75% Magenta
            new Rgba32(191, 0, 0, 255), // 75% Red
            new Rgba32(0, 0, 191, 255) // 75% Blue
        };
        var barWidth = width / colorBars.Length;
        for (var i = 0; i < colorBars.Length; i++)
        for (var x = i * barWidth; x < (i + 1) * barWidth && x < width; x++)
        for (var y = 0; y < topHeight; y++)
            image[x, y] = colorBars[i];

        // Castellations (middle strip - reverse color signal)
        var midStart = topHeight;
        var midHeight = height / 12;
        var castellations = new[]
        {
            new Rgba32(0, 0, 191, 255), // Blue
            new Rgba32(0, 0, 0, 255), // Black
            new Rgba32(191, 0, 191, 255), // Magenta
            new Rgba32(0, 0, 0, 255), // Black
            new Rgba32(0, 191, 191, 255), // Cyan
            new Rgba32(0, 0, 0, 255), // Black
            new Rgba32(191, 191, 191, 255) // Gray
        };
        for (var i = 0; i < castellations.Length; i++)
        for (var x = i * barWidth; x < (i + 1) * barWidth && x < width; x++)
        for (var y = midStart; y < midStart + midHeight; y++)
            image[x, y] = castellations[i];

        // Bottom section: PLUGE bars and grayscale
        var bottomStart = midStart + midHeight;
        var bottomHeight = height - bottomStart;
        var subBarWidth = width / 7;

        // PLUGE section (left side) - for checking black level
        // -4%, 0%, +4% black levels
        var plugeColors = new[]
        {
            new Rgba32(0, 0, 0, 255), // Superblack (clipped to 0)
            new Rgba32(8, 8, 8, 255), // -4% (should be nearly invisible)
            new Rgba32(0, 0, 0, 255), // Black reference
            new Rgba32(8, 8, 8, 255) // +4% (should be barely visible if gamma correct)
        };

        // I and Q bars (for color decoder testing)
        var iqColor1 = new Rgba32(0, 68, 130, 255); // -I (dark blue)
        var iqColor2 = new Rgba32(255, 255, 255, 255); // White
        var iqColor3 = new Rgba32(75, 0, 139, 255); // +Q (purple)

        // Grayscale ramp (right side)
        var grayRampStart = width * 4 / 7;

        for (var x = 0; x < width; x++)
        for (var y = bottomStart; y < height; y++)
            if (x < subBarWidth)
            {
                // PLUGE super black
                image[x, y] = plugeColors[0];
            }
            else if (x < subBarWidth * 2)
            {
                // PLUGE 4% below black
                image[x, y] = plugeColors[1];
            }
            else if (x < subBarWidth * 3)
            {
                // PLUGE black
                image[x, y] = plugeColors[2];
            }
            else if (x < subBarWidth * 4)
            {
                // PLUGE 4% above black
                image[x, y] = plugeColors[3];
            }
            else if (x < grayRampStart)
            {
                // White reference
                image[x, y] = iqColor2;
            }
            else
            {
                // Grayscale ramp (11 steps from black to white)
                var rampPos = (float)(x - grayRampStart) / (width - grayRampStart);
                var gray = (byte)(255 * rampPos);
                image[x, y] = new Rgba32(gray, gray, gray, 255);
            }

        return image;
    }

    /// <summary>
    ///     Render color calibration pattern using the specified mode and gamma
    /// </summary>
    public static string RenderColorCalibrationPattern(
        RenderMode mode,
        float charAspect,
        float gamma,
        bool useColor = true,
        int width = 60,
        int height = 20)
    {
        using var image = GenerateColorCalibrationImage();

        var renderOpts = new RenderOptions
        {
            CharacterAspectRatio = charAspect,
            UseColor = useColor,
            MaxWidth = width,
            MaxHeight = height,
            Gamma = gamma
        };

        return mode switch
        {
            RenderMode.Braille => RenderBraille(image, renderOpts),
            RenderMode.ColorBlocks => RenderColorBlocks(image, renderOpts),
            RenderMode.Matrix => RenderMatrix(image, renderOpts),
            _ => RenderAscii(image, renderOpts)
        };
    }

    /// <summary>
    ///     Render calibration pattern using the specified mode
    /// </summary>
    public static string RenderCalibrationPattern(
        RenderMode mode,
        float charAspect,
        bool useColor = true,
        int width = 40,
        int height = 20)
    {
        using var image = GenerateCalibrationImage();

        // Use MaxWidth/MaxHeight (not explicit Width/Height) so aspect ratio affects dimensions
        var renderOpts = new RenderOptions
        {
            CharacterAspectRatio = charAspect,
            UseColor = useColor,
            Invert = true,
            MaxWidth = width,
            MaxHeight = height
        };

        return mode switch
        {
            RenderMode.Braille => RenderBraille(image, renderOpts),
            RenderMode.ColorBlocks => RenderColorBlocks(image, renderOpts),
            RenderMode.Matrix => RenderMatrix(image, renderOpts),
            _ => RenderAscii(image, renderOpts)
        };
    }

    private static string RenderMatrix(Image<Rgba32> image, RenderOptions opts)
    {
        using var renderer = new MatrixRenderer(opts);
        var frame = renderer.RenderImage(image);
        return frame.Content;
    }

    private static string RenderBraille(Image<Rgba32> image, RenderOptions opts)
    {
        using var renderer = new BrailleRenderer(opts);
        return renderer.RenderImage(image);
    }

    private static string RenderColorBlocks(Image<Rgba32> image, RenderOptions opts)
    {
        using var renderer = new ColorBlockRenderer(opts);
        return renderer.RenderImage(image);
    }

    private static string RenderAscii(Image<Rgba32> image, RenderOptions opts)
    {
        using var renderer = new AsciiRenderer(opts);
        var frame = renderer.RenderImage(image);
        return frame.ToString();
    }
}

/// <summary>
///     Render mode for calibration
/// </summary>
public enum RenderMode
{
    Ascii,
    ColorBlocks,
    Braille,
    Matrix
}