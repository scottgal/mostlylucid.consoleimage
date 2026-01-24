using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ConsoleImage.Core;

/// <summary>
/// Provides yt-dlp binary - from PATH, common locations, or auto-downloaded.
/// </summary>
public static class YtdlpProvider
{
    private static string? _resolvedPath;
    private static readonly object _lock = new();

    // yt-dlp download URLs for different platforms
    private static readonly Dictionary<string, string> DownloadUrls = new()
    {
        ["win-x64"] = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
        ["win-arm64"] = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
        ["linux-x64"] = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux",
        ["linux-arm64"] = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux_aarch64",
        ["osx-x64"] = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos",
        ["osx-arm64"] = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos"
    };

    // YouTube URL pattern
    private static readonly Regex YouTubeRegex = new(
        @"^(https?://)?(www\.)?(youtube\.com/(watch\?v=|shorts/|embed/|v/)|youtu\.be/)[\w\-]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Get the local cache directory for yt-dlp binary.
    /// </summary>
    public static string CacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "consoleimage", "ytdlp");

    /// <summary>
    /// Check if a URL is a YouTube video URL.
    /// </summary>
    public static bool IsYouTubeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        url = url.Trim().Trim('"', '\'');
        return YouTubeRegex.IsMatch(url);
    }

    /// <summary>
    /// Gets path to yt-dlp executable, downloading if necessary.
    /// </summary>
    public static async Task<string> GetYtdlpPathAsync(
        string? customPath = null,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        // 1. Custom path takes priority
        if (!string.IsNullOrEmpty(customPath))
        {
            if (File.Exists(customPath))
                return customPath;

            throw new FileNotFoundException($"yt-dlp not found at: {customPath}");
        }

        // 2. Check if already resolved
        lock (_lock)
        {
            if (_resolvedPath != null && File.Exists(_resolvedPath))
                return _resolvedPath;
        }

        // 3. Check PATH and common locations
        var inPath = FindInPath();
        if (inPath != null)
        {
            lock (_lock) { _resolvedPath = inPath; }
            return inPath;
        }

        // 4. Check cache
        var cached = FindInCache();
        if (cached != null)
        {
            lock (_lock) { _resolvedPath = cached; }
            return cached;
        }

        // 5. Download
        progress?.Report(("yt-dlp not found, downloading...", 0));
        var downloaded = await DownloadYtdlpAsync(progress, ct);
        lock (_lock) { _resolvedPath = downloaded; }
        return downloaded;
    }

    /// <summary>
    /// Check if yt-dlp is available without downloading.
    /// </summary>
    public static bool IsAvailable(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            return File.Exists(customPath);
        }

        return FindInPath() != null || FindInCache() != null;
    }

    /// <summary>
    /// Get yt-dlp location status for display.
    /// </summary>
    public static string GetStatus(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            return File.Exists(customPath)
                ? $"Custom: {customPath}"
                : $"Not found at: {customPath}";
        }

        var inPath = FindInPath();
        if (inPath != null) return $"System: {inPath}";

        var cached = FindInCache();
        if (cached != null) return $"Downloaded: {cached}";

        return "Not found (will download on first use)";
    }

    /// <summary>
    /// Check if yt-dlp needs to be downloaded and return status information.
    /// </summary>
    /// <returns>Tuple of (needsDownload, statusMessage, downloadUrl)</returns>
    public static (bool NeedsDownload, string StatusMessage, string? DownloadUrl) GetDownloadStatus()
    {
        if (IsAvailable())
        {
            return (false, GetStatus(), null);
        }

        var rid = GetRuntimeIdentifier();
        if (!DownloadUrls.TryGetValue(rid, out var url))
        {
            return (false, $"No auto-download available for {rid}. Please install yt-dlp manually.", null);
        }

        return (true, $"yt-dlp not found. Can auto-download (~10MB) to: {CacheDirectory}", url);
    }

    /// <summary>
    /// Download yt-dlp with explicit user confirmation.
    /// </summary>
    public static async Task<string> DownloadAsync(
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        if (IsAvailable())
        {
            return (await GetYtdlpPathAsync(null, null, ct))!;
        }

        return await DownloadYtdlpAsync(progress, ct);
    }

    /// <summary>
    /// Clear downloaded yt-dlp from cache.
    /// </summary>
    public static void ClearCache()
    {
        if (Directory.Exists(CacheDirectory))
        {
            try { Directory.Delete(CacheDirectory, true); }
            catch { }
        }
        lock (_lock) { _resolvedPath = null; }
    }

    /// <summary>
    /// Get YouTube video stream information using yt-dlp.
    /// </summary>
    public static async Task<YouTubeStreamInfo?> GetStreamInfoAsync(
        string youtubeUrl,
        string? ytdlpPath = null,
        int? maxHeight = null,
        CancellationToken ct = default)
    {
        var ytdlp = ytdlpPath ?? FindInPath() ?? FindInCache();
        if (ytdlp == null)
            return null;

        // Build format selector - prefer direct MP4/WebM streams over HLS
        // [protocol!=m3u8] excludes HLS manifests which FFmpeg has trouble streaming
        // [protocol!=m3u8_native] excludes native HLS as well
        var format = maxHeight.HasValue
            ? $"best[height<={maxHeight}][protocol!*=m3u8]/bestvideo[height<={maxHeight}][protocol!*=m3u8]+bestaudio[protocol!*=m3u8]/best[height<={maxHeight}]/best"
            : "best[protocol!*=m3u8]/bestvideo[protocol!*=m3u8]+bestaudio[protocol!*=m3u8]/best";

        // Get URL and title
        var args = $"-f \"{format}\" -g --no-warnings --no-playlist \"{youtubeUrl}\"";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ytdlp,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine($"yt-dlp error: {error}");
                return null;
            }

            var lines = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return null;

            var videoUrl = lines[0].Trim();
            var audioUrl = lines.Length > 1 ? lines[1].Trim() : null;

            // Get title separately
            var title = await GetTitleAsync(ytdlp, youtubeUrl, ct);

            return new YouTubeStreamInfo
            {
                VideoUrl = videoUrl,
                AudioUrl = audioUrl,
                Title = title ?? "YouTube Video"
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Failed to extract YouTube URL: {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> GetTitleAsync(string ytdlp, string youtubeUrl, CancellationToken ct)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ytdlp,
                Arguments = $"--get-title --no-warnings --no-playlist \"{youtubeUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var title = await process.StandardOutput.ReadLineAsync(ct);
            await process.WaitForExitAsync(ct);

            return title?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string GetExecutableName()
    {
        return OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
    }

    private static string? FindInPath()
    {
        var names = new[] { "yt-dlp", "yt-dlp.exe", "youtube-dl", "youtube-dl.exe" };
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";

        foreach (var name in names)
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(dir, name);
                if (File.Exists(fullPath)) return fullPath;
            }

            // Current directory
            if (File.Exists(name))
                return Path.GetFullPath(name);
        }

        // Common installation locations
        var commonPaths = GetCommonPaths();
        foreach (var path in commonPaths)
        {
            if (File.Exists(path)) return path;
        }

        return null;
    }

    private static IEnumerable<string> GetCommonPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yt-dlp", "yt-dlp.exe");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "yt-dlp", "yt-dlp.exe");
            yield return @"C:\yt-dlp\yt-dlp.exe";
            // Python pip locations
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Programs", "Python", "Python311", "Scripts", "yt-dlp.exe");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Programs", "Python", "Python310", "Scripts", "yt-dlp.exe");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", "Python", "Python311", "Scripts", "yt-dlp.exe");
        }
        else
        {
            yield return "/usr/local/bin/yt-dlp";
            yield return "/usr/bin/yt-dlp";
            yield return "/opt/homebrew/bin/yt-dlp";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "yt-dlp");
        }
    }

    private static string? FindInCache()
    {
        var exeName = GetExecutableName();
        if (!Directory.Exists(CacheDirectory)) return null;

        var cachePath = Path.Combine(CacheDirectory, exeName);
        return File.Exists(cachePath) ? cachePath : null;
    }

    private static async Task<string> DownloadYtdlpAsync(
        IProgress<(string Status, double Progress)>? progress,
        CancellationToken ct)
    {
        var rid = GetRuntimeIdentifier();
        if (!DownloadUrls.TryGetValue(rid, out var url))
        {
            throw new PlatformNotSupportedException($"No yt-dlp download available for {rid}. Please install manually: pip install yt-dlp");
        }

        Directory.CreateDirectory(CacheDirectory);

        var exeName = GetExecutableName();
        var exePath = Path.Combine(CacheDirectory, exeName);

        progress?.Report(("Downloading yt-dlp...", 0.1));

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = File.Create(exePath);

        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;

            if (totalBytes > 0)
            {
                var pct = (double)bytesRead / totalBytes;
                progress?.Report(($"Downloading yt-dlp... {pct:P0}", 0.1 + pct * 0.8));
            }
        }

        fileStream.Close();

        // Set executable permission on Unix
        if (!OperatingSystem.IsWindows())
        {
            await SetExecutablePermissionAsync(exePath, ct);
        }

        progress?.Report(("yt-dlp ready!", 1.0));
        return exePath;
    }

    private static async Task SetExecutablePermissionAsync(string path, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process != null)
                await process.WaitForExitAsync(ct);
        }
        catch { }
    }

    private static string GetRuntimeIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x64",
            _ => "x64"
        };

        if (OperatingSystem.IsWindows()) return $"win-{arch}";
        if (OperatingSystem.IsLinux()) return $"linux-{arch}";
        if (OperatingSystem.IsMacOS()) return $"osx-{arch}";

        return "win-x64";
    }
}

/// <summary>
/// Information about a YouTube video stream.
/// </summary>
public class YouTubeStreamInfo
{
    /// <summary>Direct video stream URL.</summary>
    public required string VideoUrl { get; init; }

    /// <summary>Separate audio stream URL (null if combined with video).</summary>
    public string? AudioUrl { get; init; }

    /// <summary>Video title.</summary>
    public required string Title { get; init; }

    /// <summary>Whether this requires FFmpeg to merge video+audio.</summary>
    public bool RequiresMerge => AudioUrl != null;
}
