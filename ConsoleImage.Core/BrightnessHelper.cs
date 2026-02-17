// Shared brightness calculation utilities
// Centralizes the standard luminance formula used across all renderers

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

// Note: Rgb24 is also commonly used, so we provide overloads for both Rgba32 and Rgb24

/// <summary>
///     Utility class for brightness and luminance calculations.
///     Uses the standard perceived brightness formula (ITU BT.601).
/// </summary>
public static class BrightnessHelper
{
    // Luminance coefficients (ITU BT.601)
    private const float RedCoefficient = 0.299f;
    private const float GreenCoefficient = 0.587f;
    private const float BlueCoefficient = 0.114f;

    /// <summary>
    ///     Calculate perceived brightness using standard luminance formula.
    /// </summary>
    /// <param name="pixel">The pixel to calculate brightness for.</param>
    /// <returns>Brightness value from 0.0 (black) to 1.0 (white).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetBrightness(Rgba32 pixel)
    {
        return (RedCoefficient * pixel.R + GreenCoefficient * pixel.G + BlueCoefficient * pixel.B) / 255f;
    }

    /// <summary>
    ///     Calculate perceived brightness from RGB components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetBrightness(int r, int g, int b)
    {
        return (RedCoefficient * r + GreenCoefficient * g + BlueCoefficient * b) / 255f;
    }

    /// <summary>
    ///     Calculate perceived brightness from RGB byte components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetBrightness(byte r, byte g, byte b)
    {
        return (RedCoefficient * r + GreenCoefficient * g + BlueCoefficient * b) / 255f;
    }

    /// <summary>
    ///     Calculate perceived brightness for Rgb24 pixel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetBrightness(Rgb24 pixel)
    {
        return (RedCoefficient * pixel.R + GreenCoefficient * pixel.G + BlueCoefficient * pixel.B) / 255f;
    }

    /// <summary>
    ///     Convert a pixel to grayscale value (0-255).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToGrayscale(Rgba32 pixel)
    {
        return (byte)(RedCoefficient * pixel.R + GreenCoefficient * pixel.G + BlueCoefficient * pixel.B);
    }

    /// <summary>
    ///     Check if a color should be skipped based on brightness thresholds.
    ///     Used for terminal optimization to skip very dark/bright colors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldSkipColor(float brightness, float? darkThreshold, float? lightThreshold)
    {
        return (darkThreshold.HasValue && brightness < darkThreshold.Value) ||
               (lightThreshold.HasValue && brightness > lightThreshold.Value);
    }

    /// <summary>
    ///     Check if a color should be skipped based on brightness thresholds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldSkipColor(Rgba32 pixel, float? darkThreshold, float? lightThreshold)
    {
        if (!darkThreshold.HasValue && !lightThreshold.HasValue)
            return false;

        var brightness = GetBrightness(pixel);
        return ShouldSkipColor(brightness, darkThreshold, lightThreshold);
    }

    /// <summary>
    ///     Convert a single sRGB channel value (0-1) to linear light.
    ///     Uses the exact sRGB transfer function (IEC 61966-2-1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SrgbToLinear(float v)
    {
        return v <= 0.04045f
            ? v / 12.92f
            : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>
    ///     Convert a single linear light value (0-1) back to sRGB.
    ///     Inverse of <see cref="SrgbToLinear"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearToSrgb(float v)
    {
        return v <= 0.0031308f
            ? v * 12.92f
            : 1.055f * MathF.Pow(v, 1f / 2.4f) - 0.055f;
    }

    /// <summary>
    ///     Calculate perceived brightness in linear light space.
    ///     Linearizes each sRGB channel before applying BT.601 luminance weights,
    ///     giving physically correct results for dithering and edge detection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetLinearBrightness(Rgba32 pixel)
    {
        var rLin = SrgbToLinear(pixel.R / 255f);
        var gLin = SrgbToLinear(pixel.G / 255f);
        var bLin = SrgbToLinear(pixel.B / 255f);
        return RedCoefficient * rLin + GreenCoefficient * gLin + BlueCoefficient * bLin;
    }

    /// <summary>
    ///     Apply gamma correction to a brightness value.
    ///     Gamma less than 1.0 brightens, greater than 1.0 darkens.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ApplyGamma(float brightness, float gamma)
    {
        if (gamma == 1.0f) return brightness;
        return MathF.Pow(brightness, gamma);
    }

    /// <summary>
    ///     Apply gamma correction to a pixel, returning a new brightened/darkened pixel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba32 ApplyGamma(Rgba32 pixel, float gamma)
    {
        if (gamma == 1.0f) return pixel;

        // Apply gamma to each channel
        var r = (byte)Math.Clamp(MathF.Pow(pixel.R / 255f, gamma) * 255f, 0, 255);
        var g = (byte)Math.Clamp(MathF.Pow(pixel.G / 255f, gamma) * 255f, 0, 255);
        var b = (byte)Math.Clamp(MathF.Pow(pixel.B / 255f, gamma) * 255f, 0, 255);

        return new Rgba32(r, g, b, pixel.A);
    }
}