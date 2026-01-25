// WhisperRuntimeDownloader - Checks for Whisper native runtime availability
//
// DESIGN:
// - AOT builds: Runtime is bundled via NuGet packages in the CLI project
// - Non-AOT/Development: Runtime comes from Whisper.net.AllRuntimes reference
// - Fallback download: Simple attempt, graceful failure if unavailable
// - NO REFLECTION: Fully AOT compatible

using System.IO.Compression;
using System.Runtime.InteropServices;

namespace ConsoleImage.Transcription;

/// <summary>
/// Checks Whisper.net native runtime availability and provides fallback download.
/// For AOT builds, the runtime should be bundled. This class handles cases where
/// it's not bundled and attempts to download as a fallback.
/// </summary>
public static class WhisperRuntimeDownloader
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private static string? _lastError;
    private static bool _checked;
    private static bool _available;

    static WhisperRuntimeDownloader()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConsoleImage/1.0");
    }

    /// <summary>
    /// Runtime cache directory for downloaded files.
    /// </summary>
    public static string CacheDirectory => WhisperModelDownloader.CacheDirectory;

    /// <summary>
    /// Get the runtimes directory where native libs should be placed.
    /// </summary>
    public static string RuntimesDirectory => Path.Combine(CacheDirectory, "runtimes");

    /// <summary>
    /// Get the last error message from runtime initialization.
    /// </summary>
    public static string? LastError => _lastError;

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

        return $"linux-{arch}";
    }

    /// <summary>
    /// Get the native library name for the current platform.
    /// </summary>
    public static string GetNativeLibraryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "whisper.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "libwhisper.dylib";
        return "libwhisper.so";
    }

    /// <summary>
    /// Get all possible locations where the native library might be found.
    /// </summary>
    public static IEnumerable<string> GetSearchPaths()
    {
        var rid = GetRuntimeIdentifier();
        var libName = GetNativeLibraryName();
        var appDir = AppContext.BaseDirectory;

        // Priority 1: App directory root (where bundled libs typically go)
        yield return Path.Combine(appDir, libName);

        // Priority 2: NuGet runtime layout in app directory
        yield return Path.Combine(appDir, "runtimes", rid, "native", libName);

        // Priority 3: Download cache
        yield return Path.Combine(RuntimesDirectory, rid, "native", libName);

        // Priority 4: Parent directory (development scenarios)
        var parent = Directory.GetParent(appDir)?.FullName;
        if (parent != null)
        {
            yield return Path.Combine(parent, libName);
            yield return Path.Combine(parent, "runtimes", rid, "native", libName);
        }
    }

    /// <summary>
    /// Check if runtime is already available (file exists in any search path).
    /// </summary>
    public static bool IsRuntimeAvailable()
    {
        return GetSearchPaths().Any(File.Exists);
    }

    /// <summary>
    /// Get the path to an available runtime, or null if not found.
    /// </summary>
    public static string? GetAvailableRuntimePath()
    {
        return GetSearchPaths().FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Test if the native library can actually be loaded.
    /// </summary>
    public static bool CanLoadRuntime(out string? errorMessage)
    {
        errorMessage = null;
        var path = GetAvailableRuntimePath();

        if (path == null)
        {
            errorMessage = "Native library file not found in any search path";
            return false;
        }

        try
        {
            if (NativeLibrary.TryLoad(path, out var handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }
            errorMessage = $"NativeLibrary.TryLoad failed for {path}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Load exception for {path}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Get info about the runtime download (for progress messages).
    /// </summary>
    public static (string rid, int sizeMB) GetRuntimeInfo()
    {
        var rid = GetRuntimeIdentifier();
        var sizeMB = rid switch
        {
            "win-x64" => 45,
            "win-arm64" => 40,
            "win-x86" => 35,
            "linux-x64" => 40,
            "linux-arm64" => 35,
            "osx-arm64" => 30,
            "osx-x64" => 35,
            _ => 45
        };
        return (rid, sizeMB);
    }

    /// <summary>
    /// Ensure the Whisper runtime is available, downloading if necessary.
    /// Returns false if unavailable but DOES NOT THROW.
    /// </summary>
    public static async Task<bool> EnsureRuntimeAsync(
        IProgress<(long downloaded, long total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        _lastError = null;

        // Fast path: already checked and available
        if (_checked && _available)
        {
            progress?.Report((0, 0, "Whisper runtime available"));
            return true;
        }

        // Check if bundled runtime is available and loadable
        if (IsRuntimeAvailable())
        {
            if (CanLoadRuntime(out var loadError))
            {
                progress?.Report((0, 0, "Whisper runtime available"));
                _checked = true;
                _available = true;
                return true;
            }
            // File exists but can't load - might be corrupt or wrong architecture
            progress?.Report((0, 0, $"Runtime file found but cannot load: {loadError}"));
        }

        // Try to download runtime
        progress?.Report((0, 0, "Whisper runtime not bundled, attempting download..."));

        var rid = GetRuntimeIdentifier();
        var targetDir = Path.Combine(RuntimesDirectory, rid, "native");

        // Try each download source
        var sources = GetDownloadSources(rid);
        foreach (var (name, url) in sources)
        {
            progress?.Report((0, 0, $"Trying {name}..."));

            try
            {
                if (await TryDownloadAsync(url, targetDir, progress, ct))
                {
                    // Copy to app directory for reliability
                    CopyToAppDirectory(targetDir);

                    if (CanLoadRuntime(out _))
                    {
                        progress?.Report((0, 0, "Whisper runtime downloaded successfully"));
                        _checked = true;
                        _available = true;
                        return true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report((0, 0, $"{name} failed: {ex.Message}"));
            }
        }

        // All attempts failed
        _lastError = "Whisper runtime not available. Transcription will be disabled.";
        progress?.Report((0, 0, _lastError));
        progress?.Report((0, 0, GetManualInstallInstructions(rid)));
        _checked = true;
        _available = false;
        return false;
    }

    /// <summary>
    /// Get download sources in priority order.
    /// </summary>
    private static List<(string name, string url)> GetDownloadSources(string rid)
    {
        const string version = "1.9.0";
        var sources = new List<(string, string)>();

        // Whisper.net.Runtime contains CPU binaries for all platforms
        // This is the standard package that works on Windows, Linux, and macOS
        sources.Add((
            "NuGet (Whisper.net.Runtime)",
            $"https://api.nuget.org/v3-flatcontainer/whisper.net.runtime/{version}/whisper.net.runtime.{version}.nupkg"
        ));

        return sources;
    }

    /// <summary>
    /// Try to download and extract the runtime from a URL.
    /// </summary>
    private static async Task<bool> TryDownloadAsync(
        string url,
        string targetDir,
        IProgress<(long, long, string)>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(targetDir);
        var tempFile = Path.GetTempFileName();

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                progress?.Report((0, 0, $"HTTP {(int)response.StatusCode}"));
                return false;
            }

            // Download to temp file
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            await using var content = await response.Content.ReadAsStreamAsync(ct);
            await using var file = new FileStream(tempFile, FileMode.Create);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;

            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                totalRead += read;
                var pct = totalBytes > 0 ? (int)(totalRead * 100 / totalBytes) : 0;
                progress?.Report((totalRead, totalBytes, $"Downloading... {totalRead / 1024 / 1024}MB ({pct}%)"));
            }

            file.Close();
            progress?.Report((totalRead, totalBytes, "Extracting..."));

            // Extract from nupkg
            return ExtractFromNupkg(tempFile, targetDir, progress);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>
    /// Extract native library from NuGet package.
    /// </summary>
    private static bool ExtractFromNupkg(string zipPath, string targetDir, IProgress<(long, long, string)>? progress)
    {
        var rid = GetRuntimeIdentifier();
        var libName = GetNativeLibraryName();

        using var archive = ZipFile.OpenRead(zipPath);

        // Search for the native library in common locations
        var searchPaths = new[]
        {
            $"runtimes/{rid}/native/{libName}",
            $"runtimes/{rid}/{libName}",
            $"build/{rid}/native/{libName}",
            $"build/{rid}/{libName}",
        };

        foreach (var entry in archive.Entries)
        {
            // Check if this entry matches any expected path
            var matches = searchPaths.Any(p =>
                entry.FullName.Equals(p, StringComparison.OrdinalIgnoreCase));

            if (!matches)
            {
                // Also check by filename alone
                if (!entry.Name.Equals(libName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Make sure it's in a runtime-specific folder for our platform
                var pathLower = entry.FullName.ToLowerInvariant();
                var ridLower = rid.ToLowerInvariant();
                if (!pathLower.Contains(ridLower) && !pathLower.Contains(ridLower.Replace("-", "_")))
                    continue;
            }

            // Found it - extract
            var destPath = Path.Combine(targetDir, libName);
            entry.ExtractToFile(destPath, overwrite: true);
            progress?.Report((0, 0, $"Extracted: {libName}"));
            return true;
        }

        // Debug: list what's in the package
        progress?.Report((0, 0, "Library not found in package. Contents:"));
        foreach (var e in archive.Entries.Where(e => e.Name.EndsWith(".dll") || e.Name.EndsWith(".so") || e.Name.EndsWith(".dylib")).Take(10))
        {
            progress?.Report((0, 0, $"  {e.FullName}"));
        }

        return false;
    }

    /// <summary>
    /// Copy downloaded library to app directory for better compatibility.
    /// </summary>
    private static void CopyToAppDirectory(string sourceDir)
    {
        var libName = GetNativeLibraryName();
        var sourcePath = Path.Combine(sourceDir, libName);

        if (!File.Exists(sourcePath))
            return;

        var appDir = AppContext.BaseDirectory;

        // Try to copy to app directory root
        try
        {
            var destPath = Path.Combine(appDir, libName);
            if (!File.Exists(destPath))
                File.Copy(sourcePath, destPath);
        }
        catch { /* Best effort */ }

        // Also try NuGet layout
        try
        {
            var rid = GetRuntimeIdentifier();
            var runtimeDir = Path.Combine(appDir, "runtimes", rid, "native");
            Directory.CreateDirectory(runtimeDir);
            var destPath = Path.Combine(runtimeDir, libName);
            if (!File.Exists(destPath))
                File.Copy(sourcePath, destPath);
        }
        catch { /* Best effort */ }
    }

    /// <summary>
    /// Configure runtime path (adds to PATH environment variable).
    /// </summary>
    public static void ConfigureRuntimePath()
    {
        var rid = GetRuntimeIdentifier();
        var cacheDir = Path.Combine(RuntimesDirectory, rid, "native");

        if (!Directory.Exists(cacheDir))
            return;

        try
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!currentPath.Contains(cacheDir))
            {
                Environment.SetEnvironmentVariable("PATH", $"{cacheDir}{Path.PathSeparator}{currentPath}");
            }
        }
        catch { }
    }

    /// <summary>
    /// Get manual installation instructions.
    /// </summary>
    public static string GetManualInstallInstructions(string rid)
    {
        var libName = GetNativeLibraryName();
        var appDir = AppContext.BaseDirectory;

        return $"""
            To enable Whisper transcription, copy {libName} to:
              {appDir}

            Download from: https://www.nuget.org/packages/Whisper.net.Runtime/1.9.0
            (Rename .nupkg to .zip, extract, find runtimes/{rid}/native/{libName})
            """;
    }
}
