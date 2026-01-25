// ASCII Art Renderer - C# implementation based on Alex Harri's approach
// Original article: https://alexharri.com/blog/ascii-rendering
// Color block renderer for high-fidelity colored terminal output

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Core;

/// <summary>
///     Renders images as colored blocks using Unicode half-block characters.
///     Each character cell displays two pixels vertically using upper/lower half-blocks
///     with separate foreground and background colors, effectively doubling resolution.
/// </summary>
public class ColorBlockRenderer : IDisposable
{
    // Unicode half-block characters
    private const char UpperHalfBlock = '▀'; // Upper half solid
    private const char LowerHalfBlock = '▄'; // Lower half solid
    private const char FullBlock = '█'; // Full block
    private const char Space = ' '; // Empty
    private readonly RenderOptions _options;
    private bool _disposed;

    // Reusable buffer to reduce GC pressure during video playback
    private Rgba32[]? _pixelBuffer;
    private int _lastBufferSize;

    public ColorBlockRenderer(RenderOptions? options = null)
    {
        _options = options ?? RenderOptions.Default;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Return pooled buffer
        if (_pixelBuffer != null)
        {
            ArrayPool<Rgba32>.Shared.Return(_pixelBuffer);
            _pixelBuffer = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Render image file to colored block output
    /// </summary>
    public string RenderFile(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderImage(image);
    }

    /// <summary>
    ///     Render image stream to colored block output
    /// </summary>
    public string RenderStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderImage(image);
    }

    /// <summary>
    ///     Render image to colored block output
    /// </summary>
    public string RenderImage(Image<Rgba32> image)
    {
        // For half-block rendering, each character represents 2 pixels vertically
        // Calculate output dimensions in PIXELS (not characters)
        var (pixelWidth, pixelHeight) = CalculatePixelDimensions(image.Width, image.Height);

        // Ensure even height for half-block pairing
        if (pixelHeight % 2 != 0) pixelHeight++;

        // Resize image to target pixel dimensions
        using var resized = image.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(pixelWidth, pixelHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            });
        });

        return RenderPixels(resized);
    }

    /// <summary>
    ///     Calculate output dimensions in PIXELS for half-block rendering.
    ///     Uses the shared CalculateVisualDimensions method from RenderOptions.
    /// </summary>
    private (int width, int height) CalculatePixelDimensions(int imageWidth, int imageHeight)
    {
        // Half-blocks: 1 pixel per char width, 2 pixels per char height
        var (width, height) = _options.CalculateVisualDimensions(imageWidth, imageHeight, 1, 2);

        // Ensure even height for half-block pairing
        if (height % 2 != 0) height++;

        return (width, Math.Max(2, height));
    }

    private string RenderPixels(Image<Rgba32> image)
    {
        var charRows = image.Height / 2;
        var width = image.Width;
        var totalPixels = width * image.Height;

        // Get brightness thresholds based on terminal mode
        var darkThreshold = _options.Invert ? _options.DarkTerminalBrightnessThreshold : null;
        var lightThreshold = !_options.Invert ? _options.LightTerminalBrightnessThreshold : null;

        // Gamma correction
        var gamma = _options.Gamma;
        var applyGamma = gamma != 1.0f;

        // Color mode - when disabled, output greyscale
        var useColor = _options.UseColor;

        // Color quantization using shared helper
        var quantStep = ColorHelper.GetQuantizationStep(_options);
        var quantize = quantStep > 1;

        // Reuse pixel buffer from ArrayPool
        if (_pixelBuffer == null || _lastBufferSize < totalPixels)
        {
            if (_pixelBuffer != null)
                ArrayPool<Rgba32>.Shared.Return(_pixelBuffer);
            _pixelBuffer = ArrayPool<Rgba32>.Shared.Rent(totalPixels);
            _lastBufferSize = totalPixels;
        }

        var pixelData = _pixelBuffer;
        image.CopyPixelDataTo(pixelData.AsSpan(0, totalPixels));

        // Parallel row rendering for performance
        if (_options.UseParallelProcessing && charRows > 4)
        {
            var rowStrings = new string[charRows];

            Parallel.For(0, charRows, row =>
            {
                var rowSb = new StringBuilder(width * 30); // Pre-size for ANSI codes
                var y1 = row * 2; // Upper pixel row
                var y2 = row * 2 + 1; // Lower pixel row
                var offset1 = y1 * width;
                var offset2 = y2 * width;

                for (var x = 0; x < width; x++)
                {
                    var upper = pixelData[offset1 + x];
                    var lower = pixelData[offset2 + x];

                    // Apply gamma correction
                    if (applyGamma)
                    {
                        upper = ColorHelper.ApplyGamma(upper, gamma);
                        lower = ColorHelper.ApplyGamma(lower, gamma);
                    }

                    // Convert to greyscale if color disabled
                    if (!useColor)
                    {
                        upper = ColorHelper.ToGreyscale(upper);
                        lower = ColorHelper.ToGreyscale(lower);
                    }

                    // Quantize colors for palette reduction or temporal stability
                    if (quantize)
                    {
                        upper = ColorHelper.QuantizeColor(upper, quantStep);
                        lower = ColorHelper.QuantizeColor(lower, quantStep);
                    }

                    AppendColoredBlock(rowSb, upper, lower, darkThreshold, lightThreshold);
                }

                rowSb.Append("\x1b[0m"); // Reset at end of line
                rowStrings[row] = rowSb.ToString();
            });

            return string.Join("\n", rowStrings);
        }

        // Sequential fallback
        var sb = new StringBuilder();
        for (var row = 0; row < charRows; row++)
        {
            var y1 = row * 2; // Upper pixel row
            var y2 = row * 2 + 1; // Lower pixel row
            var offset1 = y1 * width;
            var offset2 = y2 * width;

            for (var x = 0; x < width; x++)
            {
                var upper = pixelData[offset1 + x];
                var lower = pixelData[offset2 + x];

                // Apply gamma correction
                if (applyGamma)
                {
                    upper = ColorHelper.ApplyGamma(upper, gamma);
                    lower = ColorHelper.ApplyGamma(lower, gamma);
                }

                // Convert to greyscale if color disabled
                if (!useColor)
                {
                    upper = ColorHelper.ToGreyscale(upper);
                    lower = ColorHelper.ToGreyscale(lower);
                }

                // Quantize colors for palette reduction or temporal stability
                if (quantize)
                {
                    upper = ColorHelper.QuantizeColor(upper, quantStep);
                    lower = ColorHelper.QuantizeColor(lower, quantStep);
                }

                AppendColoredBlock(sb, upper, lower, darkThreshold, lightThreshold);
            }

            sb.Append("\x1b[0m"); // Reset at end of line
            if (row < charRows - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendColoredBlock(StringBuilder sb, Rgba32 upper, Rgba32 lower, float? darkThreshold,
        float? lightThreshold)
    {
        // Calculate brightness
        var upperBrightness = BrightnessHelper.GetBrightness(upper);
        var lowerBrightness = BrightnessHelper.GetBrightness(lower);

        // Check if pixels should be skipped (blend with terminal background)
        var upperSkip = upper.A < 128 ||
                        BrightnessHelper.ShouldSkipColor(upperBrightness, darkThreshold, lightThreshold);
        var lowerSkip = lower.A < 128 ||
                        BrightnessHelper.ShouldSkipColor(lowerBrightness, darkThreshold, lightThreshold);

        if (upperSkip && lowerSkip)
        {
            // Both should blend with background - reset colors and output space
            sb.Append(AnsiCodes.Reset);
            sb.Append(' ');
            return;
        }

        if (upperSkip)
        {
            // Only lower visible - use lower half block with foreground color only
            AnsiCodes.AppendResetAndForeground(sb, lower);
            sb.Append(LowerHalfBlock);
            return;
        }

        if (lowerSkip)
        {
            // Only upper visible - use upper half block with foreground color only
            AnsiCodes.AppendResetAndForeground(sb, upper);
            sb.Append(UpperHalfBlock);
            return;
        }

        // Both visible - use upper half block with upper as foreground, lower as background
        AnsiCodes.AppendForegroundAndBackground(sb, upper, lower);
        sb.Append(UpperHalfBlock);
    }

    /// <summary>
    ///     Render a file to a ColorBlockFrame.
    /// </summary>
    public ColorBlockFrame RenderFileToFrame(string path)
    {
        return new ColorBlockFrame(RenderFile(path), 0);
    }

    /// <summary>
    ///     Render a stream to a ColorBlockFrame.
    /// </summary>
    public ColorBlockFrame RenderStreamToFrame(Stream stream)
    {
        return new ColorBlockFrame(RenderStream(stream), 0);
    }

    /// <summary>
    ///     Render animated GIF to list of colored block frames
    /// </summary>
    public IReadOnlyList<ColorBlockFrame> RenderGif(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderGifFramesInternal(image);
    }

    /// <summary>
    ///     Render animated GIF stream to list of colored block frames
    /// </summary>
    public IReadOnlyList<ColorBlockFrame> RenderGifStream(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderGifFramesInternal(image);
    }

    /// <summary>
    ///     Render animated GIF to list of colored block frames (for GIF output).
    /// </summary>
    public List<ColorBlockFrame> RenderGifFrames(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return RenderGifFramesInternal(image);
    }

    /// <summary>
    ///     Render animated GIF stream to list of colored block frames (for GIF output).
    /// </summary>
    public List<ColorBlockFrame> RenderGifFrames(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        return RenderGifFramesInternal(image);
    }

    private List<ColorBlockFrame> RenderGifFramesInternal(Image<Rgba32> image)
    {
        var frames = new List<ColorBlockFrame>();

        // Determine frame step for sampling (skip frames for efficiency)
        var frameStep = Math.Max(1, _options.FrameSampleRate);

        for (var i = 0; i < image.Frames.Count; i += frameStep)
        {
            using var frameImage = image.Frames.CloneFrame(i);

            var metadata = image.Frames[i].Metadata.GetGifMetadata();

            var delayMs = 100;
            if (metadata.FrameDelay > 0) delayMs = metadata.FrameDelay * 10;
            // Adjust delay to account for skipped frames
            delayMs = (int)(delayMs * frameStep / _options.AnimationSpeedMultiplier);

            var content = RenderImage(frameImage);
            frames.Add(new ColorBlockFrame(content, delayMs));
        }

        return frames;
    }
}

/// <summary>
///     A single frame of colored block output
/// </summary>
public class ColorBlockFrame : IAnimationFrame
{
    public ColorBlockFrame(string content, int delayMs)
    {
        Content = content;
        DelayMs = delayMs;
    }

    public string Content { get; }
    public int DelayMs { get; }

    public override string ToString()
    {
        return Content;
    }
}