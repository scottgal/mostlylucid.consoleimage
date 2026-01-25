// Shared buffer management for all renderers
// Reduces GC pressure during video playback by reusing buffers

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core;

/// <summary>
///     Shared buffer pool for renderer operations.
///     Provides reusable float and Rgba32 arrays to reduce allocations.
/// </summary>
public sealed class RendererBuffers : IDisposable
{
    private float[]? _floatBuffer1;
    private float[]? _floatBuffer2;
    private Rgba32[]? _colorBuffer;
    private int _lastSize;
    private bool _disposed;

    /// <summary>
    ///     Get or create a float buffer of at least the specified size.
    /// </summary>
    public float[] RentFloatBuffer(int minSize, int bufferIndex = 0)
    {
        ref var buffer = ref bufferIndex == 0 ? ref _floatBuffer1 : ref _floatBuffer2;

        if (buffer == null || buffer.Length < minSize)
        {
            if (buffer != null)
                ArrayPool<float>.Shared.Return(buffer);
            buffer = ArrayPool<float>.Shared.Rent(minSize);
        }

        return buffer;
    }

    /// <summary>
    ///     Get or create a color buffer of at least the specified size.
    /// </summary>
    public Rgba32[] RentColorBuffer(int minSize)
    {
        if (_colorBuffer == null || _colorBuffer.Length < minSize)
        {
            if (_colorBuffer != null)
                ArrayPool<Rgba32>.Shared.Return(_colorBuffer);
            _colorBuffer = ArrayPool<Rgba32>.Shared.Rent(minSize);
        }

        return _colorBuffer;
    }

    /// <summary>
    ///     Ensure buffers are sized for the given pixel count.
    ///     Returns (brightness, colors) tuple.
    /// </summary>
    public (float[] brightness, Rgba32[] colors) EnsurePixelBuffers(int pixelCount)
    {
        if (_lastSize < pixelCount)
        {
            // Return old buffers
            if (_floatBuffer1 != null)
                ArrayPool<float>.Shared.Return(_floatBuffer1);
            if (_colorBuffer != null)
                ArrayPool<Rgba32>.Shared.Return(_colorBuffer);

            _floatBuffer1 = ArrayPool<float>.Shared.Rent(pixelCount);
            _colorBuffer = ArrayPool<Rgba32>.Shared.Rent(pixelCount);
            _lastSize = pixelCount;
        }

        return (_floatBuffer1!, _colorBuffer!);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_floatBuffer1 != null)
        {
            ArrayPool<float>.Shared.Return(_floatBuffer1);
            _floatBuffer1 = null;
        }
        if (_floatBuffer2 != null)
        {
            ArrayPool<float>.Shared.Return(_floatBuffer2);
            _floatBuffer2 = null;
        }
        if (_colorBuffer != null)
        {
            ArrayPool<Rgba32>.Shared.Return(_colorBuffer);
            _colorBuffer = null;
        }
    }
}

/// <summary>
///     Common color operations shared across all renderers.
/// </summary>
public static class ColorHelper
{
    // Pre-computed greyscale ANSI escapes
    private static readonly string[] GreyscaleEscapes = InitGreyscaleEscapes();

    private static string[] InitGreyscaleEscapes()
    {
        var escapes = new string[256];
        for (var i = 0; i < 256; i++)
            escapes[i] = $"\x1b[38;2;{i};{i};{i}m";
        return escapes;
    }

    /// <summary>
    ///     Append ANSI color code efficiently.
    ///     Uses cached string for greyscale colors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendColorCode(System.Text.StringBuilder sb, byte r, byte g, byte b)
    {
        if (r == g && g == b)
        {
            sb.Append(GreyscaleEscapes[r]);
        }
        else
        {
            sb.Append("\x1b[38;2;");
            sb.Append(r);
            sb.Append(';');
            sb.Append(g);
            sb.Append(';');
            sb.Append(b);
            sb.Append('m');
        }
    }

    /// <summary>
    ///     Apply gamma correction to a color.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba32 ApplyGamma(Rgba32 pixel, float gamma)
    {
        if (gamma == 1.0f) return pixel;

        return new Rgba32(
            (byte)Math.Clamp(MathF.Pow(pixel.R / 255f, gamma) * 255f, 0, 255),
            (byte)Math.Clamp(MathF.Pow(pixel.G / 255f, gamma) * 255f, 0, 255),
            (byte)Math.Clamp(MathF.Pow(pixel.B / 255f, gamma) * 255f, 0, 255),
            pixel.A
        );
    }

    /// <summary>
    ///     Convert a color to greyscale using luminance formula.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba32 ToGreyscale(Rgba32 pixel)
    {
        var grey = (byte)(0.2126f * pixel.R + 0.7152f * pixel.G + 0.0722f * pixel.B);
        return new Rgba32(grey, grey, grey, pixel.A);
    }

    /// <summary>
    ///     Quantize color to reduce noise and improve temporal stability.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba32 QuantizeColor(Rgba32 pixel, int step)
    {
        if (step <= 1) return pixel;
        return new Rgba32(
            (byte)(pixel.R / step * step),
            (byte)(pixel.G / step * step),
            (byte)(pixel.B / step * step),
            pixel.A
        );
    }

    /// <summary>
    ///     Calculate quantization step from color count or temporal stability settings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetQuantizationStep(RenderOptions options)
    {
        if (options.ColorCount.HasValue && options.ColorCount.Value > 0)
            return Math.Max(1, 256 / options.ColorCount.Value);

        if (options.EnableTemporalStability)
            return Math.Max(1, options.ColorStabilityThreshold / 2);

        return 1;
    }

    /// <summary>
    ///     Compare two colors for equality within a threshold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ColorsNearlyEqual(Rgba32 a, Rgba32 b, int threshold = 0)
    {
        if (threshold == 0)
            return a.R == b.R && a.G == b.G && a.B == b.B;

        return Math.Abs(a.R - b.R) <= threshold &&
               Math.Abs(a.G - b.G) <= threshold &&
               Math.Abs(a.B - b.B) <= threshold;
    }
}
