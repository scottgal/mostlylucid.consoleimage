using System.Text.Json;
using Xunit;

namespace ConsoleImage.Player.Tests;

public class PlayerDocumentTests
{
    [Fact]
    public void FromJson_ParsesSimpleDocument()
    {
        var json = """
                   {
                       "Version": "2.0",
                       "RenderMode": "ASCII",
                       "Settings": {
                           "MaxWidth": 80,
                           "MaxHeight": 24,
                           "UseColor": true,
                           "AnimationSpeedMultiplier": 1.0,
                           "LoopCount": 0
                       },
                       "Frames": [
                           {
                               "Content": "Hello World",
                               "DelayMs": 100,
                               "Width": 11,
                               "Height": 1
                           }
                       ]
                   }
                   """;

        var doc = PlayerDocument.FromJson(json);

        Assert.Equal("2.0", doc.Version);
        Assert.Equal("ASCII", doc.RenderMode);
        Assert.Single(doc.Frames);
        Assert.Equal("Hello World", doc.Frames[0].Content);
        Assert.Equal(100, doc.Frames[0].DelayMs);
        Assert.Equal(11, doc.Frames[0].Width);
        Assert.Equal(1, doc.Frames[0].Height);
    }

    [Fact]
    public void FromJson_ParsesAnimatedDocument()
    {
        var json = """
                   {
                       "Version": "2.0",
                       "RenderMode": "ColorBlocks",
                       "Settings": {
                           "AnimationSpeedMultiplier": 2.0,
                           "LoopCount": 3
                       },
                       "Frames": [
                           { "Content": "Frame 1", "DelayMs": 100, "Width": 7, "Height": 1 },
                           { "Content": "Frame 2", "DelayMs": 100, "Width": 7, "Height": 1 },
                           { "Content": "Frame 3", "DelayMs": 100, "Width": 7, "Height": 1 }
                       ]
                   }
                   """;

        var doc = PlayerDocument.FromJson(json);

        Assert.True(doc.IsAnimated);
        Assert.Equal(3, doc.FrameCount);
        Assert.Equal(300, doc.TotalDurationMs);
        Assert.Equal(2.0f, doc.Settings.AnimationSpeedMultiplier);
        Assert.Equal(3, doc.Settings.LoopCount);
    }

    [Fact]
    public void FromJson_ParsesJsonLdFields()
    {
        var json = """
                   {
                       "@context": "https://schema.org/",
                       "@type": "ConsoleImageDocument",
                       "Version": "2.0",
                       "Frames": []
                   }
                   """;

        var doc = PlayerDocument.FromJson(json);

        Assert.Equal("https://schema.org/", doc.Context);
        Assert.Equal("ConsoleImageDocument", doc.Type);
    }

    [Fact]
    public void FromJson_ParsesExpandedSettings()
    {
        var json = """
                   {
                       "Version": "2.0",
                       "Settings": {
                           "Width": 80,
                           "Height": 24,
                           "MaxWidth": 120,
                           "MaxHeight": 60,
                           "CharacterAspectRatio": 0.45,
                           "ContrastPower": 3.0,
                           "Gamma": 0.7,
                           "UseColor": true,
                           "Invert": false,
                           "CharacterSetPreset": "extended",
                           "AnimationSpeedMultiplier": 1.5,
                           "LoopCount": 2
                       },
                       "Frames": []
                   }
                   """;

        var doc = PlayerDocument.FromJson(json);

        Assert.Equal(80, doc.Settings.Width);
        Assert.Equal(24, doc.Settings.Height);
        Assert.Equal(120, doc.Settings.MaxWidth);
        Assert.Equal(60, doc.Settings.MaxHeight);
        Assert.Equal(0.45f, doc.Settings.CharacterAspectRatio);
        Assert.Equal(3.0f, doc.Settings.ContrastPower);
        Assert.Equal(0.7f, doc.Settings.Gamma);
        Assert.True(doc.Settings.UseColor);
        Assert.False(doc.Settings.Invert);
        Assert.Equal("extended", doc.Settings.CharacterSetPreset);
        Assert.Equal(1.5f, doc.Settings.AnimationSpeedMultiplier);
        Assert.Equal(2, doc.Settings.LoopCount);
    }

    [Fact]
    public void GetFrames_EnumeratesAllFrames()
    {
        var doc = new PlayerDocument
        {
            Frames = new List<PlayerFrame>
            {
                new() { Content = "A", DelayMs = 10 },
                new() { Content = "B", DelayMs = 20 },
                new() { Content = "C", DelayMs = 30 }
            }
        };

        var contents = doc.GetFrames().Select(f => f.Content).ToList();

        Assert.Equal(["A", "B", "C"], contents);
    }

    [Fact]
    public void FrameCount_ReturnsCorrectCount()
    {
        var doc = new PlayerDocument();
        Assert.Equal(0, doc.FrameCount);

        doc.Frames.Add(new PlayerFrame());
        Assert.Equal(1, doc.FrameCount);

        doc.Frames.Add(new PlayerFrame());
        Assert.Equal(2, doc.FrameCount);
    }

    [Fact]
    public void IsAnimated_ReturnsFalseForSingleFrame()
    {
        var doc = new PlayerDocument
        {
            Frames = new List<PlayerFrame> { new() { Content = "Single" } }
        };

        Assert.False(doc.IsAnimated);
    }

    [Fact]
    public void IsAnimated_ReturnsTrueForMultipleFrames()
    {
        var doc = new PlayerDocument
        {
            Frames = new List<PlayerFrame>
            {
                new() { Content = "A" },
                new() { Content = "B" }
            }
        };

        Assert.True(doc.IsAnimated);
    }

    [Fact]
    public void TotalDurationMs_SumsAllFrameDelays()
    {
        var doc = new PlayerDocument
        {
            Frames = new List<PlayerFrame>
            {
                new() { DelayMs = 100 },
                new() { DelayMs = 200 },
                new() { DelayMs = 300 }
            }
        };

        Assert.Equal(600, doc.TotalDurationMs);
    }

    [Fact]
    public void FromJson_HandlesEmptyFrames()
    {
        var json = """
                   {
                       "Version": "2.0",
                       "Frames": []
                   }
                   """;

        var doc = PlayerDocument.FromJson(json);

        Assert.Empty(doc.Frames);
        Assert.False(doc.IsAnimated);
        Assert.Equal(0, doc.TotalDurationMs);
    }

    [Fact]
    public void FromJson_ThrowsOnMalformedJson()
    {
        var json = "{ invalid json }}}";

        Assert.Throws<JsonException>(() => PlayerDocument.FromJson(json));
    }

    [Fact]
    public void FromJson_HandlesNullFrames()
    {
        var json = """
                   {
                       "Version": "2.0"
                   }
                   """;

        var doc = PlayerDocument.FromJson(json);

        // Should have default empty list, not null
        Assert.NotNull(doc.Frames);
        Assert.Empty(doc.Frames);
    }

    [Fact]
    public async Task LoadAsync_ThrowsFileNotFound()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => PlayerDocument.LoadAsync(nonExistentPath));
    }

    [Fact]
    public async Task LoadAsync_LoadsStandardJsonFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
                       {
                           "Version": "2.0",
                           "RenderMode": "Braille",
                           "Frames": [
                               { "Content": "Test", "DelayMs": 50, "Width": 4, "Height": 1 }
                           ]
                       }
                       """;
            await File.WriteAllTextAsync(tempFile, json);

            var doc = await PlayerDocument.LoadAsync(tempFile);

            Assert.Equal("2.0", doc.Version);
            Assert.Equal("Braille", doc.RenderMode);
            Assert.Single(doc.Frames);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_LoadsStreamingNdjsonFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // NDJSON format - each line is valid JSON
            var ndjson = """
                         {"@type":"ConsoleImageDocumentHeader","Version":"2.0","RenderMode":"ASCII","Created":"2024-01-01T00:00:00Z","Settings":{"MaxWidth":80}}
                         {"@type":"Frame","Index":0,"Content":"Frame A","DelayMs":100,"Width":7,"Height":1}
                         {"@type":"Frame","Index":1,"Content":"Frame B","DelayMs":100,"Width":7,"Height":1}
                         {"@type":"ConsoleImageDocumentFooter","FrameCount":2,"TotalDurationMs":200,"IsComplete":true}
                         """;
            await File.WriteAllTextAsync(tempFile, ndjson);

            var doc = await PlayerDocument.LoadAsync(tempFile);

            Assert.Equal("2.0", doc.Version);
            Assert.Equal("ASCII", doc.RenderMode);
            Assert.Equal(80, doc.Settings.MaxWidth);
            Assert.Equal(2, doc.FrameCount);
            Assert.Equal("Frame A", doc.Frames[0].Content);
            Assert.Equal("Frame B", doc.Frames[1].Content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromJson_ParsesStreamingNdjsonString()
    {
        var ndjson = """
                     {"@type":"ConsoleImageDocumentHeader","Version":"2.0","RenderMode":"Matrix"}
                     {"@type":"Frame","Index":0,"Content":"X","DelayMs":50,"Width":1,"Height":1}
                     {"@type":"Frame","Index":1,"Content":"Y","DelayMs":50,"Width":1,"Height":1}
                     """;

        var doc = PlayerDocument.FromJson(ndjson);

        Assert.Equal("Matrix", doc.RenderMode);
        Assert.Equal(2, doc.FrameCount);
        Assert.Equal("X", doc.Frames[0].Content);
        Assert.Equal("Y", doc.Frames[1].Content);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var doc = new PlayerDocument();

        Assert.Equal("https://schema.org/", doc.Context);
        Assert.Equal("ConsoleImageDocument", doc.Type);
        Assert.Equal("2.0", doc.Version);
        Assert.Equal("ASCII", doc.RenderMode);
        Assert.NotNull(doc.Settings);
        Assert.NotNull(doc.Frames);
    }

    [Fact]
    public void PlayerSettings_DefaultValues_AreCorrect()
    {
        var settings = new PlayerSettings();

        Assert.Null(settings.Width);
        Assert.Null(settings.Height);
        Assert.Equal(120, settings.MaxWidth);
        Assert.Equal(60, settings.MaxHeight);
        Assert.Equal(0.5f, settings.CharacterAspectRatio);
        Assert.Equal(2.5f, settings.ContrastPower);
        Assert.Equal(0.85f, settings.Gamma);
        Assert.True(settings.UseColor);
        Assert.True(settings.Invert);
        Assert.Null(settings.CharacterSetPreset);
        Assert.Equal(1.0f, settings.AnimationSpeedMultiplier);
        Assert.Equal(0, settings.LoopCount);
    }

    [Fact]
    public void PlayerFrame_DefaultValues_AreCorrect()
    {
        var frame = new PlayerFrame();

        Assert.Equal(string.Empty, frame.Content);
        Assert.Equal(0, frame.DelayMs);
        Assert.Equal(0, frame.Width);
        Assert.Equal(0, frame.Height);
    }
}