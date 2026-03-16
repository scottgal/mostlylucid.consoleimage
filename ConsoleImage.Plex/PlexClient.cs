// Minimal Plex Media Server HTTP client
// Uses System.Xml.Linq for XML parsing — no NuGet Plex library needed

using System.Xml.Linq;

namespace ConsoleImage.Plex;

public sealed class PlexClient : IDisposable
{
    private readonly PlexConfig _config;
    private readonly HttpClient _http;

    public PlexClient(PlexConfig config)
    {
        _config = config;
        _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("X-Plex-Token", config.Token);
        _http.DefaultRequestHeaders.Add("Accept", "application/xml");
    }

    public void Dispose() => _http.Dispose();

    // URL helpers

    private string Url(string path) =>
        $"{_config.ServerUrl}{path}?X-Plex-Token={_config.Token}";

    public string ThumbUrl(string thumbPath) => Url(thumbPath);

    /// <summary>Direct-play stream URL for a given part key.</summary>
    public string StreamUrl(string partKey) => Url(partKey);

    // API calls

    /// <summary>List all library sections (Movies, TV Shows, etc.).</summary>
    public async Task<IReadOnlyList<PlexLibrary>> GetLibrariesAsync(CancellationToken ct = default)
    {
        var xml = await _http.GetStringAsync(Url("/library/sections"), ct);
        return ParseLibraries(xml);
    }

    /// <summary>List all items in a library section (flat — movies or top-level shows).</summary>
    public async Task<IReadOnlyList<PlexMediaItem>> GetLibraryItemsAsync(string sectionKey,
        CancellationToken ct = default)
    {
        var xml = await _http.GetStringAsync(Url($"/library/sections/{sectionKey}/all"), ct);
        return ParseItems(xml);
    }

    /// <summary>Get all leaves (episodes/tracks) under a show or season.</summary>
    public async Task<IReadOnlyList<PlexMediaItem>> GetAllLeavesAsync(string ratingKey,
        CancellationToken ct = default)
    {
        var xml = await _http.GetStringAsync(Url($"/library/metadata/{ratingKey}/allLeaves"), ct);
        return ParseItems(xml);
    }

    /// <summary>Get a single item's metadata by rating key (resolves part key for movies/episodes).</summary>
    public async Task<PlexMediaItem?> GetMetadataAsync(string ratingKey, CancellationToken ct = default)
    {
        try
        {
            var xml = await _http.GetStringAsync(Url($"/library/metadata/{ratingKey}"), ct);
            return ParseItems(xml).FirstOrDefault();
        }
        catch { return null; }
    }

    /// <summary>Search across all libraries.</summary>
    public async Task<IReadOnlyList<PlexMediaItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        var url = $"{_config.ServerUrl}/search" +
                  $"?query={Uri.EscapeDataString(query)}" +
                  $"&limit=50" +
                  $"&X-Plex-Token={_config.Token}";
        var xml = await _http.GetStringAsync(url, ct);
        return ParseItems(xml);
    }

    /// <summary>Fetch thumbnail image bytes for rendering.</summary>
    public async Task<Stream?> GetThumbStreamAsync(string thumbPath, int width = 200, int height = 300,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"{_config.ServerUrl}/photo/:/transcode" +
                      $"?url={Uri.EscapeDataString(thumbPath)}" +
                      $"&width={width}&height={height}" +
                      $"&X-Plex-Token={_config.Token}";
            return await _http.GetStreamAsync(url, ct);
        }
        catch
        {
            // Fall back to raw thumb URL
            try { return await _http.GetStreamAsync(Url(thumbPath), ct); }
            catch { return null; }
        }
    }

    /// <summary>Test connectivity — returns server friendly name or throws.</summary>
    public async Task<string> PingAsync(CancellationToken ct = default)
    {
        var xml = await _http.GetStringAsync(Url(""), ct);
        var doc = XDocument.Parse(xml);
        return doc.Root?.Attribute("friendlyName")?.Value ?? "Plex Server";
    }

    // XML parsing — internal so tests can exercise them directly

    internal static IReadOnlyList<PlexLibrary> ParseLibraries(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Root?.Elements("Directory")
            .Select(d => new PlexLibrary(
                Key:   d.Attribute("key")?.Value   ?? "",
                Title: d.Attribute("title")?.Value ?? "(unnamed)",
                Type:  d.Attribute("type")?.Value  ?? ""))
            .Where(l => l.Key != "")
            .ToList()
            ?? [];
    }

    internal static IReadOnlyList<PlexMediaItem> ParseItems(string xml) =>
        ParseItems(XDocument.Parse(xml));

    private static IReadOnlyList<PlexMediaItem> ParseItems(XDocument doc)
    {
        var items = new List<PlexMediaItem>();
        if (doc.Root == null) return items;

        // Plex returns different element names: Video (movie/episode), Directory (show/season), Track
        foreach (var el in doc.Root.Elements())
        {
            var item = ParseElement(el);
            if (item != null)
                items.Add(item);
        }

        return items;
    }

    internal static PlexMediaItem? ParseElement(XElement el)
    {
        var ratingKey = el.Attribute("ratingKey")?.Value;
        if (string.IsNullOrEmpty(ratingKey)) return null;

        var type        = el.Attribute("type")?.Value  ?? el.Name.LocalName.ToLowerInvariant();
        var title       = el.Attribute("title")?.Value ?? "(untitled)";
        var year        = el.Attribute("year")?.Value;
        var thumb       = el.Attribute("thumb")?.Value ?? $"/library/metadata/{ratingKey}/thumb";
        var grandparent = el.Attribute("grandparentTitle")?.Value;
        var parent      = el.Attribute("parentTitle")?.Value;

        // Find the first playable part (direct stream key)
        var partKey = el.Descendants("Part").FirstOrDefault()?.Attribute("key")?.Value;

        return new PlexMediaItem(
            RatingKey:        ratingKey,
            Title:            title,
            Year:             year,
            Type:             type,
            ThumbPath:        thumb,
            GrandparentTitle: grandparent,
            ParentTitle:      parent,
            PartKey:          partKey);
    }
}
