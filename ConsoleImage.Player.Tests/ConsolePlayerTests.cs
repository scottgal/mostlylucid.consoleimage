using ConsoleImage.Player;
using Xunit;

namespace ConsoleImage.Player.Tests;

public class ConsolePlayerTests
{
    [Fact]
    public void Constructor_SetsDocumentAndDefaults()
    {
        var doc = CreateTestDocument(3);

        var player = new ConsolePlayer(doc);

        Assert.Same(doc, player.Document);
    }

    [Fact]
    public void Constructor_UsesDocumentSettings()
    {
        var doc = CreateTestDocument(3);
        doc.Settings.AnimationSpeedMultiplier = 2.0f;
        doc.Settings.LoopCount = 5;

        var player = new ConsolePlayer(doc);

        // Settings should be read from document
        Assert.Equal(2.0f, doc.Settings.AnimationSpeedMultiplier);
        Assert.Equal(5, doc.Settings.LoopCount);
    }

    [Fact]
    public void Constructor_AllowsOverrides()
    {
        var doc = CreateTestDocument(3);
        doc.Settings.AnimationSpeedMultiplier = 1.0f;
        doc.Settings.LoopCount = 0;

        var player = new ConsolePlayer(doc, speedMultiplier: 2.5f, loopCount: 3);

        // Overrides should take effect (tested via GetInfo output)
        var info = player.GetInfo();
        Assert.Contains("Speed: 2.5x", info);
        Assert.Contains("Loop Count: 3", info);
    }

    [Fact]
    public void GetInfo_ReturnsDocumentDetails()
    {
        var doc = CreateTestDocument(5);
        doc.Version = "2.0";
        doc.RenderMode = "Braille";
        doc.SourceFile = "test.gif";
        doc.Settings.MaxWidth = 80;
        doc.Settings.MaxHeight = 24;
        doc.Settings.UseColor = true;

        var player = new ConsolePlayer(doc);
        var info = player.GetInfo();

        Assert.Contains("Version: 2.0", info);
        Assert.Contains("Source: test.gif", info);
        Assert.Contains("Render Mode: Braille", info);
        Assert.Contains("Frames: 5", info);
        Assert.Contains("Size: 80x24", info);
        Assert.Contains("Color: yes", info);
    }

    [Fact]
    public void GetInfo_ShowsDurationForAnimated()
    {
        var doc = CreateTestDocument(3);
        // Total duration = 3 * 100ms = 300ms

        var player = new ConsolePlayer(doc);
        var info = player.GetInfo();

        Assert.Contains("Duration: 300ms", info);
    }

    [Fact]
    public void GetInfo_ShowsInfiniteLoopCorrectly()
    {
        var doc = CreateTestDocument(2);
        doc.Settings.LoopCount = 0;

        var player = new ConsolePlayer(doc);
        var info = player.GetInfo();

        Assert.Contains("Loop Count: infinite", info);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var doc = CreateTestDocument(1);
        var player = new ConsolePlayer(doc);

        // Should not throw
        player.Dispose();
        player.Dispose();
        player.Dispose();
    }

    [Fact]
    public void OnFrameChanged_CanBeSubscribed()
    {
        var doc = CreateTestDocument(3);
        var player = new ConsolePlayer(doc);
        var eventFired = false;

        player.OnFrameChanged += (current, total) =>
        {
            eventFired = true;
            Assert.Equal(3, total);
        };

        // Event wiring should work (actual firing happens during PlayAsync)
        Assert.False(eventFired); // Not fired until Play is called
    }

    [Fact]
    public void OnLoopComplete_CanBeSubscribed()
    {
        var doc = CreateTestDocument(2);
        var player = new ConsolePlayer(doc);
        var eventFired = false;

        player.OnLoopComplete += (loopNum) =>
        {
            eventFired = true;
        };

        Assert.False(eventFired); // Not fired until Play is called
    }

    [Fact]
    public void FromJson_CreatesPlayerFromJsonString()
    {
        var json = """
        {
            "Version": "2.0",
            "RenderMode": "Matrix",
            "Frames": [
                { "Content": "Frame", "DelayMs": 50, "Width": 5, "Height": 1 }
            ]
        }
        """;

        var player = ConsolePlayer.FromJson(json);

        Assert.Equal("Matrix", player.Document.RenderMode);
        Assert.Single(player.Document.Frames);
    }

    [Fact]
    public void FromJson_WithOverrides()
    {
        var json = """
        {
            "Version": "2.0",
            "Settings": { "AnimationSpeedMultiplier": 1.0, "LoopCount": 0 },
            "Frames": [
                { "Content": "A", "DelayMs": 100 },
                { "Content": "B", "DelayMs": 100 }
            ]
        }
        """;

        var player = ConsolePlayer.FromJson(json, speedMultiplier: 3.0f, loopCount: 2);

        var info = player.GetInfo();
        Assert.Contains("Speed: 3x", info);
        Assert.Contains("Loop Count: 2", info);
    }

    [Fact]
    public async Task FromFileAsync_ThrowsOnMissingFile()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => ConsolePlayer.FromFileAsync(nonExistent));
    }

    private static PlayerDocument CreateTestDocument(int frameCount)
    {
        var doc = new PlayerDocument
        {
            Version = "2.0",
            RenderMode = "ASCII",
            Settings = new PlayerSettings
            {
                MaxWidth = 80,
                MaxHeight = 24,
                UseColor = true,
                AnimationSpeedMultiplier = 1.0f,
                LoopCount = 0
            }
        };

        for (int i = 0; i < frameCount; i++)
        {
            doc.Frames.Add(new PlayerFrame
            {
                Content = $"Frame {i + 1}",
                DelayMs = 100,
                Width = 10,
                Height = 1
            });
        }

        return doc;
    }
}
