using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Video.Core;

/// <summary>
/// FFmpeg service for video frame extraction with hardware acceleration.
/// Uses pipe-based streaming for efficient memory usage - no temp files needed.
/// </summary>
public sealed class FFmpegService : IDisposable
{
    private string _ffprobePath;
    private string _ffmpegPath;
    private readonly bool _useHardwareAcceleration;
    private string _hwAccelType;
    private bool _initialized;

    private static string? _ffmpegBinPath;
    private static readonly object _ffmpegLock = new();

    /// <summary>
    /// Create FFmpegService with optional custom paths.
    /// If no paths provided, will use FFmpegProvider to find or download FFmpeg.
    /// </summary>
    public FFmpegService(
        string? ffprobePath = null,
        string? ffmpegPath = null,
        bool useHardwareAcceleration = true)
    {
        _ffprobePath = ffprobePath ?? "";
        _ffmpegPath = ffmpegPath ?? "";
        _useHardwareAcceleration = useHardwareAcceleration;
        _hwAccelType = "";

        // If paths provided, initialize immediately
        if (!string.IsNullOrEmpty(ffmpegPath) || !string.IsNullOrEmpty(ffprobePath))
        {
            _ffmpegPath = ffmpegPath ?? FindExecutable("ffmpeg");
            _ffprobePath = ffprobePath ?? FindExecutable("ffprobe");
            _hwAccelType = useHardwareAcceleration ? DetectHardwareAcceleration() : "";
            _initialized = true;
        }
    }

    /// <summary>
    /// Initialize FFmpeg paths asynchronously, downloading if necessary.
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

        _hwAccelType = _useHardwareAcceleration ? DetectHardwareAcceleration() : "";
        _initialized = true;
    }

    /// <summary>
    /// Verify FFmpeg can execute by running a simple version check.
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
    /// Ensure initialized before operations.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(null, ct);
        }
    }

    /// <summary>
    /// Get FFmpeg status information.
    /// </summary>
    public static string GetStatus(string? customPath = null) => FFmpegProvider.GetStatus(customPath);

    /// <summary>
    /// Check if FFmpeg is available (without downloading).
    /// </summary>
    public static bool IsAvailable(string? customPath = null) => FFmpegProvider.IsAvailable(customPath);

    /// <summary>
    /// The detected hardware acceleration type (cuda, d3d11va, vaapi, or empty).
    /// </summary>
    public string HardwareAccelerationType => _hwAccelType;

    /// <summary>
    /// Detect available hardware acceleration.
    /// </summary>
    private static string DetectHardwareAcceleration()
    {
        // Check for NVIDIA GPU first (CUDA/NVDEC)
        if (File.Exists(@"C:\Windows\System32\nvidia-smi.exe") ||
            File.Exists(@"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe") ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CUDA_PATH")))
        {
            return "cuda";
        }

        // Check for AMD GPU (DirectX Video Acceleration on Windows)
        if (OperatingSystem.IsWindows())
        {
            return "d3d11va";
        }

        // Linux: try VAAPI
        if (OperatingSystem.IsLinux() && Directory.Exists("/dev/dri"))
        {
            return "vaapi";
        }

        return "";
    }

    /// <summary>
    /// Codecs known to have issues with hardware acceleration.
    /// These typically cause "auto_scale" filter errors or other conversion issues.
    /// </summary>
    private static readonly HashSet<string> _hwAccelProblematicCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "theora",   // Ogg Theora - causes auto_scale filter errors
        "vp6",      // Old Flash codec
        "vp6f",     // Flash VP6
        "gif",      // GIF as video
        "apng",     // Animated PNG
        "mpeg4",    // MPEG-4 part 2 / DivX - hwdownload nv12 format issues
    };

    /// <summary>
    /// Get FFmpeg hardware acceleration arguments.
    /// </summary>
    private string GetHwAccelArgs(string? codec = null)
    {
        if (!_useHardwareAcceleration || string.IsNullOrEmpty(_hwAccelType))
            return "";

        // Disable hwaccel for codecs known to cause issues
        if (!string.IsNullOrEmpty(codec) && _hwAccelProblematicCodecs.Contains(codec))
            return "";

        return _hwAccelType switch
        {
            "cuda" => "-hwaccel cuda -hwaccel_output_format cuda ",
            "d3d11va" => "-hwaccel d3d11va ",
            "vaapi" => "-hwaccel vaapi -vaapi_device /dev/dri/renderD128 ",
            _ => ""
        };
    }

    /// <summary>
    /// Get filter prefix for hwdownload if using CUDA.
    /// </summary>
    private string GetHwDownloadFilter(string? codec = null)
    {
        // Skip hwdownload for problematic codecs
        if (!string.IsNullOrEmpty(codec) && _hwAccelProblematicCodecs.Contains(codec))
            return "";

        if (_hwAccelType == "cuda" && _useHardwareAcceleration)
        {
            // hwdownload transfers from GPU to CPU memory
            // format=nv12 is required because that's the native format for CUDA decoded frames
            // The final -pix_fmt rgba will handle RGB conversion with proper color space
            return "hwdownload,format=nv12,";
        }
        return "";
    }

    /// <summary>
    /// Get comprehensive video information.
    /// </summary>
    public async Task<VideoInfo?> GetVideoInfoAsync(string videoPath, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
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
            int width = 0;
            int height = 0;
            double frameRate = 0;
            int totalFrames = 0;

            // Parse format info
            if (root.TryGetProperty("format", out var format))
            {
                duration = format.TryGetProperty("duration", out var d)
                    ? double.Parse(d.GetString() ?? "0") : 0;
                bitRate = format.TryGetProperty("bit_rate", out var br)
                    ? long.Parse(br.GetString() ?? "0") : 0;
            }

            // Parse streams
            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    var codecType = stream.TryGetProperty("codec_type", out var ct2)
                        ? ct2.GetString() : null;

                    if (codecType == "video" && videoCodec == null)
                    {
                        videoCodec = stream.TryGetProperty("codec_name", out var cn)
                            ? cn.GetString() : null;
                        width = stream.TryGetProperty("width", out var w)
                            ? w.GetInt32() : 0;
                        height = stream.TryGetProperty("height", out var h)
                            ? h.GetInt32() : 0;

                        if (stream.TryGetProperty("r_frame_rate", out var fps))
                        {
                            var fpsStr = fps.GetString() ?? "0/1";
                            var parts = fpsStr.Split('/');
                            if (parts.Length == 2 &&
                                double.TryParse(parts[0], out var num) &&
                                double.TryParse(parts[1], out var den) && den > 0)
                            {
                                frameRate = num / den;
                            }
                        }

                        // Get total frame count
                        if (stream.TryGetProperty("nb_frames", out var nbf))
                        {
                            if (int.TryParse(nbf.GetString(), out var frameCount))
                            {
                                totalFrames = frameCount;
                            }
                        }
                    }
                }
            }

            // Estimate total frames if not provided
            if (totalFrames == 0 && frameRate > 0 && duration > 0)
            {
                totalFrames = (int)(duration * frameRate);
            }

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
    /// Extract codec-level I-frames (keyframes) without decoding.
    /// Very fast - uses compression metadata only.
    /// </summary>
    public async Task<List<KeyframeInfo>> GetKeyframesAsync(string videoPath, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var keyframes = new List<KeyframeInfo>();
        var args = $"-v quiet -select_streams v:0 -show_frames -show_entries frame=key_frame,pkt_pts_time,pict_type,coded_picture_number -of json \"{videoPath}\"";

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
                if (frame.TryGetProperty("pkt_pts_time", out var pts))
                {
                    double.TryParse(pts.GetString(), out timestamp);
                }

                var frameNumber = frame.TryGetProperty("coded_picture_number", out var cpn)
                    ? cpn.GetInt32() : -1;

                keyframes.Add(new KeyframeInfo
                {
                    Timestamp = timestamp,
                    FrameNumber = frameNumber
                });
            }
        }
        catch { }

        return keyframes;
    }

    /// <summary>
    /// Detect scene changes using FFmpeg's scene detection filter.
    /// Returns timestamps where visual content changes significantly.
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
            inputArgs += $"-t {(endTime.Value - startTime.Value):F3} ";
        else if (endTime.HasValue)
            inputArgs += $"-t {endTime.Value:F3} ";

        var args = $"{inputArgs}-vf \"{hwDownload}select='gt(scene,{threshold:F2})',showinfo\" -f null - 2>&1";

        var output = await RunProcessAsync(_ffmpegPath, args, ct);
        if (string.IsNullOrEmpty(output)) return scenes;

        var regex = new Regex(@"pts_time:(\d+\.?\d*)", RegexOptions.Compiled);
        var matches = regex.Matches(output);

        var offset = startTime ?? 0;
        foreach (Match match in matches)
        {
            if (double.TryParse(match.Groups[1].Value, out var timestamp))
            {
                scenes.Add(timestamp + offset);
            }
        }

        return scenes;
    }

    /// <summary>
    /// Stream frames from video using pipe - no temp files needed.
    /// Most efficient method for sequential playback.
    /// Outputs RGBA32 for compatibility with renderers.
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
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
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
        if (targetFps.HasValue && targetFps.Value > 0)
        {
            filters.Add($"fps={targetFps.Value:F2}");
        }

        // Explicit format conversion to avoid auto_scale issues with some codecs (e.g., Theora)
        filters.Add("format=rgba");

        var filterChain = string.Join(",", filters);

        // Build input args for seeking
        var inputArgs = "";
        if (startTime.HasValue)
            inputArgs = $"-ss {startTime.Value:F3} ";

        inputArgs += $"{hwAccel}-i \"{videoPath}\" ";

        if (endTime.HasValue && startTime.HasValue)
            inputArgs += $"-t {(endTime.Value - startTime.Value):F3} ";
        else if (endTime.HasValue)
            inputArgs += $"-t {endTime.Value:F3} ";

        // Output raw RGBA frames to stdout
        var args = $"-loglevel error {inputArgs}-vf \"{filterChain}\" -f rawvideo -pix_fmt rgba -";

        var frameSize = outputWidth * outputHeight * 4; // RGBA = 4 bytes per pixel
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
            framesProduced++;

            yield return image;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch { }
    }

    /// <summary>
    /// Extract a single frame at a specific timestamp.
    /// Returns the frame as an Image for direct processing.
    /// </summary>
    public async Task<Image<Rgba32>?> ExtractFrameAsync(
        string videoPath,
        double timestamp,
        int? width = null,
        int? height = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var hwAccel = GetHwAccelArgs();
        var hwDownload = GetHwDownloadFilter();

        // Get video dimensions if not specified
        int outputWidth = width ?? 0;
        int outputHeight = height ?? 0;

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

        var args = $"-loglevel error -ss {timestamp:F3} {hwAccel}-i \"{videoPath}\" -vf \"{filterChain}\" -frames:v 1 -f rawvideo -pix_fmt rgba -";

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
    /// Extract a single frame very fast by seeking to nearest keyframe only.
    /// Useful for thumbnail generation where exact timestamp accuracy is not required.
    /// Uses -skip_frame nokey for ~100x faster extraction.
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
        int outputWidth = width ?? 0;
        int outputHeight = height ?? 0;

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
        var args = $"-loglevel error -skip_frame nokey -ss {timestamp:F3} -i \"{videoPath}\" -vf \"{filters}\" -vsync passthrough -frames:v 1 -f rawvideo -pix_fmt rgba -";

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
    /// Extract all I-frames (keyframes) from a video segment very quickly.
    /// Uses select filter with I-frame detection - much faster than scene detection.
    /// Great for generating scrub bar thumbnails.
    /// </summary>
    public async IAsyncEnumerable<(double Timestamp, Image<Rgba32> Frame)> ExtractKeyframeThumbnailsAsync(
        string videoPath,
        int outputWidth,
        int outputHeight,
        double? startTime = null,
        double? endTime = null,
        int maxCount = 60,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Build input args for seeking
        var inputArgs = "";
        if (startTime.HasValue)
            inputArgs = $"-ss {startTime.Value:F3} ";

        inputArgs += $"-i \"{videoPath}\" ";

        if (endTime.HasValue && startTime.HasValue)
            inputArgs += $"-t {(endTime.Value - startTime.Value):F3} ";
        else if (endTime.HasValue)
            inputArgs += $"-t {endTime.Value:F3} ";

        // Select only I-frames, scale down, limit count
        // The showinfo filter gives us the timestamp
        var filters = $"select='eq(pict_type,I)',scale={outputWidth}:{outputHeight}:flags=fast_bilinear,format=rgba,showinfo";

        // Output raw frames - limit to maxCount
        var args = $"-loglevel error {inputArgs}-vf \"{filters}\" -vsync vfr -frames:v {maxCount} -f rawvideo -pix_fmt rgba -";

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
                if (read == 0)
                {
                    yield break;
                }
                bytesRead += read;
            }

            // Create image from raw RGBA data
            var image = Image.LoadPixelData<Rgba32>(buffer, outputWidth, outputHeight);

            // Estimate timestamp based on position (we can't easily parse showinfo from pipe)
            // For thumbnails, uniform distribution is fine
            var info = await GetVideoInfoAsync(videoPath, ct);
            var duration = info?.Duration ?? 60;
            var timestamp = offset + (count * (duration - offset) / maxCount);

            yield return (timestamp, image);
            count++;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch { }
    }

    /// <summary>
    /// Extract frames at specific timestamps.
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
            if (frame != null)
            {
                results.Add((timestamp, frame));
            }
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
                lock (_ffmpegLock) { _ffmpegBinPath = dir; }
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
                        lock (_ffmpegLock) { _ffmpegBinPath = binDir; }
                        return foundExe;
                    }
                }
                catch { }
            }
            else
            {
                var fullPath = Path.Combine(basePath, exeName);
                if (File.Exists(fullPath))
                {
                    lock (_ffmpegLock) { _ffmpegBinPath = basePath; }
                    return fullPath;
                }
            }
        }

        return name;
    }

    public void Dispose()
    {
        // No managed resources to dispose
    }
}

/// <summary>
/// Video stream information.
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
/// Information about a codec-level keyframe.
/// </summary>
public record KeyframeInfo
{
    public double Timestamp { get; init; }
    public int FrameNumber { get; init; }
}
