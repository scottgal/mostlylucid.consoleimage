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

    /// <summary>
    ///     Load user defaults from ~/.config/consoleimage/defaults.json.
    ///     Returns null if no config file exists (all built-in defaults apply).
    /// </summary>
    public static TemplateSettings? LoadUserDefaults()
    {
        foreach (var path in GetDefaultConfigPaths())
        {
            var settings = Load(path);
            if (settings != null)
                return settings;
        }

        return null;
    }

    /// <summary>
    ///     Get the path where user defaults would be saved.
    /// </summary>
    public static string GetUserDefaultsPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "consoleimage", "defaults.json");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "consoleimage", "defaults.json");
    }

    /// <summary>
    ///     Generate a defaults.json with documentation comments at the given path.
    /// </summary>
    public static void CreateDefaultsFile(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Write a well-documented example config
        var example = new TemplateSettings
        {
            MaxWidth = 120,
            MaxHeight = 30,
            Braille = true,
            Contrast = 2.5f,
            Gamma = 1.0f,
            Speed = 1.0f,
            Loop = 0
        };

        Save(example, path);
    }

    /// <summary>
    ///     Merge two template settings. Values from <paramref name="overlay"/> take priority
    ///     over <paramref name="baseSettings"/>. Either or both may be null.
    /// </summary>
    public static TemplateSettings? Merge(TemplateSettings? baseSettings, TemplateSettings? overlay)
    {
        if (baseSettings == null) return overlay;
        if (overlay == null) return baseSettings;

        return new TemplateSettings
        {
            Width = overlay.Width ?? baseSettings.Width,
            Height = overlay.Height ?? baseSettings.Height,
            MaxWidth = overlay.MaxWidth ?? baseSettings.MaxWidth,
            MaxHeight = overlay.MaxHeight ?? baseSettings.MaxHeight,
            Ascii = overlay.Ascii ?? baseSettings.Ascii,
            Blocks = overlay.Blocks ?? baseSettings.Blocks,
            Braille = overlay.Braille ?? baseSettings.Braille,
            Matrix = overlay.Matrix ?? baseSettings.Matrix,
            Monochrome = overlay.Monochrome ?? baseSettings.Monochrome,
            MatrixColor = overlay.MatrixColor ?? baseSettings.MatrixColor,
            MatrixFullColor = overlay.MatrixFullColor ?? baseSettings.MatrixFullColor,
            MatrixDensity = overlay.MatrixDensity ?? baseSettings.MatrixDensity,
            MatrixSpeed = overlay.MatrixSpeed ?? baseSettings.MatrixSpeed,
            MatrixAlphabet = overlay.MatrixAlphabet ?? baseSettings.MatrixAlphabet,
            NoColor = overlay.NoColor ?? baseSettings.NoColor,
            Colors = overlay.Colors ?? baseSettings.Colors,
            Contrast = overlay.Contrast ?? baseSettings.Contrast,
            Gamma = overlay.Gamma ?? baseSettings.Gamma,
            CharAspect = overlay.CharAspect ?? baseSettings.CharAspect,
            Charset = overlay.Charset ?? baseSettings.Charset,
            Preset = overlay.Preset ?? baseSettings.Preset,
            Speed = overlay.Speed ?? baseSettings.Speed,
            Loop = overlay.Loop ?? baseSettings.Loop,
            Fps = overlay.Fps ?? baseSettings.Fps,
            FrameStep = overlay.FrameStep ?? baseSettings.FrameStep,
            Sampling = overlay.Sampling ?? baseSettings.Sampling,
            SceneThreshold = overlay.SceneThreshold ?? baseSettings.SceneThreshold,
            Buffer = overlay.Buffer ?? baseSettings.Buffer,
            NoHwAccel = overlay.NoHwAccel ?? baseSettings.NoHwAccel,
            NoAltScreen = overlay.NoAltScreen ?? baseSettings.NoAltScreen,
            NoParallel = overlay.NoParallel ?? baseSettings.NoParallel,
            ShowStatus = overlay.ShowStatus ?? baseSettings.ShowStatus,
            StatusWidth = overlay.StatusWidth ?? baseSettings.StatusWidth,
            GifFontSize = overlay.GifFontSize ?? baseSettings.GifFontSize,
            GifScale = overlay.GifScale ?? baseSettings.GifScale,
            GifFps = overlay.GifFps ?? baseSettings.GifFps,
            GifColors = overlay.GifColors ?? baseSettings.GifColors,
            NoInvert = overlay.NoInvert ?? baseSettings.NoInvert,
            EnableEdge = overlay.EnableEdge ?? baseSettings.EnableEdge,
            BgThreshold = overlay.BgThreshold ?? baseSettings.BgThreshold,
            DarkBgThreshold = overlay.DarkBgThreshold ?? baseSettings.DarkBgThreshold,
            AutoBg = overlay.AutoBg ?? baseSettings.AutoBg,
            DarkCutoff = overlay.DarkCutoff ?? baseSettings.DarkCutoff,
            LightCutoff = overlay.LightCutoff ?? baseSettings.LightCutoff,
            Dejitter = overlay.Dejitter ?? baseSettings.Dejitter,
            ColorThreshold = overlay.ColorThreshold ?? baseSettings.ColorThreshold,
            Subs = overlay.Subs ?? baseSettings.Subs,
            SubtitleLang = overlay.SubtitleLang ?? baseSettings.SubtitleLang,
            WhisperModel = overlay.WhisperModel ?? baseSettings.WhisperModel,
            SlideDelay = overlay.SlideDelay ?? baseSettings.SlideDelay,
            Shuffle = overlay.Shuffle ?? baseSettings.Shuffle,
            Recursive = overlay.Recursive ?? baseSettings.Recursive,
            SortBy = overlay.SortBy ?? baseSettings.SortBy,
            SortDesc = overlay.SortDesc ?? baseSettings.SortDesc,
            VideoPreview = overlay.VideoPreview ?? baseSettings.VideoPreview,
            GifLoop = overlay.GifLoop ?? baseSettings.GifLoop,
            HideSlideInfo = overlay.HideSlideInfo ?? baseSettings.HideSlideInfo
        };
    }

    private static IEnumerable<string> GetDefaultConfigPaths()
    {
        // 1. Next to the binary (portable config)
        var appDir = AppContext.BaseDirectory;
        yield return Path.Combine(appDir, "defaults.json");

        // 2. Platform-specific user config
        yield return GetUserDefaultsPath();
    }
}