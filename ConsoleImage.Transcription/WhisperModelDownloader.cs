namespace ConsoleImage.Transcription;

/// <summary>
///     Downloads and caches Whisper GGML models from HuggingFace.
///     Cross-platform cache location handling.
/// </summary>
public class WhisperModelDownloader
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    static WhisperModelDownloader()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConsoleImage/1.0");
    }

    /// <summary>
    ///     Get cross-platform cache directory for whisper models.
    /// </summary>
    public static string CacheDirectory
    {
        get
        {
            // Try platform-specific app data first
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(appData))
                return Path.Combine(appData, "consoleimage", "whisper");

            // Linux/WSL fallback
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
                return Path.Combine(home, ".local", "share", "consoleimage", "whisper");

            // Last resort
            return Path.Combine(Path.GetTempPath(), "consoleimage", "whisper");
        }
    }

    /// <summary>
    ///     Check if model is already downloaded locally.
    /// </summary>
    public static bool IsModelCached(string modelSize = "base", string language = "en")
    {
        var fileName = GetModelFileName(modelSize, language);
        var modelPath = Path.Combine(CacheDirectory, fileName);
        return File.Exists(modelPath);
    }

    /// <summary>
    ///     Get model info for prompting user.
    /// </summary>
    public static (string fileName, int sizeMB) GetModelInfo(string modelSize = "base", string language = "en")
    {
        return (GetModelFileName(modelSize, language), GetModelSizeMB(modelSize));
    }

    /// <summary>
    ///     Ensure model is downloaded and return local path.
    /// </summary>
    public static async Task<string> EnsureModelAsync(
        string modelSize = "base",
        string language = "en",
        IProgress<(long downloaded, long total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        var fileName = GetModelFileName(modelSize, language);
        var modelPath = Path.Combine(CacheDirectory, fileName);

        if (File.Exists(modelPath))
        {
            progress?.Report((0, 0, $"Model already cached: {fileName}"));
            return modelPath;
        }

        Directory.CreateDirectory(CacheDirectory);

        var url = GetModelUrl(modelSize, language);
        var sizeMb = GetModelSizeMB(modelSize);

        progress?.Report((0, sizeMb * 1024 * 1024, $"Downloading {modelSize} model (~{sizeMb}MB)..."));

        await DownloadFileAsync(url, modelPath, progress, ct);

        return modelPath;
    }

    private static async Task DownloadFileAsync(
        string url,
        string localPath,
        IProgress<(long downloaded, long total, string status)>? progress,
        CancellationToken ct)
    {
        var tempPath = localPath + ".tmp";

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;

            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    81920, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;
                var lastReport = DateTime.UtcNow;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;

                    // Report progress every 500ms
                    if ((DateTime.UtcNow - lastReport).TotalMilliseconds > 500)
                    {
                        var pct = totalBytes > 0 ? (int)(totalRead * 100 / totalBytes) : 0;
                        progress?.Report((totalRead, totalBytes, $"Downloading... {pct}%"));
                        lastReport = DateTime.UtcNow;
                    }
                }

                progress?.Report((totalRead, totalBytes, "Download complete"));
                // File stream is closed here when exiting the block
            }

            // Small delay to ensure file system catches up (Windows issue)
            await Task.Delay(100, ct);

            // Rename after successful download
            progress?.Report((0, 0, "Moving to cache..."));
            try
            {
                // If target exists, try to delete it first
                if (File.Exists(localPath))
                    try
                    {
                        File.Delete(localPath);
                    }
                    catch (Exception delEx)
                    {
                        throw new IOException($"Cannot delete existing model file {localPath}: {delEx.Message}", delEx);
                    }

                File.Move(tempPath, localPath);
            }
            catch (Exception moveEx)
            {
                throw new IOException($"Failed to move model from {tempPath} to {localPath}: {moveEx.Message}", moveEx);
            }
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private static string GetModelUrl(string modelSize, string language)
    {
        var baseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";
        var fileName = GetModelFileName(modelSize, language);
        return $"{baseUrl}/{fileName}";
    }

    private static string GetModelFileName(string modelSize, string language)
    {
        var size = modelSize.ToLowerInvariant();
        var lang = language.ToLowerInvariant();

        // English-specific models are smaller and faster
        if (lang == "en")
            return size switch
            {
                "tiny" => "ggml-tiny.en.bin",
                "base" => "ggml-base.en.bin",
                "small" => "ggml-small.en.bin",
                "medium" => "ggml-medium.en.bin",
                _ => "ggml-base.en.bin"
            };

        // Multi-language models
        return size switch
        {
            "tiny" => "ggml-tiny.bin",
            "base" => "ggml-base.bin",
            "small" => "ggml-small.bin",
            "medium" => "ggml-medium.bin",
            "large" => "ggml-large-v3.bin",
            _ => "ggml-base.bin"
        };
    }

    private static int GetModelSizeMB(string modelSize)
    {
        return modelSize.ToLowerInvariant() switch
        {
            "tiny" => 75,
            "base" => 142,
            "small" => 466,
            "medium" => 1530,
            "large" => 3090,
            _ => 142
        };
    }
}