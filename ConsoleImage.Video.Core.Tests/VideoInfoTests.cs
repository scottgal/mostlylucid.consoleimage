using ConsoleImage.Video.Core;

namespace ConsoleImage.Video.Core.Tests;

public class VideoInfoTests
{
    [Fact]
    public void VideoInfo_CanBeCreated()
    {
        var info = new VideoInfo
        {
            Width = 1920,
            Height = 1080,
            Duration = 120.5,
            FrameRate = 30.0,
            VideoCodec = "h264",
            BitRate = 5000000
        };

        Assert.Equal(1920, info.Width);
        Assert.Equal(1080, info.Height);
        Assert.Equal(120.5, info.Duration);
        Assert.Equal(30.0, info.FrameRate);
        Assert.Equal("h264", info.VideoCodec);
        Assert.Equal(5000000, info.BitRate);
    }

    [Fact]
    public void VideoInfo_TotalFrames_CanBeSet()
    {
        var info = new VideoInfo
        {
            Duration = 10.0,
            FrameRate = 30.0,
            TotalFrames = 300
        };

        Assert.Equal(300, info.TotalFrames);
    }

    [Fact]
    public void VideoInfo_TotalFrames_ZeroFrameRate()
    {
        var info = new VideoInfo
        {
            Duration = 10.0,
            FrameRate = 0.0
        };

        Assert.Equal(0, info.TotalFrames);
    }

    [Fact]
    public void VideoInfo_DefaultValues()
    {
        var info = new VideoInfo();

        Assert.Equal(0, info.Width);
        Assert.Equal(0, info.Height);
        Assert.Equal(0.0, info.Duration);
        Assert.Equal(0.0, info.FrameRate);
        Assert.Null(info.VideoCodec);
        Assert.Equal(0, info.BitRate);
    }
}

public class FrameSamplingStrategyTests
{
    [Fact]
    public void Uniform_IsDefault()
    {
        var strategy = default(FrameSamplingStrategy);
        Assert.Equal(FrameSamplingStrategy.Uniform, strategy);
    }

    [Fact]
    public void AllStrategies_AreDefined()
    {
        var strategies = Enum.GetValues<FrameSamplingStrategy>();

        Assert.Contains(FrameSamplingStrategy.Uniform, strategies);
        Assert.Contains(FrameSamplingStrategy.Keyframe, strategies);
        Assert.Contains(FrameSamplingStrategy.SceneAware, strategies);
        Assert.Contains(FrameSamplingStrategy.Adaptive, strategies);
    }
}

public class VideoRenderModeTests
{
    [Fact]
    public void Ascii_IsDefault()
    {
        var mode = default(VideoRenderMode);
        Assert.Equal(VideoRenderMode.Ascii, mode);
    }

    [Fact]
    public void AllModes_AreDefined()
    {
        var modes = Enum.GetValues<VideoRenderMode>();

        Assert.Contains(VideoRenderMode.Ascii, modes);
        Assert.Contains(VideoRenderMode.ColorBlocks, modes);
        Assert.Contains(VideoRenderMode.Braille, modes);
    }
}
