using System.Runtime.InteropServices;

namespace ConsoleImage.Core.Tests;

/// <summary>
/// Tests for cross-platform compatibility
/// </summary>
public class CrossPlatformTests
{
    [Fact]
    public void CalibrationHelper_GetDefaultPath_ReturnsValidPath()
    {
        var path = CalibrationHelper.GetDefaultPath();

        Assert.NotNull(path);
        Assert.NotEmpty(path);
        Assert.EndsWith("calibration.json", path);

        // Should use proper path separator for the current platform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Contains('\\', path);
        }
    }

    [Fact]
    public void TemplateHelper_Load_HandlesNonExistentPath()
    {
        // Test with both Windows-style and Unix-style paths
        var windowsPath = @"C:\nonexistent\path\template.json";
        var unixPath = "/nonexistent/path/template.json";

        // Neither should throw, both should return null
        var result1 = TemplateHelper.Load(windowsPath);
        var result2 = TemplateHelper.Load(unixPath);

        Assert.Null(result1);
        Assert.Null(result2);
    }

    [Fact]
    public void Environment_GetFolderPath_ReturnsValidPath()
    {
        // These are used throughout the codebase
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.NotNull(localAppData);
        Assert.NotNull(userProfile);

        // On Unix, LocalApplicationData maps to ~/.local/share (or similar)
        // On Windows, it's typically C:\Users\{user}\AppData\Local
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Unix, paths should use forward slashes
            Assert.True(!localAppData.Contains('\\') || localAppData.Length == 0);
            Assert.True(!userProfile.Contains('\\') || userProfile.Length == 0);
        }
    }

    [Fact]
    public void RenderOptions_HasCrossPlatformDefaults()
    {
        var options = new RenderOptions();

        // Default values should be cross-platform compatible
        Assert.True(options.MaxWidth > 0);
        Assert.True(options.MaxHeight > 0);
        Assert.True(options.CharacterAspectRatio > 0);
    }

    [Fact]
    public void CalibrationSettings_CanSerializeAndDeserialize()
    {
        var settings = new CalibrationSettings
        {
            AsciiCharacterAspectRatio = 0.45f,
            BlocksCharacterAspectRatio = 0.5f,
            BrailleCharacterAspectRatio = 0.52f,
            AsciiGamma = 0.6f,
            BlocksGamma = 0.65f,
            BrailleGamma = 0.7f
        };

        // Save to temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"xplat_test_{Guid.NewGuid()}.json");

        try
        {
            CalibrationHelper.Save(settings, tempPath);
            var loaded = CalibrationHelper.Load(tempPath);

            Assert.NotNull(loaded);
            Assert.Equal(settings.AsciiCharacterAspectRatio, loaded.AsciiCharacterAspectRatio);
            Assert.Equal(settings.BlocksCharacterAspectRatio, loaded.BlocksCharacterAspectRatio);
            Assert.Equal(settings.BrailleCharacterAspectRatio, loaded.BrailleCharacterAspectRatio);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void TemplateSettings_CanSerializeAndDeserialize()
    {
        var settings = new TemplateSettings
        {
            Width = 80,
            Height = 40,
            Braille = true,
            Gamma = 0.65f,
            Speed = 1.5f
        };

        // Save to temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"template_xplat_{Guid.NewGuid()}.json");

        try
        {
            TemplateHelper.Save(settings, tempPath);
            var loaded = TemplateHelper.Load(tempPath);

            Assert.NotNull(loaded);
            Assert.Equal(settings.Width, loaded.Width);
            Assert.Equal(settings.Height, loaded.Height);
            Assert.Equal(settings.Braille, loaded.Braille);
            Assert.Equal(settings.Gamma, loaded.Gamma);
            Assert.Equal(settings.Speed, loaded.Speed);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Theory]
    [InlineData(RenderMode.Ascii)]
    [InlineData(RenderMode.ColorBlocks)]
    [InlineData(RenderMode.Braille)]
    [InlineData(RenderMode.Matrix)]
    public void RenderMode_AllModesHaveCalibrationSupport(RenderMode mode)
    {
        var settings = new CalibrationSettings();

        // All render modes should have both aspect ratio and gamma support
        var aspectRatio = settings.GetAspectRatio(mode);
        var gamma = settings.GetGamma(mode);

        Assert.True(aspectRatio > 0);
        Assert.True(gamma > 0);
    }
}
