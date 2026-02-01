using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace ConsoleImage.Transcription;

/// <summary>
///     Whisper transcription service using Whisper.NET.
///     Supports both batch and streaming transcription.
///     Cross-platform compatible.
/// </summary>
public sealed class WhisperTranscriptionService : IDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _disposed;
    private string _language = "auto";

    private string _modelSize = "base";
    private int _threads = Math.Max(1, Environment.ProcessorCount / 2);

    // Cached processor to avoid repeated create/dispose cycles.
    // Whisper.cpp's ggml_init() asserts no prior uncaught exceptions;
    // disposing and recreating processors can leave GGML in a bad state.
    private WhisperProcessor? _cachedProcessor;
    private readonly object _processorLock = new();

    /// <summary>
    ///     Get the loaded WhisperFactory for creating processors.
    ///     Returns null if not yet initialized.
    ///     Used by RealtimeTranscriber to create processors without reflection.
    /// </summary>
    internal WhisperFactory? Factory { get; private set; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cachedProcessor?.Dispose();
        _cachedProcessor = null;
        Factory?.Dispose();
        _initLock.Dispose();
    }

    /// <summary>
    ///     Check if Whisper transcription is available (model can be downloaded).
    /// </summary>
    public static bool IsAvailable()
    {
        // Whisper.NET is always available as long as the package is included
        // The model will be downloaded on first use
        return true;
    }

    /// <summary>
    ///     Configure model size (tiny, base, small, medium, large).
    /// </summary>
    public WhisperTranscriptionService WithModel(string modelSize)
    {
        _modelSize = modelSize;
        return this;
    }

    /// <summary>
    ///     Configure language (en, es, ja, etc. or "auto" for detection).
    /// </summary>
    public WhisperTranscriptionService WithLanguage(string language)
    {
        _language = language;
        return this;
    }

    /// <summary>
    ///     Configure number of CPU threads.
    /// </summary>
    public WhisperTranscriptionService WithThreads(int threads)
    {
        _threads = Math.Max(1, threads);
        return this;
    }

    /// <summary>
    ///     Ensure model is downloaded and loaded.
    /// </summary>
    public async Task InitializeAsync(
        IProgress<(long downloaded, long total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        if (Factory != null) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (Factory != null) return;

            // Report CPU capabilities for diagnostics (AVX mismatch = segfault on Linux)
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                var avxStatus = WhisperRuntimeDownloader.IsAvxSupported() ? "supported" : "NOT supported";
                progress?.Report((0, 0, $"CPU: x64, AVX {avxStatus}"));
                if (WhisperRuntimeDownloader.NeedsNoAvxVariant())
                    progress?.Report((0, 0, "Using NoAvx runtime variant (CPU lacks AVX)"));
            }
            else
            {
                progress?.Report((0, 0, $"CPU: {RuntimeInformation.ProcessArchitecture}"));
            }

            // Step 1: Configure runtime search path early (adds library dirs to PATH)
            WhisperRuntimeDownloader.ConfigureRuntimePath();

            // Step 2: Ensure native runtime is available and loadable
            var attemptedDownload = false;
            if (!WhisperRuntimeDownloader.IsRuntimeAvailable())
            {
                var (rid, sizeMB) = WhisperRuntimeDownloader.GetRuntimeInfo();
                progress?.Report((0, 0, $"Downloading Whisper runtime for {rid} (~{sizeMB}MB)..."));

                attemptedDownload = true;
                var runtimeSuccess = await WhisperRuntimeDownloader.EnsureRuntimeAsync(progress, ct);
                if (!runtimeSuccess)
                    throw new InvalidOperationException(
                        $"Failed to obtain Whisper runtime.\n{WhisperRuntimeDownloader.GetManualInstallInstructions(rid)}");

                // Re-configure after download to pick up new location
                WhisperRuntimeDownloader.ConfigureRuntimePath();
            }

            // Verify the runtime can actually load (catches corrupted/wrong-arch files)
            if (!WhisperRuntimeDownloader.CanLoadRuntime(out var loadError))
            {
                progress?.Report((0, 0, $"Runtime load check failed: {loadError}"));

                // If we haven't tried downloading yet, attempt it now
                if (!attemptedDownload)
                {
                    var (rid, sizeMB) = WhisperRuntimeDownloader.GetRuntimeInfo();
                    progress?.Report((0, 0, $"Runtime exists but can't load. Downloading fresh copy for {rid} (~{sizeMB}MB)..."));

                    var runtimeSuccess = await WhisperRuntimeDownloader.EnsureRuntimeAsync(progress, ct);
                    if (runtimeSuccess)
                    {
                        WhisperRuntimeDownloader.ConfigureRuntimePath();

                        if (!WhisperRuntimeDownloader.CanLoadRuntime(out loadError))
                            progress?.Report((0, 0, $"Downloaded runtime still can't load: {loadError}"));
                        else
                            progress?.Report((0, 0, "Fresh runtime download loaded successfully"));
                    }
                }
            }
            else
            {
                var runtimePath = WhisperRuntimeDownloader.GetAvailableRuntimePath();
                if (runtimePath != null)
                    progress?.Report((0, 0, $"Runtime: {runtimePath}"));
            }

            // Step 3: Download model if needed
            var modelPath = await WhisperModelDownloader.EnsureModelAsync(
                _modelSize, _language, progress, ct);

            progress?.Report((0, 0, "Loading Whisper model..."));

            // Check if model file exists and is readable
            if (!File.Exists(modelPath)) throw new FileNotFoundException($"Model file not found: {modelPath}");

            var fileInfo = new FileInfo(modelPath);
            progress?.Report((0, 0, $"Model file: {fileInfo.Length / 1024 / 1024}MB at {modelPath}"));

            // On Linux, check for missing shared library dependencies before loading
            // (missing deps can cause segfault instead of a catchable exception)
            var depCheck = WhisperRuntimeDownloader.CheckLinuxDependencies();
            if (depCheck != null)
                progress?.Report((0, 0, $"Warning: {depCheck}"));

            try
            {
                Factory = WhisperFactory.FromPath(modelPath);
            }
            catch (Exception ex)
            {
                // Provide helpful error message with runtime location info
                var rid = WhisperRuntimeDownloader.GetRuntimeIdentifier();
                var libName = WhisperRuntimeDownloader.GetNativeLibraryName();
                var foundPath = WhisperRuntimeDownloader.GetAvailableRuntimePath();
                var runtimeLibPath = RuntimeOptions.LibraryPath;
                var searchedPaths = string.Join("\n  ",
                    WhisperRuntimeDownloader.GetSearchPaths().Select(p => $"{p} {(File.Exists(p) ? "[EXISTS]" : "")}"));

                throw new InvalidOperationException(
                    $"Whisper native library failed to load ({libName} for {rid}).\n" +
                    $"RuntimeOptions.LibraryPath: {runtimeLibPath ?? "(not set)"}\n" +
                    $"Found runtime file: {foundPath ?? "(none)"}\n" +
                    $"Inner error: {ex.Message}\n\n" +
                    $"Searched:\n  {searchedPaths}", ex);
            }

            progress?.Report((0, 0, "Whisper ready"));
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    ///     Apply quality tuning to a Whisper processor builder.
    ///     These settings reduce hallucinations, skip silent segments, and improve accuracy.
    /// </summary>
    private WhisperProcessorBuilder ApplyQualityTuning(WhisperProcessorBuilder builder, string? prompt = null)
    {
        builder
            .WithThreads(_threads)
            .WithNoSpeechThreshold(0.6f) // Skip segments with <60% speech probability
            .WithEntropyThreshold(2.4f) // Skip high-entropy (garbled/hallucinated) segments
            .WithLogProbThreshold(-1.0f) // Skip very low confidence output
            .WithTemperature(0.0f) // Deterministic (greedy) decoding first
            .WithTemperatureInc(0.2f); // Increase temp on fallback attempts

        if (_language != "auto" && !string.IsNullOrEmpty(_language)) builder.WithLanguage(_language);

        // Pass previous chunk's text as context for continuity between chunks.
        // This reduces cold-start hallucinations and improves word accuracy at boundaries.
        if (!string.IsNullOrWhiteSpace(prompt)) builder.WithPrompt(prompt);

        // Use greedy sampling (default)  -  beam search is 2-5x slower and
        // produces single long segments per chunk, destroying subtitle timing
        builder.WithGreedySamplingStrategy();

        return builder;
    }

    /// <summary>
    ///     Get or create a cached WhisperProcessor.
    ///     Reusing a single processor avoids repeated ggml_init/cleanup cycles that can
    ///     trigger GGML_ASSERT(prev != ggml_uncaught_exception) in whisper.cpp.
    /// </summary>
    private WhisperProcessor GetOrCreateProcessor()
    {
        if (_cachedProcessor != null)
            return _cachedProcessor;

        lock (_processorLock)
        {
            if (_cachedProcessor != null)
                return _cachedProcessor;

            var builder = ApplyQualityTuning(Factory!.CreateBuilder());
            _cachedProcessor = builder.Build();
            return _cachedProcessor;
        }
    }

    /// <summary>
    ///     Sanitize audio samples before sending to native Whisper code.
    ///     NaN, Infinity, or extreme values can cause C++ exceptions inside GGML
    ///     which corrupt internal state and trigger GGML_ASSERT on next use.
    /// </summary>
    private static void SanitizeAudioSamples(float[] samples)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]))
                samples[i] = 0f;
            else if (samples[i] > 1.0f)
                samples[i] = 1.0f;
            else if (samples[i] < -1.0f)
                samples[i] = -1.0f;
        }
    }

    /// <summary>
    ///     Transcribe an audio file (batch mode - processes entire file).
    /// </summary>
    public async Task<TranscriptionResult> TranscribeFileAsync(
        string audioPath,
        IProgress<(int segments, double seconds, string status)>? progress = null,
        CancellationToken ct = default,
        string? prompt = null)
    {
        await InitializeAsync(null, ct);

        var sw = Stopwatch.StartNew();
        var segments = new List<TranscriptSegment>();

        // Convert to 16kHz mono float samples
        progress?.Report((0, 0, "Loading audio..."));
        var samples = await ConvertToSamplesAsync(audioPath, ct);

        // Sanitize samples to prevent NaN/Inf from reaching native GGML code,
        // which can cause C++ exceptions that corrupt GGML's internal state
        SanitizeAudioSamples(samples);

        var audioDuration = samples.Length / 16000.0;
        progress?.Report((0, audioDuration, "Transcribing..."));

        // Use cached processor to avoid repeated create/dispose cycles that
        // can trigger GGML_ASSERT(prev != ggml_uncaught_exception) in whisper.cpp
        var processor = GetOrCreateProcessor();

        string? lastText = null;
        var consecutiveRepeats = 0;

        await foreach (var result in processor.ProcessAsync(samples, ct))
        {
            var text = result.Text.Trim();

            // Skip empty segments
            if (string.IsNullOrWhiteSpace(text))
                continue;

            // --- Repetition hallucination filter ---
            // Consecutive identical segments (e.g., "[INDISTINCT CHATTER]" x50)
            if (string.Equals(text, lastText, StringComparison.OrdinalIgnoreCase))
            {
                consecutiveRepeats++;
                if (consecutiveRepeats >= 2) // Allow 1 repeat, filter 3rd+
                    continue;
            }
            else
            {
                consecutiveRepeats = 0;
            }

            // Internal repetition loops (same phrase repeated within a single segment)
            if (IsRepetitiveText(text))
                continue;

            lastText = text;

            var segment = new TranscriptSegment
            {
                StartSeconds = result.Start.TotalSeconds,
                EndSeconds = result.End.TotalSeconds,
                Text = text,
                Confidence = result.Probability
            };

            segments.Add(segment);
            progress?.Report((segments.Count, result.End.TotalSeconds, text));
        }

        sw.Stop();

        return new TranscriptionResult
        {
            Segments = segments,
            Language = _language == "auto" ? null : _language,
            ProcessingTimeMs = sw.ElapsedMilliseconds,
            AudioDurationSeconds = audioDuration
        };
    }

    /// <summary>
    ///     Transcribe audio from a stream of float samples (streaming mode).
    ///     Yields segments as they're transcribed.
    /// </summary>
    public async IAsyncEnumerable<TranscriptSegment> TranscribeStreamingAsync(
        IAsyncEnumerable<float[]> audioChunks,
        double initialOffsetSeconds = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await InitializeAsync(null, ct);

        // Use cached processor to avoid GGML_ASSERT crash from create/dispose cycles
        var processor = GetOrCreateProcessor();

        var accumulatedSamples = new List<float>();
        var processedOffset = 0.0;
        const int MinChunkSamples = 16000 * 5; // Minimum 5 seconds

        await foreach (var chunk in audioChunks.WithCancellation(ct))
        {
            accumulatedSamples.AddRange(chunk);

            // Process when we have enough samples
            if (accumulatedSamples.Count >= MinChunkSamples)
            {
                var samples = accumulatedSamples.ToArray();
                accumulatedSamples.Clear();

                await foreach (var result in processor.ProcessAsync(samples, ct))
                    yield return new TranscriptSegment
                    {
                        StartSeconds = initialOffsetSeconds + processedOffset + result.Start.TotalSeconds,
                        EndSeconds = initialOffsetSeconds + processedOffset + result.End.TotalSeconds,
                        Text = result.Text.Trim(),
                        Confidence = result.Probability
                    };

                processedOffset += samples.Length / 16000.0;
            }
        }

        // Process remaining samples
        if (accumulatedSamples.Count > 0)
        {
            var samples = accumulatedSamples.ToArray();

            await foreach (var result in processor.ProcessAsync(samples, ct))
                yield return new TranscriptSegment
                {
                    StartSeconds = initialOffsetSeconds + processedOffset + result.Start.TotalSeconds,
                    EndSeconds = initialOffsetSeconds + processedOffset + result.End.TotalSeconds,
                    Text = result.Text.Trim(),
                    Confidence = result.Probability
                };
        }
    }

    /// <summary>
    ///     Detect text with repetitive hallucination patterns.
    ///     Returns true if the text contains the same short phrase repeated 3+ times.
    /// </summary>
    internal static bool IsRepetitiveText(string text)
    {
        if (text.Length < 10) return false;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 6) return false;

        // Check for repeating sequences of 1-4 words
        for (var gramSize = 1; gramSize <= Math.Min(4, words.Length / 3); gramSize++)
        {
            var repeatCount = 1;
            for (var i = gramSize; i + gramSize <= words.Length; i += gramSize)
            {
                var match = true;
                for (var j = 0; j < gramSize; j++)
                    if (!string.Equals(words[i + j], words[i + j - gramSize], StringComparison.OrdinalIgnoreCase))
                    {
                        match = false;
                        break;
                    }

                if (match) repeatCount++;
                else repeatCount = 1;

                if (repeatCount >= 3) return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Convert audio file to 16kHz mono float samples.
    /// </summary>
    private static async Task<float[]> ConvertToSamplesAsync(string audioPath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            using var reader = new AudioFileReader(audioPath);
            ISampleProvider provider = reader;

            // Resample to 16kHz if needed
            if (reader.WaveFormat.SampleRate != 16000) provider = new WdlResamplingSampleProvider(reader, 16000);

            // Convert to mono if needed
            if (provider.WaveFormat.Channels > 1)
                provider = new StereoToMonoSampleProvider(provider)
                {
                    LeftVolume = 0.5f,
                    RightVolume = 0.5f
                };

            // Read all samples
            var samples = new List<float>();
            var buffer = new float[16000]; // 1 second buffer
            int samplesRead;

            while ((samplesRead = provider.Read(buffer, 0, buffer.Length)) > 0)
                for (var i = 0; i < samplesRead; i++)
                    samples.Add(buffer[i]);

            return samples.ToArray();
        }, ct);
    }
}

/// <summary>
///     Realtime transcriber with buffering for live playback.
///     Accumulates audio and processes ahead of playback.
/// </summary>
public class RealtimeTranscriber : IAsyncDisposable
{
    private readonly Channel<(float[] samples, double timestamp)> _audioChannel;

    private readonly double _bufferSeconds;
    private readonly CancellationTokenSource _cts;
    private readonly Channel<TranscriptSegment> _outputChannel;
    private readonly List<TranscriptSegment> _segments = new();
    private readonly WhisperTranscriptionService _whisper;
    private Task? _processingTask;

    public RealtimeTranscriber(
        string modelSize = "tiny",
        string language = "auto",
        double bufferSeconds = 30.0)
    {
        _bufferSeconds = bufferSeconds;
        _whisper = new WhisperTranscriptionService()
            .WithModel(modelSize)
            .WithLanguage(language);

        _audioChannel = Channel.CreateBounded<(float[], double)>(100);
        _outputChannel = Channel.CreateUnbounded<TranscriptSegment>();
        _cts = new CancellationTokenSource();
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _audioChannel.Writer.Complete();

        if (_processingTask != null)
            try
            {
                await _processingTask;
            }
            catch
            {
            }

        _whisper.Dispose();
        _cts.Dispose();
    }

    /// <summary>
    ///     Start the background transcription task.
    /// </summary>
    public async Task StartAsync(
        IProgress<(long downloaded, long total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        // Initialize whisper (downloads model if needed)
        await _whisper.InitializeAsync(progress, ct);

        // Start background processing
        _processingTask = ProcessAudioAsync(_cts.Token);
    }

    /// <summary>
    ///     Feed audio samples (call this as FFmpeg produces audio).
    /// </summary>
    public void AddSamples(float[] samples, double timestamp)
    {
        _audioChannel.Writer.TryWrite((samples, timestamp));
    }

    /// <summary>
    ///     Get subtitle for the given playback time.
    /// </summary>
    public TranscriptSegment? GetSubtitleAt(double playbackSeconds)
    {
        // Drain any new segments from the output channel
        while (_outputChannel.Reader.TryRead(out var segment)) _segments.Add(segment);

        // Find segment that covers this time
        return _segments
            .Where(s => s.StartSeconds <= playbackSeconds && s.EndSeconds > playbackSeconds)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Check if we have enough buffered transcription to start playback.
    /// </summary>
    public bool HasSufficientBuffer(double playbackSeconds)
    {
        // Drain segments
        while (_outputChannel.Reader.TryRead(out var segment)) _segments.Add(segment);

        // Check if we have transcription ahead of playback time
        var maxTranscribedTime = _segments.Count > 0
            ? _segments.Max(s => s.EndSeconds)
            : 0;

        return maxTranscribedTime >= playbackSeconds + _bufferSeconds;
    }

    /// <summary>
    ///     Signal that no more audio will be added.
    /// </summary>
    public void Complete()
    {
        _audioChannel.Writer.Complete();
    }

    private async Task ProcessAudioAsync(CancellationToken ct)
    {
        var accumulatedSamples = new List<float>();
        var firstTimestamp = 0.0;
        var hasFirst = false;

        // Create processor once and reuse — avoids GGML_ASSERT crash from
        // repeated ggml_init/cleanup cycles in whisper.cpp
        var builder = GetFactory()!.CreateBuilder();
        using var processor = builder.Build();

        try
        {
            await foreach (var (samples, timestamp) in _audioChannel.Reader.ReadAllAsync(ct))
            {
                if (!hasFirst)
                {
                    firstTimestamp = timestamp;
                    hasFirst = true;
                }

                accumulatedSamples.AddRange(samples);

                // Process when we have enough samples (5+ seconds)
                const int minSamples = 16000 * 5;
                if (accumulatedSamples.Count >= minSamples)
                {
                    var toProcess = accumulatedSamples.ToArray();
                    accumulatedSamples.Clear();

                    await foreach (var result in processor.ProcessAsync(toProcess, ct))
                    {
                        var segment = new TranscriptSegment
                        {
                            StartSeconds = firstTimestamp + result.Start.TotalSeconds,
                            EndSeconds = firstTimestamp + result.End.TotalSeconds,
                            Text = result.Text.Trim(),
                            Confidence = result.Probability
                        };

                        await _outputChannel.Writer.WriteAsync(segment, ct);
                    }

                    firstTimestamp += toProcess.Length / 16000.0;
                }
            }

            // Process remaining samples
            if (accumulatedSamples.Count > 0)
            {
                await foreach (var result in processor.ProcessAsync(accumulatedSamples.ToArray(), ct))
                {
                    var segment = new TranscriptSegment
                    {
                        StartSeconds = firstTimestamp + result.Start.TotalSeconds,
                        EndSeconds = firstTimestamp + result.End.TotalSeconds,
                        Text = result.Text.Trim(),
                        Confidence = result.Probability
                    };

                    await _outputChannel.Writer.WriteAsync(segment, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        finally
        {
            _outputChannel.Writer.Complete();
        }
    }

    private WhisperFactory? GetFactory()
    {
        return _whisper.Factory;
    }
}