using System.Text.Json;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Core;

/// <summary>
/// Settings for calibration, saved to calibration.json.
/// Each render mode has its own character aspect ratio since they map pixels differently.
/// </summary>
public record CalibrationSettings
{
    /// <summary>Character aspect ratio for ASCII mode (1 char = 1 pixel)</summary>
    public float AsciiCharacterAspectRatio { get; init; } = 0.5f;

    /// <summary>Character aspect ratio for ColorBlocks mode (1 char = 1x2 pixels)</summary>
    public float BlocksCharacterAspectRatio { get; init; } = 0.5f;

    /// <summary>Character aspect ratio for Braille mode (1 char = 2x4 pixels)</summary>
    public float BrailleCharacterAspectRatio { get; init; } = 0.5f;

    /// <summary>Get the character aspect ratio for a specific render mode</summary>
    public float GetAspectRatio(RenderMode mode) => mode switch
    {
        RenderMode.ColorBlocks => BlocksCharacterAspectRatio,
        RenderMode.Braille => BrailleCharacterAspectRatio,
        _ => AsciiCharacterAspectRatio
    };

    /// <summary>Create a new settings with updated aspect ratio for a specific mode</summary>
    public CalibrationSettings WithAspectRatio(RenderMode mode, float aspectRatio) => mode switch
    {
        RenderMode.ColorBlocks => this with { BlocksCharacterAspectRatio = aspectRatio },
        RenderMode.Braille => this with { BrailleCharacterAspectRatio = aspectRatio },
        _ => this with { AsciiCharacterAspectRatio = aspectRatio }
    };
}

/// <summary>
/// JSON serialization context for AOT compatibility
/// </summary>
[JsonSerializable(typeof(CalibrationSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class CalibrationJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Helper for generating and managing calibration patterns
/// </summary>
public static class CalibrationHelper
{
    /// <summary>
    /// Default calibration file name
    /// </summary>
    public const string DefaultFileName = "calibration.json";

    /// <summary>
    /// Get the default calibration file path (next to the executable)
    /// </summary>
    public static string GetDefaultPath() =>
        System.IO.Path.Combine(AppContext.BaseDirectory, DefaultFileName);

    /// <summary>
    /// Load calibration settings from file
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
    /// Save calibration settings to file
    /// </summary>
    public static void Save(CalibrationSettings settings, string? path = null)
    {
        path ??= GetDefaultPath();
        var json = JsonSerializer.Serialize(settings, CalibrationJsonContext.Default.CalibrationSettings);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Generate a calibration test image with a circle and crosshairs
    /// </summary>
    public static Image<Rgba32> GenerateCalibrationImage(int size = 200)
    {
        var image = new Image<Rgba32>(size, size, Color.Black);

        var center = new PointF(size / 2f, size / 2f);
        float radius = size * 0.4f;
        float strokeWidth = size * 0.05f;

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
    /// Render calibration pattern using the specified mode
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
            _ => RenderAscii(image, renderOpts)
        };
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
/// Render mode for calibration
/// </summary>
public enum RenderMode
{
    Ascii,
    ColorBlocks,
    Braille
}
