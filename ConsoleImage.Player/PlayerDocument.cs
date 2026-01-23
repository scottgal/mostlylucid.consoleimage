// PlayerDocument - Minimal document model for playing back ConsoleImage JSON
// Zero dependencies beyond System.Text.Json (built-in)

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsoleImage.Player;

/// <summary>
///     JSON source generator for AOT compatibility
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PlayerDocument))]
[JsonSerializable(typeof(PlayerSettings))]
[JsonSerializable(typeof(PlayerFrame))]
[JsonSerializable(typeof(List<PlayerFrame>))]
[JsonSerializable(typeof(StreamingHeader))]
[JsonSerializable(typeof(StreamingFrame))]
[JsonSerializable(typeof(StreamingFooter))]
public partial class PlayerJsonContext : JsonSerializerContext;

/// <summary>
///     Minimal document model for playing ConsoleImage JSON documents.
///     Compatible with both standard JSON and streaming NDJSON formats.
/// </summary>
public class PlayerDocument
{
    /// <summary>
    ///     Document version
    /// </summary>
    public string Version { get; set; } = "2.0";

    /// <summary>
    ///     When this document was created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    ///     Original source file name (if known)
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    ///     Render mode: ASCII, ColorBlocks, Braille, or Matrix
    /// </summary>
    public string RenderMode { get; set; } = "ASCII";

    /// <summary>
    ///     Playback settings
    /// </summary>
    public PlayerSettings Settings { get; set; } = new();

    /// <summary>
    ///     All frames in the document
    /// </summary>
    public List<PlayerFrame> Frames { get; set; } = new();

    /// <summary>
    ///     Total number of frames
    /// </summary>
    public int FrameCount => Frames.Count;

    /// <summary>
    ///     Whether this is an animation (more than 1 frame)
    /// </summary>
    public bool IsAnimated => Frames.Count > 1;

    /// <summary>
    ///     Total duration in milliseconds
    /// </summary>
    public int TotalDurationMs => Frames.Sum(f => f.DelayMs);

    /// <summary>
    ///     Load a document from a JSON file (auto-detects format)
    /// </summary>
    public static async Task<PlayerDocument> LoadAsync(string path, CancellationToken ct = default)
    {
        // Check if streaming NDJSON format
        if (await IsStreamingFormatAsync(path, ct))
            return await LoadStreamingAsync(path, ct);

        // Standard JSON format
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, PlayerJsonContext.Default.PlayerDocument, ct)
               ?? throw new InvalidOperationException("Failed to deserialize document");
    }

    /// <summary>
    ///     Load a document from a JSON string
    /// </summary>
    public static PlayerDocument FromJson(string json)
    {
        // Quick check for streaming format (first line contains header type)
        if (json.StartsWith("{") && json.Contains("\"@type\":\"ConsoleImageDocumentHeader\""))
            return LoadStreamingFromString(json);

        return JsonSerializer.Deserialize(json, PlayerJsonContext.Default.PlayerDocument)
               ?? throw new InvalidOperationException("Failed to deserialize document");
    }

    /// <summary>
    ///     Get an enumerator for streaming frame access (memory efficient for large documents)
    /// </summary>
    public IEnumerable<PlayerFrame> GetFrames()
    {
        foreach (var frame in Frames)
            yield return frame;
    }

    private static async Task<bool> IsStreamingFormatAsync(string path, CancellationToken ct)
    {
        using var reader = new StreamReader(path);
        var firstLine = await reader.ReadLineAsync(ct);
        return firstLine?.Contains("\"@type\":\"ConsoleImageDocumentHeader\"") == true ||
               firstLine?.Contains("\"@type\": \"ConsoleImageDocumentHeader\"") == true;
    }

    private static async Task<PlayerDocument> LoadStreamingAsync(string path, CancellationToken ct)
    {
        var doc = new PlayerDocument();

        using var reader = new StreamReader(path);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.Contains("\"@type\":\"ConsoleImageDocumentHeader\"") ||
                line.Contains("\"@type\": \"ConsoleImageDocumentHeader\""))
            {
                var header = JsonSerializer.Deserialize(line, PlayerJsonContext.Default.StreamingHeader);
                if (header != null)
                {
                    doc.SourceFile = header.SourceFile;
                    doc.RenderMode = header.RenderMode;
                    doc.Created = header.Created;
                    if (header.Settings != null)
                        doc.Settings = header.Settings;
                }
            }
            else if (line.Contains("\"@type\":\"Frame\"") ||
                     line.Contains("\"@type\": \"Frame\""))
            {
                var frame = JsonSerializer.Deserialize(line, PlayerJsonContext.Default.StreamingFrame);
                if (frame != null)
                    doc.Frames.Add(new PlayerFrame
                    {
                        Content = frame.Content,
                        DelayMs = frame.DelayMs,
                        Width = frame.Width,
                        Height = frame.Height
                    });
            }
        }

        return doc;
    }

    private static PlayerDocument LoadStreamingFromString(string json)
    {
        var doc = new PlayerDocument();

        foreach (var line in json.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.Contains("\"@type\":\"ConsoleImageDocumentHeader\"") ||
                line.Contains("\"@type\": \"ConsoleImageDocumentHeader\""))
            {
                var header = JsonSerializer.Deserialize(line, PlayerJsonContext.Default.StreamingHeader);
                if (header != null)
                {
                    doc.SourceFile = header.SourceFile;
                    doc.RenderMode = header.RenderMode;
                    doc.Created = header.Created;
                    if (header.Settings != null)
                        doc.Settings = header.Settings;
                }
            }
            else if (line.Contains("\"@type\":\"Frame\"") ||
                     line.Contains("\"@type\": \"Frame\""))
            {
                var frame = JsonSerializer.Deserialize(line, PlayerJsonContext.Default.StreamingFrame);
                if (frame != null)
                    doc.Frames.Add(new PlayerFrame
                    {
                        Content = frame.Content,
                        DelayMs = frame.DelayMs,
                        Width = frame.Width,
                        Height = frame.Height
                    });
            }
        }

        return doc;
    }
}

/// <summary>
///     Playback settings
/// </summary>
public class PlayerSettings
{
    public int MaxWidth { get; set; } = 120;
    public int MaxHeight { get; set; } = 60;
    public bool UseColor { get; set; } = true;
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;
    public int LoopCount { get; set; }
}

/// <summary>
///     A single frame
/// </summary>
public class PlayerFrame
{
    /// <summary>
    ///     The rendered content (with ANSI escape codes if colored)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     Frame delay in milliseconds
    /// </summary>
    public int DelayMs { get; set; }

    /// <summary>
    ///     Width in characters
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Height in lines
    /// </summary>
    public int Height { get; set; }
}

// Types for streaming format parsing (public for JSON source generator)

/// <summary>
///     Header record in streaming NDJSON format
/// </summary>
public class StreamingHeader
{
    [JsonPropertyName("@type")] public string? Type { get; set; }
    public string Version { get; set; } = "2.0";
    public DateTime Created { get; set; }
    public string? SourceFile { get; set; }
    public string RenderMode { get; set; } = "ASCII";
    public PlayerSettings? Settings { get; set; }
}

/// <summary>
///     Frame record in streaming NDJSON format
/// </summary>
public class StreamingFrame
{
    [JsonPropertyName("@type")] public string? Type { get; set; }
    public int Index { get; set; }
    public string Content { get; set; } = string.Empty;
    public int DelayMs { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
///     Footer record in streaming NDJSON format
/// </summary>
public class StreamingFooter
{
    [JsonPropertyName("@type")] public string? Type { get; set; }
    public int FrameCount { get; set; }
    public int TotalDurationMs { get; set; }
    public bool IsComplete { get; set; }
}
