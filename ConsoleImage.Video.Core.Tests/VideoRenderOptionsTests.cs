using ConsoleImage.Core;

namespace ConsoleImage.Video.Core.Tests;

public class VideoRenderOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var options = VideoRenderOptions.Default;

        Assert.NotNull(options.RenderOptions);
        Assert.Equal(VideoRenderMode.Ascii, options.RenderMode);
        Assert.Equal(1, options.LoopCount);
        Assert.Equal(1.0f, options.SpeedMultiplier);
        Assert.True(options.UseAltScreen);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new VideoRenderOptions
        {
            RenderMode = VideoRenderMode.ColorBlocks,
            LoopCount = 3,
            SpeedMultiplier = 2.0f
        };

        var clone = original.Clone();

        // Modify clone
        clone.RenderMode = VideoRenderMode.Braille;
        clone.LoopCount = 5;

        // Original should be unchanged
        Assert.Equal(VideoRenderMode.ColorBlocks, original.RenderMode);
        Assert.Equal(3, original.LoopCount);
    }

    [Fact]
    public void Clone_RenderOptions_AreSeparate()
    {
        var original = new VideoRenderOptions
        {
            RenderOptions = new RenderOptions { MaxWidth = 100 }
        };

        var clone = original.Clone();
        clone.RenderOptions.MaxWidth = 200;

        // Original RenderOptions should be unchanged
        Assert.Equal(100, original.RenderOptions.MaxWidth);
    }

    [Fact]
    public void LowResource_HasOptimizedSettings()
    {
        var options = VideoRenderOptions.LowResource;

        Assert.True(options.FrameStep > 1 || options.BufferAheadFrames < 5);
    }

    [Fact]
    public void HighQuality_HasEnhancedSettings()
    {
        var options = VideoRenderOptions.HighQuality;

        Assert.True(options.FrameStep == 1);
        Assert.True(options.BufferAheadFrames >= 5);
    }

    [Theory]
    [InlineData(0.0, 10.0)]
    [InlineData(5.5, 20.0)]
    public void ForTimeRange_SetsStartAndEnd(double start, double end)
    {
        var options = VideoRenderOptions.ForTimeRange(start, end);

        Assert.Equal(start, options.StartTime);
        Assert.Equal(end, options.EndTime);
    }

    [Fact]
    public void With_ModifiesOptions()
    {
        var original = new VideoRenderOptions { LoopCount = 1 };

        var modified = original.With(o => o.LoopCount = 5);

        Assert.Equal(5, modified.LoopCount);
        Assert.Equal(1, original.LoopCount);
    }

    [Theory]
    [InlineData(VideoRenderMode.Ascii)]
    [InlineData(VideoRenderMode.ColorBlocks)]
    [InlineData(VideoRenderMode.Braille)]
    public void RenderMode_CanBeSet(VideoRenderMode mode)
    {
        var options = new VideoRenderOptions { RenderMode = mode };

        Assert.Equal(mode, options.RenderMode);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void SpeedMultiplier_CanBeSet(float speed)
    {
        var options = new VideoRenderOptions { SpeedMultiplier = speed };

        Assert.Equal(speed, options.SpeedMultiplier);
    }

    [Theory]
    [InlineData(FrameSamplingStrategy.Uniform)]
    [InlineData(FrameSamplingStrategy.Keyframe)]
    [InlineData(FrameSamplingStrategy.SceneAware)]
    [InlineData(FrameSamplingStrategy.Adaptive)]
    public void SamplingStrategy_CanBeSet(FrameSamplingStrategy strategy)
    {
        var options = new VideoRenderOptions { SamplingStrategy = strategy };

        Assert.Equal(strategy, options.SamplingStrategy);
    }

    [Theory]
    [InlineData(0)] // Infinite
    [InlineData(1)] // Single play
    [InlineData(5)] // Multiple loops
    public void LoopCount_CanBeSet(int loops)
    {
        var options = new VideoRenderOptions { LoopCount = loops };

        Assert.Equal(loops, options.LoopCount);
    }

    [Fact]
    public void TargetFps_CanBeNull()
    {
        var options = new VideoRenderOptions { TargetFps = null };

        Assert.Null(options.TargetFps);
    }

    [Theory]
    [InlineData(24.0)]
    [InlineData(30.0)]
    [InlineData(60.0)]
    public void TargetFps_CanBeSet(double fps)
    {
        var options = new VideoRenderOptions { TargetFps = fps };

        Assert.Equal(fps, options.TargetFps);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void FrameStep_CanBeSet(int step)
    {
        var options = new VideoRenderOptions { FrameStep = step };

        Assert.Equal(step, options.FrameStep);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void BufferAheadFrames_CanBeSet(int frames)
    {
        var options = new VideoRenderOptions { BufferAheadFrames = frames };

        Assert.Equal(frames, options.BufferAheadFrames);
    }

    [Fact]
    public void UseHardwareAcceleration_DefaultsToTrue()
    {
        var options = new VideoRenderOptions();

        Assert.True(options.UseHardwareAcceleration);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UseAltScreen_CanBeSet(bool useAlt)
    {
        var options = new VideoRenderOptions { UseAltScreen = useAlt };

        Assert.Equal(useAlt, options.UseAltScreen);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.4)]
    [InlineData(0.8)]
    public void SceneThreshold_CanBeSet(double threshold)
    {
        var options = new VideoRenderOptions { SceneThreshold = threshold };

        Assert.Equal(threshold, options.SceneThreshold);
    }

    [Fact]
    public void RenderOptions_CharacterAspectRatio_Preserved()
    {
        var options = new VideoRenderOptions
        {
            RenderOptions = new RenderOptions
            {
                CharacterAspectRatio = 0.42f
            }
        };

        Assert.Equal(0.42f, options.RenderOptions.CharacterAspectRatio);

        var clone = options.Clone();
        Assert.Equal(0.42f, clone.RenderOptions.CharacterAspectRatio);
    }
}