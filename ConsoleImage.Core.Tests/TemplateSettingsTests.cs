using System.Text.Json;

namespace ConsoleImage.Core.Tests;

public class TemplateSettingsTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }

    private string CreateTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"template_test_{Guid.NewGuid()}.json");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void TemplateSettings_DefaultValues_AreNull()
    {
        var settings = new TemplateSettings();

        // All nullable properties should be null by default
        Assert.Null(settings.Width);
        Assert.Null(settings.Height);
        Assert.Null(settings.Ascii);
        Assert.Null(settings.Blocks);
        Assert.Null(settings.Braille);
        Assert.Null(settings.Gamma);
        Assert.Null(settings.Speed);
    }

    [Fact]
    public void TemplateHelper_Save_CreatesValidJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"template_save_{Guid.NewGuid()}.json");
        _tempFiles.Add(path);

        var settings = new TemplateSettings
        {
            Width = 80,
            Braille = true,
            Gamma = 0.65f,
            Speed = 1.5f
        };

        TemplateHelper.Save(settings, path);

        Assert.True(File.Exists(path));
        var json = File.ReadAllText(path);
        Assert.Contains("\"Width\": 80", json);
        Assert.Contains("\"Braille\": true", json);
        Assert.Contains("\"Speed\": 1.5", json);
    }

    [Fact]
    public void TemplateHelper_Load_ReturnsNullForMissingFile()
    {
        var result = TemplateHelper.Load("/nonexistent/path/template.json");
        Assert.Null(result);
    }

    [Fact]
    public void TemplateHelper_Load_ParsesValidJson()
    {
        var json = @"{
            ""Width"": 100,
            ""Height"": 50,
            ""Blocks"": true,
            ""NoColor"": false,
            ""Gamma"": 0.8,
            ""Loop"": 3
        }";
        var path = CreateTempFile(json);

        var settings = TemplateHelper.Load(path);

        Assert.NotNull(settings);
        Assert.Equal(100, settings.Width);
        Assert.Equal(50, settings.Height);
        Assert.True(settings.Blocks);
        Assert.False(settings.NoColor);
        Assert.Equal(0.8f, settings.Gamma);
        Assert.Equal(3, settings.Loop);
    }

    [Fact]
    public void TemplateHelper_Load_HandlesPartialJson()
    {
        // Only some properties set - others should be null
        var json = @"{ ""Width"": 60, ""ShowStatus"": true }";
        var path = CreateTempFile(json);

        var settings = TemplateHelper.Load(path);

        Assert.NotNull(settings);
        Assert.Equal(60, settings.Width);
        Assert.True(settings.ShowStatus);
        Assert.Null(settings.Height);
        Assert.Null(settings.Gamma);
        Assert.Null(settings.Braille);
    }

    [Fact]
    public void TemplateHelper_Load_HandlesInvalidJson()
    {
        var json = "{ invalid json }";
        var path = CreateTempFile(json);

        var settings = TemplateHelper.Load(path);

        // Should return null for invalid JSON, not throw
        Assert.Null(settings);
    }

    [Fact]
    public void TemplateHelper_SaveAndLoad_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"template_roundtrip_{Guid.NewGuid()}.json");
        _tempFiles.Add(path);

        var original = new TemplateSettings
        {
            Width = 120,
            MaxWidth = 200,
            Braille = true,
            MatrixColor = "#FF0000",
            MatrixDensity = 0.7f,
            Gamma = 0.5f,
            Speed = 2.0f,
            Loop = 5,
            ShowStatus = true,
            Dejitter = true,
            ColorThreshold = 20,
            Subs = "auto",
            SubtitleLang = "es",
            SlideDelay = 5.0f,
            Shuffle = true
        };

        TemplateHelper.Save(original, path);
        var loaded = TemplateHelper.Load(path);

        Assert.NotNull(loaded);
        Assert.Equal(original.Width, loaded.Width);
        Assert.Equal(original.MaxWidth, loaded.MaxWidth);
        Assert.Equal(original.Braille, loaded.Braille);
        Assert.Equal(original.MatrixColor, loaded.MatrixColor);
        Assert.Equal(original.MatrixDensity, loaded.MatrixDensity);
        Assert.Equal(original.Gamma, loaded.Gamma);
        Assert.Equal(original.Speed, loaded.Speed);
        Assert.Equal(original.Loop, loaded.Loop);
        Assert.Equal(original.ShowStatus, loaded.ShowStatus);
        Assert.Equal(original.Dejitter, loaded.Dejitter);
        Assert.Equal(original.ColorThreshold, loaded.ColorThreshold);
        Assert.Equal(original.Subs, loaded.Subs);
        Assert.Equal(original.SubtitleLang, loaded.SubtitleLang);
        Assert.Equal(original.SlideDelay, loaded.SlideDelay);
        Assert.Equal(original.Shuffle, loaded.Shuffle);
    }

    [Fact]
    public void TemplateSettings_NullValuesNotSerialized()
    {
        var settings = new TemplateSettings
        {
            Width = 80,
            // All other properties null
        };

        var json = JsonSerializer.Serialize(settings, TemplateJsonContext.Default.TemplateSettings);

        // Should only contain Width, not null properties
        Assert.Contains("\"Width\"", json);
        Assert.DoesNotContain("\"Height\"", json);
        Assert.DoesNotContain("\"Gamma\"", json);
        Assert.DoesNotContain("\"Braille\"", json);
    }

    [Fact]
    public void TemplateHelper_Load_CrossPlatformPaths()
    {
        // Test with different path separators
        var json = @"{ ""Width"": 80 }";
        var path = CreateTempFile(json);

        // Convert to forward slashes (Unix style)
        var unixStylePath = path.Replace('\\', '/');

        // Should work with either path style on any platform
        var settings = TemplateHelper.Load(path);
        Assert.NotNull(settings);

        // Note: On Windows, forward slashes also work
        if (OperatingSystem.IsWindows())
        {
            var settingsUnix = TemplateHelper.Load(unixStylePath);
            Assert.NotNull(settingsUnix);
        }
    }
}
