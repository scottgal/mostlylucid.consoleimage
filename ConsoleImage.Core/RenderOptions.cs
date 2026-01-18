// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering

namespace ConsoleImage.Core;

/// <summary>
/// Configuration options for ASCII rendering
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
    /// </summary>
    public string? CharacterSet { get; set; }

    /// <summary>
    /// Font family to use for character shape analysis. If null, uses default monospace.
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// Global contrast enhancement power (1.0 = no enhancement, higher = more contrast)
    /// Recommended: 2.0-4.0
    /// </summary>
    public float ContrastPower { get; set; } = 2.5f;

    /// <summary>
    /// Directional contrast enhancement strength (0.0 = disabled, 1.0 = full)
    /// </summary>
    public float DirectionalContrastStrength { get; set; } = 0.3f;

    /// <summary>
    /// Invert the output (light characters on dark background)
    /// </summary>
    public bool Invert { get; set; }

    /// <summary>
    /// Enable colored output using ANSI escape codes
    /// </summary>
    public bool UseColor { get; set; }

    /// <summary>
    /// For animated GIFs: frame delay multiplier (1.0 = original speed)
    /// </summary>
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// For animated GIFs: loop count (0 = infinite)
    /// </summary>
    public int LoopCount { get; set; } = 0;

    /// <summary>
    /// Create default options
    /// </summary>
    public static RenderOptions Default => new();

    /// <summary>
    /// Create options optimized for terminal output
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
        DirectionalContrastStrength = 0.4f
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
            // Auto-calculate based on max dimensions
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

        // Clamp to max dimensions
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
}
