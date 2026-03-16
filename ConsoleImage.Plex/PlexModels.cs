// Plex Media Server data models (XML-deserialized via System.Xml.Linq)

namespace ConsoleImage.Plex;

/// <summary>Plex server connection config — loaded from file or env vars.</summary>
public record PlexConfig(string ServerUrl, string Token);

/// <summary>A Plex library section (Movies, TV Shows, Music, Photos).</summary>
public record PlexLibrary(string Key, string Title, string Type)
{
    public string DisplayTitle => Type switch
    {
        "movie"   => $"[Movies] {Title}",
        "show"    => $"[TV]     {Title}",
        "music"   => $"[Music]  {Title}",
        "photo"   => $"[Photos] {Title}",
        _         => Title
    };
}

/// <summary>A playable media item returned from search or library browse.</summary>
public record PlexMediaItem(
    string RatingKey,
    string Title,
    string? Year,
    string Type,
    string ThumbPath,
    string? GrandparentTitle,  // Show title for episodes
    string? ParentTitle,       // Season title for episodes
    string? PartKey)           // Direct stream path (null for shows/seasons)
{
    public bool IsPlayable => PartKey != null;

    public string DisplayTitle => Type switch
    {
        "episode" => $"{GrandparentTitle} – {Title}",
        _         => Year != null ? $"{Title} ({Year})" : Title
    };

    public string DetailLine => Type switch
    {
        "movie"   => $"Movie · {Year}",
        "episode" => $"Episode · {GrandparentTitle} · {ParentTitle}",
        "show"    => $"TV Show · {Year}",
        "track"   => $"Track · {GrandparentTitle}",
        _         => Type
    };
}
