using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleImage.Video.Core;

/// <summary>
/// Memory-efficient GIF writer using FFmpeg pipe protocol.
/// Streams raw frames directly to FFmpeg - only one frame in memory at a time.
/// Ideal for long videos where buffering all frames would exhaust memory.
/// </summary>
public sealed class FFmpegGifWriter : IAsyncDisposable, IDisposable
{
    private readonly string _outputPath;
    private readonly int _width;
    private readonly int _height;
    private readonly int _fps;
    private readonly int _loopCount;
    private readonly int _maxColors;
    private readonly double? _maxLengthSeconds;
    private readonly int? _maxFrames;

    private Process? _ffmpegProcess;
    private Stream? _stdin;
    private int _frameCount;
    private double _totalMs;
    private bool _disposed;
    private readonly byte[] _frameBuffer;

    /// <summary>
    /// Create a streaming GIF writer.
    /// </summary>
    /// <param name="outputPath">Output GIF file path</param>
    /// <param name="width">Frame width in pixels</param>
    /// <param name="height">Frame height in pixels</param>
    /// <param name="fps">Target frames per second (affects playback speed)</param>
    /// <param name="loopCount">Loop count (0 = infinite)</param>
    /// <param name="maxColors">Max colors in palette (2-256)</param>
    /// <param name="maxLengthSeconds">Optional max duration</param>
    /// <param name="maxFrames">Optional max frame count</param>
    public FFmpegGifWriter(
        string outputPath,
        int width,
        int height,
        int fps = 10,
        int loopCount = 0,
        int maxColors = 64,
        double? maxLengthSeconds = null,
        int? maxFrames = null)
    {
        _outputPath = outputPath;
        _width = width;
        _height = height;
        _fps = Math.Max(1, fps);
        _loopCount = loopCount;
        _maxColors = Math.Clamp(maxColors, 2, 256);
        _maxLengthSeconds = maxLengthSeconds;
        _maxFrames = maxFrames;

        // Pre-allocate frame buffer (RGBA = 4 bytes per pixel)
        _frameBuffer = new byte[width * height * 4];
    }

    /// <summary>
    /// Number of frames written.
    /// </summary>
    public int FrameCount => _frameCount;

    /// <summary>
    /// Total duration in milliseconds.
    /// </summary>
    public double TotalDurationMs => _totalMs;

    /// <summary>
    /// Check if we should stop adding frames.
    /// </summary>
    public bool ShouldStop =>
        (_maxFrames.HasValue && _frameCount >= _maxFrames.Value) ||
        (_maxLengthSeconds.HasValue && _totalMs / 1000.0 >= _maxLengthSeconds.Value);

    /// <summary>
    /// Start the FFmpeg process for GIF encoding.
    /// Must be called before adding frames.
    /// </summary>
    public async Task StartAsync(string? ffmpegPath = null, CancellationToken ct = default)
    {
        var ffmpeg = ffmpegPath ?? await FFmpegProvider.GetFFmpegPathAsync(null, null, ct);

        // FFmpeg command for high-quality GIF with palette generation
        // Uses split filter to generate optimal palette, then apply it
        // -loop 0 = infinite loop, -loop 1 = play once
        var loopArg = _loopCount == 0 ? "0" : _loopCount.ToString();

        // For better quality GIFs, we use a two-pass palettegen approach
        // But for streaming, we use single-pass with stats_mode=diff for reasonable quality
        var args = $"-y -f rawvideo -pix_fmt rgba -s {_width}x{_height} -r {_fps} -i pipe:0 " +
                   $"-vf \"split[s0][s1];[s0]palettegen=max_colors={_maxColors}:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3\" " +
                   $"-loop {loopArg} \"{_outputPath}\"";

        _ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _ffmpegProcess.Start();
        _stdin = _ffmpegProcess.StandardInput.BaseStream;

        // Read stderr asynchronously to prevent blocking
        _ = _ffmpegProcess.StandardError.ReadToEndAsync(ct);
    }

    /// <summary>
    /// Add an image frame to the GIF.
    /// The image will be written immediately to FFmpeg and can be disposed after this call.
    /// </summary>
    public void AddFrame(Image<Rgba32> image)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FFmpegGifWriter));
        if (_stdin == null) throw new InvalidOperationException("Call StartAsync before adding frames");
        if (ShouldStop) return;

        // Resize if needed
        Image<Rgba32> frameToWrite = image;
        bool needsDispose = false;

        if (image.Width != _width || image.Height != _height)
        {
            frameToWrite = image.Clone();
            frameToWrite.Mutate(x => x.Resize(_width, _height));
            needsDispose = true;
        }

        // Copy pixel data to buffer
        frameToWrite.CopyPixelDataTo(_frameBuffer);

        // Write raw RGBA data to FFmpeg stdin
        _stdin.Write(_frameBuffer, 0, _frameBuffer.Length);

        if (needsDispose)
            frameToWrite.Dispose();

        _frameCount++;
        _totalMs += 1000.0 / _fps;
    }

    /// <summary>
    /// Add a frame asynchronously.
    /// </summary>
    public async Task AddFrameAsync(Image<Rgba32> image, CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FFmpegGifWriter));
        if (_stdin == null) throw new InvalidOperationException("Call StartAsync before adding frames");
        if (ShouldStop) return;

        Image<Rgba32> frameToWrite = image;
        bool needsDispose = false;

        if (image.Width != _width || image.Height != _height)
        {
            frameToWrite = image.Clone();
            frameToWrite.Mutate(x => x.Resize(_width, _height));
            needsDispose = true;
        }

        frameToWrite.CopyPixelDataTo(_frameBuffer);
        await _stdin.WriteAsync(_frameBuffer, ct);

        if (needsDispose)
            frameToWrite.Dispose();

        _frameCount++;
        _totalMs += 1000.0 / _fps;
    }

    /// <summary>
    /// Finish writing and close the GIF file.
    /// </summary>
    public async Task FinishAsync(CancellationToken ct = default)
    {
        if (_disposed) return;
        if (_ffmpegProcess == null) return;

        // Close stdin to signal end of input
        if (_stdin != null)
        {
            await _stdin.FlushAsync(ct);
            _stdin.Close();
            _stdin = null;
        }

        // Wait for FFmpeg to finish
        await _ffmpegProcess.WaitForExitAsync(ct);

        if (_ffmpegProcess.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg exited with code {_ffmpegProcess.ExitCode}");
        }
    }

    /// <summary>
    /// Finish synchronously.
    /// </summary>
    public void Finish()
    {
        if (_disposed) return;
        if (_ffmpegProcess == null) return;

        _stdin?.Flush();
        _stdin?.Close();
        _stdin = null;

        _ffmpegProcess.WaitForExit();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _stdin?.Close();
        }
        catch { }

        try
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                _ffmpegProcess.Kill();
            }
            _ffmpegProcess?.Dispose();
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            await FinishAsync();
        }
        catch { }

        Dispose();
    }
}
