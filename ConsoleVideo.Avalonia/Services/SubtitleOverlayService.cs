using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleVideo.Avalonia.Services;

/// <summary>
/// Service for rendering subtitle text overlays onto images.
/// Uses ImageSharp for high-quality text rendering.
/// </summary>
public class SubtitleOverlayService
{
    private FontFamily? _fontFamily;
    private readonly string _fontName;

    /// <summary>
    /// Default subtitle style settings.
    /// </summary>
    public SubtitleStyle DefaultStyle { get; set; } = new();

    public SubtitleOverlayService(string fontName = "Arial")
    {
        _fontName = fontName;
        InitializeFont();
    }

    private void InitializeFont()
    {
        // Try to load the font
        if (SystemFonts.TryGet(_fontName, out var family))
        {
            _fontFamily = family;
        }
        else if (SystemFonts.TryGet("Segoe UI", out family))
        {
            _fontFamily = family;
        }
        else if (SystemFonts.TryGet("DejaVu Sans", out family))
        {
            _fontFamily = family;
        }
        else
        {
            // Fallback to first available font
            _fontFamily = SystemFonts.Families.FirstOrDefault();
        }
    }

    /// <summary>
    /// Render subtitle text onto an image.
    /// </summary>
    public Image<Rgba32> RenderSubtitle(
        Image<Rgba32> sourceImage,
        string text,
        SubtitleStyle? style = null)
    {
        style ??= DefaultStyle;

        // Clone the source image
        var result = sourceImage.Clone();

        if (string.IsNullOrWhiteSpace(text) || _fontFamily == null)
            return result;

        // Calculate font size based on image height
        var fontSize = (float)(style.FontSizePercent * result.Height);
        var font = _fontFamily.Value.CreateFont(fontSize, FontStyle.Bold);

        // Calculate text metrics
        var textOptions = new RichTextOptions(font)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            WrappingLength = result.Width * 0.9f,
            Origin = new PointF(result.Width / 2f, (float)(result.Height * style.VerticalPosition))
        };

        // Draw text with outline (for readability)
        result.Mutate(ctx =>
        {
            // Draw outline/stroke
            if (style.OutlineWidth > 0)
            {
                var outlineColor = Color.ParseHex(style.OutlineColor);
                var pen = new SolidPen(outlineColor, (float)style.OutlineWidth);
                ctx.DrawText(textOptions, text, pen);
            }

            // Draw fill
            var fillColor = Color.ParseHex(style.FillColor);
            ctx.DrawText(textOptions, text, fillColor);
        });

        return result;
    }

    /// <summary>
    /// Find which transcription segment applies to a given timestamp.
    /// </summary>
    public static TranscriptionSegment? FindSegmentForTimestamp(
        IEnumerable<TranscriptionSegment> segments,
        double timestamp)
    {
        return segments.FirstOrDefault(s =>
            timestamp >= s.StartTime && timestamp <= s.EndTime);
    }

    /// <summary>
    /// Apply subtitles to a list of keyframes based on their timestamps.
    /// </summary>
    public List<(int Index, double Timestamp, Image<Rgba32> Image, string? Subtitle)> ApplySubtitles(
        IEnumerable<(int Index, double Timestamp, Image<Rgba32> Image)> keyframes,
        IEnumerable<TranscriptionSegment> segments,
        SubtitleStyle? style = null)
    {
        var segmentList = segments.ToList();
        var results = new List<(int Index, double Timestamp, Image<Rgba32> Image, string? Subtitle)>();

        foreach (var kf in keyframes)
        {
            var segment = FindSegmentForTimestamp(segmentList, kf.Timestamp);
            var subtitle = segment?.Text;

            Image<Rgba32> outputImage;
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                outputImage = RenderSubtitle(kf.Image, subtitle, style);
            }
            else
            {
                outputImage = kf.Image.Clone();
            }

            results.Add((kf.Index, kf.Timestamp, outputImage, subtitle));
        }

        return results;
    }
}

/// <summary>
/// Style settings for subtitle rendering.
/// </summary>
public class SubtitleStyle
{
    /// <summary>Font size as percentage of image height (0.05 = 5%).</summary>
    public double FontSizePercent { get; set; } = 0.06;

    /// <summary>Vertical position as percentage from top (0.85 = 85% from top, near bottom).</summary>
    public double VerticalPosition { get; set; } = 0.88;

    /// <summary>Fill color in hex format.</summary>
    public string FillColor { get; set; } = "#FFFFFF";

    /// <summary>Outline/stroke color in hex format.</summary>
    public string OutlineColor { get; set; } = "#000000";

    /// <summary>Outline width in pixels.</summary>
    public double OutlineWidth { get; set; } = 3;
}

/// <summary>
/// An editable subtitle entry for the UI.
/// </summary>
public class EditableSubtitle
{
    public int Index { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public string Text { get; set; } = "";

    public string TimeRange => $"{FormatTime(StartTime)} - {FormatTime(EndTime)}";

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.ToString(@"mm\:ss\.f");
    }

    public static EditableSubtitle FromTranscription(TranscriptionSegment segment, int index) => new()
    {
        Index = index,
        StartTime = segment.StartTime,
        EndTime = segment.EndTime,
        Text = segment.Text
    };

    public TranscriptionSegment ToTranscription() => new()
    {
        StartTime = StartTime,
        EndTime = EndTime,
        Text = Text
    };
}
