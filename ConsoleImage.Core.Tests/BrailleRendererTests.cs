using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core.Tests;

public class BrailleRendererTests : IDisposable
{
    // Braille character range: U+2800 to U+28FF
    private const char BrailleBase = '\u2800';
    private const char BrailleMax = '\u28FF';
    private readonly List<Image<Rgba32>> _disposableImages = new();

    public void Dispose()
    {
        foreach (var image in _disposableImages) image.Dispose();
    }

    private Image<Rgba32> CreateTestImage(int width, int height, Rgba32 color)
    {
        var image = new Image<Rgba32>(width, height, color);
        _disposableImages.Add(image);
        return image;
    }

    [Fact]
    public void Constructor_WithDefaultOptions_DoesNotThrow()
    {
        using var renderer = new BrailleRenderer();
        Assert.NotNull(renderer);
    }

    [Fact]
    public void Constructor_WithCustomOptions_DoesNotThrow()
    {
        var options = new RenderOptions
        {
            MaxWidth = 40,
            MaxHeight = 20,
            CharacterAspectRatio = 0.45f
        };

        using var renderer = new BrailleRenderer(options);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void RenderImage_ReturnsNonEmptyString()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 20,
            MaxHeight = 10
        });

        var image = CreateTestImage(100, 100, Color.White);
        var output = renderer.RenderImage(image);

        Assert.NotNull(output);
        Assert.NotEmpty(output);
    }

    [Fact]
    public void RenderImage_ContainsBrailleCharacters()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 20,
            MaxHeight = 10
        });

        var image = CreateTestImage(100, 100, Color.Gray);
        var output = renderer.RenderImage(image);

        // Check if output contains braille characters
        var containsBraille = output.Any(c => c >= BrailleBase && c <= BrailleMax);
        Assert.True(containsBraille, "Output should contain braille characters");
    }

    [Fact]
    public void RenderImage_WithColor_ContainsAnsiCodes()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 20,
            MaxHeight = 10,
            UseColor = true
        });

        var image = CreateTestImage(100, 100, Color.Blue);
        var output = renderer.RenderImage(image);

        Assert.Contains("\x1b[", output);
    }

    [Fact]
    public void RenderImage_WithoutColor_NoAnsiCodes()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 20,
            MaxHeight = 10,
            UseColor = false
        });

        var image = CreateTestImage(100, 100, Color.Blue);
        var output = renderer.RenderImage(image);

        Assert.DoesNotContain("\x1b[", output);
    }

    [Fact]
    public void RenderImage_RespectsMaxDimensions()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 30,
            MaxHeight = 15,
            CharacterAspectRatio = 0.5f
        });

        var image = CreateTestImage(500, 500, Color.Gray);
        var output = renderer.RenderImage(image);

        var lines = output.Split('\n');

        // Height in characters (each char = 4 pixels vertically)
        Assert.True(lines.Length <= 15);
    }

    [Theory]
    [InlineData(0.3f)]
    [InlineData(0.5f)]
    [InlineData(0.7f)]
    public void RenderImage_CharacterAspectRatio_AffectsOutput(float aspectRatio)
    {
        using var renderer1 = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 60,
            MaxHeight = 30,
            CharacterAspectRatio = 0.5f,
            UseColor = false
        });

        using var renderer2 = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 60,
            MaxHeight = 30,
            CharacterAspectRatio = aspectRatio,
            UseColor = false
        });

        var image = CreateTestImage(200, 200, Color.Gray);

        var output1 = renderer1.RenderImage(image);
        var output2 = renderer2.RenderImage(image);

        if (Math.Abs(aspectRatio - 0.5f) > 0.01f)
        {
            var lines1 = output1.Split('\n');
            var lines2 = output2.Split('\n');

            Assert.True(
                lines1.Length != lines2.Length ||
                lines1[0].Length != lines2[0].Length,
                "Different aspect ratios should produce different dimensions");
        }
    }

    [Fact]
    public void RenderImage_2x4Resolution()
    {
        // Braille provides 2x horizontal and 4x vertical resolution
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 40,
            MaxHeight = 20,
            CharacterAspectRatio = 0.5f,
            UseColor = false
        });

        var image = CreateTestImage(80, 80, Color.White);
        var output = renderer.RenderImage(image);

        var lines = output.Split('\n');

        // With 4 pixels per character vertically and an 80-pixel image,
        // the output should have at most 20 lines (80/4 = 20, but limited by MaxHeight)
        Assert.True(lines.Length <= 20);
        Assert.True(lines.Length > 0);
    }

    [Fact]
    public void RenderImage_WhiteImage_UsesFilledDots()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 10,
            MaxHeight = 5,
            UseColor = false,
            Invert = true // Dark terminal: bright pixels = dots
        });

        var image = CreateTestImage(20, 20, Color.White);
        var output = renderer.RenderImage(image);

        // White on dark terminal should show as filled braille
        // Full braille character (all dots) is U+28FF
        var hasDots = output.Any(c => c >= BrailleBase && c <= BrailleMax && c != BrailleBase);
        Assert.True(hasDots, "White image should produce braille characters with dots");
    }

    [Fact]
    public void RenderImage_BlackImage_UsesEmptyDots()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 10,
            MaxHeight = 5,
            UseColor = false,
            Invert = true // Dark terminal: dark pixels = no dots
        });

        var image = CreateTestImage(20, 20, Color.Black);
        var output = renderer.RenderImage(image);

        // Black on dark terminal should show as empty braille (or spaces)
        // Empty braille character is U+2800
        var hasEmptyOrSpaces = output.Any(c => c == BrailleBase || c == ' ');
        Assert.True(hasEmptyOrSpaces, "Black image on dark terminal should produce empty/minimal braille");
    }

    [Fact]
    public void RenderImage_DimensionsMultipleOf2x4()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 25, // Not divisible by 2
            MaxHeight = 13, // Not divisible by 4
            CharacterAspectRatio = 0.5f,
            UseColor = false
        });

        var image = CreateTestImage(100, 100, Color.Gray);
        var output = renderer.RenderImage(image);

        // Should still work without errors
        Assert.NotNull(output);
        Assert.NotEmpty(output);
    }

    [Fact]
    public void RenderImage_EndsWithColorReset_WhenColorEnabled()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 10,
            MaxHeight = 5,
            UseColor = true
        });

        var image = CreateTestImage(20, 20, Color.Purple);
        var output = renderer.RenderImage(image);

        Assert.EndsWith("\x1b[0m", output.TrimEnd('\n'));
    }

    [Fact]
    public void RenderImage_ExplicitDimensions_Override()
    {
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            Width = 20,
            Height = 10,
            MaxWidth = 100,
            MaxHeight = 50
        });

        var image = CreateTestImage(200, 200, Color.Cyan);
        var output = renderer.RenderImage(image);

        var lines = output.Split('\n');
        Assert.True(lines.Length <= 10);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var renderer = new BrailleRenderer();
        renderer.Dispose();
        renderer.Dispose(); // Should not throw
    }

    [Fact]
    public void RenderImage_BlackImage_ColorIsBlack()
    {
        // Critical test: ensure black pixels output BLACK color, not white
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 10,
            MaxHeight = 5,
            UseColor = true,
            Gamma = 0.65f // Default gamma
        });

        var image = CreateTestImage(20, 20, Color.Black);
        var output = renderer.RenderImage(image);

        // Black image should NOT contain white color codes (255;255;255)
        Assert.DoesNotContain(";255;255;255m", output);

        // Black image should either have black color codes OR no color codes (spaces for invisible)
        // If it has color codes, they should be dark (< 50 per channel)
        if (output.Contains("\x1b[38;2;"))
        {
            // Extract color values from ANSI codes
            var matches = Regex.Matches(
                output, @"\x1b\[38;2;(\d+);(\d+);(\d+)m");

            foreach (Match match in matches)
            {
                var r = int.Parse(match.Groups[1].Value);
                var g = int.Parse(match.Groups[2].Value);
                var b = int.Parse(match.Groups[3].Value);

                // Colors should be dark (< 50) for a black input image
                Assert.True(r < 50 && g < 50 && b < 50,
                    $"Black image produced non-dark color: R={r}, G={g}, B={b}");
            }
        }
    }

    [Fact]
    public void RenderImage_RedImage_ColorContainsRed()
    {
        // Test that colored images output correct colors
        using var renderer = new BrailleRenderer(new RenderOptions
        {
            MaxWidth = 10,
            MaxHeight = 5,
            UseColor = true,
            Gamma = 0.65f
        });

        var image = CreateTestImage(20, 20, new Rgba32(255, 0, 0, 255)); // Pure red
        var output = renderer.RenderImage(image);

        // Should contain red-ish ANSI codes
        Assert.Contains("\x1b[38;2;", output);

        // Extract color values - red channel should be > 100, green/blue should be lower
        var matches = Regex.Matches(
            output, @"\x1b\[38;2;(\d+);(\d+);(\d+)m");

        Assert.True(matches.Count > 0, "Red image should output color codes");

        foreach (Match match in matches)
        {
            var r = int.Parse(match.Groups[1].Value);
            var g = int.Parse(match.Groups[2].Value);
            var b = int.Parse(match.Groups[3].Value);

            // Red channel should be the dominant one
            Assert.True(r > g && r > b,
                $"Red image should have red as dominant channel: R={r}, G={g}, B={b}");
        }
    }
}