using System.Text.RegularExpressions;

namespace ConsoleImage.Core;

/// <summary>
/// Helper for loading images from remote URLs with download progress.
/// </summary>
public static partial class UrlHelper
{
    /// <summary>
    /// Check if the given path is a URL (http:// or https://).
    /// </summary>
    public static bool IsUrl(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Download content from a URL to a memory stream with progress reporting.
    /// </summary>
    /// <param name="url">The URL to download from</param>
    /// <param name="progress">Optional progress callback (bytesDownloaded, totalBytes). totalBytes may be -1 if unknown.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MemoryStream containing the downloaded content, positioned at the beginning</returns>
    public static async Task<MemoryStream> DownloadAsync(
        string url,
        Action<long, long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ConsoleImage/1.0");

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var memoryStream = new MemoryStream();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[81920]; // 80KB buffer
        long bytesRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await memoryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesRead += read;
            progress?.Invoke(bytesRead, totalBytes);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Download content from a URL synchronously with progress reporting.
    /// </summary>
    public static MemoryStream Download(
        string url,
        Action<long, long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return DownloadAsync(url, progress, cancellationToken).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Get the file extension from a URL, or guess from content type if not present.
    /// </summary>
    public static string? GetExtension(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var lastDot = path.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < path.Length - 1)
            {
                var ext = path[(lastDot + 1)..].ToLowerInvariant();
                // Clean up query string if present
                var queryIndex = ext.IndexOf('?');
                if (queryIndex >= 0)
                    ext = ext[..queryIndex];
                return ext;
            }
        }
        catch
        {
            // Ignore URL parsing errors
        }
        return null;
    }

    /// <summary>
    /// Get content type from URL using HEAD request.
    /// </summary>
    public static async Task<string?> GetContentTypeAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ConsoleImage/1.0");

            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return response.Content.Headers.ContentType?.MediaType;
            }
        }
        catch
        {
            // Ignore errors - content type is optional
        }
        return null;
    }

    /// <summary>
    /// Determine if URL likely points to a video file based on extension or content type.
    /// </summary>
    public static bool IsLikelyVideo(string url)
    {
        var ext = GetExtension(url);
        if (ext != null)
        {
            return VideoExtensions.Contains(ext);
        }
        return false;
    }

    /// <summary>
    /// Determine if URL likely points to an animated image (GIF, WebP, APNG).
    /// </summary>
    public static bool IsLikelyAnimated(string url)
    {
        var ext = GetExtension(url);
        if (ext != null)
        {
            return AnimatedImageExtensions.Contains(ext);
        }
        return false;
    }

    /// <summary>
    /// Determine if URL likely points to a static image.
    /// </summary>
    public static bool IsLikelyImage(string url)
    {
        var ext = GetExtension(url);
        if (ext != null)
        {
            return ImageExtensions.Contains(ext);
        }
        return false;
    }

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "avi", "mov", "wmv", "flv", "webm", "m4v", "mpeg", "mpg", "3gp"
    };

    private static readonly HashSet<string> AnimatedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "gif", "webp", "apng"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg", "jpeg", "png", "bmp", "tiff", "tif", "ico", "svg", "gif", "webp"
    };
}
