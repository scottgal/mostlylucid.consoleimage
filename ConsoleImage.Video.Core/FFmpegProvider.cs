using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace ConsoleImage.Video.Core;

/// <summary>
/// Provides FFmpeg binaries - either bundled, from PATH, or auto-downloaded.
/// </summary>
public static class FFmpegProvider
{
    private static string? _resolvedPath;
    private static readonly object _lock = new();

    // FFmpeg download URLs for different platforms (GPL builds with all codecs)
    private static readonly Dictionary<string, string> DownloadUrls = new()
    {
        ["win-x64"] = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
        ["win-arm64"] = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", // Use x64 on ARM Windows
        ["linux-x64"] = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz",
        ["linux-arm64"] = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linuxarm64-gpl.tar.xz",
        ["osx-x64"] = "https://evermeet.cx/ffmpeg/getrelease/zip",
        ["osx-arm64"] = "https://evermeet.cx/ffmpeg/getrelease/zip"
    };

    /// <summary>
    /// Get the local cache directory for FFmpeg binaries.
    /// </summary>
    public static string CacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "consoleimage", "ffmpeg");

    /// <summary>
    /// Gets path to ffmpeg executable, downloading if necessary.
    /// </summary>
    public static async Task<string> GetFFmpegPathAsync(
        string? customPath = null,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        // 1. Custom path takes priority
        if (!string.IsNullOrEmpty(customPath))
        {
            if (File.Exists(customPath))
                return customPath;

            var ffmpegInDir = Path.Combine(customPath, GetExecutableName("ffmpeg"));
            if (File.Exists(ffmpegInDir))
                return ffmpegInDir;

            throw new FileNotFoundException($"FFmpeg not found at: {customPath}");
        }

        // 2. Check if already resolved
        lock (_lock)
        {
            if (_resolvedPath != null && File.Exists(_resolvedPath))
                return _resolvedPath;
        }

        // 3. Check bundled (in same directory as executable)
        var bundled = FindBundled("ffmpeg");
        if (bundled != null)
        {
            lock (_lock) { _resolvedPath = bundled; }
            return bundled;
        }

        // 4. Check PATH and common locations
        var inPath = FindInPath("ffmpeg");
        if (inPath != null)
        {
            lock (_lock) { _resolvedPath = inPath; }
            return inPath;
        }

        // 5. Check cache
        var cached = FindInCache("ffmpeg");
        if (cached != null)
        {
            lock (_lock) { _resolvedPath = cached; }
            return cached;
        }

        // 6. Download
        progress?.Report(("FFmpeg not found, downloading...", 0));
        var downloaded = await DownloadFFmpegAsync(progress, ct);
        lock (_lock) { _resolvedPath = downloaded; }
        return downloaded;
    }

    /// <summary>
    /// Gets path to ffprobe executable, downloading if necessary.
    /// </summary>
    public static async Task<string> GetFFprobePathAsync(
        string? customPath = null,
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        // If we have ffmpeg path, ffprobe should be in same directory
        var ffmpegPath = await GetFFmpegPathAsync(customPath, progress, ct);
        var ffmpegDir = Path.GetDirectoryName(ffmpegPath)!;
        var ffprobePath = Path.Combine(ffmpegDir, GetExecutableName("ffprobe"));

        if (File.Exists(ffprobePath))
            return ffprobePath;

        // Fallback to search
        return FindInPath("ffprobe") ?? "ffprobe";
    }

    /// <summary>
    /// Check if FFmpeg is available without downloading.
    /// </summary>
    public static bool IsAvailable(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            if (File.Exists(customPath)) return true;
            var ffmpegInDir = Path.Combine(customPath, GetExecutableName("ffmpeg"));
            return File.Exists(ffmpegInDir);
        }

        return FindBundled("ffmpeg") != null ||
               FindInPath("ffmpeg") != null ||
               FindInCache("ffmpeg") != null;
    }

    /// <summary>
    /// Get FFmpeg location status for display.
    /// </summary>
    public static string GetStatus(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            if (File.Exists(customPath))
                return $"Custom: {customPath}";
            var ffmpegInDir = Path.Combine(customPath, GetExecutableName("ffmpeg"));
            if (File.Exists(ffmpegInDir))
                return $"Custom: {ffmpegInDir}";
            return $"Not found at: {customPath}";
        }

        var bundled = FindBundled("ffmpeg");
        if (bundled != null) return $"Bundled: {bundled}";

        var inPath = FindInPath("ffmpeg");
        if (inPath != null) return $"System: {inPath}";

        var cached = FindInCache("ffmpeg");
        if (cached != null) return $"Downloaded: {cached}";

        return "Not found (will download on first use)";
    }

    /// <summary>
    /// Pre-download FFmpeg (useful for setup).
    /// </summary>
    public static async Task EnsureDownloadedAsync(
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        if (IsAvailable()) return;
        await GetFFmpegPathAsync(null, progress, ct);
    }

    /// <summary>
    /// Check if FFmpeg needs to be downloaded and return status information.
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
            return (false, $"No auto-download available for {rid}. Please install FFmpeg manually.", null);
        }

        return (true, $"FFmpeg not found. Can auto-download (~100MB) to: {CacheDirectory}", url);
    }

    /// <summary>
    /// Download FFmpeg with explicit user confirmation (non-interactive).
    /// Call this after checking GetDownloadStatus() and confirming with user.
    /// </summary>
    public static async Task<string> DownloadAsync(
        IProgress<(string Status, double Progress)>? progress = null,
        CancellationToken ct = default)
    {
        if (IsAvailable())
        {
            return (await GetFFmpegPathAsync(null, null, ct))!;
        }

        return await DownloadFFmpegAsync(progress, ct);
    }

    /// <summary>
    /// Clear downloaded FFmpeg from cache.
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

    private static string GetExecutableName(string name)
    {
        return OperatingSystem.IsWindows() ? $"{name}.exe" : name;
    }

    private static string? FindBundled(string name)
    {
        var exeName = GetExecutableName(name);

        // Check next to the running executable
        var exeDir = AppContext.BaseDirectory;
        var bundledPath = Path.Combine(exeDir, exeName);
        if (File.Exists(bundledPath)) return bundledPath;

        // Check in a 'tools' subdirectory
        var toolsPath = Path.Combine(exeDir, "tools", exeName);
        if (File.Exists(toolsPath)) return toolsPath;

        // Check in ffmpeg subdirectory
        var ffmpegDir = Path.Combine(exeDir, "ffmpeg", exeName);
        if (File.Exists(ffmpegDir)) return ffmpegDir;

        return null;
    }

    private static string? FindInPath(string name)
    {
        var exeName = GetExecutableName(name);
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";

        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir, exeName);
            if (File.Exists(fullPath)) return fullPath;
        }

        // Common installation locations
        var searchPaths = GetCommonSearchPaths();
        foreach (var dir in searchPaths)
        {
            if (!Directory.Exists(dir)) continue;
            var fullPath = Path.Combine(dir, exeName);
            if (File.Exists(fullPath)) return fullPath;
        }

        // WinGet deep search
        var wingetBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wingetBase))
        {
            try
            {
                var found = Directory.GetFiles(wingetBase, exeName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (found != null) return found;
            }
            catch { }
        }

        return null;
    }

    private static string? FindInCache(string name)
    {
        var exeName = GetExecutableName(name);
        if (!Directory.Exists(CacheDirectory)) return null;

        var cachePath = Path.Combine(CacheDirectory, exeName);
        if (File.Exists(cachePath)) return cachePath;

        // Also check subdirectories (archive extraction creates subfolders)
        try
        {
            var found = Directory.GetFiles(CacheDirectory, exeName, SearchOption.AllDirectories)
                .FirstOrDefault();
            return found;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> GetCommonSearchPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return @"C:\ffmpeg\bin";
            yield return @"C:\Program Files\ffmpeg\bin";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin");
            yield return @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "apps", "ffmpeg", "current", "bin");
        }
        else if (OperatingSystem.IsLinux())
        {
            yield return "/usr/bin";
            yield return "/usr/local/bin";
            yield return "/snap/bin";
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/usr/local/bin";
            yield return "/opt/homebrew/bin";
        }
    }

    internal static async Task<string> DownloadFFmpegAsync(
        IProgress<(string Status, double Progress)>? progress,
        CancellationToken ct)
    {
        var rid = GetRuntimeIdentifier();
        if (!DownloadUrls.TryGetValue(rid, out var url))
        {
            throw new PlatformNotSupportedException($"No FFmpeg download available for {rid}. Please install FFmpeg manually.");
        }

        Directory.CreateDirectory(CacheDirectory);

        // Determine file type from URL
        var isZip = url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var isTarXz = url.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase);
        var extension = isZip ? ".zip" : (isTarXz ? ".tar.xz" : ".tar.gz");
        var archivePath = Path.Combine(CacheDirectory, $"ffmpeg{extension}");

        progress?.Report(("Downloading FFmpeg...", 0.1));

        // Download
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(10);

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = File.Create(archivePath);

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
                progress?.Report(($"Downloading FFmpeg... {pct:P0}", 0.1 + pct * 0.6));
            }
        }

        fileStream.Close();
        progress?.Report(("Extracting FFmpeg...", 0.75));

        // Extract
        if (isZip)
        {
            ZipFile.ExtractToDirectory(archivePath, CacheDirectory, overwriteFiles: true);
        }
        else
        {
            // For tar.xz on Windows, use tar command if available
            await ExtractTarAsync(archivePath, CacheDirectory, ct);
        }

        // Clean up archive
        try { File.Delete(archivePath); } catch { }

        progress?.Report(("Finding FFmpeg executable...", 0.9));

        // Find extracted ffmpeg
        var ffmpegPath = FindInCache("ffmpeg")
            ?? throw new FileNotFoundException("FFmpeg extraction failed - executable not found");

        // Set executable permission on Unix
        if (!OperatingSystem.IsWindows())
        {
            await SetExecutablePermissionAsync(ffmpegPath, ct);
            var ffprobePath = Path.Combine(Path.GetDirectoryName(ffmpegPath)!, "ffprobe");
            if (File.Exists(ffprobePath))
                await SetExecutablePermissionAsync(ffprobePath, ct);
        }

        progress?.Report(("FFmpeg ready!", 1.0));
        return ffmpegPath;
    }

    private static async Task ExtractTarAsync(string archivePath, string destDir, CancellationToken ct)
    {
        // Try using system tar first (works on Windows 10+, Linux, macOS)
        var tarPath = OperatingSystem.IsWindows() ? "tar.exe" : "tar";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = tarPath,
                Arguments = $"-xf \"{archivePath}\" -C \"{destDir}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync(ct);
                if (process.ExitCode == 0) return;
            }
        }
        catch { }

        throw new NotSupportedException("Cannot extract tar.xz archive. Please install FFmpeg manually or use Windows where zip archives are available.");
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
            Architecture.X86 => "x64", // Use x64 on x86
            _ => "x64"
        };

        if (OperatingSystem.IsWindows()) return $"win-{arch}";
        if (OperatingSystem.IsLinux()) return $"linux-{arch}";
        if (OperatingSystem.IsMacOS()) return $"osx-{arch}";

        return "win-x64"; // Fallback
    }
}
