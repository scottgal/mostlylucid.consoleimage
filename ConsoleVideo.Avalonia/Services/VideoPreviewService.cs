using Avalonia.Media.Imaging;
using ConsoleImage.Video.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleVideo.Avalonia.Services;

/// <summary>
/// Service for video preview and frame extraction.
/// Uses FFmpegService for all video operations.
/// </summary>
public class VideoPreviewService : IDisposable
{
    private readonly FFmpegService _ffmpeg;
    private string? _currentVideoPath;
    private VideoInfo? _currentVideoInfo;
    private bool _disposed;

    public VideoPreviewService()
    {
        _ffmpeg = new FFmpegService();
    }

    /// <summary>
    /// Load a video file and return its metadata.
    /// </summary>
    public async Task<VideoInfo?> LoadVideoAsync(string path, CancellationToken ct = default)
    {
        await _ffmpeg.InitializeAsync(null, ct);

        _currentVideoPath = path;
        _currentVideoInfo = await _ffmpeg.GetVideoInfoAsync(path, ct);

        return _currentVideoInfo;
    }

    /// <summary>
    /// Get the currently loaded video info.
    /// </summary>
    public VideoInfo? CurrentVideo => _currentVideoInfo;

    /// <summary>
    /// Extract a single frame at the specified timestamp and return as Avalonia Bitmap.
    /// </summary>
    public async Task<Bitmap?> GetFrameAtAsync(
        double timestamp,
        int previewWidth,
        int previewHeight,
        CancellationToken ct = default)
    {
        if (_currentVideoPath == null) return null;

        var image = await _ffmpeg.ExtractFrameAsync(
            _currentVideoPath,
            timestamp,
            previewWidth,
            previewHeight,
            ct);

        if (image == null) return null;

        try
        {
            using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, ct);
            ms.Position = 0;
            return new Bitmap(ms);
        }
        finally
        {
            image.Dispose();
        }
    }

    /// <summary>
    /// Get frame as ImageSharp Image for further processing.
    /// </summary>
    public async Task<Image<Rgba32>?> GetFrameImageAsync(
        double timestamp,
        int? width = null,
        int? height = null,
        CancellationToken ct = default)
    {
        if (_currentVideoPath == null) return null;

        return await _ffmpeg.ExtractFrameAsync(
            _currentVideoPath,
            timestamp,
            width,
            height,
            ct);
    }

    /// <summary>
    /// Generate a strip of thumbnails for timeline preview.
    /// </summary>
    public async Task<List<(double Timestamp, Bitmap Thumbnail)>> GenerateThumbnailStripAsync(
        int thumbnailCount,
        int thumbnailWidth,
        int thumbnailHeight,
        CancellationToken ct = default)
    {
        var results = new List<(double, Bitmap)>();

        if (_currentVideoPath == null || _currentVideoInfo == null)
            return results;

        var duration = _currentVideoInfo.Duration;
        var interval = duration / (thumbnailCount + 1);

        for (int i = 1; i <= thumbnailCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var timestamp = interval * i;
            var bitmap = await GetFrameAtAsync(timestamp, thumbnailWidth, thumbnailHeight, ct);

            if (bitmap != null)
            {
                results.Add((timestamp, bitmap));
            }
        }

        return results;
    }

    /// <summary>
    /// Detect scene changes in the specified time range.
    /// </summary>
    public async Task<List<double>> DetectSceneChangesAsync(
        double? startTime,
        double? endTime,
        double threshold = 0.4,
        CancellationToken ct = default)
    {
        if (_currentVideoPath == null)
            return [];

        return await _ffmpeg.DetectSceneChangesAsync(
            _currentVideoPath,
            threshold,
            startTime,
            endTime,
            ct);
    }

    /// <summary>
    /// Get codec keyframes in the specified time range.
    /// </summary>
    public async Task<List<KeyframeInfo>> GetKeyframesAsync(CancellationToken ct = default)
    {
        if (_currentVideoPath == null)
            return [];

        return await _ffmpeg.GetKeyframesAsync(_currentVideoPath, ct);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _ffmpeg.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
