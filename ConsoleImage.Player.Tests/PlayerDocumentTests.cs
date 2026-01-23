using ConsoleImage.Player;
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
}
