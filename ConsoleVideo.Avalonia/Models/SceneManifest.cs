using System.Text.Json.Serialization;

namespace ConsoleVideo.Avalonia.Models;

/// <summary>
///     Scene manifest containing keyframes and metadata.
///     JSON-LD compatible format.
/// </summary>
public class SceneManifest
{
    [JsonPropertyName("@context")] public string Context { get; set; } = "https://schema.org/";

    [JsonPropertyName("@type")] public string Type { get; set; } = "SceneManifest";

    public string Version { get; set; } = "1.0";
    public DateTime Created { get; set; } = DateTime.UtcNow;

    public VideoMetadata Video { get; set; } = new();
    public ExtractionMetadata Extraction { get; set; } = new();
    public List<KeyframeEntry> Keyframes { get; set; } = [];
}

public class VideoMetadata
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public double Duration { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public string? Codec { get; set; }
}

public class ExtractionMetadata
{
    public string Strategy { get; set; } = "uniform";
    public int RequestedCount { get; set; }
    public int ActualCount { get; set; }
    public double? StartTime { get; set; }
    public double? EndTime { get; set; }
    public double SceneThreshold { get; set; }
}

public class KeyframeEntry
{
    public int Index { get; set; }

    /// <summary>
    ///     Timestamp in seconds from video start - use this to seek to this position.
    /// </summary>
    public double Timestamp { get; set; }

    /// <summary>
    ///     Human-readable timestamp (e.g., "01:23.4") for display.
    /// </summary>
    public string? TimestampFormatted { get; set; }

    /// <summary>
    ///     Percentage position in video (0-100) for quick positioning.
    /// </summary>
    public double? PositionPercent { get; set; }

    /// <summary>
    ///     Source/extraction method that selected this frame.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    ///     Whether this frame is at a scene boundary.
    /// </summary>
    public bool? IsSceneBoundary { get; set; }

    public string Path { get; set; } = "";
    public string? AsciiPath { get; set; }
    public double? SceneChangeScore { get; set; }
}

/// <summary>
///     JSON source generation context for AOT compatibility.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SceneManifest))]
[JsonSerializable(typeof(VideoMetadata))]
[JsonSerializable(typeof(ExtractionMetadata))]
[JsonSerializable(typeof(KeyframeEntry))]
[JsonSerializable(typeof(List<KeyframeEntry>))]
public partial class SceneManifestJsonContext : JsonSerializerContext
{
}