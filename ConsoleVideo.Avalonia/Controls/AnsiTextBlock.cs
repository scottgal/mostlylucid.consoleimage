using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ConsoleVideo.Avalonia.Services;

namespace ConsoleVideo.Avalonia.Controls;

/// <summary>
/// A control that renders ANSI-colored text with proper monospace formatting.
/// Parses ANSI escape codes and displays colored text.
/// </summary>
public class AnsiTextBlock : Control
{
    private readonly AsciiPreviewService _previewService = new();
    private List<AnsiSegment>? _segments;

    public static readonly StyledProperty<string?> AnsiTextProperty =
        AvaloniaProperty.Register<AnsiTextBlock, string?>(nameof(AnsiText));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<AnsiTextBlock, double>(nameof(FontSize), 12);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<AnsiTextBlock, FontFamily>(nameof(FontFamily),
            new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"));

    public string? AnsiText
    {
        get => GetValue(AnsiTextProperty);
        set => SetValue(AnsiTextProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    static AnsiTextBlock()
    {
        AffectsRender<AnsiTextBlock>(AnsiTextProperty, FontSizeProperty, FontFamilyProperty);
        AffectsMeasure<AnsiTextBlock>(AnsiTextProperty, FontSizeProperty, FontFamilyProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AnsiTextProperty)
        {
            _segments = null;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void EnsureParsed()
    {
        if (_segments == null && AnsiText != null)
        {
            _segments = _previewService.ParseAnsiToSegments(AnsiText);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureParsed();

        if (_segments == null || _segments.Count == 0)
            return new Size(0, 0);

        // Build plain text for measurement
        var plainText = string.Concat(_segments.Select(s => s.Text));
        var lines = plainText.Split('\n');

        var typeface = new Typeface(FontFamily);
        var maxWidth = 0.0;

        foreach (var line in lines)
        {
            var formatted = new FormattedText(
                line,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Brushes.White);

            maxWidth = Math.Max(maxWidth, formatted.Width);
        }

        var lineHeight = FontSize * 1.2;
        var totalHeight = lines.Length * lineHeight;

        return new Size(maxWidth, totalHeight);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        EnsureParsed();

        if (_segments == null || _segments.Count == 0)
            return;

        var typeface = new Typeface(FontFamily);
        var lineHeight = FontSize * 1.2;
        var x = 0.0;
        var y = 0.0;

        foreach (var segment in _segments)
        {
            if (string.IsNullOrEmpty(segment.Text))
                continue;

            // Handle newlines
            var parts = segment.Text.Split('\n');
            var brush = new SolidColorBrush(Color.FromRgb(segment.Color.R, segment.Color.G, segment.Color.B));

            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    // Move to next line
                    x = 0;
                    y += lineHeight;
                }

                if (!string.IsNullOrEmpty(parts[i]))
                {
                    var formatted = new FormattedText(
                        parts[i],
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        FontSize,
                        brush);

                    context.DrawText(formatted, new Point(x, y));
                    x += formatted.Width;
                }
            }
        }
    }
}
