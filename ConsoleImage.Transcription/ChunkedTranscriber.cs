using System.Threading.Channels;
using ConsoleImage.Core.Subtitles;

namespace ConsoleImage.Transcription;

/// <summary>
/// Transcribes video/audio in chunks for efficient streaming subtitle generation.
/// Audio is extracted and transcribed ahead of playback position.
/// Implements ILiveSubtitleProvider for integration with video playback.
/// </summary>
public class ChunkedTranscriber : ILiveSubtitleProvider, IAsyncDisposable
{
    private readonly WhisperTranscriptionService _whisper;
    private readonly string _inputPath;
    private readonly double _chunkDurationSeconds;
    private readonly double _bufferAheadSeconds;
    private readonly double _startTimeOffset;

    private readonly SubtitleTrack _track = new();
    private readonly SemaphoreSlim _transcribeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    private Task? _backgroundTask;
    private double _lastTranscribedEndTime;
    private int _entryIndex;
    private bool _isComplete;
    private string? _tempAudioDir;

    /// <summary>
    /// Event raised when transcription progress changes (for UI feedback).
    /// </summary>
    public event Action<double, string>? OnProgress;

    /// <summary>
    /// The subtitle track being populated with transcribed segments.
    /// </summary>
    public SubtitleTrack Track => _track;

    /// <summary>
    /// Whether all audio has been transcribed.
    /// </summary>
    public bool IsComplete => _isComplete;

    /// <summary>
    /// The furthest timestamp that has been transcribed.
    /// </summary>
    public double LastTranscribedTime => _lastTranscribedEndTime;

    /// <summary>
    /// Event raised when a new subtitle segment is available.
    /// </summary>
    public event Action<SubtitleEntry>? OnSubtitleReady;

    /// <summary>
    /// Create a chunked transcriber for a video/audio file.
    /// </summary>
    /// <param name="inputPath">Path to video or audio file (or URL).</param>
    /// <param name="modelSize">Whisper model size (tiny, base, small, medium, large).</param>
    /// <param name="language">Language code (en, es, etc.) or "auto".</param>
    /// <param name="chunkDurationSeconds">Duration of each audio chunk (default: 30s).</param>
    /// <param name="bufferAheadSeconds">How far ahead to transcribe (default: 60s).</param>
    /// <param name="startTimeOffset">Start transcription from this time (default: 0 = from beginning).</param>
    public ChunkedTranscriber(
        string inputPath,
        string modelSize = "base",
        string language = "en",
        double chunkDurationSeconds = 30.0,
        double bufferAheadSeconds = 60.0,
        double startTimeOffset = 0.0)
    {
        _inputPath = inputPath;
        _chunkDurationSeconds = chunkDurationSeconds;
        _bufferAheadSeconds = bufferAheadSeconds;
        _startTimeOffset = startTimeOffset;
        _lastTranscribedEndTime = startTimeOffset; // Start from the offset position

        _whisper = new WhisperTranscriptionService()
            .WithModel(modelSize)
            .WithLanguage(language);
    }

    /// <summary>
    /// Initialize the transcriber and start background processing.
    /// </summary>
    public async Task StartAsync(
        IProgress<(long downloaded, long total, string status)>? downloadProgress = null,
        CancellationToken ct = default)
    {
        // Initialize whisper (downloads model if needed)
        await _whisper.InitializeAsync(downloadProgress, ct);

        // Create temp directory for audio chunks
        _tempAudioDir = Path.Combine(Path.GetTempPath(), $"consoleimage_chunks_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempAudioDir);

        _track.Language = _whisper.ToString(); // Will be updated with detected language
    }

    /// <summary>
    /// Ensure transcription has processed up to the specified time.
    /// Call this periodically during playback to keep subtitles buffered ahead.
    /// </summary>
    /// <param name="currentPlaybackTime">Current playback position in seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task EnsureTranscribedUpToAsync(double currentPlaybackTime, CancellationToken ct = default)
    {
        if (_isComplete) return;

        var targetTime = currentPlaybackTime + _bufferAheadSeconds;

        // If we're already transcribed past the target, nothing to do
        if (_lastTranscribedEndTime >= targetTime) return;

        await _transcribeLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_lastTranscribedEndTime >= targetTime) return;

            // Transcribe chunks until we reach the target
            while (_lastTranscribedEndTime < targetTime && !_isComplete)
            {
                await TranscribeNextChunkAsync(ct);
            }
        }
        finally
        {
            _transcribeLock.Release();
        }
    }

    /// <summary>
    /// Wait until transcription has caught up to at least the given playback time.
    /// Use this to prevent subtitles from getting out of sync - playback pauses if needed.
    /// </summary>
    /// <param name="playbackTime">The playback timestamp we need subtitles for.</param>
    /// <param name="timeoutMs">Maximum time to wait before giving up (0 = wait forever).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if transcription is ready, false if timed out or complete but not at target.</returns>
    public async Task<bool> WaitForTranscriptionAsync(
        double playbackTime,
        int timeoutMs = 30000,
        CancellationToken ct = default)
    {
        // If complete, we have all subtitles (or as many as we'll get)
        if (_isComplete) return true;

        // Already transcribed past this point
        if (_lastTranscribedEndTime >= playbackTime) return true;

        // Need to wait for transcription to catch up
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var maxWait = timeoutMs > 0 ? timeoutMs : int.MaxValue;

        while (_lastTranscribedEndTime < playbackTime && !_isComplete)
        {
            // Check timeout
            if (sw.ElapsedMilliseconds >= maxWait)
                return false;

            // If background transcription is running, just wait
            if (_backgroundTask != null)
            {
                await Task.Delay(50, ct);
                continue;
            }

            // No background task - do on-demand transcription
            await EnsureTranscribedUpToAsync(playbackTime, ct);
        }

        return true;
    }

    /// <summary>
    /// Check if we have subtitles ready for the given time without blocking.
    /// </summary>
    public bool HasSubtitlesReadyFor(double playbackTime)
    {
        return _isComplete || _lastTranscribedEndTime >= playbackTime;
    }

    /// <summary>
    /// Start background transcription that runs ahead of playback.
    /// </summary>
    public void StartBackgroundTranscription()
    {
        if (_backgroundTask != null) return;

        _backgroundTask = Task.Run(async () =>
        {
            var consecutiveErrors = 0;
            const int maxConsecutiveErrors = 3;

            try
            {
                while (!_isComplete && !_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await TranscribeNextChunkAsync(_cts.Token);
                        consecutiveErrors = 0; // Reset on success

                        // Small delay between chunks to not overwhelm CPU
                        await Task.Delay(100, _cts.Token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        consecutiveErrors++;
                        if (consecutiveErrors >= maxConsecutiveErrors)
                        {
                            // Too many errors in a row - stop background transcription
                            Console.Error.WriteLine($"\nBackground transcription stopped after {maxConsecutiveErrors} consecutive errors");
                            break;
                        }

                        // Wait a bit before retrying
                        await Task.Delay(1000, _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
        });
    }

    /// <summary>
    /// Transcribe the entire input synchronously (for batch mode).
    /// </summary>
    public async Task TranscribeAllAsync(
        IProgress<(int segments, double seconds, string status)>? progress = null,
        CancellationToken ct = default)
    {
        while (!_isComplete)
        {
            await TranscribeNextChunkAsync(ct);
            progress?.Report((_track.Count, _lastTranscribedEndTime, $"Transcribed {_lastTranscribedEndTime:F1}s"));
        }
    }

    private async Task TranscribeNextChunkAsync(CancellationToken ct)
    {
        if (_isComplete) return;

        var startTime = _lastTranscribedEndTime;
        var endTime = startTime + _chunkDurationSeconds;

        // Report progress - extracting audio
        OnProgress?.Invoke(startTime, $"Extracting audio at {FormatTime(startTime)}...");

        // Extract audio chunk
        var chunkPath = Path.Combine(_tempAudioDir!, $"chunk_{startTime:F0}_{endTime:F0}.wav");

        try
        {
            var (success, actualDuration) = await ExtractAudioChunkAsync(
                _inputPath, chunkPath, startTime, _chunkDurationSeconds, ct);

            if (!success || actualDuration <= 0)
            {
                // No more audio or extraction failed - we're done
                if (_lastTranscribedEndTime == _startTimeOffset)
                {
                    // Failed on first chunk - let user know
                    Console.Error.WriteLine($"\nCould not extract audio from {FormatTime(startTime)}. The video may not have audio at this position.");
                }
                _isComplete = true;
                return;
            }

            // Ensure we have enough audio to transcribe (at least 0.5 seconds)
            if (actualDuration < 0.5)
            {
                Console.Error.WriteLine($"\nAudio chunk too short at {FormatTime(startTime)} ({actualDuration:F1}s) - skipping.");
                _lastTranscribedEndTime = startTime + _chunkDurationSeconds;
                return;
            }

            // Report progress - transcribing
            OnProgress?.Invoke(startTime, $"Transcribing {FormatTime(startTime)} - {FormatTime(startTime + actualDuration)}...");

            try
            {
                // Verify the audio file is valid before sending to Whisper
                if (!File.Exists(chunkPath))
                {
                    Console.Error.WriteLine($"\nAudio file missing at {FormatTime(startTime)} - skipping.");
                    _lastTranscribedEndTime = startTime + actualDuration;
                    return;
                }

                // Transcribe the chunk
                var result = await _whisper.TranscribeFileAsync(chunkPath, null, ct);

                // Convert segments to subtitle entries with time offset
                foreach (var segment in result.Segments)
                {
                    // Skip empty or whitespace-only segments
                    if (string.IsNullOrWhiteSpace(segment.Text))
                        continue;

                    var entry = new SubtitleEntry
                    {
                        Index = ++_entryIndex,
                        StartTime = TimeSpan.FromSeconds(startTime + segment.StartSeconds),
                        EndTime = TimeSpan.FromSeconds(startTime + segment.EndSeconds),
                        Text = segment.Text.Trim(),
                        SpeakerId = segment.SpeakerId  // Pass through speaker ID for diarization
                    };

                    _track.AddEntry(entry);
                    OnSubtitleReady?.Invoke(entry);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log the error but continue processing - audio may have silent segments
                // or other issues that cause transcription to fail for a chunk
                Console.Error.WriteLine($"\nTranscription error at {FormatTime(startTime)}: {ex.Message}");
            }

            _lastTranscribedEndTime = startTime + actualDuration;

            // If we got less than requested, we've reached the end
            if (actualDuration < _chunkDurationSeconds - 0.5)
            {
                _isComplete = true;
            }
        }
        finally
        {
            // Cleanup chunk file
            if (File.Exists(chunkPath))
            {
                try { File.Delete(chunkPath); } catch { }
            }
        }
    }

    private static async Task<(bool success, double duration)> ExtractAudioChunkAsync(
        string inputPath,
        string outputPath,
        double startTime,
        double duration,
        CancellationToken ct)
    {
        var ffmpegPath = FindFFmpeg();
        if (ffmpegPath == null)
        {
            throw new InvalidOperationException("FFmpeg not found. Install FFmpeg to use transcription.");
        }

        // Build FFmpeg arguments
        var isUrl = inputPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    inputPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    inputPath.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase);

        var inputArgs = isUrl
            ? $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5"
            : "";

        // -ss before -i for fast seeking (input seeking)
        // -t for duration limit
        // -vn to skip video, -ar 16000 -ac 1 for Whisper format
        var args = $"{inputArgs} -ss {startTime:F3} -i \"{inputPath}\" -t {duration:F3} -vn -ar 16000 -ac 1 -acodec pcm_s16le -y \"{outputPath}\"";

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
            return (false, 0);

        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        // Timeout for chunk extraction
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(true);
            return (false, 0);
        }

        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            return (false, 0);
        }

        // Get actual duration of extracted audio
        var fileInfo = new FileInfo(outputPath);
        // WAV header is 44 bytes, need at least that plus some audio data
        // Minimum 0.5 seconds of audio = 16000 samples/sec * 2 bytes * 0.5 = 16000 bytes
        if (fileInfo.Length < 16044) // 44 byte header + 16000 bytes minimum audio
        {
            return (false, 0);
        }

        // Calculate duration from file size (16kHz, 16-bit mono = 32000 bytes/second)
        // WAV header is 44 bytes, subtract that from total
        var audioBytes = fileInfo.Length - 44;
        var actualDuration = audioBytes / 32000.0;
        return (true, Math.Max(0, actualDuration));
    }

    private static string? FindFFmpeg()
    {
        var candidates = new[]
        {
            "ffmpeg",
            "ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "consoleimage", "ffmpeg", "ffmpeg.exe"),
            "/usr/bin/ffmpeg",
            "/usr/local/bin/ffmpeg"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(1000);
                    if (process.ExitCode == 0)
                        return candidate;
                }
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// Format time in human-readable format.
    /// </summary>
    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0 ? $"{ts:hh\\:mm\\:ss}" : $"{ts:mm\\:ss}";
    }

    /// <summary>
    /// Convert all transcribed segments to VTT format.
    /// </summary>
    public string ToVtt()
    {
        return VttFormatter.Format(_track.Entries.Select(e => new TranscriptSegment
        {
            StartSeconds = e.StartTime.TotalSeconds,
            EndSeconds = e.EndTime.TotalSeconds,
            Text = e.Text
        }));
    }

    /// <summary>
    /// Save transcription to file.
    /// </summary>
    public async Task SaveAsync(string outputPath, CancellationToken ct = default)
    {
        var isVtt = outputPath.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase);

        var segments = _track.Entries.Select(e => new TranscriptSegment
        {
            StartSeconds = e.StartTime.TotalSeconds,
            EndSeconds = e.EndTime.TotalSeconds,
            Text = e.Text
        });

        if (isVtt)
        {
            await VttFormatter.WriteAsync(outputPath, segments, false, ct);
        }
        else
        {
            await SrtFormatter.WriteAsync(outputPath, segments, false, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_backgroundTask != null)
        {
            try { await _backgroundTask; } catch { }
        }

        _whisper.Dispose();
        _transcribeLock.Dispose();
        _cts.Dispose();

        // Cleanup temp directory
        if (_tempAudioDir != null && Directory.Exists(_tempAudioDir))
        {
            try { Directory.Delete(_tempAudioDir, true); } catch { }
        }
    }
}
