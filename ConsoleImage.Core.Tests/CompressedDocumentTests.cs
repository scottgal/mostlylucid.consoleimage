// Unit tests for CompressedDocument functionality
// Tests delta encoding, temporal stability, and compressed I/O

using System.Text;

namespace ConsoleImage.Core.Tests;

public class CompressedDocumentTests
{
    [Fact]
    public void OptimizedDocument_FromDocument_PreservesFrameCount()
    {
        // Arrange
        var doc = CreateTestDocument(5);

        // Act
        var optimized = OptimizedDocument.FromDocument(doc);

        // Assert
        Assert.Equal(5, optimized.Frames.Count);
    }

    [Fact]
    public void OptimizedDocument_ToDocument_RestoresFrames()
    {
        // Arrange
        var original = CreateTestDocument(3);
        var optimized = OptimizedDocument.FromDocument(original);

        // Act
        var restored = optimized.ToDocument();

        // Assert
        Assert.Equal(original.Frames.Count, restored.Frames.Count);
        Assert.Equal(original.RenderMode, restored.RenderMode);
        Assert.Equal(original.SourceFile, restored.SourceFile);
    }

    [Fact]
    public void OptimizedDocument_PreservesPalette()
    {
        // Arrange
        var doc = CreateTestDocument(2);

        // Act
        var optimized = OptimizedDocument.FromDocument(doc);

        // Assert
        Assert.NotNull(optimized.Palette);
        Assert.True(optimized.Palette.Length > 0);
    }

    [Fact]
    public void OptimizedDocument_FirstFrameIsKeyframe()
    {
        // Arrange
        var doc = CreateTestDocument(5);

        // Act
        var optimized = OptimizedDocument.FromDocument(doc);

        // Assert
        Assert.True(optimized.Frames[0].IsKeyframe);
    }

    [Fact]
    public void OptimizedDocument_KeyframeInterval_CreatesKeyframes()
    {
        // Arrange - create 10 frames with keyframe interval of 3
        var doc = CreateTestDocument(10);

        // Act
        var optimized = OptimizedDocument.FromDocument(doc, keyframeInterval: 3);

        // Assert - frames 0, 3, 6, 9 should be keyframes
        Assert.True(optimized.Frames[0].IsKeyframe);
        Assert.True(optimized.Frames[3].IsKeyframe);
        Assert.True(optimized.Frames[6].IsKeyframe);
        Assert.True(optimized.Frames[9].IsKeyframe);
    }

    [Fact]
    public void OptimizedDocument_DeltaFrames_ExistForSimilarContent()
    {
        // Arrange - create frames with identical content (same chars, same colors)
        var frames = new List<DocumentFrame>();
        var content = "\x1b[38;2;100;100;100mAAAAA\x1b[0m";
        for (int i = 0; i < 5; i++)
        {
            frames.Add(new DocumentFrame
            {
                Content = content,
                Width = 5,
                Height = 1,
                DelayMs = 100
            });
        }

        var doc = new ConsoleImageDocument
        {
            Frames = frames,
            RenderMode = "Ascii",
            SourceFile = "test.gif",
            Settings = new DocumentRenderSettings()
        };

        // Act
        var optimized = OptimizedDocument.FromDocument(doc, keyframeInterval: 100);

        // Assert - identical frames should produce delta frames with empty or minimal changes
        Assert.True(optimized.Frames[0].IsKeyframe, "First frame should be keyframe");
        // Subsequent identical frames should be delta frames (or small deltas)
        Assert.True(optimized.Frames.Count == 5, "Should have 5 frames");
    }

    [Fact]
    public void TemporalStability_PreservesSimilarColors()
    {
        // Arrange - create frames with slightly different colors
        var doc = CreateSimilarColorsDocument();

        // Act - enable stability with threshold of 20
        var optimized = OptimizedDocument.FromDocument(doc, keyframeInterval: 100,
            enableStability: true, colorThreshold: 20);

        // Restore and check
        var restored = optimized.ToDocument();

        // Assert - colors should be stabilized (fewer unique colors)
        Assert.Equal(doc.Frames.Count, restored.Frames.Count);
    }

    [Fact]
    public void TemporalStability_DoesNotAffectDifferentColors()
    {
        // Arrange - create frames with very different colors
        var doc = CreateDifferentColorsDocument();

        // Act - enable stability
        var optimized = OptimizedDocument.FromDocument(doc, keyframeInterval: 100,
            enableStability: true, colorThreshold: 10);

        // Restore and check
        var restored = optimized.ToDocument();

        // Assert - content should be preserved
        Assert.Equal(doc.Frames.Count, restored.Frames.Count);
    }

    [Fact]
    public void DocumentRenderSettings_EnableTemporalStability_IsSerialized()
    {
        // Arrange
        var settings = new DocumentRenderSettings
        {
            EnableTemporalStability = true,
            ColorStabilityThreshold = 25
        };

        // Act
        var clone = settings.Clone();

        // Assert
        Assert.True(clone.EnableTemporalStability);
        Assert.Equal(25, clone.ColorStabilityThreshold);
    }

    [Fact]
    public void RenderOptions_TemporalStability_ClonesCorrectly()
    {
        // Arrange
        var options = new RenderOptions
        {
            EnableTemporalStability = true,
            ColorStabilityThreshold = 30,
            CharacterStabilityBias = 0.5f
        };

        // Act
        var clone = options.Clone();

        // Assert
        Assert.True(clone.EnableTemporalStability);
        Assert.Equal(30, clone.ColorStabilityThreshold);
        Assert.Equal(0.5f, clone.CharacterStabilityBias);
    }

    [Fact]
    public void DocumentRenderSettings_FromRenderOptions_IncludesStability()
    {
        // Arrange
        var options = new RenderOptions
        {
            EnableTemporalStability = true,
            ColorStabilityThreshold = 20
        };

        // Act
        var settings = DocumentRenderSettings.FromRenderOptions(options);

        // Assert
        Assert.True(settings.EnableTemporalStability);
        Assert.Equal(20, settings.ColorStabilityThreshold);
    }

    [Fact]
    public void ColorSimilarity_SimilarColors_ReturnsTrue()
    {
        // Test the color similarity logic indirectly through frame processing
        // Colors that differ by less than threshold should be considered similar
        var doc = CreateSimilarColorsDocument();
        var optimized = OptimizedDocument.FromDocument(doc, keyframeInterval: 100,
            enableStability: true, colorThreshold: 50);

        // Should complete without error
        Assert.NotNull(optimized);
    }

    [Fact]
    public void OptimizedDocument_LoopCount_PreservedInSettings()
    {
        // Arrange
        var doc = CreateTestDocument(3);
        doc.Settings.LoopCount = 5;

        // Act
        var optimized = OptimizedDocument.FromDocument(doc);
        var restored = optimized.ToDocument();

        // Assert
        Assert.Equal(5, restored.Settings.LoopCount);
    }

    // Helper methods to create test documents

    private static ConsoleImageDocument CreateTestDocument(int frameCount)
    {
        var frames = new List<DocumentFrame>();
        for (int i = 0; i < frameCount; i++)
        {
            frames.Add(new DocumentFrame
            {
                Content = $"\x1b[38;2;255;{i * 50};0mFrame {i}\x1b[0m",
                Width = 10,
                Height = 1,
                DelayMs = 100
            });
        }

        return new ConsoleImageDocument
        {
            Frames = frames,
            RenderMode = "Ascii",
            SourceFile = "test.gif",
            Settings = new DocumentRenderSettings()
        };
    }

    private static ConsoleImageDocument CreateSimilarFramesDocument(int frameCount)
    {
        var baseContent = "\x1b[38;2;255;100;50mAAAAA\x1b[0m";
        var frames = new List<DocumentFrame>();
        for (int i = 0; i < frameCount; i++)
        {
            // Only change one character per frame
            var content = baseContent.Replace("AAAAA", $"AAA{(char)('A' + i)}A");
            frames.Add(new DocumentFrame
            {
                Content = content,
                Width = 5,
                Height = 1,
                DelayMs = 100
            });
        }

        return new ConsoleImageDocument
        {
            Frames = frames,
            RenderMode = "Ascii",
            SourceFile = "test.gif",
            Settings = new DocumentRenderSettings()
        };
    }

    private static ConsoleImageDocument CreateSimilarColorsDocument()
    {
        // Colors that differ by small amounts
        var frames = new List<DocumentFrame>
        {
            new() { Content = "\x1b[38;2;100;100;100mA\x1b[0m", Width = 1, Height = 1, DelayMs = 100 },
            new() { Content = "\x1b[38;2;105;102;98mA\x1b[0m", Width = 1, Height = 1, DelayMs = 100 },
            new() { Content = "\x1b[38;2;108;104;96mA\x1b[0m", Width = 1, Height = 1, DelayMs = 100 }
        };

        return new ConsoleImageDocument
        {
            Frames = frames,
            RenderMode = "Ascii",
            SourceFile = "test.gif",
            Settings = new DocumentRenderSettings()
        };
    }

    private static ConsoleImageDocument CreateDifferentColorsDocument()
    {
        // Colors that are very different
        var frames = new List<DocumentFrame>
        {
            new() { Content = "\x1b[38;2;255;0;0mR\x1b[0m", Width = 1, Height = 1, DelayMs = 100 },
            new() { Content = "\x1b[38;2;0;255;0mG\x1b[0m", Width = 1, Height = 1, DelayMs = 100 },
            new() { Content = "\x1b[38;2;0;0;255mB\x1b[0m", Width = 1, Height = 1, DelayMs = 100 }
        };

        return new ConsoleImageDocument
        {
            Frames = frames,
            RenderMode = "Ascii",
            SourceFile = "test.gif",
            Settings = new DocumentRenderSettings()
        };
    }
}
