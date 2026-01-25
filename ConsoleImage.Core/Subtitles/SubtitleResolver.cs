using System.Diagnostics;
using System.Text.Json;

namespace ConsoleImage.Core.Subtitles;

/// <summary>
/// Resolves subtitles from multiple sources in priority order:
/// 1. Explicit path (user specified --srt path.srt)
/// 2. YouTube subtitles (if YouTube URL)
/// 3. Embedded subtitles in video file
/// 4. Search for matching subtitle files (video.srt, video.en.srt)
/// 5. Whisper transcription (handled separately - last resort)
/// </summary>
public static class SubtitleResolver
{
    /// <summary>
    /// Search for subtitle files matching a video file.
    /// Searches for common naming patterns: video.srt, video.en.srt, video.eng.srt, etc.
    /// </summary>
    /// <param name="videoPath">Path to video file</param>
    /// <param name="preferredLanguage">Preferred language code (en, es, etc.)</param>
    /// <returns>Path to found subtitle file, or null if none found</returns>
    public static string? FindMatchingSubtitleFile(string videoPath, string? preferredLanguage = null)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            return null;

        var dir = Path.GetDirectoryName(videoPath);
        if (string.IsNullOrEmpty(dir))
            dir = ".";

        var baseName = Path.GetFileNameWithoutExtension(videoPath);
        var lang = preferredLanguage ?? "en";

        // Priority order for subtitle file search
        var patterns = new[]
        {
            // Exact language match first
            $"{baseName}.{lang}.srt",
            $"{baseName}.{lang}.vtt",
            // 3-letter language codes
            $"{baseName}.{Get3LetterCode(lang)}.srt",
            $"{baseName}.{Get3LetterCode(lang)}.vtt",
            // No language suffix
            $"{baseName}.srt",
            $"{baseName}.vtt",
            // Common variations
            $"{baseName}.english.srt",
            $"{baseName}.English.srt",
            $"{baseName}_en.srt",
            $"{baseName}_eng.srt",
            // Forced subs
            $"{baseName}.{lang}.forced.srt",
            $"{baseName}.forced.srt",
        };

        foreach (var pattern in patterns)
        {
            var path = Path.Combine(dir, pattern);
            if (File.Exists(path))
                return path;
        }

        // Search for any .srt/.vtt file with matching base name
        try
        {
            var srtFiles = Directory.GetFiles(dir, $"{baseName}*.srt")
                .Concat(Directory.GetFiles(dir, $"{baseName}*.vtt"))
                .ToArray();

            if (srtFiles.Length > 0)
            {
                // Prefer language-specific files
                var langFile = srtFiles.FirstOrDefault(f =>
                    f.Contains($".{lang}.", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains($"_{lang}.", StringComparison.OrdinalIgnoreCase));

                return langFile ?? srtFiles[0];
            }
        }
        catch
        {
            // Ignore search errors
        }

        return null;
    }

    /// <summary>
    /// Search for subtitle files using OpenSubtitles-style naming from media servers.
    /// </summary>
    /// <param name="videoPath">Path to video file</param>
    /// <param name="searchSubdirectories">Whether to search in Subs/ subdirectory</param>
    /// <returns>Path to found subtitle file, or null if none found</returns>
    public static string? FindSubtitleInMediaLibrary(string videoPath, bool searchSubdirectories = true)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            return null;

        var dir = Path.GetDirectoryName(videoPath);
        if (string.IsNullOrEmpty(dir))
            dir = ".";

        var baseName = Path.GetFileNameWithoutExtension(videoPath);

        // Check for Subs/ or Subtitles/ subdirectory (common in media libraries)
        if (searchSubdirectories)
        {
            var subDirs = new[] { "Subs", "subs", "Subtitles", "subtitles", "Sub" };
            foreach (var subDir in subDirs)
            {
                var subPath = Path.Combine(dir, subDir);
                if (Directory.Exists(subPath))
                {
                    // Search in subdirectory
                    try
                    {
                        var files = Directory.GetFiles(subPath, "*.srt")
                            .Concat(Directory.GetFiles(subPath, "*.vtt"))
                            .ToArray();

                        // First try to match video name
                        var match = files.FirstOrDefault(f =>
                            Path.GetFileNameWithoutExtension(f).StartsWith(baseName, StringComparison.OrdinalIgnoreCase));

                        if (match != null)
                            return match;

                        // If single subtitle file, use it
                        if (files.Length == 1)
                            return files[0];

                        // Prefer English
                        var englishFile = files.FirstOrDefault(f =>
                            f.Contains("eng", StringComparison.OrdinalIgnoreCase) ||
                            f.Contains("english", StringComparison.OrdinalIgnoreCase));

                        if (englishFile != null)
                            return englishFile;
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Text-based subtitle codecs that FFmpeg can extract to SRT.
    /// Bitmap-based codecs (hdmv_pgs_subtitle, dvd_subtitle, dvb_subtitle) are excluded.
    /// </summary>
    private static readonly HashSet<string> TextSubtitleCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "subrip", "ass", "ssa", "webvtt", "mov_text", "sami", "microdvd", "srt",
        "subviewer", "subviewer1", "realtext", "stl", "vplayer", "pjs", "mpl2",
        "jacosub", "text"
    };

    /// <summary>
    /// Extract embedded text subtitles from a video file using FFmpeg/FFprobe.
    /// Probes the file for text-based subtitle streams, selects the best match
    /// by language preference and default disposition, then extracts to SRT.
    /// </summary>
    /// <param name="videoPath">Path to video file (MKV, MP4, etc.)</param>
    /// <param name="preferredLanguage">Preferred language code (en, es, etc.)</param>
    /// <param name="outputDir">Directory for extracted SRT file (null = subtitle cache dir)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Path to extracted SRT file, or null if no text subtitles found</returns>
    public static async Task<string?> ExtractEmbeddedSubtitlesAsync(
        string videoPath,
        string? preferredLanguage = null,
        string? outputDir = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            return null;

        var lang = preferredLanguage ?? "en";
        var lang3 = Get3LetterCode(lang);

        // Probe for subtitle streams
        var streams = await ProbeSubtitleStreamsAsync(videoPath, ct);
        if (streams.Count == 0)
            return null;

        // Filter to text-based subtitles only
        var textStreams = streams.Where(s => TextSubtitleCodecs.Contains(s.CodecName)).ToList();
        if (textStreams.Count == 0)
        {
            // Only bitmap subtitles available
            var bitmapCodecs = string.Join(", ", streams.Select(s => s.CodecName).Distinct());
            Console.Error.WriteLine($"Embedded subtitles are bitmap-based ({bitmapCodecs}) - cannot extract as text.");
            return null;
        }

        // Select best stream: prefer language match, then default disposition
        var selected = textStreams
            .OrderByDescending(s => s.Language == lang || s.Language == lang3 ? 2 : 0)
            .ThenByDescending(s => s.IsDefault ? 1 : 0)
            .ThenBy(s => s.StreamIndex)
            .First();

        // Extract to SRT
        var cacheDir = outputDir ?? GetSubtitleCacheDirectory();
        Directory.CreateDirectory(cacheDir);

        var baseName = Path.GetFileNameWithoutExtension(videoPath);
        var srtPath = Path.Combine(cacheDir, $"{baseName}.embedded.{selected.Language ?? lang}.srt");

        // Use cached extraction if already exists
        if (File.Exists(srtPath) && new FileInfo(srtPath).Length > 0)
            return srtPath;

        var success = await ExtractSubtitleStreamAsync(videoPath, selected.StreamIndex, srtPath, ct);
        if (success && File.Exists(srtPath) && new FileInfo(srtPath).Length > 0)
            return srtPath;

        // Cleanup failed extraction
        try { if (File.Exists(srtPath)) File.Delete(srtPath); } catch { }
        return null;
    }

    /// <summary>
    /// Probe a video file for subtitle streams using ffprobe.
    /// </summary>
    private static async Task<List<SubtitleStreamInfo>> ProbeSubtitleStreamsAsync(
        string videoPath, CancellationToken ct)
    {
        var result = new List<SubtitleStreamInfo>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v quiet -print_format json -show_streams -select_streams s \"{videoPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return result;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return result;

            using var doc = JsonDocument.Parse(output);
            if (!doc.RootElement.TryGetProperty("streams", out var streams))
                return result;

            foreach (var stream in streams.EnumerateArray())
            {
                var codecName = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() ?? "" : "";
                var index = stream.TryGetProperty("index", out var idx) ? idx.GetInt32() : -1;
                var isDefault = stream.TryGetProperty("disposition", out var disp)
                    && disp.TryGetProperty("default", out var def) && def.GetInt32() == 1;

                string? language = null;
                string? title = null;
                if (stream.TryGetProperty("tags", out var tags))
                {
                    if (tags.TryGetProperty("language", out var langProp))
                        language = langProp.GetString();
                    if (tags.TryGetProperty("title", out var titleProp))
                        title = titleProp.GetString();
                }

                if (index >= 0)
                {
                    result.Add(new SubtitleStreamInfo
                    {
                        StreamIndex = index,
                        CodecName = codecName,
                        Language = language,
                        Title = title,
                        IsDefault = isDefault,
                        IsTextBased = TextSubtitleCodecs.Contains(codecName)
                    });
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ffprobe not available or failed
        }

        return result;
    }

    /// <summary>
    /// Extract a specific subtitle stream to SRT using ffmpeg.
    /// </summary>
    private static async Task<bool> ExtractSubtitleStreamAsync(
        string videoPath, int streamIndex, string outputPath, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-v quiet -y -i \"{videoPath}\" -map 0:{streamIndex} -c:s srt \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Information about a subtitle stream in a video file.
    /// </summary>
    internal sealed class SubtitleStreamInfo
    {
        public int StreamIndex { get; init; }
        public string CodecName { get; init; } = "";
        public string? Language { get; init; }
        public string? Title { get; init; }
        public bool IsDefault { get; init; }
        public bool IsTextBased { get; init; }

        public override string ToString()
        {
            var parts = new List<string> { $"#{StreamIndex} {CodecName}" };
            if (!string.IsNullOrEmpty(Language)) parts.Add(Language);
            if (!string.IsNullOrEmpty(Title)) parts.Add($"\"{Title}\"");
            if (IsDefault) parts.Add("(default)");
            if (IsTextBased) parts.Add("[text]");
            return string.Join(" ", parts);
        }
    }

    /// <summary>
    /// Get 3-letter ISO 639-2 language code from 2-letter code.
    /// </summary>
    private static string Get3LetterCode(string twoLetterCode)
    {
        return twoLetterCode.ToLowerInvariant() switch
        {
            "en" => "eng",
            "es" => "spa",
            "fr" => "fra",
            "de" => "deu",
            "it" => "ita",
            "pt" => "por",
            "ru" => "rus",
            "ja" => "jpn",
            "ko" => "kor",
            "zh" => "zho",
            "ar" => "ara",
            "hi" => "hin",
            "nl" => "nld",
            "pl" => "pol",
            "sv" => "swe",
            "tr" => "tur",
            _ => twoLetterCode
        };
    }

    /// <summary>
    /// Get the cache directory for subtitle files.
    /// </summary>
    public static string GetCacheDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            return Path.Combine(localAppData, "consoleimage", "subtitles");
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
        {
            return Path.Combine(home, ".local", "share", "consoleimage", "subtitles");
        }

        return Path.Combine(Path.GetTempPath(), "consoleimage", "subtitles");
    }

    /// <summary>
    /// Get the cache directory for YouTube video streams.
    /// </summary>
    public static string GetVideoCacheDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            return Path.Combine(localAppData, "consoleimage", "videos");
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
        {
            return Path.Combine(home, ".local", "share", "consoleimage", "videos");
        }

        return Path.Combine(Path.GetTempPath(), "consoleimage", "videos");
    }

    /// <summary>
    /// Get cached video path for a YouTube video ID.
    /// </summary>
    public static string GetCachedVideoPath(string videoId)
    {
        var cacheDir = GetVideoCacheDirectory();
        Directory.CreateDirectory(cacheDir);
        return Path.Combine(cacheDir, $"{videoId}.mp4");
    }

    /// <summary>
    /// Check if a YouTube video is cached locally.
    /// </summary>
    public static bool IsVideoCached(string videoId)
    {
        var path = GetCachedVideoPath(videoId);
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    /// <summary>
    /// Maximum video cache size in megabytes (default: 2GB).
    /// </summary>
    public static long MaxVideoCacheSizeMB { get; set; } = 2048;

    /// <summary>
    /// Maximum subtitle cache size in megabytes (default: 100MB).
    /// </summary>
    public static long MaxSubtitleCacheSizeMB { get; set; } = 100;

    /// <summary>
    /// Clean up cache files to stay within size limits.
    /// Uses LRU (Least Recently Used) strategy - deletes oldest files first.
    /// </summary>
    public static void CleanupCache()
    {
        CleanupCacheDirectory(GetSubtitleCacheDirectory(), MaxSubtitleCacheSizeMB);
        CleanupCacheDirectory(GetVideoCacheDirectory(), MaxVideoCacheSizeMB);
    }

    /// <summary>
    /// Clean up old cache files (older than specified days).
    /// </summary>
    public static void CleanupOldCacheFiles(int maxAgeDays = 7)
    {
        var cutoff = DateTime.Now.AddDays(-maxAgeDays);

        CleanupOldFilesInDirectory(GetCacheDirectory(), cutoff);
        CleanupOldFilesInDirectory(GetVideoCacheDirectory(), cutoff);
    }

    /// <summary>
    /// Ensure video cache has space for a new file.
    /// Deletes oldest files if cache would exceed limit.
    /// </summary>
    /// <param name="requiredSpaceMB">Space needed for new file in MB</param>
    public static void EnsureVideoCacheSpace(long requiredSpaceMB = 500)
    {
        var cacheDir = GetVideoCacheDirectory();
        if (!Directory.Exists(cacheDir))
            return;

        try
        {
            var files = Directory.GetFiles(cacheDir)
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastAccessTime)
                .ToList();

            var totalSizeMB = files.Sum(f => f.Length) / (1024 * 1024);

            // Delete oldest files until we have enough space
            while (totalSizeMB + requiredSpaceMB > MaxVideoCacheSizeMB && files.Count > 0)
            {
                var oldest = files[0];
                files.RemoveAt(0);
                totalSizeMB -= oldest.Length / (1024 * 1024);

                try
                {
                    oldest.Delete();
                }
                catch { }
            }
        }
        catch { }
    }

    /// <summary>
    /// Get the subtitle cache directory.
    /// </summary>
    public static string GetSubtitleCacheDirectory() => GetCacheDirectory();

    private static void CleanupCacheDirectory(string cacheDir, long maxSizeMB)
    {
        if (!Directory.Exists(cacheDir))
            return;

        try
        {
            var files = Directory.GetFiles(cacheDir)
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastAccessTime)
                .ToList();

            var totalSizeMB = files.Sum(f => f.Length) / (1024 * 1024);

            // Delete oldest files until under limit
            while (totalSizeMB > maxSizeMB && files.Count > 0)
            {
                var oldest = files[0];
                files.RemoveAt(0);
                totalSizeMB -= oldest.Length / (1024 * 1024);

                try
                {
                    oldest.Delete();
                }
                catch { }
            }
        }
        catch { }
    }

    private static void CleanupOldFilesInDirectory(string dir, DateTime cutoff)
    {
        if (!Directory.Exists(dir))
            return;

        try
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                        File.Delete(file);
                }
                catch { }
            }
        }
        catch { }
    }
}
