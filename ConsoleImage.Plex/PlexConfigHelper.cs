// Plex configuration - env vars take precedence over config file

namespace ConsoleImage.Plex;

public static class PlexConfigHelper
{
    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "consoleimage");

    private static string ConfigPath => Path.Combine(ConfigDir, "plex.json");

    /// <summary>
    ///     Load config: env vars (PLEX_URL, PLEX_TOKEN) take precedence over saved file.
    /// </summary>
    public static PlexConfig? Load()
    {
        var url   = Environment.GetEnvironmentVariable("PLEX_URL");
        var token = Environment.GetEnvironmentVariable("PLEX_TOKEN");

        if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(token))
            return new PlexConfig(url.TrimEnd('/'), token);

        if (!File.Exists(ConfigPath))
            return null;

        try
        {
            var json = File.ReadAllText(ConfigPath);
            // Hand-rolled parse — two known string fields, no reflection needed (AOT-safe)
            url   = ExtractJsonString(json, "ServerUrl");
            token = ExtractJsonString(json, "Token");
            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(token))
                return new PlexConfig(url, token);
        }
        catch { /* corrupt config */ }

        return null;
    }

    /// <summary>Save config to %APPDATA%/consoleimage/plex.json.</summary>
    public static void Save(PlexConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        // Hand-rolled serialization — AOT-safe, no reflection
        var json = $$"""{"ServerUrl":"{{EscapeJson(config.ServerUrl)}}","Token":"{{EscapeJson(config.Token)}}"}""";
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>Load config from a specific JSON string (used for testing).</summary>
    internal static PlexConfig? LoadFromJson(string json)
    {
        var url   = ExtractJsonString(json, "ServerUrl");
        var token = ExtractJsonString(json, "Token");
        return !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(token)
            ? new PlexConfig(url, token)
            : null;
    }

    internal static string? ExtractJsonString(string json, string key)
    {
        var search = $"\"{key}\":\"";
        var start  = json.IndexOf(search, StringComparison.Ordinal);
        if (start < 0) return null;
        start += search.Length;
        var end = json.IndexOf('"', start);
        return end < 0 ? null : json[start..end];
    }

    internal static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
