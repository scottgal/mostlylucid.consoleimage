// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering

using System.Text.Json.Serialization;

namespace ConsoleImage.Core;

/// <summary>
/// Configuration options for ASCII rendering.
/// Can be bound from appsettings.json via IConfiguration.
/// </summary>
public class RenderOptions
{
    /// <summary>
    /// Width of the output in characters. If null, calculated from height maintaining aspect ratio.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height of the output in characters. If null, calculated from width maintaining aspect ratio.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Maximum width in characters (default: 120)
    /// </summary>
    public int MaxWidth { get; set; } = 120;

    /// <summary>
    /// Maximum height in characters (default: 60)
    /// </summary>
    public int MaxHeight { get; set; } = 60;

    /// <summary>
    /// Character aspect ratio compensation. Console fonts are typically taller than wide.
    /// Default is 0.5 (characters are about twice as tall as wide)
    /// </summary>
    public float CharacterAspectRatio { get; set; } = 0.5f;

    /// <summary>
    /// Character set to use for rendering. If null, uses default set.
    /// Characters should be ordered from lightest to darkest.
    /// </summary>
    public string? CharacterSet { get; set; }

    /// <summary>
    /// Character set preset name: "default", "simple", "block", "extended"
    /// Only used if CharacterSet is null.
    /// </summary>
    public string? CharacterSetPreset { get; set; }

    /// <summary>
    /// Font family to use for character shape analysis. If null, uses default monospace.
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// Global contrast enhancement power (1.0 = no enhancement, higher = more contrast)
    /// Recommended: 2.0-4.0. Default: 2.5
    /// </summary>
    public float ContrastPower { get; set; } = 2.5f;

    /// <summary>
    /// Directional contrast enhancement strength (0.0 = disabled, 1.0 = full)
    /// Default: 0.3
    /// </summary>
    public float DirectionalContrastStrength { get; set; } = 0.3f;

    /// <summary>
    /// Invert the output so black source pixels become spaces.
    /// Default is TRUE because most terminals have dark backgrounds.
    /// </summary>
    public bool Invert { get; set; } = true;

    /// <summary>
    /// Enable colored output using ANSI escape codes.
    /// Default is TRUE for modern terminals.
    /// </summary>
    public bool UseColor { get; set; } = true;

    /// <summary>
    /// For animated GIFs: frame delay multiplier (1.0 = original speed)
    /// </summary>
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// For animated GIFs: loop count (0 = infinite)
    /// </summary>
    public int LoopCount { get; set; } = 0;

    /// <summary>
    /// Enable edge detection to enhance foreground visibility
    /// </summary>
    public bool EnableEdgeDetection { get; set; }

    /// <summary>
    /// Edge detection strength (0.0-1.0). Higher values make edges more prominent.
    /// </summary>
    public float EdgeStrength { get; set; } = 0.5f;

    /// <summary>
    /// Background suppression threshold (0.0-1.0). Pixels above this brightness are suppressed.
    /// Useful for removing uniform light backgrounds. Null = disabled.
    /// </summary>
    public float? BackgroundThreshold { get; set; }

    /// <summary>
    /// Dark background suppression threshold (0.0-1.0). Pixels below this brightness are suppressed.
    /// Useful for removing uniform dark backgrounds (like black space backgrounds). Null = disabled.
    /// </summary>
    public float? DarkBackgroundThreshold { get; set; }

    /// <summary>
    /// Automatically detect and suppress background based on edge pixels.
    /// Works for both light and dark backgrounds. Default is TRUE.
    /// </summary>
    public bool AutoBackgroundSuppression { get; set; } = true;

    /// <summary>
    /// Enable parallel processing for faster rendering (default: true)
    /// </summary>
    public bool UseParallelProcessing { get; set; } = true;

    /// <summary>
    /// Pre-buffer all frames before playback for smoother animation (default: true).
    /// When false, frames are rendered on-demand which uses less memory but may stutter.
    /// </summary>
    public bool PreBufferFrames { get; set; } = true;

    /// <summary>
    /// Gets the effective character set, considering presets
    /// </summary>
    [JsonIgnore]
    public string EffectiveCharacterSet => CharacterSet ?? GetPresetCharacterSet(CharacterSetPreset);

    private static string GetPresetCharacterSet(string? preset) => preset?.ToLowerInvariant() switch
    {
        "simple" => CharacterMap.SimpleCharacterSet,
        "block" => CharacterMap.BlockCharacterSet,
        "extended" => CharacterMap.ExtendedCharacterSet,
        _ => CharacterMap.DefaultCharacterSet
    };

    /// <summary>
    /// Create default options - works well for most images
    /// </summary>
    public static RenderOptions Default => new();

    /// <summary>
    /// Create options optimized for terminal output with specified dimensions
    /// </summary>
    public static RenderOptions ForTerminal(int? width = null, int? height = null) => new()
    {
        Width = width,
        Height = height,
        MaxWidth = width ?? 120,
        MaxHeight = height ?? 40,
        ContrastPower = 2.5f,
        DirectionalContrastStrength = 0.3f
    };

    /// <summary>
    /// Create options for high-detail output
    /// </summary>
    public static RenderOptions HighDetail => new()
    {
        MaxWidth = 200,
        MaxHeight = 100,
        ContrastPower = 2.0f,
        DirectionalContrastStrength = 0.4f,
        CharacterSetPreset = "extended"
    };

    /// <summary>
    /// Create options for non-colored output (monochrome)
    /// </summary>
    public static RenderOptions Monochrome => new()
    {
        UseColor = false,
        ContrastPower = 2.5f
    };

    /// <summary>
    /// Create options for light terminal/paper (non-inverted output)
    /// </summary>
    public static RenderOptions ForLightBackground => new()
    {
        Invert = false,
        UseColor = false,
        ContrastPower = 2.5f
    };

    /// <summary>
    /// Create options for animated GIF playback
    /// </summary>
    public static RenderOptions ForAnimation(int loopCount = 0) => new()
    {
        LoopCount = loopCount,
        MaxWidth = 100,
        MaxHeight = 40
    };

    /// <summary>
    /// Create options for images with dark backgrounds (like space images).
    /// Suppresses dark pixels so they appear as empty space.
    /// </summary>
    public static RenderOptions ForDarkBackground => new()
    {
        DarkBackgroundThreshold = 0.15f,
        ContrastPower = 3.0f,
        DirectionalContrastStrength = 0.4f
    };

    /// <summary>
    /// Create options with automatic background detection and suppression.
    /// Works for both light and dark backgrounds.
    /// </summary>
    public static RenderOptions WithAutoBackground => new()
    {
        AutoBackgroundSuppression = true,
        ContrastPower = 2.5f
    };

    /// <summary>
    /// Calculate output dimensions maintaining aspect ratio
    /// </summary>
    public (int width, int height) CalculateDimensions(int imageWidth, int imageHeight)
    {
        float imageAspect = (float)imageWidth / imageHeight;
        float adjustedAspect = imageAspect / CharacterAspectRatio;

        int outputWidth, outputHeight;

        if (Width.HasValue && Height.HasValue)
        {
            outputWidth = Width.Value;
            outputHeight = Height.Value;
        }
        else if (Width.HasValue)
        {
            outputWidth = Width.Value;
            outputHeight = (int)(outputWidth / adjustedAspect);
        }
        else if (Height.HasValue)
        {
            outputHeight = Height.Value;
            outputWidth = (int)(outputHeight * adjustedAspect);
        }
        else
        {
            if (adjustedAspect > (float)MaxWidth / MaxHeight)
            {
                outputWidth = MaxWidth;
                outputHeight = (int)(MaxWidth / adjustedAspect);
            }
            else
            {
                outputHeight = MaxHeight;
                outputWidth = (int)(MaxHeight * adjustedAspect);
            }
        }

        if (outputWidth > MaxWidth)
        {
            outputWidth = MaxWidth;
            outputHeight = (int)(outputWidth / adjustedAspect);
        }
        if (outputHeight > MaxHeight)
        {
            outputHeight = MaxHeight;
            outputWidth = (int)(outputHeight * adjustedAspect);
        }

        return (Math.Max(1, outputWidth), Math.Max(1, outputHeight));
    }

    /// <summary>
    /// Create a copy with modifications using a fluent builder pattern
    /// </summary>
    public RenderOptions With(Action<RenderOptions> configure)
    {
        var copy = Clone();
        configure(copy);
        return copy;
    }

    /// <summary>
    /// Create a deep copy of these options
    /// </summary>
    public RenderOptions Clone() => new()
    {
        Width = Width,
        Height = Height,
        MaxWidth = MaxWidth,
        MaxHeight = MaxHeight,
        CharacterAspectRatio = CharacterAspectRatio,
        CharacterSet = CharacterSet,
        CharacterSetPreset = CharacterSetPreset,
        FontFamily = FontFamily,
        ContrastPower = ContrastPower,
        DirectionalContrastStrength = DirectionalContrastStrength,
        Invert = Invert,
        UseColor = UseColor,
        AnimationSpeedMultiplier = AnimationSpeedMultiplier,
        LoopCount = LoopCount,
        EnableEdgeDetection = EnableEdgeDetection,
        EdgeStrength = EdgeStrength,
        BackgroundThreshold = BackgroundThreshold,
        DarkBackgroundThreshold = DarkBackgroundThreshold,
        AutoBackgroundSuppression = AutoBackgroundSuppression,
        UseParallelProcessing = UseParallelProcessing,
        PreBufferFrames = PreBufferFrames
    };
}
