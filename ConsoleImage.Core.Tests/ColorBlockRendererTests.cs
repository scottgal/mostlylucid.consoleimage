using ConsoleImage.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core.Tests;

public class ColorBlockRendererTests : IDisposable
{
    private readonly List<Image<Rgba32>> _disposableImages = new();

    public void Dispose()
    {
        foreach (var image in _disposableImages)
        {
            image.Dispose();
        }
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
        using var renderer = new ColorBlockRenderer();
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

        using var renderer = new ColorBlockRenderer(options);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void RenderImage_SolidColor_ContainsAnsiCodes()
    {
        using var renderer = new ColorBlockRenderer(new RenderOptions
        {
            MaxWidth = 20,
            MaxHeight = 10
        });

        var image = CreateTestImage(100, 100, Color.Red);
        var output = renderer.RenderImage(image);

        Assert.NotNull(output);
        Assert.NotEmpty(output);
        Assert.Contains("\x1b[", output); // Should contain ANSI codes
    }

    [Fact]
    public void RenderImage_ContainsHalfBlockCharacters()
    {
        using var renderer = new ColorBlockRenderer(new RenderOptions
        {
            MaxWidth = 20,
            MaxHeight = 10
        });

        var image = CreateTestImage(100, 100, Color.Gray);
        var output = renderer.RenderImage(image);

        // Should contain half-block characters
        Assert.True(
            output.Contains('\u2580') || // Upper half block
            output.Contains('\u2584') || // Lower half block
            output.Contains('\u2588') || // Full block
            output.Contains(' '),        // Space (for transparency)
            "Output should contain block characters");
    }

    [Fact]
    public void RenderImage_RespectsMaxDimensions()
    {
        using var renderer = new ColorBlockRenderer(new RenderOptions
        {
            MaxWidth = 30,
            MaxHeight = 15,
            CharacterAspectRatio = 0.5f
        });

        var image = CreateTestImage(500, 500, Color.Blue);
        var output = renderer.RenderImage(image);

        var lines = output.Split('\n');

        // Height in characters (each char = 2 pixels vertically)
        Assert.True(lines.Length <= 15);
    }

    [Theory]
    [InlineData(0.3f)]
    [InlineData(0.5f)]
    [InlineData(0.7f)]
    public void RenderImage_CharacterAspectRatio_AffectsOutput(float aspectRatio)
    {
        using var renderer1 = new ColorBlockRenderer(new RenderOptions
        {
            MaxWidth = 60,
            MaxHeight = 30,
            CharacterAspectRatio = 0.5f
        });

        using var renderer2 = new ColorBlockRenderer(new RenderOptions
        {
            MaxWidth = 60,
            MaxHeight = 30,
            CharacterAspectRatio = aspectRatio
        });

        var image = CreateTestImage(200, 200, Color.Green);

        var output1 = renderer1.RenderImage(image);
        var output2 = renderer2.RenderImage(image);

        if (Math.Abs(aspectRatio - 0.5f) > 0.01f)
        {
            // Different aspect ratios should produce different outputs
            var lines1 = output1.Split('\n');
            var lines2 = output2.Split('\n');

            Assert.True(
                lines1.Length != lines2.Length ||
                StripAnsiCodes(lines1[0]).Length != StripAnsiCodes(lines2[0]).Length,
                "Different aspect ratios should produce different dimensions");
        }
    }

    [Fact]
    public void RenderImage_TransparentPixels_HandleGracefully()
    {
        using var renderer = new ColorBlockRenderer(new RenderOptions
        {
            MaxWidth = 20,
            MaxHeight = 10
        });

        // Create image with transparent pixels
        var image = new Image<Rgba32>(100, 100, new Rgba32(0, 0, 0, 0));
        _disposableImages.Add(image);

        var output = renderer.RenderImage(image);

        Assert.NotNull(output);
        Assert.NotEmpty(output);
    }

    [Fact]
    public void RenderImage_EndsWithColorReset()
    {
        using var renderer = new ColorBlockRenderer(new RenderOptions
        {
            MaxWidth = 10,
            MaxHeight = 5
        });

        var image = CreateTestImage(50, 50, Color.Purple);
        var output = renderer.RenderImage(image);

        Assert.EndsWith("\x1b[0m", output.TrimEnd('\n'));
    }

    [Fact]
    public void RenderImage_TwoPixelVerticalResolution()
    {
        using var renderer = new ColorBlockRenderer(new RenderOptions
        {
            MaxWidth = 40,
            MaxHeight = 20,
            CharacterAspectRatio = 0.5f
        });

        // Create an image that should result in known dimensions
        var image = CreateTestImage(40, 40, Color.Yellow);
        var output = renderer.RenderImage(image);

        var lines = output.Split('\n');

        // Each line of output represents 2 vertical pixels
        // So a 40-pixel tall image should result in approximately 20 lines
        // (accounting for aspect ratio adjustments)
        Assert.True(lines.Length > 0);
        Assert.True(lines.Length <= 20);
    }

    [Fact]
    public void RenderImage_ExplicitDimensions_Override()
    {
        using var renderer = new ColorBlockRenderer(new RenderOptions
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
        var renderer = new ColorBlockRenderer();
        renderer.Dispose();
        renderer.Dispose(); // Should not throw
    }

    private static string StripAnsiCodes(string input)
    {
        // Remove ANSI escape sequences
        return System.Text.RegularExpressions.Regex.Replace(input, @"\x1b\[[0-9;]*m", "");
    }
}
