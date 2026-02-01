// WhisperRuntimeDownloader - Checks for Whisper native runtime availability
//
// DESIGN:
// - AOT builds: Runtime is bundled via NuGet packages in the CLI project
// - Non-AOT/Development: Runtime comes from Whisper.net.AllRuntimes reference
// - Fallback download: Simple attempt, graceful failure if unavailable
// - NO REFLECTION: Fully AOT compatible

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using Whisper.net.LibraryLoader;

namespace ConsoleImage.Transcription;

/// <summary>
///     Checks Whisper.net native runtime availability and provides fallback download.
///     For AOT builds, the runtime should be bundled. This class handles cases where
///     it's not bundled and attempts to download as a fallback.
/// </summary>
public static class WhisperRuntimeDownloader
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private static bool _checked;
    private static bool _available;

    static WhisperRuntimeDownloader()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConsoleImage/1.0");
    }

    /// <summary>
    ///     Runtime cache directory for downloaded files.
    /// </summary>
    public static string CacheDirectory => WhisperModelDownloader.CacheDirectory;

    /// <summary>
    ///     Get the runtimes directory where native libs should be placed.
    /// </summary>
    public static string RuntimesDirectory => Path.Combine(CacheDirectory, "runtimes");

    /// <summary>
    ///     Get the last error message from runtime initialization.
    /// </summary>
    public static string? LastError { get; private set; }

    /// <summary>
    ///     Path to a marker file tracking which variant (avx/noavx) was downloaded.
    /// </summary>
    private static string VariantMarkerPath => Path.Combine(RuntimesDirectory, ".whisper-variant");

    /// <summary>
    ///     Check if the current x64 CPU supports AVX instructions.
    ///     Whisper.net.Runtime (standard) is compiled with AVX; running it on a CPU
    ///     without AVX causes SIGILL → segfault on Linux (uncatchable in managed code).
    /// </summary>
    public static bool IsAvxSupported()
    {
        try
        {
            // AVX is an x86/x64 feature  -  ARM uses NEON instead, no AVX concern
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64
                && RuntimeInformation.ProcessArchitecture != Architecture.X86)
                return false;

            return Avx.IsSupported;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Whether this CPU needs the NoAvx variant of the Whisper runtime.
    ///     True for x64 CPUs without AVX. ARM64 uses NEON and doesn't need NoAvx.
    /// </summary>
    public static bool NeedsNoAvxVariant()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return false; // ARM64, x86 — not applicable

        return !IsAvxSupported();
    }

    /// <summary>
    ///     Check if the cached runtime variant matches what this CPU needs.
    ///     Returns false if an AVX variant is cached but the CPU doesn't support AVX.
    /// </summary>
    private static bool IsCorrectVariantCached()
    {
        if (!NeedsNoAvxVariant())
            return true; // CPU supports AVX, any variant works

        // CPU needs NoAvx — check what's cached
        try
        {
            if (!File.Exists(VariantMarkerPath))
                return true; // No marker = unknown, assume ok (first run)

            var cached = File.ReadAllText(VariantMarkerPath).Trim();
            return string.Equals(cached, "noavx", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    ///     Write a marker file recording which variant was installed.
    /// </summary>
    private static void WriteVariantMarker(string variant)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(VariantMarkerPath)!);
            File.WriteAllText(VariantMarkerPath, variant);
        }
        catch
        {
            // Best effort
        }
    }

    /// <summary>
    ///     On Linux, check for missing shared library dependencies using ldd.
    ///     Returns a diagnostic string if dependencies are missing, null otherwise.
    /// </summary>
    public static string? CheckLinuxDependencies()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return null;

        var libPath = GetAvailableRuntimePath();
        if (libPath == null)
            return null;

        try
        {
            var psi = new ProcessStartInfo("ldd", libPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            var missing = output.Split('\n')
                .Where(l => l.Contains("not found", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Trim())
                .ToList();

            if (missing.Count > 0)
                return $"Missing dependencies for {Path.GetFileName(libPath)}:\n  " +
                       string.Join("\n  ", missing);
        }
        catch
        {
            // ldd not available
        }

        return null;
    }

    /// <summary>
    ///     Get platform-specific runtime identifier.
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
    ///     Get the native library name for the current platform.
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
    ///     Get all possible locations where the native library might be found.
    /// </summary>
    public static IEnumerable<string> GetSearchPaths()
    {
        var rid = GetRuntimeIdentifier();
        var libName = GetNativeLibraryName();
        var appDir = AppContext.BaseDirectory;

        // Priority 1: App directory root (where bundled libs typically go in AOT publish)
        yield return Path.Combine(appDir, libName);

        // Priority 2: NuGet runtime layout in app directory (with native subfolder)
        yield return Path.Combine(appDir, "runtimes", rid, "native", libName);

        // Priority 3: NuGet runtime layout without native subfolder
        yield return Path.Combine(appDir, "runtimes", rid, libName);

        // Priority 4: Download cache (with native subfolder)
        yield return Path.Combine(RuntimesDirectory, rid, "native", libName);

        // Priority 5: Download cache (without native subfolder)
        yield return Path.Combine(RuntimesDirectory, rid, libName);

        // Priority 6: NuGet package cache (for dotnet run with AllRuntimes/Runtime reference)
        var nugetCache = GetNuGetCacheDirectory();
        if (nugetCache != null)
        {
            // Whisper.net.Runtime uses build/{rid}/ layout
            yield return Path.Combine(nugetCache, "whisper.net.runtime", "1.9.0", "build", rid, libName);

            // Whisper.net.Runtime may also use macos instead of osx
            if (rid.StartsWith("osx"))
            {
                var macosRid = rid.Replace("osx", "macos");
                yield return Path.Combine(nugetCache, "whisper.net.runtime", "1.9.0", "build", macosRid, libName);
            }

            // Also check runtimes/ layout
            yield return Path.Combine(nugetCache, "whisper.net.runtime", "1.9.0", "runtimes", rid, "native", libName);
        }

        // Priority 7: Parent directory (development scenarios)
        var parent = Directory.GetParent(appDir)?.FullName;
        if (parent != null)
        {
            yield return Path.Combine(parent, libName);
            yield return Path.Combine(parent, "runtimes", rid, "native", libName);
            yield return Path.Combine(parent, "runtimes", rid, libName);
        }
    }

    /// <summary>
    ///     Get the NuGet global packages cache directory.
    /// </summary>
    private static string? GetNuGetCacheDirectory()
    {
        // Standard NuGet cache locations
        var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(nugetPackages) && Directory.Exists(nugetPackages))
            return nugetPackages;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return null;

        var defaultCache = Path.Combine(home, ".nuget", "packages");
        return Directory.Exists(defaultCache) ? defaultCache : null;
    }

    /// <summary>
    ///     Check if runtime is already available (file exists in any search path).
    /// </summary>
    public static bool IsRuntimeAvailable()
    {
        return GetSearchPaths().Any(File.Exists);
    }

    /// <summary>
    ///     Get the path to an available runtime, or null if not found.
    /// </summary>
    public static string? GetAvailableRuntimePath()
    {
        return GetSearchPaths().FirstOrDefault(File.Exists);
    }

    /// <summary>
    ///     Test if the native library can actually be loaded.
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
    ///     Get info about the runtime download (for progress messages).
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
    ///     Ensure the Whisper runtime is available, downloading if necessary.
    ///     Returns false if unavailable but DOES NOT THROW.
    /// </summary>
    public static async Task<bool> EnsureRuntimeAsync(
        IProgress<(long downloaded, long total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        LastError = null;

        // Fast path: already checked and available
        if (_checked && _available)
        {
            progress?.Report((0, 0, "Whisper runtime available"));
            return true;
        }

        // Check if bundled runtime is available and loadable
        if (IsRuntimeAvailable())
        {
            // Check if cached variant matches CPU capabilities
            if (!IsCorrectVariantCached())
            {
                progress?.Report((0, 0,
                    "Cached Whisper runtime is AVX variant but CPU lacks AVX support. Re-downloading NoAvx variant..."));
                // Fall through to download the correct variant
            }
            else if (CanLoadRuntime(out var loadError))
            {
                progress?.Report((0, 0, "Whisper runtime available"));
                _checked = true;
                _available = true;
                return true;
            }
            else
            {
                // File exists but can't load - might be corrupt or wrong architecture
                progress?.Report((0, 0, $"Runtime file found but cannot load: {loadError}"));
            }
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

                    // Track which variant was installed for future runs
                    var variant = name.Contains("NoAvx", StringComparison.OrdinalIgnoreCase)
                        ? "noavx"
                        : "avx";
                    WriteVariantMarker(variant);

                    if (CanLoadRuntime(out _))
                    {
                        progress?.Report((0, 0, $"Whisper runtime downloaded successfully ({variant})"));
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
        LastError = "Whisper runtime not available. Transcription will be disabled.";
        progress?.Report((0, 0, LastError));
        progress?.Report((0, 0, GetManualInstallInstructions(rid)));
        _checked = true;
        _available = false;
        return false;
    }

    /// <summary>
    ///     Get download sources in priority order.
    ///     If CPU lacks AVX, the NoAvx variant is tried first to prevent segfault.
    /// </summary>
    private static List<(string name, string url)> GetDownloadSources(string rid)
    {
        const string version = "1.9.0";
        var sources = new List<(string, string)>();

        var avxSource = (
            name: "NuGet (Whisper.net.Runtime)",
            url: $"https://api.nuget.org/v3-flatcontainer/whisper.net.runtime/{version}/whisper.net.runtime.{version}.nupkg"
        );

        var noAvxSource = (
            name: "NuGet (Whisper.net.Runtime.NoAvx)",
            url: $"https://api.nuget.org/v3-flatcontainer/whisper.net.runtime.noavx/{version}/whisper.net.runtime.noavx.{version}.nupkg"
        );

        if (NeedsNoAvxVariant())
        {
            // CPU doesn't support AVX — NoAvx first to prevent segfault
            sources.Add(noAvxSource);
            sources.Add(avxSource);
        }
        else
        {
            // CPU supports AVX (or is ARM64 where AVX isn't relevant)
            sources.Add(avxSource);
            sources.Add(noAvxSource);
        }

        return sources;
    }

    /// <summary>
    ///     Try to download and extract the runtime from a URL.
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
            try
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    ///     Extract ALL native libraries for this platform from NuGet package.
    ///     Whisper.dll depends on ggml-*.dll — all must be extracted together.
    /// </summary>
    private static bool ExtractFromNupkg(string zipPath, string targetDir, IProgress<(long, long, string)>? progress)
    {
        var rid = GetRuntimeIdentifier();

        // Native library extensions for current platform
        var nativeExtensions = GetNativeExtensions();

        using var archive = ZipFile.OpenRead(zipPath);

        // Whisper.net.Runtime NuGet package structure:
        //   build/{rid}/*.dll (Windows) or *.so (Linux) or *.dylib (macOS)
        // Also check runtimes/ path used by some package versions
        // Note: Whisper.net.Runtime uses 'macos' instead of 'osx' for macOS RIDs
        var rids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rid };
        if (rid.StartsWith("osx"))
            rids.Add(rid.Replace("osx", "macos"));
        else if (rid.StartsWith("macos"))
            rids.Add(rid.Replace("macos", "osx"));

        var prefixes = rids.SelectMany(r => new[]
        {
            $"build/{r}/",
            $"build/{r}/native/",
            $"runtimes/{r}/native/",
            $"runtimes/{r}/"
        }).ToArray();

        var extractedCount = 0;

        foreach (var entry in archive.Entries)
        {
            // Skip directories and non-native files
            if (string.IsNullOrEmpty(entry.Name)) continue;
            if (!nativeExtensions.Any(ext => entry.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Check if this entry is under a path for our platform
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!prefixes.Any(p => entryPath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Extract to target directory
            var destPath = Path.Combine(targetDir, entry.Name);
            entry.ExtractToFile(destPath, true);
            progress?.Report((0, 0, $"Extracted: {entry.Name}"));
            extractedCount++;
        }

        if (extractedCount > 0)
        {
            // On macOS, remove quarantine attributes and ad-hoc sign extracted libraries.
            // Without this, Gatekeeper blocks dlopen for downloaded unsigned dylibs.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var extractedFiles = Directory.GetFiles(targetDir)
                    .Where(f => nativeExtensions.Any(ext =>
                        f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                RemoveMacQuarantine(extractedFiles);
                progress?.Report((0, 0, "Cleared macOS quarantine and signed libraries"));
            }

            progress?.Report((0, 0, $"Extracted {extractedCount} native libraries for {rid}"));
            return true;
        }

        // Debug: list what's in the package
        progress?.Report((0, 0, $"No native libraries found for {rid}. Package contents:"));
        foreach (var e in archive.Entries
                     .Where(e => nativeExtensions.Any(ext => e.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                     .Take(15)) progress?.Report((0, 0, $"  {e.FullName}"));

        return false;
    }

    /// <summary>
    ///     Get native library file extensions for the current platform.
    /// </summary>
    private static string[] GetNativeExtensions()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return [".dll"];
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return [".dylib", ".metal"];
        return [".so"];
    }

    /// <summary>
    ///     Copy ALL downloaded native libraries to app directory for better compatibility.
    ///     whisper.dll depends on ggml-*.dll — all must be co-located for loading.
    /// </summary>
    private static void CopyToAppDirectory(string sourceDir)
    {
        if (!Directory.Exists(sourceDir))
            return;

        var nativeExtensions = GetNativeExtensions();
        var nativeFiles = Directory.GetFiles(sourceDir)
            .Where(f => nativeExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (nativeFiles.Count == 0)
            return;

        var appDir = AppContext.BaseDirectory;

        // Copy all native libs to app directory root (best for AOT/single-file)
        // Overwrite existing files: a previous runtime variant (e.g. AVX) may have failed
        // to load, and we need the new variant (e.g. NoAvx) to replace it.
        foreach (var sourceFile in nativeFiles)
        {
            try
            {
                var destPath = Path.Combine(appDir, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, destPath, overwrite: true);
            }
            catch
            {
                /* Best effort */
            }
        }

        // Also copy to runtimes/{rid}/ layout that Whisper.net's NativeLibraryLoader searches.
        // For RuntimeLibrary.Cpu, it searches runtimes/{platform}-{arch}/ (no "native" subfolder).
        try
        {
            var rid = GetRuntimeIdentifier();
            var runtimeDir = Path.Combine(appDir, "runtimes", rid);
            Directory.CreateDirectory(runtimeDir);

            foreach (var sourceFile in nativeFiles)
            {
                try
                {
                    var destPath = Path.Combine(runtimeDir, Path.GetFileName(sourceFile));
                    File.Copy(sourceFile, destPath, overwrite: true);
                }
                catch
                {
                    /* Best effort */
                }
            }
        }
        catch
        {
            /* Best effort */
        }
    }

    /// <summary>
    ///     Configure runtime path (adds to PATH environment variable and sets Whisper.net RuntimeOptions).
    ///     Must be called before any WhisperFactory is created.
    /// </summary>
    public static void ConfigureRuntimePath()
    {
        // Set Whisper.net RuntimeOptions.LibraryPath to the full FILE path of the native library.
        // This is critical for AOT/single-file builds where Assembly.Location returns empty,
        // causing Whisper.net's internal NativeLibraryLoader to fail (IL3000).
        // NOTE: We pass the full file path (not directory) because Whisper.net's
        // GetSafeDirectoryName() calls Path.GetDirectoryName() on this value — if we pass
        // a directory path, it returns the PARENT directory, missing the actual DLLs.
        var nativeLibPath = GetAvailableRuntimePath();
        var libDir = nativeLibPath != null ? Path.GetDirectoryName(nativeLibPath) : null;

        if (nativeLibPath != null)
            RuntimeOptions.LibraryPath = nativeLibPath;

        // Also check the download cache directory
        var rid = GetRuntimeIdentifier();
        var cacheDir = Path.Combine(RuntimesDirectory, rid, "native");

        // Collect all directories to add to PATH (for dependent DLLs like ggml-*.dll)
        var dirsToAdd = new List<string>();
        if (libDir != null && Directory.Exists(libDir))
            dirsToAdd.Add(libDir);
        if (Directory.Exists(cacheDir) && cacheDir != libDir)
            dirsToAdd.Add(cacheDir);

        // Also add the app base directory (where bundled libs go in AOT publish)
        var appDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(appDir) && appDir != libDir)
            dirsToAdd.Add(appDir);

        try
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            var newDirs = dirsToAdd
                .Where(d => !currentPath.Contains(d, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (newDirs.Length > 0)
            {
                var newPaths = string.Join(Path.PathSeparator, newDirs);
                Environment.SetEnvironmentVariable("PATH", $"{newPaths}{Path.PathSeparator}{currentPath}");
            }
        }
        catch
        {
        }

        // Pre-load ALL native libraries from the directory containing whisper.dll.
        // whisper.dll depends on ggml-*.dll; if they're not on PATH or in the exe directory,
        // the OS loader can't find them. Pre-loading them into the process ensures they're
        // available when Whisper.NET later loads whisper.dll via DllImport.
        if (libDir != null)
        {
            PreloadNativeLibraries(libDir);

            // Ensure runtimes/{rid}/ directory exists with the native DLLs.
            // Whisper.net's NativeLibraryLoader searches runtimes/{platform}-{arch}/ for CPU,
            // but in published single-file builds this directory may not exist (the NuGet
            // targets' copies get bundled inside the exe). Create it at runtime if needed.
            EnsureRuntimesDirectory(libDir);
        }
    }

    /// <summary>
    ///     Ensure the runtimes/{rid}/ directory exists with native DLLs.
    ///     Whisper.net's NativeLibraryLoader searches runtimes/{platform}-{arch}/ for CPU runtime.
    ///     In published single-file builds, the NuGet targets' copies get bundled inside the exe,
    ///     so this directory may not exist. Create it at runtime by copying from the source directory.
    /// </summary>
    private static void EnsureRuntimesDirectory(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory)) return;

        try
        {
            var rid = GetRuntimeIdentifier();
            var runtimeDir = Path.Combine(sourceDirectory, "runtimes", rid);

            if (Directory.Exists(runtimeDir)) return; // Already exists

            var nativeExtensions = GetNativeExtensions();
            var nativeFiles = Directory.GetFiles(sourceDirectory)
                .Where(f => nativeExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (nativeFiles.Count == 0) return;

            Directory.CreateDirectory(runtimeDir);

            foreach (var sourceFile in nativeFiles)
            {
                try
                {
                    var destPath = Path.Combine(runtimeDir, Path.GetFileName(sourceFile));
                    if (!File.Exists(destPath))
                        File.Copy(sourceFile, destPath);
                }
                catch
                {
                    /* Best effort — directory may be read-only */
                }
            }
        }
        catch
        {
            /* Best effort */
        }
    }

    /// <summary>
    ///     Pre-load all native libraries from a directory into the process.
    ///     Loads dependency DLLs (ggml-*.dll) before whisper.dll so the OS loader
    ///     can resolve them when whisper.dll is loaded.
    /// </summary>
    private static void PreloadNativeLibraries(string directory)
    {
        if (!Directory.Exists(directory)) return;

        var nativeExtensions = GetNativeExtensions();

        try
        {
            var nativeFiles = Directory.GetFiles(directory)
                .Where(f => nativeExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                // Load dependencies (ggml-*) before whisper.dll
                .OrderBy(f => Path.GetFileName(f).Contains("whisper", StringComparison.OrdinalIgnoreCase)
                              && !Path.GetFileName(f).Contains("ggml", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 0)
                .ToList();

            // On macOS, remove quarantine attributes before loading.
            // Downloaded files get com.apple.quarantine which causes Gatekeeper to block dlopen.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                RemoveMacQuarantine(nativeFiles);

            foreach (var file in nativeFiles)
                try
                {
                    NativeLibrary.Load(file);
                }
                catch
                {
                    // Best effort — some may already be loaded or not needed on this platform
                }
        }
        catch
        {
            // Best effort
        }
    }

    /// <summary>
    ///     On macOS, remove the com.apple.quarantine extended attribute from downloaded files.
    ///     Files downloaded from the internet are quarantined by Gatekeeper and will fail
    ///     to load via dlopen with "library not valid for use in process" or similar errors.
    ///     Also handles codesign requirements for ad-hoc signing of unsigned binaries.
    /// </summary>
    private static void RemoveMacQuarantine(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            try
            {
                // Remove quarantine attribute
                var xattr = new ProcessStartInfo("xattr", $"-d com.apple.quarantine \"{file}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var xattrProc = Process.Start(xattr);
                xattrProc?.WaitForExit(5000);

                // Ad-hoc codesign — required on Apple Silicon for unsigned dylibs.
                // The -f flag re-signs even if already signed (handles corrupt/missing signatures).
                var codesign = new ProcessStartInfo("codesign", $"--force --sign - \"{file}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var codesignProc = Process.Start(codesign);
                codesignProc?.WaitForExit(5000);
            }
            catch
            {
                // Best effort — xattr/codesign may not be available
            }
        }
    }

    /// <summary>
    ///     Get manual installation instructions.
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