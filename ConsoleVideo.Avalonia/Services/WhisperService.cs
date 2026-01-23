using Whisper.net;
using Whisper.net.Ggml;

namespace ConsoleVideo.Avalonia.Services;

/// <summary>
/// Service for speech recognition using Whisper.net.
/// Handles automatic model download and transcription.
/// </summary>
public class WhisperService : IDisposable
{
    private WhisperProcessor? _processor;
    private string? _modelPath;
    private static readonly string ModelsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ConsoleVideo", "whisper-models");

    /// <summary>
    /// Available model sizes with their approximate sizes.
    /// </summary>
    public static readonly Dictionary<GgmlType, string> ModelSizes = new()
    {
        { GgmlType.Tiny, "~75 MB" },
        { GgmlType.Base, "~142 MB" },
        { GgmlType.Small, "~466 MB" },
        { GgmlType.Medium, "~1.5 GB" },
        { GgmlType.LargeV3, "~3 GB" }
    };

    /// <summary>
    /// Check if a model is already downloaded.
    /// </summary>
    public static bool IsModelDownloaded(GgmlType modelType)
    {
        var modelPath = GetModelPath(modelType);
        return File.Exists(modelPath);
    }

    /// <summary>
    /// Get the path where a model would be stored.
    /// </summary>
    public static string GetModelPath(GgmlType modelType)
    {
        Directory.CreateDirectory(ModelsFolder);
        return Path.Combine(ModelsFolder, $"ggml-{modelType.ToString().ToLowerInvariant()}.bin");
    }

    /// <summary>
    /// Download a Whisper model from Hugging Face.
    /// </summary>
    public async Task DownloadModelAsync(
        GgmlType modelType,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var modelPath = GetModelPath(modelType);

        if (File.Exists(modelPath))
        {
            progress?.Report(("Model already downloaded", 1.0));
            return;
        }

        progress?.Report(($"Downloading {modelType} model ({ModelSizes[modelType]})...", 0.0));

        try
        {
            using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(modelType);

            // Get stream length for progress if available
            var totalBytes = modelStream.CanSeek ? modelStream.Length : -1;
            var downloadedBytes = 0L;

            using var fileStream = File.Create(modelPath);
            var buffer = new byte[81920]; // 80KB buffer
            int bytesRead;

            while ((bytesRead = await modelStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    var progressPct = (double)downloadedBytes / totalBytes;
                    progress?.Report(($"Downloading {modelType} model... {progressPct:P0}", progressPct));
                }
            }

            progress?.Report(("Download complete", 1.0));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Clean up partial download
            if (File.Exists(modelPath))
            {
                try { File.Delete(modelPath); } catch { }
            }
            throw;
        }
    }

    /// <summary>
    /// Initialize the Whisper processor with a model.
    /// </summary>
    public async Task InitializeAsync(
        GgmlType modelType = GgmlType.Base,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        _modelPath = GetModelPath(modelType);

        if (!File.Exists(_modelPath))
        {
            await DownloadModelAsync(modelType, progress, ct);
        }

        progress?.Report(("Loading model...", 0.9));

        var factory = WhisperFactory.FromPath(_modelPath);
        _processor = factory.CreateBuilder()
            .WithLanguage("auto") // Auto-detect language
            .Build();

        progress?.Report(("Ready", 1.0));
    }

    /// <summary>
    /// Transcribe audio from a video file.
    /// Extracts audio using FFmpeg first.
    /// </summary>
    public async Task<List<TranscriptionSegment>> TranscribeVideoAsync(
        string videoPath,
        double? startTime = null,
        double? endTime = null,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        if (_processor == null)
        {
            throw new InvalidOperationException("Whisper not initialized. Call InitializeAsync first.");
        }

        // Extract audio to temp WAV file
        var tempWav = Path.Combine(Path.GetTempPath(), $"whisper_{Guid.NewGuid():N}.wav");

        try
        {
            progress?.Report(("Extracting audio...", 0.1));

            await ExtractAudioAsync(videoPath, tempWav, startTime, endTime, ct);

            progress?.Report(("Transcribing...", 0.3));

            var segments = new List<TranscriptionSegment>();

            // ProcessAsync requires a Stream
            await using var audioStream = File.OpenRead(tempWav);
            await foreach (var segment in _processor.ProcessAsync(audioStream, ct))
            {
                segments.Add(new TranscriptionSegment
                {
                    StartTime = segment.Start.TotalSeconds + (startTime ?? 0),
                    EndTime = segment.End.TotalSeconds + (startTime ?? 0),
                    Text = segment.Text.Trim()
                });

                // Report progress (estimate based on segments)
                var progressPct = 0.3 + (0.7 * Math.Min(1.0, segments.Count / 100.0));
                progress?.Report(($"Transcribed {segments.Count} segments...", progressPct));
            }

            progress?.Report(($"Transcription complete: {segments.Count} segments", 1.0));
            return segments;
        }
        finally
        {
            // Clean up temp file
            try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
        }
    }

    /// <summary>
    /// Extract audio from video to WAV format using FFmpeg.
    /// </summary>
    private static async Task ExtractAudioAsync(
        string videoPath,
        string outputPath,
        double? startTime,
        double? endTime,
        CancellationToken ct)
    {
        var ffmpegPath = await ConsoleImage.Video.Core.FFmpegProvider.GetFFmpegPathAsync(ct: ct);

        var args = new List<string> { "-y" }; // Overwrite output

        if (startTime.HasValue)
            args.AddRange(new[] { "-ss", startTime.Value.ToString("F3") });

        args.AddRange(new[] { "-i", $"\"{videoPath}\"" });

        if (endTime.HasValue && startTime.HasValue)
            args.AddRange(new[] { "-t", (endTime.Value - startTime.Value).ToString("F3") });
        else if (endTime.HasValue)
            args.AddRange(new[] { "-t", endTime.Value.ToString("F3") });

        // Output: 16kHz mono WAV (Whisper's expected format)
        args.AddRange(new[] { "-ar", "16000", "-ac", "1", "-f", "wav", $"\"{outputPath}\"" });

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new Exception($"FFmpeg audio extraction failed: {error}");
        }
    }

    public void Dispose()
    {
        _processor?.Dispose();
        _processor = null;
    }
}

/// <summary>
/// A segment of transcribed speech.
/// </summary>
public record TranscriptionSegment
{
    /// <summary>Start time in seconds from video start.</summary>
    public double StartTime { get; init; }

    /// <summary>End time in seconds from video start.</summary>
    public double EndTime { get; init; }

    /// <summary>Transcribed text.</summary>
    public required string Text { get; init; }

    /// <summary>Formatted time range.</summary>
    public string TimeRange => $"{FormatTime(StartTime)} - {FormatTime(EndTime)}";

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.ToString(@"mm\:ss\.f");
    }
}
