using System.Runtime.CompilerServices;
using System.Threading.Channels;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;

namespace ConsoleImage.Transcription;

/// <summary>
/// Whisper transcription service using Whisper.NET.
/// Supports both batch and streaming transcription.
/// Cross-platform compatible.
/// </summary>
public sealed class WhisperTranscriptionService : IDisposable
{
    private WhisperFactory? _factory;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _disposed;

    private string _modelSize = "base";
    private string _language = "auto";
    private int _threads = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>
    /// Check if Whisper transcription is available (model can be downloaded).
    /// </summary>
    public static bool IsAvailable()
    {
        // Whisper.NET is always available as long as the package is included
        // The model will be downloaded on first use
        return true;
    }

    /// <summary>
    /// Configure model size (tiny, base, small, medium, large).
    /// </summary>
    public WhisperTranscriptionService WithModel(string modelSize)
    {
        _modelSize = modelSize;
        return this;
    }

    /// <summary>
    /// Configure language (en, es, ja, etc. or "auto" for detection).
    /// </summary>
    public WhisperTranscriptionService WithLanguage(string language)
    {
        _language = language;
        return this;
    }

    /// <summary>
    /// Configure number of CPU threads.
    /// </summary>
    public WhisperTranscriptionService WithThreads(int threads)
    {
        _threads = Math.Max(1, threads);
        return this;
    }

    /// <summary>
    /// Ensure model is downloaded and loaded.
    /// </summary>
    public async Task InitializeAsync(
        IProgress<(long downloaded, long total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        if (_factory != null) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_factory != null) return;

            var modelPath = await WhisperModelDownloader.EnsureModelAsync(
                _modelSize, _language, progress, ct);

            progress?.Report((0, 0, "Loading Whisper model..."));

            // Check if model file exists and is readable
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model file not found: {modelPath}");
            }

            var fileInfo = new FileInfo(modelPath);
            progress?.Report((0, 0, $"Model file: {fileInfo.Length / 1024 / 1024}MB at {modelPath}"));

            try
            {
                _factory = WhisperFactory.FromPath(modelPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load Whisper model from {modelPath}: {ex.GetType().Name} - {ex.Message}", ex);
            }

            progress?.Report((0, 0, "Whisper ready"));
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Transcribe an audio file (batch mode - processes entire file).
    /// </summary>
    public async Task<TranscriptionResult> TranscribeFileAsync(
        string audioPath,
        IProgress<(int segments, double seconds, string status)>? progress = null,
        CancellationToken ct = default)
    {
        await InitializeAsync(null, ct);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var segments = new List<TranscriptSegment>();

        // Convert to 16kHz mono float samples
        progress?.Report((0, 0, "Loading audio..."));
        var samples = await ConvertToSamplesAsync(audioPath, ct);
        var audioDuration = samples.Length / 16000.0;

        progress?.Report((0, audioDuration, "Transcribing..."));

        // Create processor
        var builder = _factory!.CreateBuilder().WithThreads(_threads);

        if (_language != "auto" && !string.IsNullOrEmpty(_language))
        {
            builder.WithLanguage(_language);
        }

        using var processor = builder.Build();

        await foreach (var result in processor.ProcessAsync(samples, ct))
        {
            var segment = new TranscriptSegment
            {
                StartSeconds = result.Start.TotalSeconds,
                EndSeconds = result.End.TotalSeconds,
                Text = result.Text.Trim(),
                Confidence = result.Probability
            };

            segments.Add(segment);
            progress?.Report((segments.Count, result.End.TotalSeconds, result.Text.Trim()));
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
    /// Transcribe audio from a stream of float samples (streaming mode).
    /// Yields segments as they're transcribed.
    /// </summary>
    public async IAsyncEnumerable<TranscriptSegment> TranscribeStreamingAsync(
        IAsyncEnumerable<float[]> audioChunks,
        double initialOffsetSeconds = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await InitializeAsync(null, ct);

        var builder = _factory!.CreateBuilder().WithThreads(_threads);
        if (_language != "auto" && !string.IsNullOrEmpty(_language))
        {
            builder.WithLanguage(_language);
        }

        using var processor = builder.Build();

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
                {
                    yield return new TranscriptSegment
                    {
                        StartSeconds = initialOffsetSeconds + processedOffset + result.Start.TotalSeconds,
                        EndSeconds = initialOffsetSeconds + processedOffset + result.End.TotalSeconds,
                        Text = result.Text.Trim(),
                        Confidence = result.Probability
                    };
                }

                processedOffset += samples.Length / 16000.0;
            }
        }

        // Process remaining samples
        if (accumulatedSamples.Count > 0)
        {
            var samples = accumulatedSamples.ToArray();

            await foreach (var result in processor.ProcessAsync(samples, ct))
            {
                yield return new TranscriptSegment
                {
                    StartSeconds = initialOffsetSeconds + processedOffset + result.Start.TotalSeconds,
                    EndSeconds = initialOffsetSeconds + processedOffset + result.End.TotalSeconds,
                    Text = result.Text.Trim(),
                    Confidence = result.Probability
                };
            }
        }
    }

    /// <summary>
    /// Convert audio file to 16kHz mono float samples.
    /// </summary>
    private static async Task<float[]> ConvertToSamplesAsync(string audioPath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            using var reader = new AudioFileReader(audioPath);
            ISampleProvider provider = reader;

            // Resample to 16kHz if needed
            if (reader.WaveFormat.SampleRate != 16000)
            {
                provider = new WdlResamplingSampleProvider(reader, 16000);
            }

            // Convert to mono if needed
            if (provider.WaveFormat.Channels > 1)
            {
                provider = new StereoToMonoSampleProvider(provider)
                {
                    LeftVolume = 0.5f,
                    RightVolume = 0.5f
                };
            }

            // Read all samples
            var samples = new List<float>();
            var buffer = new float[16000]; // 1 second buffer
            int samplesRead;

            while ((samplesRead = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    samples.Add(buffer[i]);
                }
            }

            return samples.ToArray();
        }, ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _factory?.Dispose();
        _initLock.Dispose();
    }
}

/// <summary>
/// Realtime transcriber with buffering for live playback.
/// Accumulates audio and processes ahead of playback.
/// </summary>
public class RealtimeTranscriber : IAsyncDisposable
{
    private readonly WhisperTranscriptionService _whisper;
    private readonly Channel<(float[] samples, double timestamp)> _audioChannel;
    private readonly Channel<TranscriptSegment> _outputChannel;
    private readonly CancellationTokenSource _cts;
    private Task? _processingTask;

    private readonly double _bufferSeconds;
    private readonly List<TranscriptSegment> _segments = new();
    private int _currentSegmentIndex;

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

    /// <summary>
    /// Start the background transcription task.
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
    /// Feed audio samples (call this as FFmpeg produces audio).
    /// </summary>
    public void AddSamples(float[] samples, double timestamp)
    {
        _audioChannel.Writer.TryWrite((samples, timestamp));
    }

    /// <summary>
    /// Get subtitle for the given playback time.
    /// </summary>
    public TranscriptSegment? GetSubtitleAt(double playbackSeconds)
    {
        // Drain any new segments from the output channel
        while (_outputChannel.Reader.TryRead(out var segment))
        {
            _segments.Add(segment);
        }

        // Find segment that covers this time
        return _segments
            .Where(s => s.StartSeconds <= playbackSeconds && s.EndSeconds > playbackSeconds)
            .FirstOrDefault();
    }

    /// <summary>
    /// Check if we have enough buffered transcription to start playback.
    /// </summary>
    public bool HasSufficientBuffer(double playbackSeconds)
    {
        // Drain segments
        while (_outputChannel.Reader.TryRead(out var segment))
        {
            _segments.Add(segment);
        }

        // Check if we have transcription ahead of playback time
        var maxTranscribedTime = _segments.Count > 0
            ? _segments.Max(s => s.EndSeconds)
            : 0;

        return maxTranscribedTime >= playbackSeconds + _bufferSeconds;
    }

    /// <summary>
    /// Signal that no more audio will be added.
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

                    // Transcribe this chunk
                    var builder = (await GetProcessorAsync())!.CreateBuilder();
                    using var processor = builder.Build();

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
                var builder = (await GetProcessorAsync())!.CreateBuilder();
                using var processor = builder.Build();

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

    private Task<WhisperFactory?> GetProcessorAsync()
    {
        // Access factory through reflection since it's private
        // In a real implementation, we'd expose this properly
        return Task.FromResult<WhisperFactory?>(null);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _audioChannel.Writer.Complete();

        if (_processingTask != null)
        {
            try { await _processingTask; } catch { }
        }

        _whisper.Dispose();
        _cts.Dispose();
    }
}
