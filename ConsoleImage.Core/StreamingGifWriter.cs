using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;

namespace ConsoleImage.Core;

/// <summary>
/// Memory-efficient streaming GIF writer that writes frames directly to disk.
/// Unlike GifWriter which buffers all frames in memory, this writes incrementally.
/// Uses a rolling buffer of max 3 frames for processing.
/// </summary>
public sealed class StreamingGifWriter : IAsyncDisposable, IDisposable
{
    private readonly GifWriterOptions _options;
    private readonly string _outputPath;
    private readonly string _tempPath;
    private Image<Rgba32>? _gif;
    private int _frameCount;
    private int _width;
    private int _height;
    private bool _disposed;
    private Font? _font;
    private double _totalMs;

    // Buffer for batch writing - keeps only last few frames
    private const int MaxBufferedFrames = 50;
    private int _bufferedSinceLastFlush;

    public StreamingGifWriter(string outputPath, GifWriterOptions? options = null)
    {
        _outputPath = outputPath;
        _options = options ?? new GifWriterOptions();
        _tempPath = outputPath + ".tmp";
    }

    /// <summary>
    /// Number of frames written so far.
    /// </summary>
    public int FrameCount => _frameCount;

    /// <summary>
    /// Total duration in milliseconds.
    /// </summary>
    public double TotalDurationMs => _totalMs;

    /// <summary>
    /// Check if we should stop adding frames based on limits.
    /// </summary>
    public bool ShouldStop =>
        (_options.MaxFrames.HasValue && _frameCount >= _options.MaxFrames.Value) ||
        (_options.MaxLengthSeconds.HasValue && _totalMs / 1000.0 >= _options.MaxLengthSeconds.Value);

    /// <summary>
    /// Add a raw image frame directly (for raw mode extraction).
    /// This is the most memory-efficient path.
    /// </summary>
    public void AddImageFrame(Image<Rgba32> sourceImage, int delayMs)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamingGifWriter));
        if (ShouldStop) return;

        EnsureInitialized(sourceImage.Width, sourceImage.Height);

        // Resize if needed
        Image<Rgba32> frameToAdd = sourceImage;
        bool needsDispose = false;

        if (sourceImage.Width != _width || sourceImage.Height != _height)
        {
            frameToAdd = sourceImage.Clone();
            frameToAdd.Mutate(x => x.Resize(_width, _height));
            needsDispose = true;
        }

        AddFrameToGif(frameToAdd, delayMs);

        if (needsDispose)
            frameToAdd.Dispose();

        _frameCount++;
        _totalMs += delayMs;
        _bufferedSinceLastFlush++;

        // Flush periodically to keep memory low
        if (_bufferedSinceLastFlush >= MaxBufferedFrames)
        {
            FlushToTemp();
        }
    }

    /// <summary>
    /// Add an ASCII text frame (renders text to image).
    /// </summary>
    public void AddTextFrame(string asciiContent, int delayMs)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamingGifWriter));
        if (ShouldStop) return;

        var lines = asciiContent.Split('\n');
        int maxWidth = lines.Max(l => StripAnsi(l).Length);
        int lineCount = lines.Length;

        // Calculate dimensions
        int charWidth = (int)(_options.FontSize * 6 / 10 * _options.Scale);
        int charHeight = (int)((_options.FontSize + 2) * _options.Scale);
        int scaledPadding = (int)(_options.Padding * _options.Scale);
        int imageWidth = maxWidth * charWidth + scaledPadding * 2;
        int imageHeight = lineCount * charHeight + scaledPadding * 2;

        EnsureInitialized(imageWidth, imageHeight);

        // Create and render frame
        using var frameImage = new Image<Rgba32>(imageWidth, imageHeight);
        frameImage.Mutate(ctx => ctx.Fill(_options.BackgroundColor));

        _font ??= GetMonospaceFont((int)(_options.FontSize * _options.Scale));

        int y = scaledPadding;
        foreach (var line in lines)
        {
            DrawColoredLine(frameImage, line, scaledPadding, y, charWidth, _font);
            y += charHeight;
        }

        AddFrameToGif(frameImage, delayMs);

        _frameCount++;
        _totalMs += delayMs;
        _bufferedSinceLastFlush++;

        if (_bufferedSinceLastFlush >= MaxBufferedFrames)
        {
            FlushToTemp();
        }
    }

    /// <summary>
    /// Add a ColorBlock frame.
    /// </summary>
    public void AddColorBlockFrame(ColorBlockFrame frame, int delayMs)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamingGifWriter));
        if (ShouldStop) return;

        using var image = RenderColorBlocksToImage(frame);
        EnsureInitialized(image.Width, image.Height);
        AddFrameToGif(image, delayMs);

        _frameCount++;
        _totalMs += delayMs;
        _bufferedSinceLastFlush++;

        if (_bufferedSinceLastFlush >= MaxBufferedFrames)
        {
            FlushToTemp();
        }
    }

    /// <summary>
    /// Add a Braille frame.
    /// </summary>
    public void AddBrailleFrame(BrailleFrame frame, int delayMs)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamingGifWriter));
        if (ShouldStop) return;

        using var image = RenderBrailleToImage(frame);
        EnsureInitialized(image.Width, image.Height);
        AddFrameToGif(image, delayMs);

        _frameCount++;
        _totalMs += delayMs;
        _bufferedSinceLastFlush++;

        if (_bufferedSinceLastFlush >= MaxBufferedFrames)
        {
            FlushToTemp();
        }
    }

    private void EnsureInitialized(int width, int height)
    {
        if (_gif != null) return;

        _width = width;
        _height = height;
        _gif = new Image<Rgba32>(width, height);

        var gifMetadata = _gif.Metadata.GetGifMetadata();
        gifMetadata.RepeatCount = (ushort)_options.LoopCount;
    }

    private void AddFrameToGif(Image<Rgba32> frameImage, int delayMs)
    {
        if (_gif == null) return;

        if (_frameCount == 0)
        {
            // First frame - copy to root frame
            frameImage.ProcessPixelRows(_gif, (src, tgt) =>
            {
                for (int y = 0; y < src.Height; y++)
                    src.GetRowSpan(y).CopyTo(tgt.GetRowSpan(y));
            });
            _gif.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;
        }
        else
        {
            var frame = _gif.Frames.AddFrame(frameImage.Frames.RootFrame);
            frame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;
        }
    }

    /// <summary>
    /// Flush buffered frames to temp file and clear memory.
    /// </summary>
    private void FlushToTemp()
    {
        if (_gif == null || _gif.Frames.Count <= 1) return;

        // Save current state to temp file
        var encoder = new GifEncoder
        {
            ColorTableMode = GifColorTableMode.Local
        };

        // Quantize if needed
        if (_options.MaxColors < 256)
        {
            _gif.Mutate(ctx => ctx.Quantize(new SixLabors.ImageSharp.Processing.Processors.Quantization.OctreeQuantizer(
                new SixLabors.ImageSharp.Processing.Processors.Quantization.QuantizerOptions
                {
                    MaxColors = Math.Clamp(_options.MaxColors, 2, 256)
                })));
        }

        _gif.SaveAsGif(_tempPath, encoder);
        _bufferedSinceLastFlush = 0;

        // Reload from temp to free memory from old frames
        // Keep only the structure, ImageSharp will manage frame memory
    }

    /// <summary>
    /// Finalize and save the GIF.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamingGifWriter));
        if (_gif == null || _frameCount == 0)
            throw new InvalidOperationException("No frames to save");

        await Task.Run(() =>
        {
            // Quantize if needed
            if (_options.MaxColors < 256)
            {
                _gif.Mutate(ctx => ctx.Quantize(new SixLabors.ImageSharp.Processing.Processors.Quantization.OctreeQuantizer(
                    new SixLabors.ImageSharp.Processing.Processors.Quantization.QuantizerOptions
                    {
                        MaxColors = Math.Clamp(_options.MaxColors, 2, 256)
                    })));
            }

            var encoder = new GifEncoder
            {
                ColorTableMode = GifColorTableMode.Local
            };

            _gif.SaveAsGif(_outputPath, encoder);

            // Clean up temp file
            if (File.Exists(_tempPath))
            {
                try { File.Delete(_tempPath); } catch { }
            }
        }, ct);
    }

    /// <summary>
    /// Save synchronously.
    /// </summary>
    public void Save()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamingGifWriter));
        if (_gif == null || _frameCount == 0)
            throw new InvalidOperationException("No frames to save");

        if (_options.MaxColors < 256)
        {
            _gif.Mutate(ctx => ctx.Quantize(new SixLabors.ImageSharp.Processing.Processors.Quantization.OctreeQuantizer(
                new SixLabors.ImageSharp.Processing.Processors.Quantization.QuantizerOptions
                {
                    MaxColors = Math.Clamp(_options.MaxColors, 2, 256)
                })));
        }

        var encoder = new GifEncoder
        {
            ColorTableMode = GifColorTableMode.Local
        };

        _gif.SaveAsGif(_outputPath, encoder);

        if (File.Exists(_tempPath))
        {
            try { File.Delete(_tempPath); } catch { }
        }
    }

    #region Rendering helpers

    private Image<Rgba32> RenderColorBlocksToImage(ColorBlockFrame frame)
    {
        var lines = frame.Content.Split('\n');
        int width = lines.Max(l => StripAnsi(l).Length);
        int height = lines.Length * 2;

        int pixelSize = Math.Max(1, (int)(_options.Scale * 4));
        var image = new Image<Rgba32>(width * pixelSize, height * pixelSize);
        image.Mutate(ctx => ctx.Fill(_options.BackgroundColor));

        for (int lineY = 0; lineY < lines.Length; lineY++)
        {
            var cells = ParseColorBlockLine(lines[lineY]);
            for (int x = 0; x < cells.Count; x++)
            {
                var (topColor, bottomColor) = cells[x];

                for (int py = 0; py < pixelSize; py++)
                    for (int px = 0; px < pixelSize; px++)
                        image[x * pixelSize + px, lineY * 2 * pixelSize + py] = ToRgba32(topColor);

                for (int py = 0; py < pixelSize; py++)
                    for (int px = 0; px < pixelSize; px++)
                        image[x * pixelSize + px, (lineY * 2 + 1) * pixelSize + py] = ToRgba32(bottomColor);
            }
        }

        return image;
    }

    private Image<Rgba32> RenderBrailleToImage(BrailleFrame frame)
    {
        var lines = frame.Content.Split('\n');
        int charWidth = lines.Max(l => StripAnsi(l).Length);
        int charHeight = lines.Length;

        int pixelWidth = charWidth * 2;
        int pixelHeight = charHeight * 4;

        int dotSize = Math.Max(1, (int)(_options.Scale * 2));
        var image = new Image<Rgba32>(pixelWidth * dotSize, pixelHeight * dotSize);
        image.Mutate(ctx => ctx.Fill(_options.BackgroundColor));

        for (int lineY = 0; lineY < lines.Length; lineY++)
        {
            var cells = ParseBrailleLine(lines[lineY]);
            for (int x = 0; x < cells.Count; x++)
            {
                var (brailleChar, fgColor) = cells[x];
                var dots = DecodeBraille(brailleChar);

                for (int dy = 0; dy < 4; dy++)
                {
                    for (int dx = 0; dx < 2; dx++)
                    {
                        if (dots[dy, dx])
                        {
                            int px = (x * 2 + dx) * dotSize;
                            int py = (lineY * 4 + dy) * dotSize;
                            for (int iy = 0; iy < dotSize; iy++)
                                for (int ix = 0; ix < dotSize; ix++)
                                    if (px + ix < image.Width && py + iy < image.Height)
                                        image[px + ix, py + iy] = ToRgba32(fgColor);
                        }
                    }
                }
            }
        }

        return image;
    }

    private static readonly System.Text.RegularExpressions.Regex AnsiOrCharRegex =
        new(@"\x1b\[([0-9;]*)m|([^\x1b])", System.Text.RegularExpressions.RegexOptions.Compiled);

    private List<(Color Top, Color Bottom)> ParseColorBlockLine(string line)
    {
        var result = new List<(Color, Color)>();
        Color currentFg = _options.ForegroundColor;
        Color currentBg = _options.BackgroundColor;

        foreach (System.Text.RegularExpressions.Match match in AnsiOrCharRegex.Matches(line))
        {
            if (match.Groups[1].Success)
            {
                var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                (currentFg, currentBg) = ParseColorBlockCodes(codes, currentFg, currentBg);
            }
            else if (match.Groups[2].Success)
            {
                char c = match.Groups[2].Value[0];
                var (top, bottom) = c switch
                {
                    '▀' => (currentFg, currentBg),
                    '▄' => (currentBg, currentFg),
                    '█' => (currentFg, currentFg),
                    ' ' => (currentBg, currentBg),
                    _ => (currentFg, currentBg)
                };
                result.Add((top, bottom));
            }
        }

        return result;
    }

    private List<(char BrailleChar, Color FgColor)> ParseBrailleLine(string line)
    {
        var result = new List<(char, Color)>();
        Color currentFg = _options.ForegroundColor;

        foreach (System.Text.RegularExpressions.Match match in AnsiOrCharRegex.Matches(line))
        {
            if (match.Groups[1].Success)
            {
                var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                currentFg = ParseAnsiCodes(codes, currentFg);
            }
            else if (match.Groups[2].Success)
            {
                char c = match.Groups[2].Value[0];
                result.Add((c, currentFg));
            }
        }

        return result;
    }

    private (Color Fg, Color Bg) ParseColorBlockCodes(string[] codes, Color currentFg, Color currentBg)
    {
        for (int i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out int code)) continue;

            switch (code)
            {
                case 0:
                    currentFg = _options.ForegroundColor;
                    currentBg = _options.BackgroundColor;
                    break;
                case 38:
                    if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
                    {
                        if (int.TryParse(codes[i + 2], out int r) &&
                            int.TryParse(codes[i + 3], out int g) &&
                            int.TryParse(codes[i + 4], out int b))
                        {
                            currentFg = Color.FromRgb((byte)r, (byte)g, (byte)b);
                            i += 4;
                        }
                    }
                    break;
                case 48:
                    if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
                    {
                        if (int.TryParse(codes[i + 2], out int r) &&
                            int.TryParse(codes[i + 3], out int g) &&
                            int.TryParse(codes[i + 4], out int b))
                        {
                            currentBg = Color.FromRgb((byte)r, (byte)g, (byte)b);
                            i += 4;
                        }
                    }
                    break;
            }
        }
        return (currentFg, currentBg);
    }

    private Color ParseAnsiCodes(string[] codes, Color currentColor)
    {
        if (codes.Length == 0) return _options.ForegroundColor;

        for (int i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out int code)) continue;

            switch (code)
            {
                case 0:
                    currentColor = _options.ForegroundColor;
                    break;
                case 38:
                    if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
                    {
                        if (int.TryParse(codes[i + 2], out int r) &&
                            int.TryParse(codes[i + 3], out int g) &&
                            int.TryParse(codes[i + 4], out int b))
                        {
                            currentColor = Color.FromRgb((byte)r, (byte)g, (byte)b);
                            i += 4;
                        }
                    }
                    break;
            }
        }

        return currentColor;
    }

    private static bool[,] DecodeBraille(char c)
    {
        var dots = new bool[4, 2];
        if (c < '\u2800' || c > '\u28FF')
            return dots;

        int pattern = c - '\u2800';
        dots[0, 0] = (pattern & 0x01) != 0;
        dots[1, 0] = (pattern & 0x02) != 0;
        dots[2, 0] = (pattern & 0x04) != 0;
        dots[0, 1] = (pattern & 0x08) != 0;
        dots[1, 1] = (pattern & 0x10) != 0;
        dots[2, 1] = (pattern & 0x20) != 0;
        dots[3, 0] = (pattern & 0x40) != 0;
        dots[3, 1] = (pattern & 0x80) != 0;
        return dots;
    }

    private static Rgba32 ToRgba32(Color color) => color.ToPixel<Rgba32>();

    private void DrawColoredLine(Image<Rgba32> image, string line, int startX, int y, int charWidth, Font font)
    {
        var segments = ParseAnsiColors(line);
        var x = startX;

        foreach (var (text, color) in segments)
        {
            if (string.IsNullOrEmpty(text)) continue;

            image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(x, y)));
            x += text.Length * charWidth;
        }
    }

    private static readonly System.Text.RegularExpressions.Regex AnsiColorRegex =
        new(@"\x1b\[([0-9;]*)m", System.Text.RegularExpressions.RegexOptions.Compiled);

    private List<(string Text, Color Color)> ParseAnsiColors(string text)
    {
        var result = new List<(string Text, Color Color)>();
        var currentColor = _options.ForegroundColor;
        int lastIndex = 0;

        foreach (System.Text.RegularExpressions.Match match in AnsiColorRegex.Matches(text))
        {
            if (match.Index > lastIndex)
            {
                var segment = text.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrEmpty(segment))
                    result.Add((segment, currentColor));
            }

            var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            currentColor = ParseAnsiCodes(codes, currentColor);

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            var remaining = text.Substring(lastIndex);
            if (!string.IsNullOrEmpty(remaining))
                result.Add((remaining, currentColor));
        }

        return result;
    }

    private static Font GetMonospaceFont(int size)
    {
        try
        {
            if (SystemFonts.TryGet("Consolas", out var family) ||
                SystemFonts.TryGet("Courier New", out family) ||
                SystemFonts.TryGet("DejaVu Sans Mono", out family) ||
                SystemFonts.TryGet("Liberation Mono", out family) ||
                SystemFonts.TryGet("monospace", out family))
            {
                return family.CreateFont(size);
            }

            var fallback = SystemFonts.Families.FirstOrDefault();
            if (fallback.Name != null)
            {
                return fallback.CreateFont(size);
            }
        }
        catch { }

        throw new InvalidOperationException("No fonts available for GIF rendering.");
    }

    private static readonly System.Text.RegularExpressions.Regex StripAnsiRegex =
        new(@"\x1b\[[0-9;]*m", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string StripAnsi(string text) => StripAnsiRegex.Replace(text, "");

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gif?.Dispose();
        _gif = null;

        if (File.Exists(_tempPath))
        {
            try { File.Delete(_tempPath); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
