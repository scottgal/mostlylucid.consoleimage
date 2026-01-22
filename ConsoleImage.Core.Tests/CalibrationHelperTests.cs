namespace ConsoleImage.Core.Tests;

public class CalibrationHelperTests
{
    [Fact]
    public void CalibrationSettings_DefaultValues_AreHalf()
    {
        var settings = new CalibrationSettings();

        Assert.Equal(0.5f, settings.AsciiCharacterAspectRatio);
        Assert.Equal(0.5f, settings.BlocksCharacterAspectRatio);
        Assert.Equal(0.5f, settings.BrailleCharacterAspectRatio);
    }

    [Theory]
    [InlineData(RenderMode.Ascii, 0.5f)]
    [InlineData(RenderMode.ColorBlocks, 0.5f)]
    [InlineData(RenderMode.Braille, 0.5f)]
    public void CalibrationSettings_GetAspectRatio_ReturnsCorrectDefault(RenderMode mode, float expected)
    {
        var settings = new CalibrationSettings();
        Assert.Equal(expected, settings.GetAspectRatio(mode));
    }

    [Fact]
    public void CalibrationSettings_GetAspectRatio_ReturnsCustomValues()
    {
        var settings = new CalibrationSettings
        {
            AsciiCharacterAspectRatio = 0.45f,
            BlocksCharacterAspectRatio = 0.48f,
            BrailleCharacterAspectRatio = 0.52f
        };

        Assert.Equal(0.45f, settings.GetAspectRatio(RenderMode.Ascii));
        Assert.Equal(0.48f, settings.GetAspectRatio(RenderMode.ColorBlocks));
        Assert.Equal(0.52f, settings.GetAspectRatio(RenderMode.Braille));
    }

    [Theory]
    [InlineData(RenderMode.Ascii, 0.4f)]
    [InlineData(RenderMode.ColorBlocks, 0.6f)]
    [InlineData(RenderMode.Braille, 0.55f)]
    [InlineData(RenderMode.Matrix, 0.45f)]
    public void CalibrationSettings_WithAspectRatio_UpdatesCorrectMode(RenderMode mode, float newValue)
    {
        var original = new CalibrationSettings();
        var updated = original.WithAspectRatio(mode, newValue);

        // Verify the correct mode was updated
        Assert.Equal(newValue, updated.GetAspectRatio(mode));

        // Verify other modes remain unchanged (accounting for shared properties)
        // Matrix and Ascii share the same underlying property (AsciiCharacterAspectRatio)
        foreach (var otherMode in Enum.GetValues<RenderMode>())
            if (otherMode != mode)
            {
                // Matrix and Ascii share the same property, so both change together
                var sharesProperty = (mode == RenderMode.Ascii && otherMode == RenderMode.Matrix) ||
                                     (mode == RenderMode.Matrix && otherMode == RenderMode.Ascii);

                if (sharesProperty)
                    Assert.Equal(newValue, updated.GetAspectRatio(otherMode));
                else
                    Assert.Equal(0.5f, updated.GetAspectRatio(otherMode));
            }

        // Verify original is unchanged (immutability)
        Assert.Equal(0.5f, original.GetAspectRatio(mode));
    }

    [Fact]
    public void CalibrationSettings_WithAspectRatio_PreservesOtherValues()
    {
        var original = new CalibrationSettings
        {
            AsciiCharacterAspectRatio = 0.45f,
            BlocksCharacterAspectRatio = 0.48f,
            BrailleCharacterAspectRatio = 0.52f
        };

        var updated = original.WithAspectRatio(RenderMode.Ascii, 0.40f);

        Assert.Equal(0.40f, updated.AsciiCharacterAspectRatio);
        Assert.Equal(0.48f, updated.BlocksCharacterAspectRatio);
        Assert.Equal(0.52f, updated.BrailleCharacterAspectRatio);
    }

    [Fact]
    public void GenerateCalibrationImage_ReturnsValidImage()
    {
        using var image = CalibrationHelper.GenerateCalibrationImage();

        Assert.NotNull(image);
        Assert.Equal(200, image.Width);
        Assert.Equal(200, image.Height);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(400)]
    public void GenerateCalibrationImage_RespectsCustomSize(int size)
    {
        using var image = CalibrationHelper.GenerateCalibrationImage(size);

        Assert.Equal(size, image.Width);
        Assert.Equal(size, image.Height);
    }

    [Theory]
    [InlineData(RenderMode.Ascii)]
    [InlineData(RenderMode.ColorBlocks)]
    [InlineData(RenderMode.Braille)]
    public void RenderCalibrationPattern_ReturnsNonEmptyString(RenderMode mode)
    {
        var output = CalibrationHelper.RenderCalibrationPattern(mode, 0.5f);

        Assert.NotNull(output);
        Assert.NotEmpty(output);
        Assert.Contains('\n', output); // Should have multiple lines
    }

    [Theory]
    [InlineData(0.3f)]
    [InlineData(0.5f)]
    [InlineData(0.7f)]
    public void RenderCalibrationPattern_DifferentAspectRatios_ProduceDifferentOutput(float aspectRatio)
    {
        var output1 = CalibrationHelper.RenderCalibrationPattern(RenderMode.Ascii, 0.5f, width: 40, height: 20);
        var output2 = CalibrationHelper.RenderCalibrationPattern(RenderMode.Ascii, aspectRatio, width: 40, height: 20);

        // Different aspect ratios should produce different output dimensions
        if (Math.Abs(aspectRatio - 0.5f) > 0.01f)
        {
            // The outputs will have different widths due to aspect ratio compensation
            var lines1 = output1.Split('\n');
            var lines2 = output2.Split('\n');

            // At least one property should differ
            Assert.True(lines1.Length != lines2.Length || lines1[0].Length != lines2[0].Length,
                "Different aspect ratios should produce different output dimensions");
        }
    }

    [Fact]
    public void RenderCalibrationPattern_ColoredOutput_ContainsAnsiCodes()
    {
        var output = CalibrationHelper.RenderCalibrationPattern(RenderMode.ColorBlocks, 0.5f);

        // ANSI escape codes start with ESC (0x1b)
        Assert.Contains("\x1b[", output);
    }

    [Fact]
    public void RenderCalibrationPattern_NonColoredAscii_NoAnsiCodes()
    {
        var output = CalibrationHelper.RenderCalibrationPattern(RenderMode.Ascii, 0.5f, false);

        // Should not contain ANSI escape codes
        Assert.DoesNotContain("\x1b[", output);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesValues()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"calibration_test_{Guid.NewGuid()}.json");

        try
        {
            var original = new CalibrationSettings
            {
                AsciiCharacterAspectRatio = 0.42f,
                BlocksCharacterAspectRatio = 0.47f,
                BrailleCharacterAspectRatio = 0.53f
            };

            CalibrationHelper.Save(original, tempPath);
            var loaded = CalibrationHelper.Load(tempPath);

            Assert.NotNull(loaded);
            Assert.Equal(original.AsciiCharacterAspectRatio, loaded.AsciiCharacterAspectRatio);
            Assert.Equal(original.BlocksCharacterAspectRatio, loaded.BlocksCharacterAspectRatio);
            Assert.Equal(original.BrailleCharacterAspectRatio, loaded.BrailleCharacterAspectRatio);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void Load_NonExistentFile_ReturnsNull()
    {
        var result = CalibrationHelper.Load("/nonexistent/path/calibration.json");
        Assert.Null(result);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsNull()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"calibration_invalid_{Guid.NewGuid()}.json");

        try
        {
            File.WriteAllText(tempPath, "not valid json {{{");
            var result = CalibrationHelper.Load(tempPath);
            Assert.Null(result);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void GetDefaultPath_ReturnsPathWithCalibrationJson()
    {
        var path = CalibrationHelper.GetDefaultPath();

        Assert.NotNull(path);
        Assert.EndsWith("calibration.json", path);
    }
}