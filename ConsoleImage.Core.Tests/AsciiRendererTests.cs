using ConsoleImage.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Core.Tests;

public class AsciiRendererTests : IDisposable
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
        using var renderer = new AsciiRenderer();
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

        using var renderer = new AsciiRenderer(options);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void RenderImage_SolidWhite_ReturnsFrame()
    {
        using var renderer = new AsciiRenderer(new RenderOptions { MaxWidth = 20, MaxHeight = 10 });
        var image = CreateTestImage(100, 100, Color.White);

        var frame = renderer.RenderImage(image);

        Assert.NotNull(frame);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
    }

    [Fact]
    public void RenderImage_SolidBlack_ReturnsFrame()
    {
        using var renderer = new AsciiRenderer(new RenderOptions { MaxWidth = 20, MaxHeight = 10 });
        var image = CreateTestImage(100, 100, Color.Black);

        var frame = renderer.RenderImage(image);

        Assert.NotNull(frame);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
    }

    [Fact]
    public void RenderImage_WithColor_ContainsAnsiCodes()
    {
        using var renderer = new AsciiRenderer(new RenderOptions
        {
            MaxWidth = 20,
            MaxHeight = 10,
            UseColor = true
        });

        var image = CreateTestImage(100, 100, Color.Red);
        var frame = renderer.RenderImage(image);

        var colorOutput = frame.ToAnsiString();
        Assert.Contains("\x1b[", colorOutput);
    }

    [Fact]
    public void RenderImage_WithoutColor_NoAnsiCodes()
    {
        using var renderer = new AsciiRenderer(new RenderOptions
        {
            MaxWidth = 20,
            MaxHeight = 10,
            UseColor = false
        });

        var image = CreateTestImage(100, 100, Color.Red);
        var frame = renderer.RenderImage(image);

        var output = frame.ToString();
        Assert.DoesNotContain("\x1b[", output);
    }

    [Fact]
    public void RenderImage_RespectsMaxWidth()
    {
        using var renderer = new AsciiRenderer(new RenderOptions
        {
            MaxWidth = 40,
            MaxHeight = 100,
            CharacterAspectRatio = 0.5f
        });

        var image = CreateTestImage(1000, 100, Color.Gray);
        var frame = renderer.RenderImage(image);

        Assert.True(frame.Width <= 40);
    }

    [Fact]
    public void RenderImage_RespectsMaxHeight()
    {
        using var renderer = new AsciiRenderer(new RenderOptions
        {
            MaxWidth = 100,
            MaxHeight = 20,
            CharacterAspectRatio = 0.5f
        });

        var image = CreateTestImage(100, 1000, Color.Gray);
        var frame = renderer.RenderImage(image);

        Assert.True(frame.Height <= 20);
    }

    [Fact]
    public void RenderImage_ExplicitDimensions_OverridesMax()
    {
        using var renderer = new AsciiRenderer(new RenderOptions
        {
            Width = 30,
            Height = 15,
            MaxWidth = 100,
            MaxHeight = 50
        });

        var image = CreateTestImage(100, 100, Color.Gray);
        var frame = renderer.RenderImage(image);

        Assert.True(frame.Width <= 30);
        Assert.True(frame.Height <= 15);
    }

    [Theory]
    [InlineData(0.3f)]
    [InlineData(0.5f)]
    [InlineData(0.7f)]
    public void RenderImage_DifferentAspectRatios_ProduceDifferentDimensions(float aspectRatio)
    {
        using var renderer1 = new AsciiRenderer(new RenderOptions
        {
            MaxWidth = 80,
            MaxHeight = 40,
            CharacterAspectRatio = 0.5f
        });

        using var renderer2 = new AsciiRenderer(new RenderOptions
        {
            MaxWidth = 80,
            MaxHeight = 40,
            CharacterAspectRatio = aspectRatio
        });

        var image = CreateTestImage(200, 200, Color.Gray);

        var frame1 = renderer1.RenderImage(image);
        var frame2 = renderer2.RenderImage(image);

        if (Math.Abs(aspectRatio - 0.5f) > 0.01f)
        {
            Assert.True(frame1.Width != frame2.Width || frame1.Height != frame2.Height,
                "Different aspect ratios should produce different dimensions");
        }
    }

    [Fact]
    public void AsciiFrame_ToString_ReturnsCharacters()
    {
        using var renderer = new AsciiRenderer(new RenderOptions { MaxWidth = 10, MaxHeight = 5 });
        var image = CreateTestImage(50, 50, Color.Gray);

        var frame = renderer.RenderImage(image);
        var output = frame.ToString();

        Assert.NotEmpty(output);
        Assert.Contains('\n', output); // Should have multiple lines
    }

    [Fact]
    public void AsciiFrame_ToAnsiString_ReturnsColoredOutput()
    {
        using var renderer = new AsciiRenderer(new RenderOptions
        {
            MaxWidth = 10,
            MaxHeight = 5,
            UseColor = true
        });

        var image = CreateTestImage(50, 50, Color.Blue);

        var frame = renderer.RenderImage(image);
        var colorOutput = frame.ToAnsiString();

        Assert.Contains("\x1b[38;2;", colorOutput); // RGB color code
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var renderer = new AsciiRenderer();
        renderer.Dispose();
        renderer.Dispose(); // Should not throw
    }
}
