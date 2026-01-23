using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConsoleImage.Video.Core;
using ConsoleVideo.Avalonia.Models;
using ConsoleVideo.Avalonia.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Whisper.net.Ggml;

namespace ConsoleVideo.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly VideoPreviewService _previewService;
    private readonly KeyframeExtractionService _extractionService;
    private readonly ThumbnailTimelineService _thumbnailService;
    private CancellationTokenSource? _extractionCts;
    private CancellationTokenSource? _thumbnailCts;

    public MainWindowViewModel()
    {
        _previewService = new VideoPreviewService();
        _extractionService = new KeyframeExtractionService();
        _thumbnailService = new ThumbnailTimelineService();
    }

    // Video state
    [ObservableProperty]
    private string? _videoPath;

    [ObservableProperty]
    private VideoInfo? _videoInfo;

    [ObservableProperty]
    private Bitmap? _currentFrame;

    [ObservableProperty]
    private double _currentPosition;

    [ObservableProperty]
    private double _duration;

    // Timeline range
    [ObservableProperty]
    private double _rangeStart;

    [ObservableProperty]
    private double _rangeEnd;

    // Extraction settings
    [ObservableProperty]
    private int _targetKeyframeCount = 10;

    [ObservableProperty]
    private ExtractionStrategy _selectedStrategy = ExtractionStrategy.Uniform;

    [ObservableProperty]
    private RenderMode _selectedRenderMode = RenderMode.Ascii;

    [ObservableProperty]
    private double _sceneThreshold = 0.4;

    // Extracted keyframes
    [ObservableProperty]
    private ObservableCollection<KeyframeViewModel> _keyframes = [];

    [ObservableProperty]
    private KeyframeViewModel? _selectedKeyframe;

    // ASCII Preview
    [ObservableProperty]
    private string? _asciiPreview;

    [ObservableProperty]
    private int _previewWidth = 80;

    [ObservableProperty]
    private int _previewHeight = 30;

    // Playback state
    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private int _currentKeyframeIndex;

    [ObservableProperty]
    private double _playbackSpeed = 1.0;

    // GIF export settings
    [ObservableProperty]
    private double _gifDuration = 5.0; // Total GIF duration in seconds

    [ObservableProperty]
    private int _gifFontSize = 10;

    // Visual timeline scrubbing
    [ObservableProperty]
    private ObservableCollection<TimelineThumbnail> _timelineThumbnails = [];

    [ObservableProperty]
    private bool _isLoadingTimeline;

    [ObservableProperty]
    private double _timelineProgress;

    [ObservableProperty]
    private TimelineThumbnail? _hoveredThumbnail;

    // Transcription
    [ObservableProperty]
    private ObservableCollection<EditableSubtitle> _subtitles = [];

    [ObservableProperty]
    private bool _isTranscribing;

    [ObservableProperty]
    private string _transcriptionStatus = "";

    private WhisperService? _whisperService;
    private CancellationTokenSource? _transcriptionCts;

    private CancellationTokenSource? _playbackCts;
    private readonly AsciiPreviewService _asciiPreviewService = new();
    private readonly SubtitleOverlayService _subtitleOverlayService = new();

    // Status
    [ObservableProperty]
    private string _statusText = "Ready. Open a video file to begin.";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private int _extractedCount;

    [ObservableProperty]
    private int _requestedCount;

    // Computed properties
    public bool HasVideo => VideoInfo != null;
    public string RangeDisplay => $"{FormatTime(RangeStart)} - {FormatTime(RangeEnd)}";
    public string CountDisplay => ExtractedCount > 0
        ? $"Requested: {RequestedCount} | Extracted: {ExtractedCount}"
        : "";

    partial void OnVideoInfoChanged(VideoInfo? value)
    {
        OnPropertyChanged(nameof(HasVideo));
    }

    partial void OnRangeStartChanged(double value)
    {
        OnPropertyChanged(nameof(RangeDisplay));
    }

    partial void OnRangeEndChanged(double value)
    {
        OnPropertyChanged(nameof(RangeDisplay));
    }

    partial void OnExtractedCountChanged(int value)
    {
        OnPropertyChanged(nameof(CountDisplay));
    }

    partial void OnSelectedKeyframeChanged(KeyframeViewModel? value)
    {
        UpdateAsciiPreview();
        if (value != null)
        {
            // Update current frame index
            var index = Keyframes.IndexOf(value);
            if (index >= 0)
            {
                CurrentKeyframeIndex = index;
            }
            // Seek video to this timestamp
            _ = SeekToAsync(value.Timestamp);
        }
    }

    partial void OnSelectedRenderModeChanged(RenderMode value)
    {
        UpdateAsciiPreview();
    }

    partial void OnPreviewWidthChanged(int value)
    {
        UpdateAsciiPreview();
    }

    partial void OnPreviewHeightChanged(int value)
    {
        UpdateAsciiPreview();
    }

    private void UpdateAsciiPreview()
    {
        if (SelectedKeyframe?.OriginalImage == null)
        {
            AsciiPreview = null;
            return;
        }

        try
        {
            AsciiPreview = _asciiPreviewService.RenderToAnsi(
                SelectedKeyframe.OriginalImage,
                SelectedRenderMode,
                PreviewWidth,
                PreviewHeight);
        }
        catch
        {
            AsciiPreview = "Preview error";
        }
    }

    [ObservableProperty]
    private bool _isImage;

    [ObservableProperty]
    private bool _isAnimatedGif;

    [RelayCommand]
    private async Task OpenVideoAsync()
    {
        // This will be triggered by the view's file picker
        // For now, we'll handle the path directly when set
    }

    public async Task LoadImageAsync(string path)
    {
        try
        {
            IsLoading = true;
            StatusText = "Loading image...";
            VideoPath = path;
            IsImage = true;
            Keyframes.Clear();
            TimelineThumbnails.Clear();

            var ext = Path.GetExtension(path).ToLowerInvariant();
            var image = await Image.LoadAsync<Rgba32>(path);

            // Check if it's an animated GIF
            IsAnimatedGif = ext == ".gif" && image.Frames.Count > 1;

            if (IsAnimatedGif)
            {
                // Extract frames from animated GIF
                StatusText = $"Loading animated GIF ({image.Frames.Count} frames)...";
                Duration = image.Frames.Count / 10.0; // Approximate duration
                RangeStart = 0;
                RangeEnd = Duration;

                for (int i = 0; i < image.Frames.Count; i++)
                {
                    var frame = image.Frames.CloneFrame(i);
                    var thumbnail = await ConvertToThumbnailAsync(frame, 120, 90);

                    Keyframes.Add(new KeyframeViewModel
                    {
                        Index = i + 1,
                        Timestamp = i / 10.0,
                        Thumbnail = thumbnail,
                        IsSceneBoundary = i == 0,
                        Source = "gif",
                        OriginalImage = frame
                    });

                    Progress = (double)(i + 1) / image.Frames.Count;
                }

                // Show first frame
                if (Keyframes.Count > 0)
                {
                    SelectedKeyframe = Keyframes[0];
                    CurrentFrame = Keyframes[0].Thumbnail;
                }

                StatusText = $"Loaded: {Path.GetFileName(path)} ({image.Width}x{image.Height}, {image.Frames.Count} frames)";
            }
            else
            {
                // Static image - create single keyframe
                Duration = 1;
                RangeStart = 0;
                RangeEnd = 1;

                var thumbnail = await ConvertToThumbnailAsync(image, 640, 480);
                CurrentFrame = thumbnail;

                Keyframes.Add(new KeyframeViewModel
                {
                    Index = 1,
                    Timestamp = 0,
                    Thumbnail = await ConvertToThumbnailAsync(image.Clone(), 120, 90),
                    IsSceneBoundary = true,
                    Source = "image",
                    OriginalImage = image
                });

                SelectedKeyframe = Keyframes[0];

                StatusText = $"Loaded: {Path.GetFileName(path)} ({image.Width}x{image.Height})";
            }

            // Create fake VideoInfo for compatibility
            VideoInfo = new VideoInfo
            {
                FilePath = path,
                Width = image.Width,
                Height = image.Height,
                Duration = Duration,
                FrameRate = IsAnimatedGif ? 10 : 1,
                TotalFrames = IsAnimatedGif ? image.Frames.Count : 1
            };

            ExtractedCount = Keyframes.Count;
            RequestedCount = Keyframes.Count;
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading image: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            Progress = 0;
        }
    }

    public async Task LoadVideoAsync(string path)
    {
        try
        {
            IsLoading = true;
            StatusText = "Loading video...";
            VideoPath = path;
            IsImage = false;
            IsAnimatedGif = false;

            VideoInfo = await _previewService.LoadVideoAsync(path);
            if (VideoInfo != null)
            {
                Duration = VideoInfo.Duration;
                RangeStart = 0;
                RangeEnd = Duration;

                // Load first frame
                await SeekToAsync(0);

                StatusText = $"Loaded: {Path.GetFileName(path)} ({VideoInfo.Width}x{VideoInfo.Height}, {VideoInfo.FrameRate:F2} fps)";

                // Start loading thumbnail timeline in background
                _ = LoadTimelineThumbnailsAsync();
            }
            else
            {
                StatusText = "Failed to load video.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadTimelineThumbnailsAsync()
    {
        if (VideoPath == null || Duration <= 0) return;

        try
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts = new CancellationTokenSource();

            IsLoadingTimeline = true;
            TimelineThumbnails.Clear();
            TimelineProgress = 0;

            var count = 0;
            var total = _thumbnailService.ThumbnailCount;

            // Stream thumbnails and add them incrementally to the UI
            await foreach (var thumb in _thumbnailService.ExtractTimelineStreamAsync(
                VideoPath,
                Duration,
                _thumbnailCts.Token))
            {
                TimelineThumbnails.Add(thumb);
                count++;
                TimelineProgress = (double)count / total;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Timeline error: {ex.Message}");
        }
        finally
        {
            IsLoadingTimeline = false;
        }
    }

    /// <summary>
    /// Seek to position when clicking a timeline thumbnail.
    /// </summary>
    public async Task SeekToThumbnailAsync(TimelineThumbnail thumbnail)
    {
        if (thumbnail != null)
        {
            await SeekToAsync(thumbnail.Timestamp);
        }
    }

    public async Task SeekToAsync(double position)
    {
        if (VideoPath == null || VideoInfo == null) return;

        try
        {
            CurrentPosition = Math.Clamp(position, 0, Duration);
            var frame = await _previewService.GetFrameAtAsync(
                CurrentPosition,
                Math.Min(VideoInfo.Width, 640),
                Math.Min(VideoInfo.Height, 480));

            if (frame != null)
            {
                CurrentFrame = frame;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Seek error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExtractKeyframesAsync()
    {
        if (VideoPath == null || VideoInfo == null) return;

        try
        {
            _extractionCts?.Cancel();
            _extractionCts = new CancellationTokenSource();

            IsLoading = true;
            Keyframes.Clear();
            RequestedCount = TargetKeyframeCount;
            ExtractedCount = 0;
            StatusText = "Extracting keyframes...";
            Progress = 0;

            var settings = new ExtractionSettings
            {
                TargetKeyframeCount = TargetKeyframeCount,
                StartTime = RangeStart > 0 ? RangeStart : null,
                EndTime = RangeEnd < Duration ? RangeEnd : null,
                Strategy = SelectedStrategy,
                SceneThreshold = SceneThreshold
            };

            var progress = new Progress<(string Status, double Progress)>(report =>
            {
                StatusText = report.Status;
                Progress = report.Progress;
            });

            var extractedKeyframes = await _extractionService.ExtractKeyframesAsync(
                VideoPath,
                settings,
                progress,
                _extractionCts.Token);

            ExtractedCount = extractedKeyframes.Count;

            foreach (var kf in extractedKeyframes)
            {
                var thumbnail = await ConvertToThumbnailAsync(kf.Image, 120, 90);
                Keyframes.Add(new KeyframeViewModel
                {
                    Index = kf.Index,
                    Timestamp = kf.Timestamp,
                    Thumbnail = thumbnail,
                    IsSceneBoundary = kf.IsSceneBoundary,
                    Source = kf.Source,
                    OriginalImage = kf.Image.Clone() // Keep original for ASCII preview
                });
            }

            // Auto-select first keyframe
            if (Keyframes.Count > 0)
            {
                SelectedKeyframe = Keyframes[0];
            }

            StatusText = $"Extracted {ExtractedCount} keyframes (requested: {RequestedCount})";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Extraction cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Extraction error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            Progress = 0;
        }
    }

    [RelayCommand]
    private void CancelExtraction()
    {
        _extractionCts?.Cancel();
    }

    [RelayCommand]
    private void NextKeyframe()
    {
        if (Keyframes.Count == 0) return;
        CurrentKeyframeIndex = (CurrentKeyframeIndex + 1) % Keyframes.Count;
        SelectedKeyframe = Keyframes[CurrentKeyframeIndex];
    }

    [RelayCommand]
    private void PreviousKeyframe()
    {
        if (Keyframes.Count == 0) return;
        CurrentKeyframeIndex = (CurrentKeyframeIndex - 1 + Keyframes.Count) % Keyframes.Count;
        SelectedKeyframe = Keyframes[CurrentKeyframeIndex];
    }

    [RelayCommand]
    private async Task TogglePlaybackAsync()
    {
        if (IsPlaying)
        {
            StopPlayback();
        }
        else
        {
            await StartPlaybackAsync();
        }
    }

    private async Task StartPlaybackAsync()
    {
        if (Keyframes.Count == 0) return;

        _playbackCts?.Cancel();
        _playbackCts = new CancellationTokenSource();
        IsPlaying = true;

        try
        {
            while (!_playbackCts.Token.IsCancellationRequested)
            {
                NextKeyframe();
                var delay = (int)(1000 / PlaybackSpeed); // Default ~1 fps
                await Task.Delay(delay, _playbackCts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsPlaying = false;
        }
    }

    private void StopPlayback()
    {
        _playbackCts?.Cancel();
        IsPlaying = false;
    }

    /// <summary>
    /// Transcribe speech from the video and apply subtitles to keyframes.
    /// </summary>
    public async Task TranscribeSpeechAsync()
    {
        if (VideoPath == null) return;

        try
        {
            _transcriptionCts?.Cancel();
            _transcriptionCts = new CancellationTokenSource();

            IsTranscribing = true;
            Subtitles.Clear();
            TranscriptionStatus = "Initializing Whisper...";
            StatusText = "Setting up speech recognition...";

            // Initialize Whisper if not already
            if (_whisperService == null)
            {
                _whisperService = new WhisperService();
            }

            var progress = new Progress<(string Status, double Progress)>(report =>
            {
                TranscriptionStatus = report.Status;
                StatusText = report.Status;
                Progress = report.Progress;
            });

            // Initialize with Base model (good balance of speed/accuracy)
            await _whisperService.InitializeAsync(GgmlType.Base, progress, _transcriptionCts.Token);

            TranscriptionStatus = "Transcribing audio...";

            // Transcribe the selected range
            var segments = await _whisperService.TranscribeVideoAsync(
                VideoPath,
                RangeStart > 0 ? RangeStart : null,
                RangeEnd < Duration ? RangeEnd : null,
                progress,
                _transcriptionCts.Token);

            // Convert to editable subtitles
            for (int i = 0; i < segments.Count; i++)
            {
                Subtitles.Add(EditableSubtitle.FromTranscription(segments[i], i + 1));
            }

            StatusText = $"Transcription complete: {segments.Count} segments. Edit subtitles in the list below.";
            TranscriptionStatus = $"{segments.Count} segments";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Transcription cancelled.";
            TranscriptionStatus = "Cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Transcription error: {ex.Message}";
            TranscriptionStatus = "Error";
        }
        finally
        {
            IsTranscribing = false;
            Progress = 0;
        }
    }

    /// <summary>
    /// Apply subtitles to keyframes and render overlay images.
    /// </summary>
    [RelayCommand]
    private async Task ApplySubtitlesAsync()
    {
        if (Keyframes.Count == 0 || Subtitles.Count == 0) return;

        try
        {
            IsLoading = true;
            StatusText = "Applying subtitles to keyframes...";

            var transcriptionSegments = Subtitles.Select(s => s.ToTranscription()).ToList();

            for (int i = 0; i < Keyframes.Count; i++)
            {
                var kf = Keyframes[i];
                if (kf.OriginalImage == null) continue;

                var segment = SubtitleOverlayService.FindSegmentForTimestamp(transcriptionSegments, kf.Timestamp);

                if (segment != null)
                {
                    // Apply subtitle overlay
                    var overlayedImage = _subtitleOverlayService.RenderSubtitle(kf.OriginalImage, segment.Text);
                    kf.OriginalImage.Dispose();
                    kf.OriginalImage = overlayedImage;

                    // Update thumbnail
                    kf.Thumbnail = await ConvertToThumbnailAsync(overlayedImage.Clone(), 120, 90);
                }

                Progress = (double)(i + 1) / Keyframes.Count;
            }

            // Refresh preview
            UpdateAsciiPreview();

            StatusText = "Subtitles applied to keyframes.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error applying subtitles: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            Progress = 0;
        }
    }

    [RelayCommand]
    private async Task SaveKeyframesAsync(string outputFolder)
    {
        if (Keyframes.Count == 0 || VideoPath == null) return;

        try
        {
            IsLoading = true;
            StatusText = "Saving keyframes...";

            var framesFolder = Path.Combine(outputFolder, "frames");
            Directory.CreateDirectory(framesFolder);

            // Get full-size frames and save
            for (int i = 0; i < Keyframes.Count; i++)
            {
                var kf = Keyframes[i];
                var frame = await _previewService.GetFrameImageAsync(kf.Timestamp);
                if (frame != null)
                {
                    var filename = $"keyframe_{kf.Index:D3}_{kf.Timestamp:F2}s.png";
                    var path = Path.Combine(framesFolder, filename);
                    await frame.SaveAsPngAsync(path);
                    frame.Dispose();
                }
                Progress = (double)(i + 1) / Keyframes.Count;
            }

            StatusText = $"Saved {Keyframes.Count} keyframes to {framesFolder}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            Progress = 0;
        }
    }

    private async Task<Bitmap?> ConvertToThumbnailAsync(Image<Rgba32> image, int maxWidth, int maxHeight)
    {
        try
        {
            // Calculate thumbnail size maintaining aspect ratio
            var scale = Math.Min((double)maxWidth / image.Width, (double)maxHeight / image.Height);
            var thumbWidth = (int)(image.Width * scale);
            var thumbHeight = (int)(image.Height * scale);

            // Resize and convert to Avalonia Bitmap
            image.Mutate(x => x.Resize(thumbWidth, thumbHeight));

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

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }
}

public partial class KeyframeViewModel : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private double _timestamp;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isSceneBoundary;

    [ObservableProperty]
    private string _source = "";

    /// <summary>
    /// Original full-resolution image for ASCII rendering.
    /// </summary>
    public Image<Rgba32>? OriginalImage { get; set; }

    public string TimestampFormatted => TimeSpan.FromSeconds(Timestamp).ToString(@"mm\:ss\.f");
}
