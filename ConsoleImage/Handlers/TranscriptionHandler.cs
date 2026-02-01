using System.Diagnostics;
using ConsoleImage.Transcription;

namespace ConsoleImage.Cli.Handlers;

/// <summary>
///     Handles transcription operations - generates VTT/SRT from video/audio.
///     Usage: consoleimage transcribe input.mp4 -o output.vtt
///     consoleimage input.mp4 --transcript (streams to stdout)
/// </summary>
public static class TranscriptionHandler
{
    public static async Task<int> HandleAsync(TranscriptionOptions opts, CancellationToken ct)
    {
        // Determine output path - default to VTT
        var outputPath = opts.OutputPath;
        if (string.IsNullOrEmpty(outputPath)) outputPath = Path.ChangeExtension(opts.InputPath, ".vtt");

        return await GenerateSubtitlesAsync(opts, outputPath, ct);
    }

    private static async Task<int> GenerateSubtitlesAsync(
        TranscriptionOptions opts,
        string outputPath,
        CancellationToken ct)
    {
        var isVtt = outputPath.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase);
        var quiet = opts.Quiet || opts.StreamToStdout;

        if (!quiet)
        {
            Console.Error.WriteLine($"Transcribing: {opts.InputPath}");
            Console.Error.WriteLine($"Model: {opts.ModelSize}, Language: {opts.Language}");
            Console.Error.WriteLine($"Output: {outputPath} ({(isVtt ? "WebVTT" : "SRT")})");
            if (opts.Diarize)
                Console.Error.WriteLine("Diarization: enabled");
            Console.Error.WriteLine();
        }

        // Initialize whisper service
        var whisper = new WhisperTranscriptionService()
            .WithModel(opts.ModelSize)
            .WithLanguage(opts.Language);

        if (opts.Threads.HasValue) whisper.WithThreads(opts.Threads.Value);

        // Progress for model download (always show on stderr)
        var downloadProgress = new Progress<(long downloaded, long total, string status)>(p =>
        {
            if (p.total > 0)
            {
                var pct = (int)(p.downloaded * 100 / p.total);
                Console.Error.Write($"\r{p.status} ({pct}%)          ");
            }
            else
            {
                Console.Error.Write($"\r{p.status}          ");
            }
        });

        var transcribeProgress = new Progress<(int segments, double seconds, string status)>(p =>
        {
            if (!quiet)
                Console.Error.Write(
                    $"\rSegments: {p.segments} | Time: {p.seconds:F1}s | {TruncateText(p.status, 40)}          ");
        });

        try
        {
            // Pre-initialize whisper (download model if needed)
            if (!quiet)
                Console.Error.WriteLine("Initializing Whisper...");
            await whisper.InitializeAsync(downloadProgress, ct);
            if (!quiet)
                Console.Error.WriteLine();

            // Check if we need to extract audio first
            // URLs and video files need FFmpeg extraction - NAudio can't read URLs directly
            var audioPath = opts.InputPath;
            var tempAudioPath = (string?)null;
            var needsExtraction = IsVideoFile(opts.InputPath) || IsUrl(opts.InputPath);

            if (needsExtraction)
            {
                if (!quiet)
                    Console.Error.WriteLine("Extracting audio...");
                tempAudioPath = Path.Combine(Path.GetTempPath(), $"consoleimage_audio_{Guid.NewGuid():N}.wav");
                await ExtractAudioAsync(opts.InputPath, tempAudioPath, opts.StartTime, opts.Duration, opts.EnhanceAudio,
                    ct);
                audioPath = tempAudioPath;
                if (!quiet)
                    Console.Error.WriteLine("Audio extracted.");
            }

            try
            {
                // Streaming progress handler that outputs text as it's transcribed
                var streamingProgress = new Progress<(int segments, double seconds, string status)>(p =>
                {
                    if (!quiet)
                        Console.Error.Write(
                            $"\rSegments: {p.segments} | Time: {p.seconds:F1}s | {TruncateText(p.status, 40)}          ");
                });

                // Transcribe
                var result = await whisper.TranscribeFileAsync(audioPath, streamingProgress, ct);

                // Stream output to stdout if requested
                if (opts.StreamToStdout)
                    foreach (var seg in result.Segments)
                    {
                        // Output format: [timestamp] text
                        var startTime = FormatTime(seg.StartTime);
                        var endTime = FormatTime(seg.EndTime);
                        Console.WriteLine($"[{startTime} --> {endTime}] {seg.Text.Trim()}");
                    }

                if (!quiet)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(
                        $"Transcription complete: {result.Segments.Count} segments in {result.ProcessingTimeMs}ms");
                    Console.Error.WriteLine($"Audio duration: {result.AudioDurationSeconds:F1}s");
                    Console.Error.WriteLine(
                        $"Speed: {result.AudioDurationSeconds / (result.ProcessingTimeMs / 1000.0):F1}x realtime");
                }

                // Write output file (VTT or SRT)
                if (isVtt)
                    await VttFormatter.WriteAsync(outputPath, result.Segments, opts.Diarize, ct);
                else
                    await SrtFormatter.WriteAsync(outputPath, result.Segments, opts.Diarize, ct);

                if (!quiet)
                    Console.Error.WriteLine($"Saved: {outputPath}");

                return 0;
            }
            finally
            {
                // Cleanup temp audio
                if (tempAudioPath != null && File.Exists(tempAudioPath))
                    try
                    {
                        File.Delete(tempAudioPath);
                    }
                    catch
                    {
                    }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nTranscription failed: {ex.Message}");
            if (ex.InnerException != null) Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
            // Debug: show full exception type
            Console.Error.WriteLine($"  Exception type: {ex.GetType().FullName}");
            return 1;
        }
        finally
        {
            whisper.Dispose();
        }
    }

    private static string FormatTime(TimeSpan ts)
    {
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    private static bool IsVideoFile(string path)
    {
        // Don't check extension for URLs - they may not have one
        if (IsUrl(path)) return false;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".m4v" or ".wmv" or ".flv";
    }

    private static bool IsUrl(string path)
    {
        return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ExtractAudioAsync(string inputPath, string audioPath, double? startTime, double? duration,
        bool enhanceAudio, CancellationToken ct)
    {
        // Use FFmpeg to extract audio
        var ffmpegPath = FindFFmpeg();
        if (ffmpegPath == null)
            throw new InvalidOperationException("FFmpeg not found. Install FFmpeg or use --ffmpeg-path");

        // Build FFmpeg arguments - handle URLs with proper quoting
        // For URLs, we need to handle special characters and add timeout/reconnect options
        var isUrl = IsUrl(inputPath);
        var inputArg = isUrl
            ? $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i \"{inputPath}\""
            : $"-i \"{inputPath}\"";

        // Add time range options if specified
        var timeArgs = "";
        if (startTime.HasValue)
            timeArgs += $"-ss {startTime.Value:F2} ";
        if (duration.HasValue)
            timeArgs += $"-t {duration.Value:F2} ";

        // Speech-optimized audio filters for better Whisper recognition:
        // highpass=f=200    - Remove low-freq rumble/hum below 200Hz
        // lowpass=f=3000    - Remove high-freq noise above 3kHz (speech is 300-3000Hz)
        // NOTE: loudnorm and afftdn removed  -  loudnorm requires two-pass (slow),
        // afftdn distorts clean audio. These simple filters are fast and safe.
        var audioFilter = enhanceAudio
            ? "-af \"highpass=f=200,lowpass=f=3000\" "
            : "";

        // Extract audio: 16kHz mono WAV (required for Whisper)
        var args = $"{timeArgs}{inputArg} -vn {audioFilter}-ar 16000 -ac 1 -acodec pcm_s16le -y \"{audioPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start FFmpeg");

        // Read stderr in background (FFmpeg writes progress here)
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        // Wait with timeout for URL sources (they can hang)
        var timeout = isUrl ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(30);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(true);
            throw new TimeoutException($"FFmpeg audio extraction timed out after {timeout.TotalMinutes} minutes");
        }

        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            // Extract just the error message, not the full FFmpeg banner
            var errorLines = stderr.Split('\n')
                .Where(l => l.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                            l.Contains("Invalid", StringComparison.OrdinalIgnoreCase) ||
                            l.Contains("failed", StringComparison.OrdinalIgnoreCase))
                .Take(3);
            var errorMsg = string.Join("\n", errorLines);
            if (string.IsNullOrEmpty(errorMsg))
                errorMsg = "Unknown error (check FFmpeg output)";
            throw new InvalidOperationException($"FFmpeg failed: {errorMsg}");
        }

        // Verify output file exists
        if (!File.Exists(audioPath))
            throw new InvalidOperationException("FFmpeg completed but audio file was not created");
    }

    private static string? FindFFmpeg()
    {
        // Check common locations
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

            // Check if it's in PATH
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

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    public class TranscriptionOptions
    {
        public string InputPath { get; set; } = "";
        public string? OutputPath { get; set; }
        public string ModelSize { get; set; } = "base";
        public string Language { get; set; } = "en";
        public bool Diarize { get; set; } = false;
        public int? Threads { get; set; }

        /// <summary>
        ///     Start time in seconds (for time-limited transcription).
        /// </summary>
        public double? StartTime { get; set; }

        /// <summary>
        ///     Duration in seconds (for time-limited transcription).
        /// </summary>
        public double? Duration { get; set; }

        /// <summary>
        ///     Stream transcribed text to stdout as it's generated (for tool/pipe usage).
        /// </summary>
        public bool StreamToStdout { get; set; } = false;

        /// <summary>
        ///     Quiet mode - suppress progress messages (only output transcribed text).
        /// </summary>
        public bool Quiet { get; set; } = false;

        /// <summary>
        ///     Apply FFmpeg audio preprocessing filters for better speech recognition.
        ///     Default: true. Disable with --no-enhance for recordings where filtering hurts quality.
        /// </summary>
        public bool EnhanceAudio { get; set; } = true;
    }
}