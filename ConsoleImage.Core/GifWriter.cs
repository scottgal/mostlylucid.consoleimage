using System.Text.RegularExpressions;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

using ConsoleImage.Core.Subtitles;

namespace ConsoleImage.Core;

/// <summary>
///     Writes ASCII art frames to an animated GIF file.
/// </summary>
public class GifWriter : IDisposable
{
    // Pre-compiled regex for ColorBlock/Braille parsing
    private static readonly Regex AnsiOrCharRegex =
        new(@"\x1b\[([0-9;]*)m|([^\x1b])", RegexOptions.Compiled);

    // Pre-compiled regex for better performance
    private static readonly Regex AnsiColorRegex =
        new(@"\x1b\[([0-9;]*)m", RegexOptions.Compiled);

    // Pre-compiled regex for stripping ANSI
    private static readonly Regex StripAnsiRegex =
        new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

    private readonly List<(string Content, int DelayMs)> _frames = [];
    private readonly List<(Image<Rgba32> Image, int DelayMs)> _imageFrames = [];
    private readonly GifWriterOptions _options;
    private bool _disposed;
    private bool _useImageMode;

    public GifWriter(GifWriterOptions? options = null)
    {
        _options = options ?? new GifWriterOptions();
    }

    /// <summary>
    ///     Number of frames currently added.
    /// </summary>
    public int FrameCount => _useImageMode ? _imageFrames.Count : _frames.Count;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _frames.Clear();
        foreach (var (img, _) in _imageFrames)
            img.Dispose();
        _imageFrames.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Add a frame to the GIF.
    /// </summary>
    /// <param name="asciiContent">The ASCII art content (with or without ANSI codes)</param>
    /// <param name="delayMs">Frame delay in milliseconds</param>
    public void AddFrame(string asciiContent, int delayMs = 100)
    {
        _frames.Add((asciiContent, delayMs));
    }

    /// <summary>
    ///     Add a frame from an AsciiFrame.
    /// </summary>
    public void AddFrame(AsciiFrame frame, int delayMs = 100)
    {
        // Use ANSI string for colored output
        AddFrame(frame.ToAnsiString(), delayMs);
    }

    /// <summary>
    ///     Add a frame directly from an image (for high-fidelity modes like ColorBlocks/Braille).
    /// </summary>
    public void AddImageFrame(Image<Rgba32> image, int delayMs = 100)
    {
        _useImageMode = true;
        // Clone the image since the caller may dispose it
        _imageFrames.Add((image.Clone(), delayMs));
    }

    /// <summary>
    ///     Add a frame with subtitle text appended (for ASCII mode).
    /// </summary>
    /// <param name="asciiContent">The ASCII art content.</param>
    /// <param name="subtitle">Subtitle text to display below the content.</param>
    /// <param name="maxWidth">Maximum width for centering subtitles.</param>
    /// <param name="delayMs">Frame delay in milliseconds.</param>
    public void AddFrameWithSubtitle(string asciiContent, string? subtitle, int maxWidth, int delayMs = 100)
    {
        if (string.IsNullOrEmpty(subtitle))
        {
            AddFrame(asciiContent, delayMs);
            return;
        }

        // Split subtitle into lines and center each
        var subtitleLines = subtitle.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder(asciiContent);

        // Add separator line
        sb.AppendLine();

        foreach (var line in subtitleLines.Take(2)) // Max 2 lines
        {
            var trimmed = line.Trim();
            var padding = Math.Max(0, (maxWidth - trimmed.Length) / 2);
            sb.AppendLine(new string(' ', padding) + trimmed);
        }

        AddFrame(sb.ToString(), delayMs);
    }

    /// <summary>
    ///     Add an image frame with burned-in subtitle (for pixel-based modes).
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="subtitle">Subtitle text to burn into the image.</param>
    /// <param name="delayMs">Frame delay in milliseconds.</param>
    public void AddImageFrameWithSubtitle(Image<Rgba32> image, string? subtitle, int delayMs = 100)
    {
        if (string.IsNullOrEmpty(subtitle))
        {
            AddImageFrame(image, delayMs);
            return;
        }

        // Clone and burn subtitle into the image
        var withSubtitle = image.Clone();
        BurnSubtitleIntoImage(withSubtitle, subtitle);

        _useImageMode = true;
        _imageFrames.Add((withSubtitle, delayMs));
    }

    /// <summary>
    ///     Add an image frame with burned-in subtitle and/or status line (for pixel-based modes).
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="subtitle">Subtitle text to burn into the image (displayed above status).</param>
    /// <param name="statusLine">Status line text to burn at the very bottom.</param>
    /// <param name="delayMs">Frame delay in milliseconds.</param>
    public void AddImageFrameWithOverlays(Image<Rgba32> image, string? subtitle, string? statusLine, int delayMs = 100)
    {
        if (string.IsNullOrEmpty(subtitle) && string.IsNullOrEmpty(statusLine))
        {
            AddImageFrame(image, delayMs);
            return;
        }

        // Clone and burn overlays into the image
        var withOverlays = image.Clone();
        BurnOverlaysIntoImage(withOverlays, subtitle, statusLine);

        _useImageMode = true;
        _imageFrames.Add((withOverlays, delayMs));
    }

    /// <summary>
    ///     Burn subtitle text into an image at the bottom with outline effect.
    /// </summary>
    private void BurnSubtitleIntoImage(Image<Rgba32> image, string subtitle)
    {
        var lines = subtitle.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(2).ToArray();
        if (lines.Length == 0) return;

        var fontSize = Math.Max(12, image.Height / 15);
        Font font;

        try
        {
            font = GetSubtitleFont(fontSize);
        }
        catch
        {
            // If font loading fails, skip subtitle
            return;
        }

        var textColor = Color.White;
        var outlineColor = Color.Black;

        // Calculate positions - center text at bottom of image
        var bottomMargin = image.Height / 20;
        var lineHeight = fontSize + 4;
        var startY = image.Height - bottomMargin - (lines.Length * lineHeight);

        image.Mutate(ctx =>
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var text = lines[i].Trim();
                if (string.IsNullOrEmpty(text)) continue;

                // Measure text to center it
                var textOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Origin = new PointF(image.Width / 2f, startY + i * lineHeight)
                };

                // Draw outline (4 offsets)
                var outlineOffset = Math.Max(1, fontSize / 10);
                var offsets = new[] {
                    (-outlineOffset, 0), (outlineOffset, 0),
                    (0, -outlineOffset), (0, outlineOffset)
                };

                foreach (var (dx, dy) in offsets)
                {
                    var outlineOptions = new RichTextOptions(font)
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Origin = new PointF(image.Width / 2f + dx, startY + i * lineHeight + dy)
                    };
                    ctx.DrawText(outlineOptions, text, outlineColor);
                }

                // Draw main text
                ctx.DrawText(textOptions, text, textColor);
            }
        });
    }

    /// <summary>
    ///     Burn subtitle and status line text into an image with outline effect.
    ///     Subtitle appears above the status line at the bottom of the image.
    /// </summary>
    private void BurnOverlaysIntoImage(Image<Rgba32> image, string? subtitle, string? statusLine)
    {
        var subtitleLines = string.IsNullOrEmpty(subtitle)
            ? Array.Empty<string>()
            : subtitle.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(2).ToArray();
        var statusText = statusLine?.Trim();
        var hasStatus = !string.IsNullOrEmpty(statusText);

        if (subtitleLines.Length == 0 && !hasStatus) return;

        var fontSize = Math.Max(12, image.Height / 15);
        var statusFontSize = Math.Max(10, image.Height / 20);
        Font font;
        Font statusFont;

        try
        {
            font = GetSubtitleFont(fontSize);
            statusFont = GetSubtitleFont(statusFontSize);
        }
        catch
        {
            // If font loading fails, skip overlays
            return;
        }

        var textColor = Color.White;
        var outlineColor = Color.Black;
        var statusColor = Color.FromRgb(180, 180, 180); // Dim gray for status

        // Calculate positions from bottom up
        var bottomMargin = image.Height / 30;
        var lineHeight = fontSize + 4;
        var statusLineHeight = statusFontSize + 2;

        // Status line at very bottom
        var statusY = image.Height - bottomMargin - (hasStatus ? statusLineHeight : 0);

        // Subtitle above status
        var subtitleStartY = statusY - (subtitleLines.Length * lineHeight) - (hasStatus ? 4 : 0);

        image.Mutate(ctx =>
        {
            // Draw subtitle lines
            for (var i = 0; i < subtitleLines.Length; i++)
            {
                var text = subtitleLines[i].Trim();
                if (string.IsNullOrEmpty(text)) continue;

                var textOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Origin = new PointF(image.Width / 2f, subtitleStartY + i * lineHeight)
                };

                // Draw outline
                var outlineOffset = Math.Max(1, fontSize / 10);
                var offsets = new[] {
                    (-outlineOffset, 0), (outlineOffset, 0),
                    (0, -outlineOffset), (0, outlineOffset)
                };

                foreach (var (dx, dy) in offsets)
                {
                    var outlineOptions = new RichTextOptions(font)
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Origin = new PointF(image.Width / 2f + dx, subtitleStartY + i * lineHeight + dy)
                    };
                    ctx.DrawText(outlineOptions, text, outlineColor);
                }

                ctx.DrawText(textOptions, text, textColor);
            }

            // Draw status line
            if (hasStatus)
            {
                // Strip ANSI codes from status for image rendering
                var cleanStatus = StripAnsi(statusText!);

                var statusOptions = new RichTextOptions(statusFont)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Origin = new PointF(image.Width / 2f, statusY)
                };

                // Draw outline for status
                var statusOutlineOffset = Math.Max(1, statusFontSize / 12);
                var offsets = new[] {
                    (-statusOutlineOffset, 0), (statusOutlineOffset, 0),
                    (0, -statusOutlineOffset), (0, statusOutlineOffset)
                };

                foreach (var (dx, dy) in offsets)
                {
                    var outlineOptions = new RichTextOptions(statusFont)
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Origin = new PointF(image.Width / 2f + dx, statusY + dy)
                    };
                    ctx.DrawText(outlineOptions, cleanStatus, outlineColor);
                }

                ctx.DrawText(statusOptions, cleanStatus, statusColor);
            }
        });
    }

    /// <summary>
    ///     Get a font suitable for subtitle rendering.
    /// </summary>
    private static Font GetSubtitleFont(int size)
    {
        // Prefer sans-serif fonts for subtitle readability
        string[] preferredFonts =
        {
            "Arial",
            "Helvetica",
            "Segoe UI",
            "DejaVu Sans",
            "Liberation Sans",
            "Noto Sans",
            "Verdana",
            "Tahoma"
        };

        try
        {
            foreach (var fontName in preferredFonts)
                if (SystemFonts.TryGet(fontName, out var family))
                    return family.CreateFont(size, FontStyle.Bold);

            // Fall back to any available font
            var fallback = SystemFonts.Families.FirstOrDefault();
            if (fallback.Name != null)
                return fallback.CreateFont(size, FontStyle.Bold);
        }
        catch
        {
            // Font loading failed
        }

        throw new InvalidOperationException("No fonts available for subtitle rendering.");
    }

    /// <summary>
    ///     Add a ColorBlock frame - renders as pixels (2 pixels per character cell vertically).
    /// </summary>
    public void AddColorBlockFrame(ColorBlockFrame frame, int delayMs = 100)
    {
        _useImageMode = true;
        var image = RenderColorBlocksToImage(frame);
        _imageFrames.Add((image, delayMs));
    }

    /// <summary>
    ///     Add a Braille frame - renders as pixels (2x4 dots per character cell).
    /// </summary>
    public void AddBrailleFrame(BrailleFrame frame, int delayMs = 100)
    {
        _useImageMode = true;
        var image = RenderBrailleToImage(frame);
        _imageFrames.Add((image, delayMs));
    }

    /// <summary>
    ///     Static helper to render a BrailleFrame to an image (for external use).
    /// </summary>
    /// <param name="frame">The braille frame to render.</param>
    /// <param name="scale">Scale factor for output (default: 1.0).</param>
    /// <param name="backgroundColor">Background color (default: black).</param>
    /// <returns>The rendered image.</returns>
    public static Image<Rgba32> RenderBrailleFrameToImage(BrailleFrame frame, float scale = 1.0f, Color? backgroundColor = null)
    {
        var lines = frame.Content.Split('\n');
        var charWidth = lines.Max(l => StripAnsi(l).Length);
        var charHeight = lines.Length;

        // Braille is 2x4 dots per character
        var pixelWidth = charWidth * 2;
        var pixelHeight = charHeight * 4;

        var dotSize = Math.Max(1, (int)(scale * 2));
        var image = new Image<Rgba32>(pixelWidth * dotSize, pixelHeight * dotSize);
        image.Mutate(ctx => ctx.Fill(backgroundColor ?? Color.Black));

        for (var lineY = 0; lineY < lines.Length; lineY++)
        {
            var cells = ParseBrailleLineStatic(lines[lineY]);
            for (var x = 0; x < cells.Count; x++)
            {
                var (brailleChar, fgColor) = cells[x];
                var dots = DecodeBraille(brailleChar);

                for (var dy = 0; dy < 4; dy++)
                for (var dx = 0; dx < 2; dx++)
                    if (dots[dy, dx])
                    {
                        var px = (x * 2 + dx) * dotSize;
                        var py = (lineY * 4 + dy) * dotSize;
                        for (var iy = 0; iy < dotSize; iy++)
                        for (var ix = 0; ix < dotSize; ix++)
                            if (px + ix < image.Width && py + iy < image.Height)
                                image[px + ix, py + iy] = ToRgba32(fgColor);
                    }
            }
        }

        return image;
    }

    /// <summary>
    ///     Static helper to render a ColorBlockFrame to an image (for external use).
    /// </summary>
    /// <param name="frame">The color block frame to render.</param>
    /// <param name="scale">Scale factor for output (default: 1.0).</param>
    /// <param name="backgroundColor">Background color (default: black).</param>
    /// <returns>The rendered image.</returns>
    public static Image<Rgba32> RenderColorBlockFrameToImage(ColorBlockFrame frame, float scale = 1.0f, Color? backgroundColor = null)
    {
        var lines = frame.Content.Split('\n');
        var width = lines.Max(l => StripAnsi(l).Length);
        var height = lines.Length * 2; // 2 pixels per row

        var pixelSize = Math.Max(1, (int)(scale * 4));
        var image = new Image<Rgba32>(width * pixelSize, height * pixelSize);
        image.Mutate(ctx => ctx.Fill(backgroundColor ?? Color.Black));

        for (var lineY = 0; lineY < lines.Length; lineY++)
        {
            var cells = ParseColorBlockLineStatic(lines[lineY]);
            for (var x = 0; x < cells.Count; x++)
            {
                var (topColor, bottomColor) = cells[x];

                // Draw top pixel
                for (var py = 0; py < pixelSize; py++)
                for (var px = 0; px < pixelSize; px++)
                    image[x * pixelSize + px, lineY * 2 * pixelSize + py] = ToRgba32(topColor);

                // Draw bottom pixel
                for (var py = 0; py < pixelSize; py++)
                for (var px = 0; px < pixelSize; px++)
                    image[x * pixelSize + px, (lineY * 2 + 1) * pixelSize + py] = ToRgba32(bottomColor);
            }
        }

        return image;
    }

    private static List<(char BrailleChar, Color FgColor)> ParseBrailleLineStatic(string line)
    {
        var result = new List<(char, Color)>();
        var currentFg = Color.White;

        foreach (Match match in AnsiOrCharRegex.Matches(line))
            if (match.Groups[1].Success)
            {
                var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                currentFg = ParseAnsiCodesStatic(codes, currentFg);
            }
            else if (match.Groups[2].Success)
            {
                var c = match.Groups[2].Value[0];
                result.Add((c, currentFg));
            }

        return result;
    }

    private static List<(Color Top, Color Bottom)> ParseColorBlockLineStatic(string line)
    {
        var result = new List<(Color, Color)>();
        var currentFg = Color.White;
        var currentBg = Color.Black;

        foreach (Match match in AnsiOrCharRegex.Matches(line))
            if (match.Groups[1].Success)
            {
                // ANSI escape - parse colors
                var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                (currentFg, currentBg) = ParseColorBlockCodesStatic(codes, currentFg, currentBg);
            }
            else if (match.Groups[2].Success)
            {
                // Character - determine top/bottom colors based on block type
                var c = match.Groups[2].Value[0];
                var (top, bottom) = c switch
                {
                    '▀' => (currentFg, currentBg), // Upper half block
                    '▄' => (currentBg, currentFg), // Lower half block
                    '█' => (currentFg, currentFg), // Full block
                    ' ' => (currentBg, currentBg), // Space
                    _ => (currentFg, currentBg)
                };
                result.Add((top, bottom));
            }

        return result;
    }

    private static (Color Fg, Color Bg) ParseColorBlockCodesStatic(string[] codes, Color currentFg, Color currentBg)
    {
        for (var i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out var code)) continue;

            switch (code)
            {
                case 0:
                    currentFg = Color.White;
                    currentBg = Color.Black;
                    break;
                case 38: // Foreground
                    if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
                        if (int.TryParse(codes[i + 2], out var r) &&
                            int.TryParse(codes[i + 3], out var g) &&
                            int.TryParse(codes[i + 4], out var b))
                        {
                            currentFg = Color.FromRgb((byte)r, (byte)g, (byte)b);
                            i += 4;
                        }

                    break;
                case 48: // Background
                    if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
                        if (int.TryParse(codes[i + 2], out var r) &&
                            int.TryParse(codes[i + 3], out var g) &&
                            int.TryParse(codes[i + 4], out var b))
                        {
                            currentBg = Color.FromRgb((byte)r, (byte)g, (byte)b);
                            i += 4;
                        }

                    break;
            }
        }

        return (currentFg, currentBg);
    }

    private static Color ParseAnsiCodesStatic(string[] codes, Color currentColor)
    {
        if (codes.Length == 0) return Color.White;

        for (var i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out var code)) continue;

            switch (code)
            {
                case 0:
                    currentColor = Color.Black; // Reset to background
                    break;
                case 38: // 24-bit or 256 foreground
                    if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
                    {
                        if (int.TryParse(codes[i + 2], out var r) &&
                            int.TryParse(codes[i + 3], out var g) &&
                            int.TryParse(codes[i + 4], out var b))
                        {
                            currentColor = Color.FromRgb((byte)r, (byte)g, (byte)b);
                            i += 4;
                        }
                    }
                    else if (i + 1 < codes.Length && codes[i + 1] == "5" && i + 2 < codes.Length)
                    {
                        if (int.TryParse(codes[i + 2], out var colorIndex))
                        {
                            currentColor = Get256Color(colorIndex);
                            i += 2;
                        }
                    }

                    break;
                case >= 30 and <= 37:
                    currentColor = GetStandardColor(code - 30);
                    break;
                case >= 90 and <= 97:
                    currentColor = GetBrightColor(code - 90);
                    break;
            }
        }

        return currentColor;
    }

    /// <summary>
    ///     Add a Matrix frame - renders as text with fonts by default, or as pixel blocks if useBlockMode is true.
    /// </summary>
    /// <param name="frame">The Matrix frame to add</param>
    /// <param name="delayMs">Frame delay in milliseconds</param>
    /// <param name="useBlockMode">If true, render as pixel blocks instead of text</param>
    public void AddMatrixFrame(MatrixFrame frame, int delayMs = 100, bool useBlockMode = false)
    {
        _useImageMode = true;
        var image = useBlockMode ? RenderMatrixToImageAsBlocks(frame) : RenderMatrixToImage(frame);
        _imageFrames.Add((image, delayMs));
    }

    /// <summary>
    ///     Render ColorBlockFrame to an image (2 pixels per character cell vertically).
    /// </summary>
    private Image<Rgba32> RenderColorBlocksToImage(ColorBlockFrame frame)
        => RenderColorBlockFrameToImage(frame, _options.Scale, _options.BackgroundColor);

    /// <summary>
    ///     Render BrailleFrame to an image (2x4 dots per character cell).
    /// </summary>
    private Image<Rgba32> RenderBrailleToImage(BrailleFrame frame)
        => RenderBrailleFrameToImage(frame, _options.Scale, _options.BackgroundColor);

    /// <summary>
    ///     Render MatrixFrame to an image with actual text characters (like ASCII mode).
    /// </summary>
    private Image<Rgba32> RenderMatrixToImage(MatrixFrame frame)
    {
        var lines = frame.Content.Split('\n');
        var charWidth = lines.Max(l => StripAnsi(l).Length);
        var charHeight = lines.Length;

        // Use text rendering like ASCII mode
        var scaledFontSize = (int)(_options.FontSize * _options.Scale);
        var charPixelWidth = (int)(_options.FontSize * 6 / 10 * _options.Scale);
        var charPixelHeight = (int)((_options.FontSize + 2) * _options.Scale);
        var scaledPadding = (int)(_options.Padding * _options.Scale);

        var imageWidth = charWidth * charPixelWidth + scaledPadding * 2;
        var imageHeight = charHeight * charPixelHeight + scaledPadding * 2;

        var image = new Image<Rgba32>(imageWidth, imageHeight);
        image.Mutate(ctx => ctx.Fill(_options.BackgroundColor));

        // Get a font that supports the Matrix characters (katakana, etc.)
        var font = GetMatrixFont(Math.Max(6, scaledFontSize));

        for (var lineY = 0; lineY < lines.Length; lineY++)
        {
            var y = scaledPadding + lineY * charPixelHeight;
            DrawColoredLine(image, lines[lineY], scaledPadding, y, charPixelWidth, font);
        }

        return image;
    }

    /// <summary>
    ///     Render MatrixFrame to an image as pixel blocks (for block mode Matrix).
    /// </summary>
    private Image<Rgba32> RenderMatrixToImageAsBlocks(MatrixFrame frame)
    {
        var lines = frame.Content.Split('\n');
        var charWidth = lines.Max(l => StripAnsi(l).Length);
        var charHeight = lines.Length;

        // Matrix uses 1x1 character cells, but we scale them up for visibility
        var pixelSize = Math.Max(1, (int)(_options.Scale * 4));
        var image = new Image<Rgba32>(charWidth * pixelSize, charHeight * pixelSize);
        image.Mutate(ctx => ctx.Fill(_options.BackgroundColor));

        for (var lineY = 0; lineY < lines.Length; lineY++)
        {
            var cells = ParseMatrixLine(lines[lineY]);
            for (var x = 0; x < cells.Count; x++)
            {
                var (character, fgColor) = cells[x];

                // Skip space characters (background)
                if (character == ' ')
                    continue;

                // Draw filled pixel for this character
                var px = x * pixelSize;
                var py = lineY * pixelSize;
                var color = ToRgba32(fgColor);

                for (var iy = 0; iy < pixelSize; iy++)
                for (var ix = 0; ix < pixelSize; ix++)
                    if (px + ix < image.Width && py + iy < image.Height)
                        image[px + ix, py + iy] = color;
            }
        }

        return image;
    }

    /// <summary>
    ///     Parse a line of Matrix output to extract characters and their colors.
    /// </summary>
    private List<(char Character, Color FgColor)> ParseMatrixLine(string line)
    {
        var result = new List<(char, Color)>();
        var currentFg = _options.ForegroundColor;

        foreach (Match match in AnsiOrCharRegex.Matches(line))
            if (match.Groups[1].Success)
            {
                var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                currentFg = ParseAnsiCodes(codes, currentFg);
            }
            else if (match.Groups[2].Success)
            {
                var c = match.Groups[2].Value[0];
                result.Add((c, currentFg));
            }

        return result;
    }

    /// <summary>
    ///     Parse a line of ColorBlock output to extract top/bottom colors for each cell.
    /// </summary>
    private List<(Color Top, Color Bottom)> ParseColorBlockLine(string line)
    {
        var result = new List<(Color, Color)>();
        var currentFg = _options.ForegroundColor;
        var currentBg = _options.BackgroundColor;

        foreach (Match match in AnsiOrCharRegex.Matches(line))
            if (match.Groups[1].Success)
            {
                // ANSI escape - parse colors
                var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                (currentFg, currentBg) = ParseColorBlockCodes(codes, currentFg, currentBg);
            }
            else if (match.Groups[2].Success)
            {
                // Character - determine top/bottom colors based on block type
                var c = match.Groups[2].Value[0];
                var (top, bottom) = c switch
                {
                    '▀' => (currentFg, currentBg), // Upper half block
                    '▄' => (currentBg, currentFg), // Lower half block
                    '█' => (currentFg, currentFg), // Full block
                    ' ' => (currentBg, currentBg), // Space
                    _ => (currentFg, currentBg)
                };
                result.Add((top, bottom));
            }

        return result;
    }

    /// <summary>
    ///     Parse a line of Braille output to extract braille characters and their colors.
    /// </summary>
    private List<(char BrailleChar, Color FgColor)> ParseBrailleLine(string line)
    {
        var result = new List<(char, Color)>();
        var currentFg = _options.ForegroundColor;

        foreach (Match match in AnsiOrCharRegex.Matches(line))
            if (match.Groups[1].Success)
            {
                var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                currentFg = ParseAnsiCodes(codes, currentFg);
            }
            else if (match.Groups[2].Success)
            {
                var c = match.Groups[2].Value[0];
                result.Add((c, currentFg));
            }

        return result;
    }

    /// <summary>
    ///     Parse ANSI codes for ColorBlock mode (handles both fg and bg).
    /// </summary>
    private (Color Fg, Color Bg) ParseColorBlockCodes(string[] codes, Color currentFg, Color currentBg)
    {
        for (var i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out var code)) continue;

            switch (code)
            {
                case 0:
                    currentFg = _options.ForegroundColor;
                    currentBg = _options.BackgroundColor;
                    break;
                case 38: // Foreground
                    if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
                        if (int.TryParse(codes[i + 2], out var r) &&
                            int.TryParse(codes[i + 3], out var g) &&
                            int.TryParse(codes[i + 4], out var b))
                        {
                            currentFg = Color.FromRgb((byte)r, (byte)g, (byte)b);
                            i += 4;
                        }

                    break;
                case 48: // Background
                    if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
                        if (int.TryParse(codes[i + 2], out var r) &&
                            int.TryParse(codes[i + 3], out var g) &&
                            int.TryParse(codes[i + 4], out var b))
                        {
                            currentBg = Color.FromRgb((byte)r, (byte)g, (byte)b);
                            i += 4;
                        }

                    break;
            }
        }

        return (currentFg, currentBg);
    }

    /// <summary>
    ///     Decode a Unicode braille character to a 4x2 dot matrix.
    /// </summary>
    private static bool[,] DecodeBraille(char c)
    {
        var dots = new bool[4, 2];
        if (c < '\u2800' || c > '\u28FF')
            return dots;

        var pattern = c - '\u2800';
        // Braille pattern mapping: bits map to positions
        // Bit 0: (0,0), Bit 1: (1,0), Bit 2: (2,0), Bit 3: (0,1)
        // Bit 4: (1,1), Bit 5: (2,1), Bit 6: (3,0), Bit 7: (3,1)
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

    private static Rgba32 ToRgba32(Color color)
    {
        var pixel = color.ToPixel<Rgba32>();
        return pixel;
    }

    /// <summary>
    ///     Save all frames as an animated GIF.
    /// </summary>
    public void Save(string outputPath)
    {
        if (_useImageMode)
        {
            SaveImageFrames(outputPath);
            return;
        }

        if (_frames.Count == 0)
            throw new InvalidOperationException("No frames to save");

        // Apply frame limits
        var framesToProcess = _frames.AsEnumerable();
        if (_options.MaxFrames.HasValue && _options.MaxFrames.Value > 0)
            framesToProcess = framesToProcess.Take(_options.MaxFrames.Value);
        if (_options.MaxLengthSeconds.HasValue && _options.MaxLengthSeconds.Value > 0)
        {
            double totalMs = 0;
            var limitedFrames = new List<(string Content, int DelayMs)>();
            foreach (var frame in framesToProcess)
            {
                if (totalMs / 1000.0 >= _options.MaxLengthSeconds.Value)
                    break;
                limitedFrames.Add(frame);
                totalMs += frame.DelayMs;
            }

            framesToProcess = limitedFrames;
        }

        var finalFrames = framesToProcess.ToList();

        if (finalFrames.Count == 0)
            throw new InvalidOperationException("No frames to save after applying limits");

        // Parse first frame to determine dimensions
        var firstLines = finalFrames[0].Content.Split('\n');
        var maxWidth = firstLines.Max(l => StripAnsi(l).Length);
        var lineCount = firstLines.Length;

        // Calculate image dimensions with scale
        var charWidth = (int)(_options.FontSize * 6 / 10 * _options.Scale);
        var charHeight = (int)((_options.FontSize + 2) * _options.Scale);
        var scaledPadding = (int)(_options.Padding * _options.Scale);
        var imageWidth = maxWidth * charWidth + scaledPadding * 2;
        var imageHeight = lineCount * charHeight + scaledPadding * 2;

        // Create the GIF
        using var gif = new Image<Rgba32>(imageWidth, imageHeight);

        // Get font (scaled) - detect Japanese for Matrix mode
        var scaledFontSize = (int)(_options.FontSize * _options.Scale);
        var hasJapanese = ContainsJapaneseCharacters(finalFrames[0].Content);
        var font = hasJapanese
            ? GetMatrixFont(Math.Max(6, scaledFontSize))
            : GetMonospaceFont(Math.Max(6, scaledFontSize));

        for (var i = 0; i < finalFrames.Count; i++)
        {
            var (content, delayMs) = finalFrames[i];
            var lines = content.Split('\n');

            // Create frame image
            using var frameImage = new Image<Rgba32>(imageWidth, imageHeight);

            // Fill background
            frameImage.Mutate(ctx => ctx.Fill(_options.BackgroundColor));

            // Draw text with color support
            var y = scaledPadding;
            foreach (var line in lines)
            {
                DrawColoredLine(frameImage, line, scaledPadding, y, charWidth, font);
                y += charHeight;
            }

            // Add frame to GIF
            if (i == 0)
            {
                // First frame - replace root frame pixels
                frameImage.ProcessPixelRows(gif, (sourceAccessor, targetAccessor) =>
                {
                    for (var row = 0; row < sourceAccessor.Height; row++)
                    {
                        var sourceRow = sourceAccessor.GetRowSpan(row);
                        var targetRow = targetAccessor.GetRowSpan(row);
                        sourceRow.CopyTo(targetRow);
                    }
                });
                gif.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;
            }
            else
            {
                // Add subsequent frames
                var frame = gif.Frames.AddFrame(frameImage.Frames.RootFrame);
                frame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;
            }
        }

        // Configure GIF metadata
        var gifMetadata = gif.Metadata.GetGifMetadata();
        gifMetadata.RepeatCount = (ushort)_options.LoopCount;

        // Save with optimized encoding
        var encoder = new GifEncoder
        {
            ColorTableMode = GifColorTableMode.Local
        };

        // Use quantization to reduce file size
        if (_options.MaxColors < 256)
            gif.Mutate(ctx => ctx.Quantize(new OctreeQuantizer(
                new QuantizerOptions
                {
                    MaxColors = Math.Clamp(_options.MaxColors, 2, 256)
                })));

        gif.SaveAsGif(outputPath, encoder);
    }

    /// <summary>
    ///     Draw a line with ANSI color support.
    /// </summary>
    private void DrawColoredLine(Image<Rgba32> image, string line, int startX, int y, int charWidth, Font font)
    {
        var segments = ParseAnsiColors(line);
        var x = startX;

        foreach (var (text, color) in segments)
        {
            if (string.IsNullOrEmpty(text)) continue;

            // Draw each character individually for proper monospace positioning
            // This ensures Unicode characters (like katakana) align correctly
            foreach (var c in text)
            {
                var charStr = c.ToString();
                image.Mutate(ctx => ctx.DrawText(
                    charStr,
                    font,
                    color,
                    new PointF(x, y)));
                x += charWidth;
            }
        }
    }

    /// <summary>
    ///     Parse ANSI color codes and return text segments with their colors.
    /// </summary>
    private List<(string Text, Color Color)> ParseAnsiColors(string text)
    {
        var result = new List<(string Text, Color Color)>();
        var currentColor = _options.ForegroundColor;
        var lastIndex = 0;

        foreach (Match match in AnsiColorRegex.Matches(text))
        {
            // Add text before this escape sequence
            if (match.Index > lastIndex)
            {
                var segment = text.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrEmpty(segment))
                    result.Add((segment, currentColor));
            }

            // Parse the color code
            var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            currentColor = ParseAnsiCodes(codes, currentColor);

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            var remaining = text.Substring(lastIndex);
            if (!string.IsNullOrEmpty(remaining))
                result.Add((remaining, currentColor));
        }

        return result;
    }

    /// <summary>
    ///     Parse ANSI SGR codes and return the resulting color.
    /// </summary>
    private Color ParseAnsiCodes(string[] codes, Color currentColor)
    {
        if (codes.Length == 0) return _options.ForegroundColor;

        for (var i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out var code)) continue;

            switch (code)
            {
                case 0: // Reset - use background color so "skipped" characters blend in
                    currentColor = _options.BackgroundColor;
                    break;
                case 38: // 24-bit or 256 foreground
                    if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
                    {
                        // 24-bit: \x1b[38;2;R;G;Bm
                        if (int.TryParse(codes[i + 2], out var r) &&
                            int.TryParse(codes[i + 3], out var g) &&
                            int.TryParse(codes[i + 4], out var b))
                        {
                            currentColor = Color.FromRgb((byte)r, (byte)g, (byte)b);
                            i += 4;
                        }
                    }
                    else if (i + 1 < codes.Length && codes[i + 1] == "5" && i + 2 < codes.Length)
                    {
                        // 256-color: \x1b[38;5;Nm
                        if (int.TryParse(codes[i + 2], out var colorIndex))
                        {
                            currentColor = Get256Color(colorIndex);
                            i += 2;
                        }
                    }

                    break;
                case >= 30 and <= 37: // Standard foreground colors
                    currentColor = GetStandardColor(code - 30);
                    break;
                case >= 90 and <= 97: // Bright foreground colors
                    currentColor = GetBrightColor(code - 90);
                    break;
            }
        }

        return currentColor;
    }

    private static Color GetStandardColor(int index)
    {
        return index switch
        {
            0 => Color.FromRgb(0, 0, 0), // Black
            1 => Color.FromRgb(170, 0, 0), // Red
            2 => Color.FromRgb(0, 170, 0), // Green
            3 => Color.FromRgb(170, 85, 0), // Yellow/Brown
            4 => Color.FromRgb(0, 0, 170), // Blue
            5 => Color.FromRgb(170, 0, 170), // Magenta
            6 => Color.FromRgb(0, 170, 170), // Cyan
            7 => Color.FromRgb(170, 170, 170), // White
            _ => Color.White
        };
    }

    private static Color GetBrightColor(int index)
    {
        return index switch
        {
            0 => Color.FromRgb(85, 85, 85), // Bright Black
            1 => Color.FromRgb(255, 85, 85), // Bright Red
            2 => Color.FromRgb(85, 255, 85), // Bright Green
            3 => Color.FromRgb(255, 255, 85), // Bright Yellow
            4 => Color.FromRgb(85, 85, 255), // Bright Blue
            5 => Color.FromRgb(255, 85, 255), // Bright Magenta
            6 => Color.FromRgb(85, 255, 255), // Bright Cyan
            7 => Color.FromRgb(255, 255, 255), // Bright White
            _ => Color.White
        };
    }

    private static Color Get256Color(int index)
    {
        if (index < 16) return index < 8 ? GetStandardColor(index) : GetBrightColor(index - 8);

        if (index < 232)
        {
            // 216 color cube (6x6x6)
            index -= 16;
            var r = index / 36 * 51;
            var g = index / 6 % 6 * 51;
            var b = index % 6 * 51;
            return Color.FromRgb((byte)r, (byte)g, (byte)b);
        }

        // 24 grayscale
        var gray = (index - 232) * 10 + 8;
        return Color.FromRgb((byte)gray, (byte)gray, (byte)gray);
    }

    /// <summary>
    ///     Save image frames directly to GIF.
    /// </summary>
    private void SaveImageFrames(string outputPath)
    {
        if (_imageFrames.Count == 0)
            throw new InvalidOperationException("No image frames to save");

        // Apply frame limits
        var framesToProcess = _imageFrames.AsEnumerable();
        if (_options.MaxFrames.HasValue && _options.MaxFrames.Value > 0)
            framesToProcess = framesToProcess.Take(_options.MaxFrames.Value);

        var finalFrames = framesToProcess.ToList();
        if (finalFrames.Count == 0)
            throw new InvalidOperationException("No frames after limits");

        // Get dimensions from first frame
        var (firstImg, _) = finalFrames[0];
        var width = firstImg.Width;
        var height = firstImg.Height;

        using var gif = new Image<Rgba32>(width, height);

        for (var i = 0; i < finalFrames.Count; i++)
        {
            var (frameImg, delayMs) = finalFrames[i];

            if (i == 0)
            {
                frameImg.ProcessPixelRows(gif, (src, tgt) =>
                {
                    for (var y = 0; y < src.Height; y++) src.GetRowSpan(y).CopyTo(tgt.GetRowSpan(y));
                });
                gif.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;
            }
            else
            {
                // Resize if needed
                var resized = frameImg;
                if (frameImg.Width != width || frameImg.Height != height)
                {
                    resized = frameImg.Clone();
                    resized.Mutate(x => x.Resize(width, height));
                }

                var frame = gif.Frames.AddFrame(resized.Frames.RootFrame);
                frame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;

                if (resized != frameImg)
                    resized.Dispose();
            }
        }

        var gifMetadata = gif.Metadata.GetGifMetadata();
        gifMetadata.RepeatCount = (ushort)_options.LoopCount;

        var encoder = new GifEncoder { ColorTableMode = GifColorTableMode.Local };

        if (_options.MaxColors < 256)
            gif.Mutate(ctx => ctx.Quantize(new OctreeQuantizer(
                new QuantizerOptions
                {
                    MaxColors = Math.Clamp(_options.MaxColors, 2, 256)
                })));

        gif.SaveAsGif(outputPath, encoder);
    }

    /// <summary>
    ///     Save all frames as an animated GIF asynchronously.
    /// </summary>
    public async Task SaveAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Save(outputPath), cancellationToken);
    }

    /// <summary>
    ///     Save all frames as an animated GIF with progress reporting.
    /// </summary>
    /// <param name="outputPath">Output file path</param>
    /// <param name="progress">Progress callback (0.0 to 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SaveAsync(string outputPath, IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() => SaveWithProgress(outputPath, progress), cancellationToken);
    }

    private void SaveWithProgress(string outputPath, IProgress<double>? progress)
    {
        if (_useImageMode)
        {
            SaveImageFramesWithProgress(outputPath, progress);
            return;
        }

        if (_frames.Count == 0)
            throw new InvalidOperationException("No frames to save");

        // Apply frame limits
        var framesToProcess = _frames.AsEnumerable();
        if (_options.MaxFrames.HasValue && _options.MaxFrames.Value > 0)
            framesToProcess = framesToProcess.Take(_options.MaxFrames.Value);
        if (_options.MaxLengthSeconds.HasValue && _options.MaxLengthSeconds.Value > 0)
        {
            double totalMs = 0;
            var limitedFrames = new List<(string Content, int DelayMs)>();
            foreach (var frame in framesToProcess)
            {
                if (totalMs / 1000.0 >= _options.MaxLengthSeconds.Value) break;
                limitedFrames.Add(frame);
                totalMs += frame.DelayMs;
            }

            framesToProcess = limitedFrames;
        }

        var finalFrames = framesToProcess.ToList();

        if (finalFrames.Count == 0)
            throw new InvalidOperationException("No frames to save after applying limits");

        var firstLines = finalFrames[0].Content.Split('\n');
        var maxWidth = firstLines.Max(l => StripAnsi(l).Length);
        var lineCount = firstLines.Length;

        var charWidth = (int)(_options.FontSize * 6 / 10 * _options.Scale);
        var charHeight = (int)((_options.FontSize + 2) * _options.Scale);
        var scaledPadding = (int)(_options.Padding * _options.Scale);
        var imageWidth = maxWidth * charWidth + scaledPadding * 2;
        var imageHeight = lineCount * charHeight + scaledPadding * 2;

        using var gif = new Image<Rgba32>(imageWidth, imageHeight);
        var scaledFontSize = (int)(_options.FontSize * _options.Scale);

        // Detect if content contains Japanese/katakana characters and use appropriate font
        var hasJapanese = ContainsJapaneseCharacters(finalFrames[0].Content);
        var font = hasJapanese
            ? GetMatrixFont(Math.Max(6, scaledFontSize))
            : GetMonospaceFont(Math.Max(6, scaledFontSize));

        var totalSteps = finalFrames.Count + 2; // frames + quantize + save

        for (var i = 0; i < finalFrames.Count; i++)
        {
            var (content, delayMs) = finalFrames[i];
            var lines = content.Split('\n');

            using var frameImage = new Image<Rgba32>(imageWidth, imageHeight);
            frameImage.Mutate(ctx => ctx.Fill(_options.BackgroundColor));

            var y = scaledPadding;
            foreach (var line in lines)
            {
                DrawColoredLine(frameImage, line, scaledPadding, y, charWidth, font);
                y += charHeight;
            }

            if (i == 0)
            {
                frameImage.ProcessPixelRows(gif, (sourceAccessor, targetAccessor) =>
                {
                    for (var row = 0; row < sourceAccessor.Height; row++)
                        sourceAccessor.GetRowSpan(row).CopyTo(targetAccessor.GetRowSpan(row));
                });
                gif.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;
            }
            else
            {
                var frame = gif.Frames.AddFrame(frameImage.Frames.RootFrame);
                frame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;
            }

            progress?.Report((double)(i + 1) / totalSteps);
        }

        var gifMetadata = gif.Metadata.GetGifMetadata();
        gifMetadata.RepeatCount = (ushort)_options.LoopCount;

        if (_options.MaxColors < 256)
            gif.Mutate(ctx => ctx.Quantize(new OctreeQuantizer(
                new QuantizerOptions
                {
                    MaxColors = Math.Clamp(_options.MaxColors, 2, 256)
                })));
        progress?.Report((double)(finalFrames.Count + 1) / totalSteps);

        var encoder = new GifEncoder { ColorTableMode = GifColorTableMode.Local };
        gif.SaveAsGif(outputPath, encoder);
        progress?.Report(1.0);
    }

    private void SaveImageFramesWithProgress(string outputPath, IProgress<double>? progress)
    {
        if (_imageFrames.Count == 0)
            throw new InvalidOperationException("No image frames to save");

        var framesToProcess = _imageFrames.AsEnumerable();
        if (_options.MaxFrames.HasValue && _options.MaxFrames.Value > 0)
            framesToProcess = framesToProcess.Take(_options.MaxFrames.Value);

        var finalFrames = framesToProcess.ToList();
        if (finalFrames.Count == 0)
            throw new InvalidOperationException("No frames after limits");

        var (firstImg, _) = finalFrames[0];
        var width = firstImg.Width;
        var height = firstImg.Height;

        using var gif = new Image<Rgba32>(width, height);
        var totalSteps = finalFrames.Count + 2;

        for (var i = 0; i < finalFrames.Count; i++)
        {
            var (frameImg, delayMs) = finalFrames[i];

            if (i == 0)
            {
                frameImg.ProcessPixelRows(gif, (src, tgt) =>
                {
                    for (var y = 0; y < src.Height; y++)
                        src.GetRowSpan(y).CopyTo(tgt.GetRowSpan(y));
                });
                gif.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;
            }
            else
            {
                var resized = frameImg;
                if (frameImg.Width != width || frameImg.Height != height)
                {
                    resized = frameImg.Clone();
                    resized.Mutate(x => x.Resize(width, height));
                }

                var frame = gif.Frames.AddFrame(resized.Frames.RootFrame);
                frame.Metadata.GetGifMetadata().FrameDelay = delayMs / 10;

                if (resized != frameImg) resized.Dispose();
            }

            progress?.Report((double)(i + 1) / totalSteps);
        }

        var gifMetadata = gif.Metadata.GetGifMetadata();
        gifMetadata.RepeatCount = (ushort)_options.LoopCount;

        if (_options.MaxColors < 256)
            gif.Mutate(ctx => ctx.Quantize(new OctreeQuantizer(
                new QuantizerOptions
                {
                    MaxColors = Math.Clamp(_options.MaxColors, 2, 256)
                })));
        progress?.Report((double)(finalFrames.Count + 1) / totalSteps);

        var encoder = new GifEncoder { ColorTableMode = GifColorTableMode.Local };
        gif.SaveAsGif(outputPath, encoder);
        progress?.Report(1.0);
    }

    private static Font GetMonospaceFont(int size)
    {
        // Try to get a monospace font, fall back to system default
        try
        {
            if (SystemFonts.TryGet("Consolas", out var family) ||
                SystemFonts.TryGet("Courier New", out family) ||
                SystemFonts.TryGet("DejaVu Sans Mono", out family) ||
                SystemFonts.TryGet("Liberation Mono", out family) ||
                SystemFonts.TryGet("monospace", out family))
                return family.CreateFont(size);

            // Fall back to first available font
            var fallback = SystemFonts.Families.FirstOrDefault();
            if (fallback.Name != null) return fallback.CreateFont(size);
        }
        catch
        {
            // Font loading failed
        }

        throw new InvalidOperationException(
            "No fonts available for GIF rendering. Install a monospace font like Consolas or Courier New.");
    }

    /// <summary>
    ///     Get a font that supports Matrix characters (katakana, symbols).
    ///     Tries Japanese-supporting fonts first, then falls back to monospace.
    /// </summary>
    private static Font GetMatrixFont(int size)
    {
        // Try fonts that support Japanese/katakana characters
        // Windows fonts
        string[] japaneseFonts =
        [
            "MS Gothic", // Windows - good katakana support
            "Yu Gothic", // Windows 10+
            "Yu Gothic UI", // Windows 10+
            "Meiryo", // Windows Vista+
            "MS Mincho", // Windows
            // Cross-platform fonts
            "Noto Sans Mono CJK JP", // Google's Noto fonts
            "Noto Sans Mono CJK",
            "Noto Sans JP",
            "Source Han Sans", // Adobe's open source CJK font
            // macOS fonts
            "Hiragino Kaku Gothic Pro",
            "Hiragino Sans",
            "Osaka",
            // Linux fonts
            "IPAGothic",
            "TakaoGothic",
            "VL Gothic",
            // Fallback monospace fonts (may have limited katakana)
            "Consolas",
            "Courier New",
            "DejaVu Sans Mono"
        ];

        try
        {
            foreach (var fontName in japaneseFonts)
                if (SystemFonts.TryGet(fontName, out var family))
                    return family.CreateFont(size);

            // Fall back to first available font
            var fallback = SystemFonts.Families.FirstOrDefault();
            if (fallback.Name != null) return fallback.CreateFont(size);
        }
        catch
        {
            // Font loading failed
        }

        throw new InvalidOperationException(
            "No fonts available for Matrix GIF rendering. Install a Japanese-supporting font like MS Gothic, Yu Gothic, or Noto Sans CJK.");
    }

    /// <summary>
    ///     Detect if text contains Japanese characters (katakana, hiragana, or kanji).
    ///     Used to automatically select appropriate font for GIF rendering.
    /// </summary>
    private static bool ContainsJapaneseCharacters(string text)
    {
        foreach (var c in text)
        {
            // Half-width katakana (used in Matrix mode): U+FF66 to U+FF9F
            if (c >= '\uFF66' && c <= '\uFF9F')
                return true;

            // Full-width katakana: U+30A0 to U+30FF
            if (c >= '\u30A0' && c <= '\u30FF')
                return true;

            // Hiragana: U+3040 to U+309F
            if (c >= '\u3040' && c <= '\u309F')
                return true;

            // CJK Unified Ideographs (kanji): U+4E00 to U+9FAF
            if (c >= '\u4E00' && c <= '\u9FAF')
                return true;
        }

        return false;
    }

    private static string StripAnsi(string text)
    {
        // Remove ANSI escape codes
        return StripAnsiRegex.Replace(text, "");
    }
}

/// <summary>
///     Options for GIF output.
/// </summary>
public class GifWriterOptions
{
    /// <summary>
    ///     Font size for rendering ASCII art (default: 10, smaller = smaller file)
    /// </summary>
    public int FontSize { get; set; } = 10;

    /// <summary>
    ///     Foreground (text) color
    /// </summary>
    public Color ForegroundColor { get; set; } = Color.White;

    /// <summary>
    ///     Background color
    /// </summary>
    public Color BackgroundColor { get; set; } = Color.Black;

    /// <summary>
    ///     Padding around the text in pixels
    /// </summary>
    public int Padding { get; set; } = 5;

    /// <summary>
    ///     Number of times to loop (0 = infinite)
    /// </summary>
    public int LoopCount { get; set; } = 0;

    /// <summary>
    ///     Scale factor for output (0.5 = half size, smaller = smaller file)
    /// </summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    ///     Maximum colors in GIF palette (16-256, lower = smaller file)
    /// </summary>
    public int MaxColors { get; set; } = 64;

    /// <summary>
    ///     Target FPS for output (lower = smaller file)
    /// </summary>
    public int TargetFps { get; set; } = 10;

    /// <summary>
    ///     Maximum length in seconds (for limiting long animations)
    /// </summary>
    public double? MaxLengthSeconds { get; set; }

    /// <summary>
    ///     Maximum number of frames (for limiting long animations)
    /// </summary>
    public int? MaxFrames { get; set; }

    /// <summary>
    ///     Default options optimized for small file size
    /// </summary>
    public static GifWriterOptions SmallFile => new()
    {
        FontSize = 8,
        Scale = 0.75f,
        MaxColors = 32,
        TargetFps = 8,
        Padding = 3
    };

    /// <summary>
    ///     Default options optimized for quality
    /// </summary>
    public static GifWriterOptions HighQuality => new()
    {
        FontSize = 14,
        Scale = 1.0f,
        MaxColors = 256,
        TargetFps = 15,
        Padding = 10
    };
}