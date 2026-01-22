namespace ConsoleImage.Core.Tests;

public class RenderOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var options = RenderOptions.Default;

        Assert.Equal(120, options.MaxWidth);
        Assert.Equal(60, options.MaxHeight);
        Assert.Equal(0.5f, options.CharacterAspectRatio);
        Assert.True(options.UseColor);
        Assert.True(options.Invert);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new RenderOptions
        {
            MaxWidth = 100,
            MaxHeight = 50,
            CharacterAspectRatio = 0.45f
        };

        var clone = original.Clone();

        // Modify clone
        clone.MaxWidth = 200;
        clone.CharacterAspectRatio = 0.6f;

        // Original should be unchanged
        Assert.Equal(100, original.MaxWidth);
        Assert.Equal(0.45f, original.CharacterAspectRatio);
    }

    [Theory]
    [InlineData(100, 100, 0.5f, 1, 1)] // Square image, default aspect
    [InlineData(200, 100, 0.5f, 1, 1)] // Wide image
    [InlineData(100, 200, 0.5f, 1, 1)] // Tall image
    public void CalculateVisualDimensions_MaintainsAspectRatio(
        int imageWidth, int imageHeight, float charAspect, int pxPerCharW, int pxPerCharH)
    {
        var options = new RenderOptions
        {
            MaxWidth = 80,
            MaxHeight = 40,
            CharacterAspectRatio = charAspect
        };

        var (outW, outH) = options.CalculateVisualDimensions(imageWidth, imageHeight, pxPerCharW, pxPerCharH);

        Assert.True(outW > 0);
        Assert.True(outH > 0);
        Assert.True(outW <= 80 * pxPerCharW);
        Assert.True(outH <= 40 * pxPerCharH);
    }

    [Theory]
    [InlineData(0.3f)]
    [InlineData(0.5f)]
    [InlineData(0.7f)]
    public void CalculateVisualDimensions_CharacterAspectRatio_AffectsOutput(float charAspect)
    {
        var options1 = new RenderOptions { MaxWidth = 80, MaxHeight = 40, CharacterAspectRatio = 0.5f };
        var options2 = new RenderOptions { MaxWidth = 80, MaxHeight = 40, CharacterAspectRatio = charAspect };

        // Square image
        var (w1, h1) = options1.CalculateVisualDimensions(200, 200, 1, 1);
        var (w2, h2) = options2.CalculateVisualDimensions(200, 200, 1, 1);

        if (Math.Abs(charAspect - 0.5f) > 0.01f)
            // Different aspect ratios should produce different dimensions for same image
            Assert.True(w1 != w2 || h1 != h2,
                $"Different char aspects ({0.5f} vs {charAspect}) should produce different dimensions");
    }

    [Fact]
    public void CalculateVisualDimensions_ExplicitWidth_OverridesMax()
    {
        var options = new RenderOptions
        {
            Width = 60,
            MaxWidth = 80,
            MaxHeight = 40,
            CharacterAspectRatio = 0.5f
        };

        var (w, h) = options.CalculateVisualDimensions(200, 200, 1, 1);

        // Width should be based on explicit Width, not MaxWidth
        Assert.True(w <= 60);
    }

    [Fact]
    public void CalculateVisualDimensions_ExplicitHeight_OverridesMax()
    {
        var options = new RenderOptions
        {
            Height = 30,
            MaxWidth = 80,
            MaxHeight = 40,
            CharacterAspectRatio = 0.5f
        };

        var (w, h) = options.CalculateVisualDimensions(200, 200, 1, 1);

        // Height should be based on explicit Height, not MaxHeight
        Assert.True(h <= 30);
    }

    [Fact]
    public void CalculateDimensions_CallsCalculateVisualDimensions()
    {
        var options = new RenderOptions
        {
            MaxWidth = 80,
            MaxHeight = 40,
            CharacterAspectRatio = 0.5f
        };

        // CalculateDimensions is a convenience wrapper for ASCII mode (1, 1)
        var (w1, h1) = options.CalculateDimensions(200, 100);
        var (w2, h2) = options.CalculateVisualDimensions(200, 100, 1, 1);

        Assert.Equal(w1, w2);
        Assert.Equal(h1, h2);
    }

    [Theory]
    [InlineData(1, 2)] // ColorBlocks: 1 pixel/char wide, 2 pixels/char tall
    [InlineData(2, 4)] // Braille: 2 pixels/char wide, 4 pixels/char tall
    public void CalculateVisualDimensions_PixelMultipliers_Work(int pxPerCharW, int pxPerCharH)
    {
        var options = new RenderOptions
        {
            MaxWidth = 40,
            MaxHeight = 20,
            CharacterAspectRatio = 0.5f
        };

        var (w, h) = options.CalculateVisualDimensions(200, 200, pxPerCharW, pxPerCharH);

        // Output should be scaled by pixel multipliers
        Assert.True(w >= pxPerCharW);
        Assert.True(h >= pxPerCharH);
    }

    [Fact]
    public void CalculateVisualDimensions_WideImage_FitsWidth()
    {
        var options = new RenderOptions
        {
            MaxWidth = 80,
            MaxHeight = 40,
            CharacterAspectRatio = 0.5f
        };

        // Very wide image (10:1 aspect ratio)
        var (w, h) = options.CalculateVisualDimensions(1000, 100, 1, 1);

        // Should be width-constrained
        Assert.True(w <= 80);
    }

    [Fact]
    public void CalculateVisualDimensions_TallImage_FitsHeight()
    {
        var options = new RenderOptions
        {
            MaxWidth = 80,
            MaxHeight = 40,
            CharacterAspectRatio = 0.5f
        };

        // Very tall image (1:10 aspect ratio)
        var (w, h) = options.CalculateVisualDimensions(100, 1000, 1, 1);

        // Should be height-constrained
        Assert.True(h <= 40);
    }

    [Fact]
    public void CalculateVisualDimensions_MinimumDimensions()
    {
        var options = new RenderOptions
        {
            MaxWidth = 80,
            MaxHeight = 40,
            CharacterAspectRatio = 0.5f
        };

        // Very small image
        var (w, h) = options.CalculateVisualDimensions(1, 1, 1, 1);

        // Should have at least 1x1 output
        Assert.True(w >= 1);
        Assert.True(h >= 1);
    }

    [Fact]
    public void With_ModifiesOptions()
    {
        var original = new RenderOptions { MaxWidth = 80 };

        var modified = original.With(o => o.MaxWidth = 100);

        Assert.Equal(100, modified.MaxWidth);
        Assert.Equal(80, original.MaxWidth); // Original unchanged
    }

    [Fact]
    public void ForAnimation_SetsCorrectDefaults()
    {
        var options = RenderOptions.ForAnimation(2);

        Assert.Equal(2, options.LoopCount);
        Assert.Equal(100, options.MaxWidth);
        Assert.Equal(40, options.MaxHeight);
    }

    [Fact]
    public void ForLightBackground_SetsCorrectDefaults()
    {
        var options = RenderOptions.ForLightBackground;

        Assert.False(options.Invert);
        Assert.False(options.UseColor);
    }

    [Fact]
    public void Default_IsDarkTerminalMode()
    {
        var options = RenderOptions.Default;

        // Default is for dark terminals (Invert=true)
        Assert.True(options.Invert);
        Assert.True(options.UseColor);
    }

    [Fact]
    public void CalculateVisualDimensions_ExplicitWidth_UsesExactWidth()
    {
        var options = new RenderOptions
        {
            Width = 80,
            MaxWidth = 120,
            MaxHeight = 60,
            CharacterAspectRatio = 0.5f
        };

        // Portrait image (taller than wide)
        var (w, h) = options.CalculateVisualDimensions(100, 200, 1, 1);

        // Width should be exactly 80 (the explicit width)
        Assert.Equal(80, w);
    }

    [Fact]
    public void CalculateVisualDimensions_ExplicitHeight_UsesExactHeight()
    {
        var options = new RenderOptions
        {
            Height = 40,
            MaxWidth = 120,
            MaxHeight = 60,
            CharacterAspectRatio = 0.5f
        };

        // Landscape image (wider than tall)
        var (w, h) = options.CalculateVisualDimensions(200, 100, 1, 1);

        // Height should be exactly 40 (the explicit height)
        Assert.Equal(40, h);
    }

    [Fact]
    public void CalculateVisualDimensions_BothExplicit_UsesExactDimensions()
    {
        var options = new RenderOptions
        {
            Width = 80,
            Height = 40,
            MaxWidth = 120,
            MaxHeight = 60,
            CharacterAspectRatio = 0.5f
        };

        // Any aspect ratio image
        var (w, h) = options.CalculateVisualDimensions(100, 300, 1, 1);

        // Both should be exact
        Assert.Equal(80, w);
        Assert.Equal(40, h);
    }
}