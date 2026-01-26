using System.Diagnostics;
using ConsoleImage.Core.Subtitles;
using static ConsoleImage.Core.Subtitles.SubtitleSplitter;

namespace ConsoleImage.Transcription;

/// <summary>
///     Transcribes video/audio in chunks for efficient streaming subtitle generation.
///     Audio is extracted and transcribed ahead of playback position.
///     Implements ILiveSubtitleProvider for integration with video playback.
/// </summary>
public class ChunkedTranscriber : ILiveSubtitleProvider, IAsyncDisposable
{
    // Overlap between chunks to prevent cutting words at boundaries
    private const double OverlapSeconds = 2.0;
    private const int MaxExtractionRetries = 3;
    private readonly double _bufferAheadSeconds;
    private readonly double _chunkDurationSeconds;
    private readonly CancellationTokenSource _cts = new();
    private readonly bool _enhanceAudio;
    private readonly string _inputPath;
    private readonly double _startTimeOffset;

    private readonly SemaphoreSlim _transcribeLock = new(1, 1);
    private readonly WhisperTranscriptionService _whisper;

    private Task? _backgroundTask;
    private int _entryIndex;

    private int _extractionFailures;
    private string? _lastChunkPrompt;
    private string? _tempAudioDir;

    /// <summary>
    ///     Create a chunked transcriber for a video/audio file.
    /// </summary>
    /// <param name="inputPath">Path to video or audio file (or URL).</param>
    /// <param name="modelSize">Whisper model size (tiny, base, small, medium, large).</param>
    /// <param name="language">Language code (en, es, etc.) or "auto".</param>
    /// <param name="chunkDurationSeconds">Duration of each audio chunk (default: 30s).</param>
    /// <param name="bufferAheadSeconds">How far ahead to transcribe (default: 60s).</param>
    /// <param name="startTimeOffset">Start transcription from this time (default: 0 = from beginning).</param>
    /// <param name="enhanceAudio">Apply FFmpeg audio preprocessing filters for better speech recognition.</param>
    public ChunkedTranscriber(
        string inputPath,
        string modelSize = "base",
        string language = "en",
        double chunkDurationSeconds = 30.0,
        double bufferAheadSeconds = 60.0,
        double startTimeOffset = 0.0,
        bool enhanceAudio = true)
    {
        _inputPath = inputPath;
        _chunkDurationSeconds = chunkDurationSeconds;
        _bufferAheadSeconds = bufferAheadSeconds;
        _startTimeOffset = startTimeOffset;
        _enhanceAudio = enhanceAudio;
        LastTranscribedTime = startTimeOffset; // Start from the offset position

        _whisper = new WhisperTranscriptionService()
            .WithModel(modelSize)
            .WithLanguage(language);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_backgroundTask != null)
            try
            {
                await _backgroundTask;
            }
            catch
            {
            }

        _whisper.Dispose();
        _transcribeLock.Dispose();
        _cts.Dispose();

        // Cleanup temp directory
        if (_tempAudioDir != null && Directory.Exists(_tempAudioDir))
            try
            {
                Directory.Delete(_tempAudioDir, true);
            }
            catch
            {
            }
    }

    /// <summary>
    ///     The subtitle track being populated with transcribed segments.
    /// </summary>
    public SubtitleTrack Track { get; } = new();

    /// <summary>
    ///     Whether all audio has been transcribed.
    /// </summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    ///     The furthest timestamp that has been transcribed.
    /// </summary>
    public double LastTranscribedTime { get; private set; }

    /// <summary>
    ///     Ensure transcription has processed up to the specified time.
    ///     Call this periodically during playback to keep subtitles buffered ahead.
    /// </summary>
    /// <param name="currentPlaybackTime">Current playback position in seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task EnsureTranscribedUpToAsync(double currentPlaybackTime, CancellationToken ct = default)
    {
        if (IsComplete) return;

        var targetTime = currentPlaybackTime + _bufferAheadSeconds;

        // If we're already transcribed past the target, nothing to do
        if (LastTranscribedTime >= targetTime) return;

        await _transcribeLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (LastTranscribedTime >= targetTime) return;

            // Transcribe chunks until we reach the target
            while (LastTranscribedTime < targetTime && !IsComplete) await TranscribeNextChunkAsync(ct);
        }
        finally
        {
            _transcribeLock.Release();
        }
    }

    /// <summary>
    ///     Wait until transcription has caught up to at least the given playback time.
    ///     Use this to prevent subtitles from getting out of sync - playback pauses if needed.
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
        if (IsComplete) return true;

        // Already transcribed past this point
        if (LastTranscribedTime >= playbackTime) return true;

        // Need to wait for transcription to catch up
        var sw = Stopwatch.StartNew();
        var maxWait = timeoutMs > 0 ? timeoutMs : int.MaxValue;

        while (LastTranscribedTime < playbackTime && !IsComplete)
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
    ///     Check if we have subtitles ready for the given time without blocking.
    /// </summary>
    public bool HasSubtitlesReadyFor(double playbackTime)
    {
        return IsComplete || LastTranscribedTime >= playbackTime;
    }

    /// <summary>
    ///     Event raised when transcription progress changes (for UI feedback).
    /// </summary>
    public event Action<double, string>? OnProgress;

    /// <summary>
    ///     Event raised when a new subtitle segment is available.
    /// </summary>
    public event Action<SubtitleEntry>? OnSubtitleReady;

    /// <summary>
    ///     Initialize the transcriber and start background processing.
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

        Track.Language = _whisper.ToString(); // Will be updated with detected language
    }

    /// <summary>
    ///     Start background transcription that runs ahead of playback.
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
                while (!IsComplete && !_cts.Token.IsCancellationRequested)
                    try
                    {
                        await _transcribeLock.WaitAsync(_cts.Token);
                        try
                        {
                            await TranscribeNextChunkAsync(_cts.Token);
                        }
                        finally
                        {
                            _transcribeLock.Release();
                        }

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
                            OnProgress?.Invoke(LastTranscribedTime, "Transcription paused: too many errors");
                            break;
                        }

                        // Wait a bit before retrying
                        await Task.Delay(1000, _cts.Token);
                    }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
        });
    }

    /// <summary>
    ///     Transcribe the entire input synchronously (for batch mode).
    /// </summary>
    public async Task TranscribeAllAsync(
        IProgress<(int segments, double seconds, string status)>? progress = null,
        CancellationToken ct = default)
    {
        while (!IsComplete)
        {
            await TranscribeNextChunkAsync(ct);
            progress?.Report((Track.Count, LastTranscribedTime, $"Transcribed {LastTranscribedTime:F1}s"));
        }
    }

    private async Task TranscribeNextChunkAsync(CancellationToken ct)
    {
        if (IsComplete) return;

        var startTime = LastTranscribedTime;
        var endTime = startTime + _chunkDurationSeconds;

        // Report progress - extracting audio
        OnProgress?.Invoke(startTime, $"Extracting audio at {FormatTime(startTime)}...");

        // Overlapping chunks: extract 2s earlier to avoid cutting words at boundaries.
        // First chunk has no overlap. Segments from the overlap zone are discarded below.
        var isFirstChunk = startTime <= _startTimeOffset;
        var extractStart = isFirstChunk ? startTime : startTime - OverlapSeconds;
        var extractDuration = isFirstChunk ? _chunkDurationSeconds : _chunkDurationSeconds + OverlapSeconds;

        // Extract audio chunk
        var chunkPath = Path.Combine(_tempAudioDir!, $"chunk_{startTime:F0}_{endTime:F0}.wav");

        try
        {
            var (success, actualDuration) = await ExtractAudioChunkAsync(
                _inputPath, chunkPath, extractStart, extractDuration, _enhanceAudio, ct);

            if (!success || actualDuration <= 0)
            {
                _extractionFailures++;

                // Only mark complete if this is the first chunk (truly no audio)
                // or if we've had multiple consecutive failures
                if (LastTranscribedTime == _startTimeOffset && _extractionFailures >= MaxExtractionRetries)
                {
                    OnProgress?.Invoke(startTime, "No audio found at start position");
                    IsComplete = true;
                    return;
                }

                // For non-first chunks, skip ahead and try the next chunk
                // This handles temporary extraction issues or silent sections
                if (_extractionFailures >= MaxExtractionRetries)
                {
                    OnProgress?.Invoke(startTime, $"Skipping chunk after {_extractionFailures} failures");
                    LastTranscribedTime = startTime + _chunkDurationSeconds;
                    _extractionFailures = 0; // Reset for next chunk
                }

                return;
            }

            // Reset failure counter on success
            _extractionFailures = 0;

            // Ensure we have enough audio to transcribe (at least 0.5 seconds)
            if (actualDuration < 0.5)
            {
                // Chunk too short - skip ahead
                LastTranscribedTime = startTime + _chunkDurationSeconds;
                return;
            }

            // Energy-based silence detection: skip chunks with no speech energy
            // Saves significant CPU by avoiding Whisper processing on silent sections
            if (!HasSpeechEnergy(chunkPath))
            {
                OnProgress?.Invoke(startTime, $"Skipping silent chunk at {FormatTime(startTime)}");
                LastTranscribedTime = startTime + actualDuration - (isFirstChunk ? 0 : OverlapSeconds);
                return;
            }

            // Report progress - transcribing
            OnProgress?.Invoke(startTime,
                $"Transcribing {FormatTime(startTime)} - {FormatTime(startTime + actualDuration)}...");

            try
            {
                // Verify the audio file is valid before sending to Whisper
                if (!File.Exists(chunkPath))
                {
                    LastTranscribedTime = startTime + actualDuration;
                    return;
                }

                // Transcribe with context from previous chunk for continuity
                var result = await _whisper.TranscribeFileAsync(chunkPath, null, ct, _lastChunkPrompt);

                // Filter hallucinations and convert segments to subtitle entries
                var chunkTexts = new List<string>();
                string? lastEmittedText = null;
                var consecutiveRepeats = 0;

                foreach (var segment in result.Segments)
                {
                    // Skip empty or whitespace-only segments
                    if (string.IsNullOrWhiteSpace(segment.Text))
                        continue;

                    var text = segment.Text.Trim();

                    // Calculate absolute timestamp from extraction start
                    var absStart = extractStart + segment.StartSeconds;
                    var absEnd = extractStart + segment.EndSeconds;

                    // Discard segments from the overlap zone (already covered by previous chunk)
                    if (!isFirstChunk && absEnd <= startTime)
                        continue;

                    // --- Repetition hallucination filter ---
                    // Detect consecutive identical segments (e.g., "[INDISTINCT CHATTER]" x50)
                    if (string.Equals(text, lastEmittedText, StringComparison.OrdinalIgnoreCase))
                    {
                        consecutiveRepeats++;
                        if (consecutiveRepeats >= 2) // Allow 1 repeat, filter 3+
                            continue;
                    }
                    else
                    {
                        consecutiveRepeats = 0;
                    }

                    // Detect internal repetition loops (e.g., same phrase repeated within segment)
                    if (IsRepetitiveText(text))
                        continue;

                    lastEmittedText = text;
                    chunkTexts.Add(text);

                    var rawEntry = new SubtitleEntry
                    {
                        StartTime = TimeSpan.FromSeconds(absStart),
                        EndTime = TimeSpan.FromSeconds(absEnd),
                        Text = text,
                        SpeakerId = segment.SpeakerId
                    };

                    // Split long segments into readable subtitle entries.
                    // Whisper sometimes produces 200+ char segments spanning 20-30s;
                    // this splits at sentence boundaries with proportional timing.
                    var splitEntries = Split(rawEntry, ref _entryIndex);
                    foreach (var splitEntry in splitEntries)
                    {
                        Track.AddEntry(splitEntry);
                        OnSubtitleReady?.Invoke(splitEntry);
                    }
                }

                // Update prompt for next chunk: last ~200 chars of transcribed text
                if (chunkTexts.Count > 0)
                {
                    var combined = string.Join(" ", chunkTexts);
                    _lastChunkPrompt = combined.Length > 200
                        ? combined[^200..]
                        : combined;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Silent error - continue processing. Audio may have silent segments
                // or other issues that cause transcription to fail for a chunk
                OnProgress?.Invoke(startTime, $"Chunk error: {ex.Message[..Math.Min(30, ex.Message.Length)]}");
            }

            // Advance past the actual new content (not the overlap)
            var newContentDuration = actualDuration - (isFirstChunk ? 0 : OverlapSeconds);
            LastTranscribedTime = startTime + Math.Max(newContentDuration, _chunkDurationSeconds * 0.3);

            // Only mark complete if we got a very short chunk (likely at end of media)
            if (newContentDuration < _chunkDurationSeconds * 0.3)
            {
                IsComplete = true;
                OnProgress?.Invoke(LastTranscribedTime, "Transcription complete");
            }
        }
        finally
        {
            // Cleanup chunk file
            if (File.Exists(chunkPath))
                try
                {
                    File.Delete(chunkPath);
                }
                catch
                {
                }
        }
    }

    // Delegate to shared implementation in WhisperTranscriptionService
    private static bool IsRepetitiveText(string text)
    {
        return WhisperTranscriptionService.IsRepetitiveText(text);
    }

    private static async Task<(bool success, double duration)> ExtractAudioChunkAsync(
        string inputPath,
        string outputPath,
        double startTime,
        double duration,
        bool enhanceAudio,
        CancellationToken ct)
    {
        var ffmpegPath = FindFFmpeg();
        if (ffmpegPath == null)
            throw new InvalidOperationException("FFmpeg not found. Install FFmpeg to use transcription.");

        // Build FFmpeg arguments
        var isUrl = inputPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    inputPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    inputPath.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase);

        var inputArgs = isUrl
            ? "-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5"
            : "";

        // Speech-optimized audio filters for better Whisper recognition:
        // highpass=f=200    - Remove low-freq rumble/hum below 200Hz
        // lowpass=f=3000    - Remove high-freq noise above 3kHz (speech is 300-3000Hz)
        // NOTE: loudnorm and afftdn removed — loudnorm requires two-pass (slow),
        // afftdn distorts clean audio. These simple filters are fast and safe.
        var audioFilter = enhanceAudio
            ? "-af \"highpass=f=200,lowpass=f=3000\" "
            : "";

        // -ss before -i for fast seeking (input seeking)
        // -t for duration limit
        // -vn to skip video, -ar 16000 -ac 1 for Whisper format
        var args =
            $"{inputArgs} -ss {startTime:F3} -i \"{inputPath}\" -t {duration:F3} -vn {audioFilter}-ar 16000 -ac 1 -acodec pcm_s16le -y \"{outputPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
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

        // Ensure stderr is fully drained before checking results
        await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0 || !File.Exists(outputPath)) return (false, 0);

        // Get actual duration of extracted audio
        var fileInfo = new FileInfo(outputPath);
        // WAV header is 44 bytes, need at least that plus some audio data
        // Minimum 0.5 seconds of audio = 16000 samples/sec * 2 bytes * 0.5 = 16000 bytes
        if (fileInfo.Length < 16044) // 44 byte header + 16000 bytes minimum audio
            return (false, 0);

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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "consoleimage",
                "ffmpeg", "ffmpeg.exe"),
            "/usr/bin/ffmpeg",
            "/usr/local/bin/ffmpeg"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(1000);
                    if (process.ExitCode == 0)
                        return candidate;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    /// <summary>
    ///     Check if a WAV file contains enough audio energy to be worth transcribing.
    ///     Reads PCM samples and calculates RMS energy. Skips silent chunks to save CPU.
    /// </summary>
    private static bool HasSpeechEnergy(string wavPath, float rmsThreshold = 0.01f)
    {
        var fileInfo = new FileInfo(wavPath);
        if (fileInfo.Length < 1000) return false; // Too small to contain speech

        try
        {
            using var stream = File.OpenRead(wavPath);
            stream.Seek(44, SeekOrigin.Begin); // Skip WAV header

            long sumSquares = 0;
            var sampleCount = 0;
            var buffer = new byte[8192]; // Read in 8KB blocks for performance
            int bytesRead;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) >= 2)
            {
                // Process pairs of bytes as Int16 samples (little-endian PCM)
                var sampleBytes = bytesRead & ~1; // Round down to even
                for (var i = 0; i < sampleBytes; i += 2)
                {
                    var sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                    sumSquares += (long)sample * sample;
                    sampleCount++;
                }
            }

            if (sampleCount == 0) return false;

            // RMS normalized to 0-1 range (Int16 max = 32767)
            var rms = Math.Sqrt((double)sumSquares / sampleCount) / 32767.0;
            return rms > rmsThreshold;
        }
        catch
        {
            // If we can't read the file, assume it has speech (don't skip)
            return true;
        }
    }

    /// <summary>
    ///     Format time in human-readable format.
    /// </summary>
    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0 ? $"{ts:hh\\:mm\\:ss}" : $"{ts:mm\\:ss}";
    }

    /// <summary>
    ///     Convert all transcribed segments to VTT format.
    /// </summary>
    public string ToVtt()
    {
        return VttFormatter.Format(Track.Entries.Select(e => new TranscriptSegment
        {
            StartSeconds = e.StartTime.TotalSeconds,
            EndSeconds = e.EndTime.TotalSeconds,
            Text = e.Text
        }));
    }

    /// <summary>
    ///     Save transcription to file.
    /// </summary>
    public async Task SaveAsync(string outputPath, CancellationToken ct = default)
    {
        var isVtt = outputPath.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase);

        var segments = Track.Entries.Select(e => new TranscriptSegment
        {
            StartSeconds = e.StartTime.TotalSeconds,
            EndSeconds = e.EndTime.TotalSeconds,
            Text = e.Text
        });

        if (isVtt)
            await VttFormatter.WriteAsync(outputPath, segments, false, ct);
        else
            await SrtFormatter.WriteAsync(outputPath, segments, false, ct);
    }
}