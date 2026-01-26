using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConsoleImage.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Video.Core;

/// <summary>
///     FFmpeg service for video frame extraction with hardware acceleration.
///     Uses pipe-based streaming for efficient memory usage - no temp files needed.
/// </summary>
public sealed class FFmpegService : IDisposable
{
    private static string? _ffmpegBinPath;
    private static readonly object _ffmpegLock = new();

    /// <summary>
    ///     Codecs known to have issues with hardware acceleration.
    ///     These typically cause "auto_scale" filter errors or other conversion issues.
    /// </summary>
    private static readonly HashSet<string> _hwAccelProblematicCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "theora", // Ogg Theora - causes auto_scale filter errors
        "vp6", // Old Flash codec
        "vp6f", // Flash VP6
        "gif", // GIF as video
        "apng", // Animated PNG
        "mpeg4", // MPEG-4 part 2 / DivX - hwdownload nv12 format issues
        "av1", // AV1 - hwdownload nv12 format issues
        "mjpeg", // Motion JPEG - CUDA decoder fails on many systems
        "mjpegb", // Motion JPEG B
        "jpeg2000", // JPEG 2000
        "jpegls", // JPEG-LS
        "rawvideo", // Raw video - no hardware decode
        "prores", // ProRes - not supported by CUDA
        "dnxhd", // DNxHD - not supported by CUDA
        "huffyuv", // Huffyuv - lossless, no CUDA
        "ffv1" // FFV1 - lossless, no CUDA
    };

    private readonly bool _useHardwareAcceleration;
    private string _ffmpegPath;
    private string _ffprobePath;
    private bool _initialized;

    /// <summary>
    ///     Create FFmpegService with optional custom paths.
    ///     If no paths provided, will use FFmpegProvider to find or download FFmpeg.
    /// </summary>
    public FFmpegService(
        string? ffprobePath = null,
        string? ffmpegPath = null,
        bool useHardwareAcceleration = true)
    {
        _ffprobePath = ffprobePath ?? "";
        _ffmpegPath = ffmpegPath ?? "";
        _useHardwareAcceleration = useHardwareAcceleration;
        HardwareAccelerationType = "";

        // If paths provided, initialize immediately
        if (!string.IsNullOrEmpty(ffmpegPath) || !string.IsNullOrEmpty(ffprobePath))
        {
            _ffmpegPath = ffmpegPath ?? FindExecutable("ffmpeg");
            _ffprobePath = ffprobePath ?? FindExecutable("ffprobe");
            HardwareAccelerationType = useHardwareAcceleration ? DetectHardwareAcceleration() : "";
            _initialized = true;
        }
    }

    /// <summary>
    ///     The detected hardware acceleration type (cuda, d3d11va, vaapi, or empty).
    /// </summary>
    public string HardwareAccelerationType { get; private set; }

    public void Dispose()
    {
        // No managed resources to dispose
    }

    /// <summary>
    ///     Initialize FFmpeg paths asynchronously, downloading if necessary.
    /// </summary>
    public async Task InitializeAsync(
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        if (_initialized) return;

        _ffmpegPath = await FFmpegProvider.GetFFmpegPathAsync(null, progress, ct);
        _ffprobePath = await FFmpegProvider.GetFFprobePathAsync(null, progress, ct);

        // Verify FFmpeg can actually execute (important for Linux where permissions or library issues can occur)
        await VerifyFFmpegAsync(ct);

        HardwareAccelerationType = _useHardwareAcceleration ? DetectHardwareAcceleration() : "";
        _initialized = true;
    }

    /// <summary>
    ///     Verify FFmpeg can execute by running a simple version check.
    /// </summary>
    private async Task VerifyFFmpegAsync(CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                var errorDetail = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : "No output";
                throw new InvalidOperationException(
                    $"FFmpeg binary at '{_ffmpegPath}' cannot execute properly.\n" +
                    $"Exit code: {process.ExitCode}\n" +
                    $"Error: {errorDetail}\n" +
                    "On Linux, this may indicate missing libraries. Try: ldd " + _ffmpegPath);
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to execute FFmpeg at '{_ffmpegPath}': {ex.Message}\n" +
                "On Linux, ensure the binary has execute permissions (chmod +x) and required libraries are installed.");
        }
    }

    /// <summary>
    ///     Ensure initialized before operations.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (!_initialized) await InitializeAsync(null, ct);
    }

    /// <summary>
    ///     Get FFmpeg status information.
    /// </summary>
    public static string GetStatus(string? customPath = null)
    {
        return FFmpegProvider.GetStatus(customPath);
    }

    /// <summary>
    ///     Check if FFmpeg is available (without downloading).
    /// </summary>
    public static bool IsAvailable(string? customPath = null)
    {
        return FFmpegProvider.IsAvailable(customPath);
    }

    /// <summary>
    ///     Detect available hardware acceleration.
    /// </summary>
    private static string DetectHardwareAcceleration()
    {
        // Check for NVIDIA GPU first (CUDA/NVDEC)
        if (File.Exists(@"C:\Windows\System32\nvidia-smi.exe") ||
            File.Exists(@"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe") ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CUDA_PATH")))
            return "cuda";

        // Check for AMD GPU (DirectX Video Acceleration on Windows)
        if (OperatingSystem.IsWindows()) return "d3d11va";

        // Linux: try VAAPI
        if (OperatingSystem.IsLinux() && Directory.Exists("/dev/dri")) return "vaapi";

        return "";
    }

    /// <summary>
    ///     Get FFmpeg hardware acceleration arguments.
    /// </summary>
    private string GetHwAccelArgs(string? codec = null)
    {
        if (!_useHardwareAcceleration || string.IsNullOrEmpty(HardwareAccelerationType))
            return "";

        // Disable hwaccel for codecs known to cause issues
        if (!string.IsNullOrEmpty(codec) && _hwAccelProblematicCodecs.Contains(codec))
            return "";

        return HardwareAccelerationType switch
        {
            "cuda" => "-hwaccel cuda -hwaccel_output_format cuda ",
            "d3d11va" => "-hwaccel d3d11va ",
            "vaapi" => "-hwaccel vaapi -vaapi_device /dev/dri/renderD128 ",
            _ => ""
        };
    }

    /// <summary>
    ///     Get filter prefix for hwdownload if using CUDA.
    /// </summary>
    private string GetHwDownloadFilter(string? codec = null)
    {
        // Skip hwdownload for problematic codecs
        if (!string.IsNullOrEmpty(codec) && _hwAccelProblematicCodecs.Contains(codec))
            return "";

        if (HardwareAccelerationType == "cuda" && _useHardwareAcceleration)
            // hwdownload transfers from GPU to CPU memory
            // format=nv12 is required because that's the native format for CUDA decoded frames
            // The final -pix_fmt rgba will handle RGB conversion with proper color space
            return "hwdownload,format=nv12,";
        return "";
    }

    /// <summary>
    ///     Get comprehensive video information.
    /// </summary>
    public async Task<VideoInfo?> GetVideoInfoAsync(string videoPath, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Validate path to prevent command injection (allow URLs for streaming)
        if (!SecurityHelper.IsValidFilePath(videoPath) && !SecurityHelper.IsValidUrl(videoPath))
            throw new ArgumentException("Invalid video path", nameof(videoPath));

        var args = $"-v quiet -print_format json -show_format -show_streams \"{videoPath}\"";
        var output = await RunProcessAsync(_ffprobePath, args, ct);

        if (string.IsNullOrEmpty(output)) return null;

        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            double duration = 0;
            long bitRate = 0;
            string? videoCodec = null;
            var width = 0;
            var height = 0;
            double frameRate = 0;
            var totalFrames = 0;

            // Parse format info
            if (root.TryGetProperty("format", out var format))
            {
                duration = format.TryGetProperty("duration", out var d)
                    ? double.Parse(d.GetString() ?? "0")
                    : 0;
                bitRate = format.TryGetProperty("bit_rate", out var br)
                    ? long.Parse(br.GetString() ?? "0")
                    : 0;
            }

            // Parse streams
            if (root.TryGetProperty("streams", out var streams))
                foreach (var stream in streams.EnumerateArray())
                {
                    var codecType = stream.TryGetProperty("codec_type", out var ct2)
                        ? ct2.GetString()
                        : null;

                    if (codecType == "video" && videoCodec == null)
                    {
                        videoCodec = stream.TryGetProperty("codec_name", out var cn)
                            ? cn.GetString()
                            : null;
                        width = stream.TryGetProperty("width", out var w)
                            ? w.GetInt32()
                            : 0;
                        height = stream.TryGetProperty("height", out var h)
                            ? h.GetInt32()
                            : 0;

                        if (stream.TryGetProperty("r_frame_rate", out var fps))
                        {
                            var fpsStr = fps.GetString() ?? "0/1";
                            var parts = fpsStr.Split('/');
                            if (parts.Length == 2 &&
                                double.TryParse(parts[0], out var num) &&
                                double.TryParse(parts[1], out var den) && den > 0)
                                frameRate = num / den;
                        }

                        // Get total frame count
                        if (stream.TryGetProperty("nb_frames", out var nbf))
                            if (int.TryParse(nbf.GetString(), out var frameCount))
                                totalFrames = frameCount;
                    }
                }

            // Estimate total frames if not provided
            if (totalFrames == 0 && frameRate > 0 && duration > 0) totalFrames = (int)(duration * frameRate);

            return new VideoInfo
            {
                FilePath = videoPath,
                Duration = duration,
                BitRate = bitRate,
                VideoCodec = videoCodec,
                Width = width,
                Height = height,
                FrameRate = frameRate,
                TotalFrames = totalFrames
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Extract codec-level I-frames (keyframes) without decoding.
    ///     Very fast - uses compression metadata only.
    /// </summary>
    public async Task<List<KeyframeInfo>> GetKeyframesAsync(string videoPath, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var keyframes = new List<KeyframeInfo>();
        var args =
            $"-v quiet -select_streams v:0 -show_frames -show_entries frame=key_frame,pkt_pts_time,pict_type,coded_picture_number -of json \"{videoPath}\"";

        var output = await RunProcessAsync(_ffprobePath, args, ct);
        if (string.IsNullOrEmpty(output)) return keyframes;

        try
        {
            using var doc = JsonDocument.Parse(output);
            var frames = doc.RootElement.GetProperty("frames");

            foreach (var frame in frames.EnumerateArray())
            {
                var isKeyFrame = frame.TryGetProperty("key_frame", out var kf) && kf.GetInt32() == 1;
                if (!isKeyFrame) continue;

                var timestamp = 0.0;
                if (frame.TryGetProperty("pkt_pts_time", out var pts)) double.TryParse(pts.GetString(), out timestamp);

                var frameNumber = frame.TryGetProperty("coded_picture_number", out var cpn)
                    ? cpn.GetInt32()
                    : -1;

                keyframes.Add(new KeyframeInfo
                {
                    Timestamp = timestamp,
                    FrameNumber = frameNumber
                });
            }
        }
        catch
        {
        }

        return keyframes;
    }

    /// <summary>
    ///     Detect scene changes using FFmpeg's scene detection filter.
    ///     Returns timestamps where visual content changes significantly.
    /// </summary>
    public async Task<List<double>> DetectSceneChangesAsync(
        string videoPath,
        double threshold = 0.4,
        double? startTime = null,
        double? endTime = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var scenes = new List<double>();

        var hwAccel = GetHwAccelArgs();
        var hwDownload = GetHwDownloadFilter();

        // Build input args
        var inputArgs = "";
        if (startTime.HasValue)
            inputArgs += $"-ss {startTime.Value:F3} ";
        inputArgs += $"{hwAccel}-i \"{videoPath}\" ";
        if (endTime.HasValue && startTime.HasValue)
            inputArgs += $"-t {endTime.Value - startTime.Value:F3} ";
        else if (endTime.HasValue)
            inputArgs += $"-t {endTime.Value:F3} ";

        var args = $"{inputArgs}-vf \"{hwDownload}select='gt(scene,{threshold:F2})',showinfo\" -f null - 2>&1";

        var output = await RunProcessAsync(_ffmpegPath, args, ct);
        if (string.IsNullOrEmpty(output)) return scenes;

        var regex = new Regex(@"pts_time:(\d+\.?\d*)", RegexOptions.Compiled);
        var matches = regex.Matches(output);

        var offset = startTime ?? 0;
        foreach (Match match in matches)
            if (double.TryParse(match.Groups[1].Value, out var timestamp))
                scenes.Add(timestamp + offset);

        return scenes;
    }

    /// <summary>
    ///     Stream frames from video using pipe - no temp files needed.
    ///     Most efficient method for sequential playback.
    ///     Outputs RGBA32 for compatibility with renderers.
    /// </summary>
    /// <param name="codec">Optional codec hint to detect problematic codecs for hwaccel.</param>
    public async IAsyncEnumerable<Image<Rgba32>> StreamFramesAsync(
        string videoPath,
        int outputWidth,
        int outputHeight,
        double? startTime = null,
        double? endTime = null,
        int frameStep = 1,
        double? targetFps = null,
        string? codec = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Validate path to prevent command injection (allow URLs for streaming)
        if (!SecurityHelper.IsValidFilePath(videoPath) && !SecurityHelper.IsValidUrl(videoPath))
            throw new ArgumentException("Invalid video path", nameof(videoPath));

        var hwAccel = GetHwAccelArgs(codec);
        var hwDownload = GetHwDownloadFilter(codec);

        // Build filter chain
        var filters = new List<string>();

        if (!string.IsNullOrEmpty(hwDownload))
            filters.Add(hwDownload.TrimEnd(','));

        filters.Add($"scale={outputWidth}:{outputHeight}:flags=fast_bilinear");

        // Frame stepping via select filter
        if (frameStep > 1)
        {
            filters.Add($"select='not(mod(n\\,{frameStep}))'");
            filters.Add("setpts=N/FRAME_RATE/TB");
        }

        // Target FPS via fps filter
        if (targetFps.HasValue && targetFps.Value > 0) filters.Add($"fps={targetFps.Value:F2}");

        // Explicit format conversion to avoid auto_scale issues with some codecs (e.g., Theora)
        filters.Add("format=rgba");

        var filterChain = string.Join(",", filters);

        // Build input args for seeking
        // Always use -ss before -i for fast keyframe-based seeking
        // Even for time 0, this helps ensure we start at a proper keyframe
        var inputArgs = "";
        if (startTime.HasValue && startTime.Value > 0)
            inputArgs = $"-ss {startTime.Value:F3} ";
        else
            inputArgs = "-ss 0 "; // Explicit seek to 0 ensures keyframe alignment

        inputArgs += $"{hwAccel}-i \"{videoPath}\" ";

        if (endTime.HasValue && startTime.HasValue)
            inputArgs += $"-t {endTime.Value - startTime.Value:F3} ";
        else if (endTime.HasValue)
            inputArgs += $"-t {endTime.Value:F3} ";

        // Output raw RGBA frames to stdout
        var args = $"-loglevel error {inputArgs}-vf \"{filterChain}\" -f rawvideo -pix_fmt rgba -";

        var frameSize = outputWidth * outputHeight * 4; // RGBA = 4 bytes per pixel
        // Use ArrayPool to avoid large heap allocations (8MB+ for 1080p frames)
        var buffer = ArrayPool<byte>.Shared.Rent(frameSize);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Capture stderr for error reporting
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            var stream = process.StandardOutput.BaseStream;
            var framesProduced = 0;

            while (!ct.IsCancellationRequested)
            {
                var bytesRead = 0;
                while (bytesRead < frameSize)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(bytesRead, frameSize - bytesRead), ct);
                    if (read == 0)
                    {
                        // End of stream - check if this is an error
                        if (framesProduced == 0)
                        {
                            // No frames produced - likely an error
                            await process.WaitForExitAsync(ct);
                            var stderr = await stderrTask;
                            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(stderr))
                            {
                                var errorMsg = !string.IsNullOrWhiteSpace(stderr)
                                    ? $"FFmpeg error: {stderr.Trim()}"
                                    : $"FFmpeg exited with code {process.ExitCode} without producing any frames";
                                throw new InvalidOperationException(errorMsg);
                            }
                        }

                        yield break;
                    }

                    bytesRead += read;
                }

                // Create image from raw RGBA data
                var image = Image.LoadPixelData<Rgba32>(buffer, outputWidth, outputHeight);

                // Skip potentially corrupted frames at start from some codecs
                // Some codecs output garbage frames before keyframes are fully decoded
                // Check up to first 5 frames for corruption (after that, assume codec is stable)
                if (framesProduced < 5 && IsLikelyCorruptedFrame(buffer, outputWidth, outputHeight))
                {
                    image.Dispose();
                    continue; // Skip this frame, try next
                }

                framesProduced++;
                yield return image;
            }

            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Process already exited - safe to ignore
            }
        }
        finally
        {
            // Always return buffer to pool
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    ///     Check if a frame buffer appears corrupted (blank, uniform, or noise pattern).
    ///     Some codecs output garbage for the first frame before keyframes are decoded.
    /// </summary>
    private static bool IsLikelyCorruptedFrame(byte[] buffer, int width, int height)
    {
        // Sample a grid of pixels to check for corruption patterns
        const int sampleCount = 100;
        var totalPixels = width * height;
        var step = Math.Max(1, totalPixels / sampleCount);

        // Track color statistics
        var sameAsFirst = 0;
        byte firstR = buffer[0], firstG = buffer[1], firstB = buffer[2];

        // Count pure black/white pixels (common in corrupted frames)
        var blackCount = 0;
        var whiteCount = 0;
        var midGrayCount = 0; // Uninitialized RGBA often shows as mid-gray

        for (var i = 0; i < sampleCount; i++)
        {
            var pixelIdx = i * step * 4;
            if (pixelIdx + 3 >= buffer.Length) break;

            byte r = buffer[pixelIdx], g = buffer[pixelIdx + 1], b = buffer[pixelIdx + 2];

            // Check if same as first pixel (uniform frame)
            if (r == firstR && g == firstG && b == firstB)
                sameAsFirst++;

            // Check for common corruption patterns
            if (r == 0 && g == 0 && b == 0)
                blackCount++;
            else if (r == 255 && g == 255 && b == 255)
                whiteCount++;
            else if (r >= 120 && r <= 136 && g >= 120 && g <= 136 && b >= 120 && b <= 136)
                midGrayCount++; // Uninitialized memory often shows as gray
        }

        // Frame is likely corrupted if:
        // 1. 95%+ pixels are same color (blank frame)
        // 2. 90%+ pixels are pure black (not decoded yet)
        // 3. 90%+ pixels are pure white (not decoded yet - common with some codecs)
        // 4. 70%+ pixels are mid-gray (uninitialized)
        var uniformThreshold = sampleCount * 0.95;
        var blackThreshold = sampleCount * 0.90;
        var whiteThreshold = sampleCount * 0.90;
        var grayThreshold = sampleCount * 0.70;

        return sameAsFirst >= uniformThreshold ||
               blackCount >= blackThreshold ||
               whiteCount >= whiteThreshold ||
               midGrayCount >= grayThreshold;
    }

    /// <summary>
    ///     Extract a single frame at a specific timestamp.
    ///     Returns the frame as an Image for direct processing.
    /// </summary>
    public async Task<Image<Rgba32>?> ExtractFrameAsync(
        string videoPath,
        double timestamp,
        int? width = null,
        int? height = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Validate path to prevent command injection (allow URLs for streaming)
        if (!SecurityHelper.IsValidFilePath(videoPath) && !SecurityHelper.IsValidUrl(videoPath))
            throw new ArgumentException("Invalid video path", nameof(videoPath));

        var hwAccel = GetHwAccelArgs();
        var hwDownload = GetHwDownloadFilter();

        // Get video dimensions if not specified
        var outputWidth = width ?? 0;
        var outputHeight = height ?? 0;

        if (outputWidth == 0 || outputHeight == 0)
        {
            var info = await GetVideoInfoAsync(videoPath, ct);
            if (info == null) return null;

            if (outputWidth == 0 && outputHeight == 0)
            {
                outputWidth = info.Width;
                outputHeight = info.Height;
            }
            else if (outputWidth == 0)
            {
                outputWidth = (int)(info.Width * ((double)outputHeight / info.Height));
            }
            else
            {
                outputHeight = (int)(info.Height * ((double)outputWidth / info.Width));
            }
        }

        var filters = new List<string>();
        if (!string.IsNullOrEmpty(hwDownload))
            filters.Add(hwDownload.TrimEnd(','));
        filters.Add($"scale={outputWidth}:{outputHeight}:flags=fast_bilinear");
        filters.Add("format=rgba");

        var filterChain = string.Join(",", filters);

        var args =
            $"-loglevel error -ss {timestamp:F3} {hwAccel}-i \"{videoPath}\" -vf \"{filterChain}\" -frames:v 1 -f rawvideo -pix_fmt rgba -";

        var frameSize = outputWidth * outputHeight * 4;
        var buffer = new byte[frameSize];

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        _ = process.StandardError.ReadToEndAsync(ct);

        var pStream = process.StandardOutput.BaseStream;
        var bytesRead = 0;

        while (bytesRead < frameSize)
        {
            var read = await pStream.ReadAsync(buffer.AsMemory(bytesRead, frameSize - bytesRead), ct);
            if (read == 0) break;
            bytesRead += read;
        }

        await process.WaitForExitAsync(ct);

        if (bytesRead < frameSize) return null;

        return Image.LoadPixelData<Rgba32>(buffer, outputWidth, outputHeight);
    }

    /// <summary>
    ///     Extract a single frame very fast by seeking to nearest keyframe only.
    ///     Useful for thumbnail generation where exact timestamp accuracy is not required.
    ///     Uses -skip_frame nokey for ~100x faster extraction.
    /// </summary>
    public async Task<Image<Rgba32>?> ExtractFrameFastAsync(
        string videoPath,
        double timestamp,
        int? width = null,
        int? height = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Get video dimensions if not specified
        var outputWidth = width ?? 0;
        var outputHeight = height ?? 0;

        if (outputWidth == 0 || outputHeight == 0)
        {
            var info = await GetVideoInfoAsync(videoPath, ct);
            if (info == null) return null;

            if (outputWidth == 0 && outputHeight == 0)
            {
                outputWidth = info.Width;
                outputHeight = info.Height;
            }
            else if (outputWidth == 0)
            {
                outputWidth = (int)(info.Width * ((double)outputHeight / info.Height));
            }
            else
            {
                outputHeight = (int)(info.Height * ((double)outputWidth / info.Width));
            }
        }

        var filters = $"scale={outputWidth}:{outputHeight}:flags=fast_bilinear,format=rgba";

        // Use -skip_frame nokey to only decode keyframes (much faster)
        // -vsync passthrough avoids frame duplication
        var args =
            $"-loglevel error -skip_frame nokey -ss {timestamp:F3} -i \"{videoPath}\" -vf \"{filters}\" -vsync passthrough -frames:v 1 -f rawvideo -pix_fmt rgba -";

        var frameSize = outputWidth * outputHeight * 4;
        var buffer = new byte[frameSize];

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        _ = process.StandardError.ReadToEndAsync(ct);

        var pStream = process.StandardOutput.BaseStream;
        var bytesRead = 0;

        while (bytesRead < frameSize)
        {
            var read = await pStream.ReadAsync(buffer.AsMemory(bytesRead, frameSize - bytesRead), ct);
            if (read == 0) break;
            bytesRead += read;
        }

        await process.WaitForExitAsync(ct);

        if (bytesRead < frameSize) return null;

        return Image.LoadPixelData<Rgba32>(buffer, outputWidth, outputHeight);
    }

    /// <summary>
    ///     Extract all I-frames (keyframes) from a video segment very quickly.
    ///     Uses select filter with I-frame detection - much faster than scene detection.
    ///     Great for generating scrub bar thumbnails.
    /// </summary>
    public async IAsyncEnumerable<(double Timestamp, Image<Rgba32> Frame)> ExtractKeyframeThumbnailsAsync(
        string videoPath,
        int outputWidth,
        int outputHeight,
        double? startTime = null,
        double? endTime = null,
        int maxCount = 60,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Build input args for seeking
        var inputArgs = "";
        if (startTime.HasValue)
            inputArgs = $"-ss {startTime.Value:F3} ";

        inputArgs += $"-i \"{videoPath}\" ";

        if (endTime.HasValue && startTime.HasValue)
            inputArgs += $"-t {endTime.Value - startTime.Value:F3} ";
        else if (endTime.HasValue)
            inputArgs += $"-t {endTime.Value:F3} ";

        // Select only I-frames, scale down, limit count
        // The showinfo filter gives us the timestamp
        var filters =
            $"select='eq(pict_type,I)',scale={outputWidth}:{outputHeight}:flags=fast_bilinear,format=rgba,showinfo";

        // Output raw frames - limit to maxCount
        var args =
            $"-loglevel error {inputArgs}-vf \"{filters}\" -vsync vfr -frames:v {maxCount} -f rawvideo -pix_fmt rgba -";

        var frameSize = outputWidth * outputHeight * 4;
        var buffer = new byte[frameSize];
        var offset = startTime ?? 0;
        var count = 0;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Capture stderr for timestamp info
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var stream = process.StandardOutput.BaseStream;

        while (!ct.IsCancellationRequested && count < maxCount)
        {
            var bytesRead = 0;
            while (bytesRead < frameSize)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(bytesRead, frameSize - bytesRead), ct);
                if (read == 0) yield break;
                bytesRead += read;
            }

            // Create image from raw RGBA data
            var image = Image.LoadPixelData<Rgba32>(buffer, outputWidth, outputHeight);

            // Estimate timestamp based on position (we can't easily parse showinfo from pipe)
            // For thumbnails, uniform distribution is fine
            var info = await GetVideoInfoAsync(videoPath, ct);
            var duration = info?.Duration ?? 60;
            var timestamp = offset + count * (duration - offset) / maxCount;

            yield return (timestamp, image);
            count++;
        }

        try
        {
            if (!process.HasExited) process.Kill();
        }
        catch
        {
        }
    }

    /// <summary>
    ///     Extract frames at specific timestamps.
    /// </summary>
    public async Task<List<(double Timestamp, Image<Rgba32> Frame)>> ExtractFramesAtTimestampsAsync(
        string videoPath,
        IEnumerable<double> timestamps,
        int? width = null,
        int? height = null,
        CancellationToken ct = default)
    {
        var results = new List<(double, Image<Rgba32>)>();

        // Sort timestamps for sequential seeking
        var sortedTimestamps = timestamps.OrderBy(t => t).ToList();

        foreach (var timestamp in sortedTimestamps)
        {
            ct.ThrowIfCancellationRequested();

            var frame = await ExtractFrameAsync(videoPath, timestamp, width, height, ct);
            if (frame != null) results.Add((timestamp, frame));
        }

        return results;
    }

    private async Task<string> RunProcessAsync(string executable, string args, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var output = await outputTask;
            var error = await errorTask;

            return string.IsNullOrEmpty(output) ? error : output;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FindExecutable(string name)
    {
        var exeName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;

        if (_ffmpegBinPath != null)
        {
            var cachedPath = Path.Combine(_ffmpegBinPath, exeName);
            if (File.Exists(cachedPath)) return cachedPath;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir, exeName);
            if (File.Exists(fullPath))
            {
                lock (_ffmpegLock)
                {
                    _ffmpegBinPath = dir;
                }

                return fullPath;
            }
        }

        var searchPaths = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WinGet", "Packages"),
            @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "scoop", "apps", "ffmpeg", "current", "bin"),
            @"C:\ffmpeg\bin",
            @"C:\Program Files\ffmpeg\bin",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin"),
            "/usr/bin",
            "/usr/local/bin",
            "/opt/homebrew/bin"
        };

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            if (basePath.Contains("WinGet") || basePath.Contains("winget"))
            {
                try
                {
                    var foundExe = Directory.GetFiles(basePath, exeName, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (foundExe != null)
                    {
                        var binDir = Path.GetDirectoryName(foundExe)!;
                        lock (_ffmpegLock)
                        {
                            _ffmpegBinPath = binDir;
                        }

                        return foundExe;
                    }
                }
                catch
                {
                }
            }
            else
            {
                var fullPath = Path.Combine(basePath, exeName);
                if (File.Exists(fullPath))
                {
                    lock (_ffmpegLock)
                    {
                        _ffmpegBinPath = basePath;
                    }

                    return fullPath;
                }
            }
        }

        return name;
    }

    /// <summary>
    ///     Extract a clip from video and re-encode to a new file.
    ///     Supports various output formats based on file extension.
    /// </summary>
    public async Task<bool> ExtractClipAsync(
        string inputPath,
        string outputPath,
        double startTime,
        double duration,
        int targetWidth,
        int quality,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Validate paths to prevent command injection
        if (!SecurityHelper.IsValidFilePath(inputPath) && !SecurityHelper.IsValidUrl(inputPath))
            throw new ArgumentException("Invalid input path", nameof(inputPath));
        if (!SecurityHelper.IsValidFilePath(outputPath))
            throw new ArgumentException("Invalid output path", nameof(outputPath));

        var ext = Path.GetExtension(outputPath).ToLowerInvariant();

        // Build FFmpeg arguments based on output format
        var args = $"-y -ss {startTime:F3} -t {duration:F3} -i \"{inputPath}\"";

        // Scale filter
        args += $" -vf \"scale={targetWidth}:-2\"";

        // Format-specific encoding
        args += ext switch
        {
            ".mp4" => $" -c:v libx264 -crf {Math.Max(0, 51 - quality / 2)} -preset fast -an",
            ".webm" => $" -c:v libvpx-vp9 -crf {Math.Max(0, 63 - quality * 63 / 100)} -b:v 0 -an",
            ".mkv" => $" -c:v libx264 -crf {Math.Max(0, 51 - quality / 2)} -preset fast -an",
            ".avi" => $" -c:v mpeg4 -q:v {Math.Max(1, 31 - quality * 30 / 100)} -an",
            ".mov" => $" -c:v libx264 -crf {Math.Max(0, 51 - quality / 2)} -preset fast -an",
            _ => $" -c:v libx264 -crf {Math.Max(0, 51 - quality / 2)} -preset fast -an"
        };

        args += $" \"{outputPath}\"";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            // Read stderr to completion
            var errorOutput = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var lines = errorOutput.Split('\n').TakeLast(5);
                Console.Error.WriteLine($"FFmpeg error: {string.Join("\n", lines)}");
                return false;
            }

            return File.Exists(outputPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error extracting clip: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Get information about embedded subtitle streams in a video file.
    /// </summary>
    public async Task<List<SubtitleStreamInfo>> GetSubtitleStreamsAsync(string videoPath,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var subtitles = new List<SubtitleStreamInfo>();

        // Validate path to prevent command injection (allow URLs for streaming)
        if (!SecurityHelper.IsValidFilePath(videoPath) && !SecurityHelper.IsValidUrl(videoPath))
            throw new ArgumentException("Invalid video path", nameof(videoPath));

        var args = $"-v quiet -print_format json -show_streams -select_streams s \"{videoPath}\"";
        var output = await RunProcessAsync(_ffprobePath, args, ct);

        if (string.IsNullOrEmpty(output)) return subtitles;

        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            if (root.TryGetProperty("streams", out var streams))
            {
                var index = 0;
                foreach (var stream in streams.EnumerateArray())
                {
                    var codecName = stream.TryGetProperty("codec_name", out var cn)
                        ? cn.GetString()
                        : null;

                    // Get language from tags
                    string? language = null;
                    string? title = null;
                    if (stream.TryGetProperty("tags", out var tags))
                    {
                        if (tags.TryGetProperty("language", out var lang))
                            language = lang.GetString();
                        if (tags.TryGetProperty("title", out var t))
                            title = t.GetString();
                    }

                    var streamIndex = stream.TryGetProperty("index", out var si)
                        ? si.GetInt32()
                        : index;

                    subtitles.Add(new SubtitleStreamInfo
                    {
                        Index = streamIndex,
                        Codec = codecName ?? "unknown",
                        Language = language,
                        Title = title
                    });
                    index++;
                }
            }
        }
        catch
        {
        }

        return subtitles;
    }

    /// <summary>
    ///     Extract embedded subtitles from a video file to SRT format.
    /// </summary>
    /// <param name="videoPath">Path to video file</param>
    /// <param name="outputPath">Path to save SRT file</param>
    /// <param name="streamIndex">Subtitle stream index (from GetSubtitleStreamsAsync), or null for first subtitle stream</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if extraction succeeded</returns>
    public async Task<bool> ExtractSubtitlesAsync(
        string videoPath,
        string outputPath,
        int? streamIndex = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Validate paths
        if (!SecurityHelper.IsValidFilePath(videoPath) && !SecurityHelper.IsValidUrl(videoPath))
            throw new ArgumentException("Invalid video path", nameof(videoPath));
        if (!SecurityHelper.IsValidFilePath(outputPath))
            throw new ArgumentException("Invalid output path", nameof(outputPath));

        // Build FFmpeg command
        // -map 0:s:0 selects first subtitle stream, or use specific index
        var streamMap = streamIndex.HasValue ? $"0:{streamIndex.Value}" : "0:s:0";
        var args = $"-y -i \"{videoPath}\" -map {streamMap} -c:s srt \"{outputPath}\"";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0 && File.Exists(outputPath);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
///     Video stream information.
/// </summary>
public class VideoInfo
{
    public string FilePath { get; set; } = "";
    public double Duration { get; set; }
    public long BitRate { get; set; }
    public string? VideoCodec { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public int TotalFrames { get; set; }
}

/// <summary>
///     Information about a codec-level keyframe.
/// </summary>
public record KeyframeInfo
{
    public double Timestamp { get; init; }
    public int FrameNumber { get; init; }
}

/// <summary>
///     Information about an embedded subtitle stream.
/// </summary>
public record SubtitleStreamInfo
{
    /// <summary>Stream index in the video file.</summary>
    public int Index { get; init; }

    /// <summary>Subtitle codec (subrip, ass, dvd_subtitle, etc.).</summary>
    public string Codec { get; init; } = "";

    /// <summary>Language code (en, es, fr, etc.) if available.</summary>
    public string? Language { get; init; }

    /// <summary>Stream title if available.</summary>
    public string? Title { get; init; }

    /// <summary>
    ///     Check if this subtitle format can be extracted to text (SRT).
    ///     Bitmap-based subtitles (dvd_subtitle, hdmv_pgs, dvb_subtitle) cannot.
    /// </summary>
    public bool IsTextBased => Codec is "subrip" or "ass" or "ssa" or "webvtt" or "mov_text" or "srt";
}