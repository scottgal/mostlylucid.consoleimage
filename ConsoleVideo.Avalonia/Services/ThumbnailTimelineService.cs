using Avalonia.Media.Imaging;
using ConsoleImage.Video.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleVideo.Avalonia.Services;

/// <summary>
/// Service for extracting a visual thumbnail timeline across a video.
/// Extracts many small, low-resolution thumbnails for fast scrubbing.
/// </summary>
public class ThumbnailTimelineService
{
    private readonly FFmpegService _ffmpeg = new();

    /// <summary>
    /// Thumbnail width in pixels (very small for efficiency).
    /// </summary>
    public int ThumbnailWidth { get; set; } = 60;

    /// <summary>
    /// Number of thumbnails across the entire video.
    /// </summary>
    public int ThumbnailCount { get; set; } = 60;

    /// <summary>
    /// Extract thumbnails across the video for a visual scrub bar.
    /// Yields thumbnails as they're extracted for incremental UI updates.
    /// Uses fast keyframe-only extraction (~100x faster) for better scrubbing.
    /// </summary>
    public async IAsyncEnumerable<TimelineThumbnail> ExtractTimelineStreamAsync(
        string videoPath,
        double duration,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var interval = duration / ThumbnailCount;

        for (int i = 0; i < ThumbnailCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var timestamp = i * interval;
            TimelineThumbnail? thumb = null;

            try
            {
                // Use fast keyframe-only extraction (~100x faster)
                // This seeks to the nearest keyframe which is fine for thumbnails
                var frame = await _ffmpeg.ExtractFrameFastAsync(
                    videoPath,
                    timestamp,
                    ThumbnailWidth,
                    0, // Height auto-calculated by FFmpeg
                    cancellationToken);

                if (frame != null)
                {
                    var bitmap = await ConvertToBitmapAsync(frame);
                    thumb = new TimelineThumbnail
                    {
                        Index = i,
                        Timestamp = timestamp,
                        Thumbnail = bitmap,
                        PositionPercent = duration > 0 ? (timestamp / duration) * 100 : 0
                    };
                    frame.Dispose();
                }
            }
            catch
            {
                // Create placeholder for failed frames
                thumb = new TimelineThumbnail
                {
                    Index = i,
                    Timestamp = timestamp,
                    Thumbnail = null,
                    PositionPercent = duration > 0 ? (timestamp / duration) * 100 : 0
                };
            }

            if (thumb != null)
                yield return thumb;
        }
    }

    /// <summary>
    /// Extract thumbnails across the video for a visual scrub bar.
    /// </summary>
    public async Task<List<TimelineThumbnail>> ExtractTimelineAsync(
        string videoPath,
        double duration,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var thumbnails = new List<TimelineThumbnail>();
        var count = 0;

        await foreach (var thumb in ExtractTimelineStreamAsync(videoPath, duration, cancellationToken))
        {
            thumbnails.Add(thumb);
            count++;
            progress?.Report((double)count / ThumbnailCount);
        }

        return thumbnails;
    }

    /// <summary>
    /// Extract a single thumbnail at a specific timestamp (for on-demand scrubbing).
    /// </summary>
    public async Task<Bitmap?> ExtractThumbnailAtAsync(
        string videoPath,
        double timestamp,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var frame = await _ffmpeg.ExtractFrameAsync(
                videoPath,
                timestamp,
                ThumbnailWidth,
                0,
                cancellationToken);

            if (frame != null)
            {
                var bitmap = await ConvertToBitmapAsync(frame);
                frame.Dispose();
                return bitmap;
            }
        }
        catch { }
        return null;
    }

    private static async Task<Bitmap?> ConvertToBitmapAsync(Image<Rgba32> image)
    {
        try
        {
            using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms);
            ms.Position = 0;
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// A single thumbnail in the video timeline.
/// </summary>
public class TimelineThumbnail
{
    public int Index { get; set; }
    public double Timestamp { get; set; }
    public Bitmap? Thumbnail { get; set; }
    public double PositionPercent { get; set; }

    public string TimestampFormatted => TimeSpan.FromSeconds(Timestamp).ToString(@"m\:ss");
}
