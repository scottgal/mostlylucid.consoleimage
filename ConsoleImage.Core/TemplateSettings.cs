// Template settings for saving/loading CLI presets
// Allows users to create reusable render configurations

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsoleImage.Core;

/// <summary>
///     Serializable template for CLI options.
///     All properties are nullable - only set properties are saved/applied.
/// </summary>
public record TemplateSettings
{
    // Dimensions
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? MaxWidth { get; init; }
    public int? MaxHeight { get; init; }

    // Render mode (only one should be set)
    public bool? Ascii { get; init; }
    public bool? Blocks { get; init; }
    public bool? Braille { get; init; }
    public bool? Matrix { get; init; }
    public bool? Monochrome { get; init; }

    // Matrix options
    public string? MatrixColor { get; init; }
    public bool? MatrixFullColor { get; init; }
    public float? MatrixDensity { get; init; }
    public float? MatrixSpeed { get; init; }
    public string? MatrixAlphabet { get; init; }

    // Color/rendering
    public bool? NoColor { get; init; }
    public int? Colors { get; init; }
    public float? Contrast { get; init; }
    public float? Gamma { get; init; }
    public float? CharAspect { get; init; }
    public string? Charset { get; init; }
    public string? Preset { get; init; }

    // Playback
    public float? Speed { get; init; }
    public int? Loop { get; init; }
    public double? Fps { get; init; }
    public string? FrameStep { get; init; }
    public string? Sampling { get; init; }
    public double? SceneThreshold { get; init; }

    // Performance
    public int? Buffer { get; init; }
    public bool? NoHwAccel { get; init; }
    public bool? NoAltScreen { get; init; }
    public bool? NoParallel { get; init; }

    // Output options
    public bool? ShowStatus { get; init; }
    public int? StatusWidth { get; init; }

    // GIF output
    public int? GifFontSize { get; init; }
    public float? GifScale { get; init; }
    public int? GifFps { get; init; }
    public int? GifColors { get; init; }

    // Image adjustments
    public bool? NoInvert { get; init; }
    public bool? EnableEdge { get; init; }
    public float? BgThreshold { get; init; }
    public float? DarkBgThreshold { get; init; }
    public bool? AutoBg { get; init; }
    public float? DarkCutoff { get; init; }
    public float? LightCutoff { get; init; }

    // Temporal stability
    public bool? Dejitter { get; init; }
    public int? ColorThreshold { get; init; }

    // Subtitles
    public string? Subs { get; init; }
    public string? SubtitleLang { get; init; }
    public string? WhisperModel { get; init; }

    // Slideshow
    public float? SlideDelay { get; init; }
    public bool? Shuffle { get; init; }
    public bool? Recursive { get; init; }
    public string? SortBy { get; init; }
    public bool? SortDesc { get; init; }
    public float? VideoPreview { get; init; }
    public bool? GifLoop { get; init; }
    public bool? HideSlideInfo { get; init; }
}

/// <summary>
///     JSON serialization context for AOT compatibility
/// </summary>
[JsonSerializable(typeof(TemplateSettings))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class TemplateJsonContext : JsonSerializerContext
{
}

/// <summary>
///     Helper for loading and saving template files
/// </summary>
public static class TemplateHelper
{
    /// <summary>
    ///     Load template settings from a JSON file
    /// </summary>
    public static TemplateSettings? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, TemplateJsonContext.Default.TemplateSettings);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Save template settings to a JSON file
    /// </summary>
    public static void Save(TemplateSettings settings, string path)
    {
        var json = JsonSerializer.Serialize(settings, TemplateJsonContext.Default.TemplateSettings);
        File.WriteAllText(path, json);
    }
}