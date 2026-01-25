// WhisperRuntimeDownloader - Downloads Whisper native runtime on first use
// Keeps the main binary small by not bundling ~50-100MB of native libs

using System.IO.Compression;
using System.Runtime.InteropServices;

namespace ConsoleImage.Transcription;

/// <summary>
/// Downloads and caches Whisper.net native runtime libraries.
/// Supports side-loading to avoid bundling large native binaries in the main executable.
/// </summary>
public static class WhisperRuntimeDownloader
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    static WhisperRuntimeDownloader()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConsoleImage/1.0");
    }

    /// <summary>
    /// Runtime cache directory (same as model cache).
    /// </summary>
    public static string CacheDirectory => WhisperModelDownloader.CacheDirectory;

    /// <summary>
    /// Get the runtimes directory where native libs should be placed.
    /// </summary>
    public static string RuntimesDirectory => Path.Combine(CacheDirectory, "runtimes");

    /// <summary>
    /// Get platform-specific runtime identifier.
    /// </summary>
    public static string GetRuntimeIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"win-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"linux-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"osx-{arch}";

        return $"linux-{arch}"; // Default fallback
    }

    /// <summary>
    /// Check if runtime is already available (bundled or downloaded).
    /// </summary>
    public static bool IsRuntimeAvailable()
    {
        // Check if runtime exists in cache
        var rid = GetRuntimeIdentifier();
        var runtimePath = GetNativeLibraryPath(rid);
        if (File.Exists(runtimePath))
            return true;

        // Check if bundled with the app (development or full build)
        var appDir = AppContext.BaseDirectory;
        var bundledPath = Path.Combine(appDir, "runtimes", rid, "native", GetNativeLibraryName());
        return File.Exists(bundledPath);
    }

    /// <summary>
    /// Get info about the runtime download.
    /// </summary>
    public static (string rid, int sizeMB) GetRuntimeInfo()
    {
        var rid = GetRuntimeIdentifier();
        var sizeMB = rid switch
        {
            "win-x64" => 12,
            "linux-x64" => 10,
            "osx-arm64" => 8,
            "osx-x64" => 10,
            _ => 12
        };
        return (rid, sizeMB);
    }

    /// <summary>
    /// Ensure the Whisper runtime is available, downloading if necessary.
    /// </summary>
    public static async Task<bool> EnsureRuntimeAsync(
        IProgress<(long downloaded, long total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        if (IsRuntimeAvailable())
        {
            progress?.Report((0, 0, "Whisper runtime already available"));
            return true;
        }

        var rid = GetRuntimeIdentifier();
        var url = GetDownloadUrl(rid);
        var targetDir = Path.Combine(RuntimesDirectory, rid, "native");

        progress?.Report((0, 0, $"Downloading Whisper runtime for {rid}..."));

        Directory.CreateDirectory(targetDir);

        try
        {
            // Download the zip file
            var tempZip = Path.GetTempFileName();
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    progress?.Report((0, 0, $"Failed to download runtime: HTTP {(int)response.StatusCode}"));
                    return false;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;
                    var pct = totalBytes > 0 ? (int)(totalRead * 100 / totalBytes) : 0;
                    progress?.Report((totalRead, totalBytes, $"Downloading runtime... {pct}%"));
                }

                progress?.Report((totalRead, totalBytes, "Extracting runtime..."));
            }
            catch (Exception ex)
            {
                progress?.Report((0, 0, $"Download failed: {ex.Message}"));
                if (File.Exists(tempZip)) File.Delete(tempZip);
                return false;
            }

            // Extract native libraries from NuGet package
            // Whisper.net.Runtime v1.9.0+ stores libs at: build/{rid}/
            try
            {
                var nativePath = GetNativePathInPackage(rid);
                using var archive = ZipFile.OpenRead(tempZip);
                var extractedCount = 0;

                foreach (var entry in archive.Entries)
                {
                    // Look for native libraries in the correct RID folder
                    if (entry.FullName.StartsWith(nativePath, StringComparison.OrdinalIgnoreCase) &&
                        IsNativeLibrary(entry.Name) &&
                        !string.IsNullOrEmpty(entry.Name))
                    {
                        var destPath = Path.Combine(targetDir, entry.Name);
                        entry.ExtractToFile(destPath, overwrite: true);
                        progress?.Report((0, 0, $"Extracted: {entry.Name}"));
                        extractedCount++;
                    }
                }

                if (extractedCount == 0)
                {
                    // List what's in the package for debugging
                    progress?.Report((0, 0, $"No libs found at {nativePath}, checking package contents..."));
                    var buildFolders = archive.Entries
                        .Where(e => e.FullName.StartsWith("build/", StringComparison.OrdinalIgnoreCase))
                        .Select(e => e.FullName.Split('/').Take(2).Aggregate((a, b) => $"{a}/{b}"))
                        .Distinct()
                        .Take(10);
                    foreach (var folder in buildFolders)
                        progress?.Report((0, 0, $"  Found: {folder}"));
                }
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }

            // Verify extraction worked
            var libPath = GetNativeLibraryPath(rid);
            if (!File.Exists(libPath))
            {
                progress?.Report((0, 0, "Runtime extraction failed - library not found"));
                return false;
            }

            progress?.Report((0, 0, "Whisper runtime installed successfully"));
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report((0, 0, $"Runtime installation failed: {ex.Message}"));
            progress?.Report((0, 0, GetManualInstallInstructions(rid)));
            return false;
        }
    }

    /// <summary>
    /// Configure Whisper.net to use our cached runtime location.
    /// Call this before creating WhisperFactory.
    /// </summary>
    public static void ConfigureRuntimePath()
    {
        var rid = GetRuntimeIdentifier();
        var runtimeDir = Path.Combine(RuntimesDirectory, rid, "native");

        if (Directory.Exists(runtimeDir))
        {
            // Add to native library search path
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!currentPath.Contains(runtimeDir))
            {
                Environment.SetEnvironmentVariable("PATH", $"{runtimeDir}{Path.PathSeparator}{currentPath}");
            }
        }
    }

    private static string GetNativeLibraryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "whisper.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "libwhisper.dylib";
        return "libwhisper.so";
    }

    private static string GetNativeLibraryPath(string rid)
    {
        return Path.Combine(RuntimesDirectory, rid, "native", GetNativeLibraryName());
    }

    private static bool IsNativeLibrary(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.EndsWith(".dll") || lower.EndsWith(".so") || lower.EndsWith(".dylib");
    }

    private static string GetDownloadUrl(string rid)
    {
        // Whisper.net runtimes are distributed via NuGet packages
        // We download the .nupkg (which is a zip) and extract native libs
        // Using NuGet v3 flat container API (most reliable)
        const string version = "1.9.0";

        // Whisper.net.Runtime contains CPU binaries for all platforms
        const string packageName = "whisper.net.runtime";

        // NuGet v3 flat container API - lowercase package name required
        return $"https://api.nuget.org/v3-flatcontainer/{packageName}/{version}/{packageName}.{version}.nupkg";
    }

    /// <summary>
    /// Get manual installation instructions for the runtime.
    /// </summary>
    public static string GetManualInstallInstructions(string rid)
    {
        return $@"
Whisper Runtime Manual Installation
====================================
If automatic download fails, you can install manually:

Option 1: Install via NuGet (recommended)
  dotnet add package Whisper.net.Runtime --version 1.9.0

Option 2: Download and extract manually
  1. Download: https://www.nuget.org/packages/Whisper.net.Runtime/1.9.0
  2. Rename .nupkg to .zip and extract
  3. Copy files from runtimes/{rid}/native/ to:
     {Path.Combine(RuntimesDirectory, rid, "native")}

Option 3: Platform-specific packages (smaller)
  - Windows: Whisper.net.Runtime.Cpu.Windows
  - Linux: Whisper.net.Runtime.Cpu.Linux
  - macOS: Whisper.net.Runtime.CoreML (Apple Silicon)

For GPU acceleration:
  - CUDA: Whisper.net.Runtime.Cuda
  - OpenVINO: Whisper.net.Runtime.OpenVino
";
    }

    /// <summary>
    /// Get the path within the NuGet package where native libs are stored.
    /// </summary>
    private static string GetNativePathInPackage(string rid)
    {
        // Whisper.net.Runtime v1.9.0 stores libs at: build/{rid}/
        return $"build/{rid}/";
    }
}
