// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering

using System.Text.Json.Serialization;

namespace ConsoleImage.Core;

/// <summary>
///     Configuration options for ASCII rendering.
///     Can be bound from appsettings.json via IConfiguration.
/// </summary>
public class RenderOptions
{
    /// <summary>
    ///     Width of the output in characters. If null, calculated from height maintaining aspect ratio.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    ///     Height of the output in characters. If null, calculated from width maintaining aspect ratio.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    ///     Maximum width in characters (default: 120)
    /// </summary>
    public int MaxWidth { get; set; } = 120;

    /// <summary>
    ///     Maximum height in characters (default: 60)
    /// </summary>
    public int MaxHeight { get; set; } = 60;

    /// <summary>
    ///     Character aspect ratio compensation. Console fonts are typically taller than wide.
    ///     Default is 0.5 (characters are about twice as tall as wide)
    /// </summary>
    public float CharacterAspectRatio { get; set; } = 0.5f;

    /// <summary>
    ///     Character set to use for rendering. If null, uses default set.
    ///     Characters should be ordered from lightest to darkest.
    /// </summary>
    public string? CharacterSet { get; set; }

    /// <summary>
    ///     Character set preset name: "default", "simple", "block", "extended"
    ///     Only used if CharacterSet is null.
    /// </summary>
    public string? CharacterSetPreset { get; set; }

    /// <summary>
    ///     Font family to use for character shape analysis. If null, uses default monospace.
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    ///     Global contrast enhancement power (1.0 = no enhancement, higher = more contrast)
    ///     Recommended: 2.0-4.0. Default: 2.5
    /// </summary>
    public float ContrastPower { get; set; } = 2.5f;

    /// <summary>
    ///     Directional contrast enhancement strength (0.0 = disabled, 1.0 = full)
    ///     Default: 0.3
    /// </summary>
    public float DirectionalContrastStrength { get; set; } = 0.3f;

    /// <summary>
    ///     Gamma correction for brightness adjustment.
    ///     Values less than 1.0 brighten the output, greater than 1.0 darken it.
    ///     Default: 0.65 (brighten to compensate for character density)
    /// </summary>
    public float Gamma { get; set; } = 0.65f;

    /// <summary>
    ///     Invert the output so black source pixels become spaces.
    ///     Default is TRUE because most terminals have dark backgrounds.
    /// </summary>
    public bool Invert { get; set; } = true;

    /// <summary>
    ///     Enable colored output using ANSI escape codes.
    ///     Default is TRUE for modern terminals.
    /// </summary>
    public bool UseColor { get; set; } = true;

    /// <summary>
    ///     For animated GIFs: frame delay multiplier (1.0 = original speed)
    /// </summary>
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    /// <summary>
    ///     For animated GIFs: loop count (0 = infinite)
    /// </summary>
    public int LoopCount { get; set; }

    /// <summary>
    ///     Enable edge detection to enhance foreground visibility
    /// </summary>
    public bool EnableEdgeDetection { get; set; }

    /// <summary>
    ///     Edge detection strength (0.0-1.0). Higher values make edges more prominent.
    /// </summary>
    public float EdgeStrength { get; set; } = 0.5f;

    /// <summary>
    ///     Background suppression threshold (0.0-1.0). Pixels above this brightness are suppressed.
    ///     Useful for removing uniform light backgrounds. Null = disabled.
    /// </summary>
    public float? BackgroundThreshold { get; set; }

    /// <summary>
    ///     Dark background suppression threshold (0.0-1.0). Pixels below this brightness are suppressed.
    ///     Useful for removing uniform dark backgrounds (like black space backgrounds). Null = disabled.
    /// </summary>
    public float? DarkBackgroundThreshold { get; set; }

    /// <summary>
    ///     Automatically detect and suppress background based on edge pixels.
    ///     Works for both light and dark backgrounds. Default is FALSE because
    ///     it can cause unexpected results on high-contrast content.
    /// </summary>
    public bool AutoBackgroundSuppression { get; set; }

    /// <summary>
    ///     Enable parallel processing for faster rendering (default: true)
    /// </summary>
    public bool UseParallelProcessing { get; set; } = true;

    /// <summary>
    ///     Pre-buffer all frames before playback for smoother animation (default: true).
    ///     When false, frames are rendered on-demand which uses less memory but may stutter.
    /// </summary>
    public bool PreBufferFrames { get; set; } = true;

    /// <summary>
    ///     Frame sampling rate for animations. 1 = every frame, 2 = every 2nd frame, etc.
    ///     Higher values reduce memory usage and processing time but may cause choppy playback.
    ///     Default: 1 (no frame skipping)
    /// </summary>
    public int FrameSampleRate { get; set; } = 1;

    /// <summary>
    ///     Enable Floyd-Steinberg dithering for smoother gradients.
    ///     Spreads quantization error to neighboring pixels for better gradient rendering.
    ///     NOTE: Currently disabled by default pending stability fixes.
    /// </summary>
    public bool EnableDithering { get; set; }

    /// <summary>
    ///     Enable edge-direction aware character selection.
    ///     Uses directional characters (/ \ | -) based on detected edge angles.
    ///     NOTE: Currently disabled by default pending stability fixes.
    /// </summary>
    public bool EnableEdgeDirectionChars { get; set; }

    /// <summary>
    ///     Brightness threshold for dark terminal optimization (0.0-1.0).
    ///     Pixels with brightness below this threshold will be rendered as plain spaces
    ///     without color codes, blending with the dark terminal background.
    ///     Null = disabled (output all colors). Default: null (disabled)
    ///     Use --dark-cutoff 0.1 to enable if needed for specific content.
    /// </summary>
    public float? DarkTerminalBrightnessThreshold { get; set; }

    /// <summary>
    ///     Brightness threshold for light terminal optimization (0.0-1.0).
    ///     Pixels with brightness above this threshold will be rendered as plain spaces
    ///     without color codes, blending with the light terminal background.
    ///     Null = disabled (output all colors). Default: null (disabled)
    /// </summary>
    public float? LightTerminalBrightnessThreshold { get; set; }

    /// <summary>
    ///     Gets the effective character set, considering presets
    /// </summary>
    [JsonIgnore]
    public string EffectiveCharacterSet => CharacterSet ?? GetPresetCharacterSet(CharacterSetPreset);

    /// <summary>
    ///     Create default options - works well for most images
    /// </summary>
    public static RenderOptions Default => new();

    /// <summary>
    ///     Create options for high-detail output
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
    ///     Create options for non-colored output (monochrome)
    /// </summary>
    public static RenderOptions Monochrome => new()
    {
        UseColor = false,
        ContrastPower = 2.5f
    };

    /// <summary>
    ///     Create options for light terminal/paper (non-inverted output)
    /// </summary>
    public static RenderOptions ForLightBackground => new()
    {
        Invert = false,
        UseColor = false,
        ContrastPower = 2.5f
    };

    /// <summary>
    ///     Create options for images with dark backgrounds (like space images).
    ///     Suppresses dark pixels so they appear as empty space.
    /// </summary>
    public static RenderOptions ForDarkBackground => new()
    {
        DarkBackgroundThreshold = 0.15f,
        ContrastPower = 3.0f,
        DirectionalContrastStrength = 0.4f
    };

    /// <summary>
    ///     Create options with higher contrast for better detail.
    ///     Use BackgroundThreshold or DarkBackgroundThreshold for manual control.
    /// </summary>
    public static RenderOptions HighContrast => new()
    {
        ContrastPower = 3.5f,
        DirectionalContrastStrength = 0.4f
    };

    private static string GetPresetCharacterSet(string? preset)
    {
        return preset?.ToLowerInvariant() switch
        {
            "simple" => CharacterMap.SimpleCharacterSet,
            "block" => CharacterMap.BlockCharacterSet,
            "extended" => CharacterMap.ExtendedCharacterSet,
            _ => CharacterMap.DefaultCharacterSet
        };
    }

    /// <summary>
    ///     Create options optimized for terminal output with specified dimensions
    /// </summary>
    public static RenderOptions ForTerminal(int? width = null, int? height = null)
    {
        return new RenderOptions
        {
            Width = width,
            Height = height,
            MaxWidth = width ?? 120,
            MaxHeight = height ?? 40,
            ContrastPower = 2.5f,
            DirectionalContrastStrength = 0.3f
        };
    }

    /// <summary>
    ///     Create options for animated GIF playback
    /// </summary>
    public static RenderOptions ForAnimation(int loopCount = 0)
    {
        return new RenderOptions
        {
            LoopCount = loopCount,
            MaxWidth = 100,
            MaxHeight = 40
        };
    }

    /// <summary>
    ///     Calculate output dimensions maintaining aspect ratio (for ASCII mode - 1 char = 1 pixel)
    /// </summary>
    public (int width, int height) CalculateDimensions(int imageWidth, int imageHeight)
    {
        return CalculateVisualDimensions(imageWidth, imageHeight, 1, 1);
    }

    /// <summary>
    ///     Calculate output pixel dimensions accounting for CharacterAspectRatio.
    ///     This is the core calculation used by all render modes.
    /// </summary>
    /// <param name="imageWidth">Source image width</param>
    /// <param name="imageHeight">Source image height</param>
    /// <param name="pixelsPerCharWidth">Horizontal pixels per character (1 for ASCII/blocks, 2 for braille)</param>
    /// <param name="pixelsPerCharHeight">Vertical pixels per character (1 for ASCII, 2 for blocks, 4 for braille)</param>
    /// <returns>Output dimensions in pixels</returns>
    public (int width, int height) CalculateVisualDimensions(
        int imageWidth, int imageHeight,
        int pixelsPerCharWidth, int pixelsPerCharHeight)
    {
        var imageAspect = (float)imageWidth / imageHeight;

        int outputCharWidth, outputCharHeight;

        if (Width.HasValue && Height.HasValue)
        {
            // Both dimensions explicitly set - use exact dimensions (may distort image)
            outputCharWidth = Width.Value;
            outputCharHeight = Height.Value;
        }
        else if (Width.HasValue)
        {
            // Only width specified - use exact width, calculate height from aspect ratio
            outputCharWidth = Width.Value;
            // Visual width = outputCharWidth * CharacterAspectRatio
            // Visual height = Visual width / imageAspect
            // Output char height = Visual height
            var visualWidth = outputCharWidth * CharacterAspectRatio;
            var visualHeight = visualWidth / imageAspect;
            outputCharHeight = Math.Max(1, (int)visualHeight);
            // Clamp to MaxHeight if set
            if (outputCharHeight > MaxHeight)
                outputCharHeight = MaxHeight;
        }
        else if (Height.HasValue)
        {
            // Only height specified - use exact height, calculate width from aspect ratio
            outputCharHeight = Height.Value;
            // Visual height = outputCharHeight
            // Visual width = Visual height * imageAspect
            // Output char width = Visual width / CharacterAspectRatio
            float visualHeight = outputCharHeight;
            var visualWidth = visualHeight * imageAspect;
            outputCharWidth = Math.Max(1, (int)(visualWidth / CharacterAspectRatio));
            // Clamp to MaxWidth if set
            if (outputCharWidth > MaxWidth)
                outputCharWidth = MaxWidth;
        }
        else
        {
            // Neither dimension specified - fit into MaxWidth x MaxHeight while maintaining aspect ratio
            var visualContainerWidth = MaxWidth * CharacterAspectRatio;
            float visualContainerHeight = MaxHeight;
            var containerVisualAspect = visualContainerWidth / visualContainerHeight;

            // Fit image into visual container
            float outputVisualWidth, outputVisualHeight;
            if (imageAspect > containerVisualAspect)
            {
                // Width-constrained
                outputVisualWidth = visualContainerWidth;
                outputVisualHeight = visualContainerWidth / imageAspect;
            }
            else
            {
                // Height-constrained
                outputVisualHeight = visualContainerHeight;
                outputVisualWidth = visualContainerHeight * imageAspect;
            }

            outputCharWidth = Math.Max(1, (int)(outputVisualWidth / CharacterAspectRatio));
            outputCharHeight = Math.Max(1, (int)outputVisualHeight);
        }

        // Convert character dimensions to pixel dimensions
        var outputWidth = Math.Max(1, outputCharWidth * pixelsPerCharWidth);
        var outputHeight = Math.Max(1, outputCharHeight * pixelsPerCharHeight);

        return (outputWidth, outputHeight);
    }

    /// <summary>
    ///     Create a copy with modifications using a fluent builder pattern
    /// </summary>
    public RenderOptions With(Action<RenderOptions> configure)
    {
        var copy = Clone();
        configure(copy);
        return copy;
    }

    /// <summary>
    ///     Create a deep copy of these options
    /// </summary>
    public RenderOptions Clone()
    {
        return new RenderOptions
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
            Gamma = Gamma,
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
            PreBufferFrames = PreBufferFrames,
            FrameSampleRate = FrameSampleRate,
            EnableDithering = EnableDithering,
            EnableEdgeDirectionChars = EnableEdgeDirectionChars,
            DarkTerminalBrightnessThreshold = DarkTerminalBrightnessThreshold,
            LightTerminalBrightnessThreshold = LightTerminalBrightnessThreshold
        };
    }
}