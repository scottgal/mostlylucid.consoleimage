// PlayerDocument - Minimal document model for playing back ConsoleImage JSON
// Zero dependencies beyond System.Text.Json and System.IO.Compression (built-in)

using System.IO.Compression;
using System.Text;
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
[JsonSerializable(typeof(OptimizedDocument))]
[JsonSerializable(typeof(OptimizedFrame))]
[JsonSerializable(typeof(List<OptimizedFrame>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(PlayerSubtitleTrack))]
[JsonSerializable(typeof(PlayerSubtitleEntry))]
[JsonSerializable(typeof(List<PlayerSubtitleEntry>))]
public partial class PlayerJsonContext : JsonSerializerContext;

/// <summary>
///     Minimal document model for playing ConsoleImage JSON documents.
///     Compatible with compressed .cidz, standard JSON, and streaming NDJSON formats.
/// </summary>
public class PlayerDocument
{
    /// <summary>
    ///     JSON-LD context for semantic web compatibility
    /// </summary>
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://schema.org/";

    /// <summary>
    ///     JSON-LD type identifier
    /// </summary>
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "ConsoleImageDocument";

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
    ///     Load a document from a file (auto-detects format: .cidz, .json, .ndjson)
    /// </summary>
    /// <exception cref="FileNotFoundException">File does not exist</exception>
    /// <exception cref="JsonException">Invalid JSON format</exception>
    public static async Task<PlayerDocument> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Document not found: {path}", path);

        // Check magic bytes for format detection
        await using var checkStream = File.OpenRead(path);
        var magic = new byte[2];
        var read = await checkStream.ReadAsync(magic, ct);
        checkStream.Position = 0;

        // GZip magic: 0x1F 0x8B
        if (read >= 2 && magic[0] == 0x1F && magic[1] == 0x8B) return await LoadCompressedAsync(checkStream, ct);

        checkStream.Close();

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

        // Check for optimized format
        if (json.Contains("\"@type\":\"OptimizedConsoleImageDocument\"") ||
            json.Contains("\"@type\": \"OptimizedConsoleImageDocument\""))
            return LoadOptimizedFromString(json);

        return JsonSerializer.Deserialize(json, PlayerJsonContext.Default.PlayerDocument)
               ?? throw new InvalidOperationException("Failed to deserialize document");
    }

    /// <summary>
    ///     Load a document from GZip-compressed bytes (.cidz format)
    /// </summary>
    public static PlayerDocument FromCompressedBytes(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var gzip = new GZipStream(ms, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var json = reader.ReadToEnd();
        return FromJson(json);
    }

    /// <summary>
    ///     Load a document from a GZip-compressed stream
    /// </summary>
    public static async Task<PlayerDocument> FromCompressedStreamAsync(Stream stream, CancellationToken ct = default)
    {
        await using var gzip = new GZipStream(stream, CompressionMode.Decompress, true);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct);
        return FromJson(json);
    }

    /// <summary>
    ///     Get an enumerator for streaming frame access (memory efficient for large documents)
    /// </summary>
    public IEnumerable<PlayerFrame> GetFrames()
    {
        foreach (var frame in Frames)
            yield return frame;
    }

    private static async Task<PlayerDocument> LoadCompressedAsync(Stream stream, CancellationToken ct)
    {
        await using var gzip = new GZipStream(stream, CompressionMode.Decompress, true);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct);
        return FromJson(json);
    }

    private static PlayerDocument LoadOptimizedFromString(string json)
    {
        var optimized = JsonSerializer.Deserialize(json, PlayerJsonContext.Default.OptimizedDocument);
        if (optimized == null)
            throw new InvalidOperationException("Failed to deserialize optimized document");

        return optimized.ToPlayerDocument();
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
///     Playback and render settings preserved from document creation
/// </summary>
public class PlayerSettings
{
    /// <summary>Explicit width (null = auto)</summary>
    public int? Width { get; set; }

    /// <summary>Explicit height (null = auto)</summary>
    public int? Height { get; set; }

    /// <summary>Maximum output width</summary>
    public int MaxWidth { get; set; } = 120;

    /// <summary>Maximum output height</summary>
    public int MaxHeight { get; set; } = 60;

    /// <summary>Character aspect ratio used during rendering</summary>
    public float CharacterAspectRatio { get; set; } = 0.5f;

    /// <summary>Contrast power used during rendering</summary>
    public float ContrastPower { get; set; } = 2.5f;

    /// <summary>Gamma correction used during rendering</summary>
    public float Gamma { get; set; } = 0.85f;

    /// <summary>Whether color output is enabled</summary>
    public bool UseColor { get; set; } = true;

    /// <summary>Whether output was inverted (for dark terminals)</summary>
    public bool Invert { get; set; } = true;

    /// <summary>Character set preset name</summary>
    public string? CharacterSetPreset { get; set; }

    /// <summary>Animation speed multiplier (1.0 = normal)</summary>
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    /// <summary>Number of loops (0 = infinite)</summary>
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

// Types for optimized/compressed format (.cidz)

/// <summary>
///     Optimized document format with global color palette and delta compression.
/// </summary>
public class OptimizedDocument
{
    [JsonPropertyName("@type")] public string Type { get; set; } = "OptimizedConsoleImageDocument";

    public string Version { get; set; } = "3.1";
    public DateTime Created { get; set; }
    public string? SourceFile { get; set; }
    public string RenderMode { get; set; } = "ASCII";
    public PlayerSettings Settings { get; set; } = new();

    /// <summary>
    ///     Global color palette - each entry is "RRGGBB" hex string.
    ///     Index 0 is reserved for "no color" (use terminal default).
    /// </summary>
    public string[] Palette { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Keyframe interval for delta encoding.
    /// </summary>
    public int KeyframeInterval { get; set; } = 30;

    /// <summary>
    ///     Optimized frames - mix of keyframes and delta frames.
    /// </summary>
    public List<OptimizedFrame> Frames { get; set; } = new();

    /// <summary>
    ///     Convert optimized document to standard PlayerDocument by reconstructing frames.
    /// </summary>
    public PlayerDocument ToPlayerDocument()
    {
        var doc = new PlayerDocument
        {
            Type = "ConsoleImageDocument",
            Version = Version,
            Created = Created,
            SourceFile = SourceFile,
            RenderMode = RenderMode,
            Settings = Settings
        };

        string? prevChars = null;
        List<int>? prevIndices = null;

        foreach (var frame in Frames)
        {
            string chars;
            List<int> indices;

            if (frame.IsKeyframe)
            {
                // Keyframe - use stored characters and decompress color indices
                chars = frame.Characters ?? "";
                indices = DecompressColorIndices(frame.ColorIndices);
            }
            else
            {
                // Delta frame - apply changes to previous frame
                if (prevChars == null || prevIndices == null)
                    // No previous frame - skip this delta (shouldn't happen in valid files)
                    continue;
                (chars, indices) = ApplyDelta(prevChars, prevIndices, frame.Delta ?? "");
            }

            // Rebuild ANSI content from characters and colors
            var content = RebuildAnsiContent(chars, indices, Palette);

            doc.Frames.Add(new PlayerFrame
            {
                Content = content,
                DelayMs = frame.DelayMs,
                Width = frame.Width,
                Height = frame.Height
            });

            prevChars = chars;
            prevIndices = indices;
        }

        return doc;
    }

    private static List<int> DecompressColorIndices(string? compressed)
    {
        var result = new List<int>();
        if (string.IsNullOrEmpty(compressed)) return result;

        foreach (var run in compressed.Split(';'))
        {
            if (string.IsNullOrEmpty(run)) continue;
            var parts = run.Split(',');
            var idx = int.Parse(parts[0]);
            var count = parts.Length > 1 ? int.Parse(parts[1]) : 1;
            for (var i = 0; i < count; i++)
                result.Add(idx);
        }

        return result;
    }

    private static (string chars, List<int> indices) ApplyDelta(string prevChars, List<int> prevIndices, string delta)
    {
        var chars = prevChars.ToCharArray();
        var indices = new List<int>(prevIndices);

        if (string.IsNullOrEmpty(delta))
            return (new string(chars), indices);

        foreach (var change in delta.Split(';'))
        {
            if (string.IsNullOrEmpty(change)) continue;

            var colonIdx = change.IndexOf(':');
            if (colonIdx < 0) continue;

            var pos = int.Parse(change.Substring(0, colonIdx));
            var rest = change.Substring(colonIdx + 1);
            var parts = rest.Split(',');

            if (parts.Length >= 2)
            {
                var newChars = UnescapeDeltaChars(parts[0]);
                var colorIdx = int.Parse(parts[1]);
                var count = parts.Length > 2 ? int.Parse(parts[2]) : 1;

                for (var i = 0; i < count && pos + i < chars.Length; i++)
                {
                    chars[pos + i] = i < newChars.Length ? newChars[i] : ' ';
                    while (indices.Count <= pos + i) indices.Add(0);
                    indices[pos + i] = colorIdx;
                }
            }
        }

        return (new string(chars), indices);
    }

    private static string UnescapeDeltaChars(string s)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                sb.Append(s[i + 1] switch
                {
                    'c' => ':',
                    'm' => ',',
                    's' => ';',
                    'n' => '\n',
                    'r' => '\r',
                    '\\' => '\\',
                    _ => s[i + 1]
                });
                i++;
            }
            else
            {
                sb.Append(s[i]);
            }

        return sb.ToString();
    }

    private static string RebuildAnsiContent(string chars, List<int> indices, string[] palette)
    {
        var sb = new StringBuilder();
        var lastColorIdx = -1;

        for (var i = 0; i < chars.Length; i++)
        {
            var colorIdx = i < indices.Count ? indices[i] : 0;

            if (colorIdx != lastColorIdx)
            {
                if (colorIdx == 0 || colorIdx >= palette.Length || string.IsNullOrEmpty(palette[colorIdx]))
                {
                    sb.Append("\x1b[0m");
                }
                else
                {
                    var hex = palette[colorIdx];
                    if (hex.Length >= 6)
                    {
                        var r = Convert.ToInt32(hex.Substring(0, 2), 16);
                        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
                        var b = Convert.ToInt32(hex.Substring(4, 2), 16);
                        sb.Append($"\x1b[38;2;{r};{g};{b}m");
                    }
                }

                lastColorIdx = colorIdx;
            }

            sb.Append(chars[i]);

            // Reset at end of line to prevent color bleeding
            if (chars[i] == '\n' && lastColorIdx != 0)
            {
                sb.Insert(sb.Length - 1, "\x1b[0m");
                lastColorIdx = 0;
            }
        }

        if (lastColorIdx != 0) sb.Append("\x1b[0m");

        return sb.ToString();
    }
}

/// <summary>
///     Optimized frame - can be keyframe (full) or delta (changes only).
/// </summary>
public class OptimizedFrame
{
    /// <summary>True = full keyframe, False = delta from previous frame.</summary>
    public bool IsKeyframe { get; set; } = true;

    /// <summary>Plain characters without ANSI codes (keyframes only).</summary>
    public string? Characters { get; set; }

    /// <summary>RLE-compressed color indices (keyframes only).</summary>
    public string? ColorIndices { get; set; }

    /// <summary>Delta encoding - changes from previous frame (delta frames only).</summary>
    public string? Delta { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
    public int DelayMs { get; set; }
}

// Subtitle data classes for playback

/// <summary>
///     Subtitle track data embedded in documents.
/// </summary>
public class PlayerSubtitleTrack
{
    /// <summary>Language code (e.g., "en", "es").</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>Original source file path.</summary>
    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; set; }

    /// <summary>List of subtitle entries.</summary>
    [JsonPropertyName("entries")]
    public List<PlayerSubtitleEntry> Entries { get; set; } = new();

    /// <summary>Get the active subtitle at the given time.</summary>
    public PlayerSubtitleEntry? GetActiveAt(double seconds)
    {
        var ms = (long)(seconds * 1000);
        return Entries.FirstOrDefault(e => ms >= e.StartMs && ms <= e.EndMs);
    }
}

/// <summary>
///     A single subtitle entry.
/// </summary>
public class PlayerSubtitleEntry
{
    /// <summary>Sequential index.</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>Start time in milliseconds.</summary>
    [JsonPropertyName("startMs")]
    public long StartMs { get; set; }

    /// <summary>End time in milliseconds.</summary>
    [JsonPropertyName("endMs")]
    public long EndMs { get; set; }

    /// <summary>Subtitle text.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}