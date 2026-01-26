// ConsoleImageDocument - Self-contained JSON format for storing rendered ASCII art
// Allows saving and loading rendered frames with all settings needed to reproduce the output

using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleImage.Core.Subtitles;

namespace ConsoleImage.Core;

/// <summary>
///     JSON source generator for AOT compatibility (indented output)
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ConsoleImageDocument))]
[JsonSerializable(typeof(DocumentRenderSettings))]
[JsonSerializable(typeof(DocumentFrame))]
[JsonSerializable(typeof(List<DocumentFrame>))]
[JsonSerializable(typeof(SubtitleTrackData))]
[JsonSerializable(typeof(SubtitleEntryData))]
[JsonSerializable(typeof(List<SubtitleEntryData>))]
public partial class ConsoleImageJsonContext : JsonSerializerContext
{
}

/// <summary>
///     JSON source generator for AOT compatibility (compact output)
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ConsoleImageDocument))]
public partial class ConsoleImageJsonCompactContext : JsonSerializerContext
{
}

/// <summary>
///     A self-contained document format for storing rendered ASCII art frames.
///     Can be saved to JSON and loaded for playback without the original source.
/// </summary>
public class ConsoleImageDocument
{
    /// <summary>
    ///     Document format version for compatibility
    /// </summary>
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://schema.org/";

    [JsonPropertyName("@type")] public string Type { get; set; } = "ConsoleImageDocument";

    /// <summary>
    ///     Document version for format compatibility
    /// </summary>
    public string Version { get; set; } = "2.0";

    /// <summary>
    ///     When this document was created
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Original source file name (if known)
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    ///     Render mode used: ASCII, ColorBlocks, or Braille
    /// </summary>
    public string RenderMode { get; set; } = "ASCII";

    /// <summary>
    ///     The render settings used to generate this output
    /// </summary>
    public DocumentRenderSettings Settings { get; set; } = new();

    /// <summary>
    ///     The rendered frames
    /// </summary>
    public List<DocumentFrame> Frames { get; set; } = new();

    /// <summary>
    ///     Subtitle track data (if subtitles were included).
    /// </summary>
    public SubtitleTrackData? Subtitles { get; set; }

    /// <summary>
    ///     Total number of frames
    /// </summary>
    public int FrameCount => Frames.Count;

    /// <summary>
    ///     Whether this is an animation (more than 1 frame)
    /// </summary>
    public bool IsAnimated => Frames.Count > 1;

    /// <summary>
    ///     Total duration in milliseconds (for animations)
    /// </summary>
    public int TotalDurationMs => Frames.Sum(f => f.DelayMs);

    /// <summary>
    ///     Save this document to a JSON file (AOT-compatible).
    ///     Supports compressed formats: .cidz, .cid.7z, .7z (uses optimized palette + compression)
    /// </summary>
    public async Task SaveAsync(string path, CancellationToken ct = default)
    {
        // Check for compressed format
        if (CompressedDocumentArchive.IsCompressedDocument(path))
        {
            await CompressedDocumentArchive.SaveAsync(this, path, 30, ct);
            return;
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, this, ConsoleImageJsonContext.Default.ConsoleImageDocument, ct);
    }

    /// <summary>
    ///     Save this document with explicit compression option.
    /// </summary>
    public async Task SaveCompressedAsync(string path, CancellationToken ct = default)
    {
        // Ensure compressed extension
        var compressedPath = path;
        if (!CompressedDocumentArchive.IsCompressedDocument(path)) compressedPath = path + ".cidz";
        await CompressedDocumentArchive.SaveAsync(this, compressedPath, 30, ct);
    }

    /// <summary>
    ///     Save this document to a JSON string (AOT-compatible)
    /// </summary>
    public string ToJson(bool indented = true)
    {
        return indented
            ? JsonSerializer.Serialize(this, ConsoleImageJsonContext.Default.ConsoleImageDocument)
            : JsonSerializer.Serialize(this, ConsoleImageJsonCompactContext.Default.ConsoleImageDocument);
    }

    /// <summary>
    ///     Load a document from a JSON file (AOT-compatible).
    ///     Auto-detects format: regular JSON, streaming NDJSON, or compressed (.cidz, .7z)
    /// </summary>
    public static async Task<ConsoleImageDocument> LoadAsync(string path, CancellationToken ct = default)
    {
        // Check for compressed format first
        if (CompressedDocumentArchive.IsCompressedDocument(path))
            return await CompressedDocumentArchive.LoadAsync(path, null, ct);

        // Check magic bytes for compression even without extension
        await using var checkStream = File.OpenRead(path);
        var magic = new byte[2];
        var read = await checkStream.ReadAsync(magic, ct);
        checkStream.Close();

        // GZip magic: 0x1F 0x8B
        if (read >= 2 && magic[0] == 0x1F && magic[1] == 0x8B)
            return await CompressedDocumentArchive.LoadAsync(path, null, ct);

        // 7z magic starts with "7z"
        if (read >= 2 && magic[0] == 0x37 && magic[1] == 0x7A)
            return await CompressedDocumentArchive.LoadAsync(path, null, ct);

        // Check if this is a streaming document (NDJSON format)
        if (await StreamingDocumentReader.IsStreamingDocumentAsync(path, ct))
            return await StreamingDocumentReader.LoadAsync(path, ct);

        // Regular JSON format
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, ConsoleImageJsonContext.Default.ConsoleImageDocument, ct)
               ?? throw new InvalidOperationException("Failed to deserialize document");
    }

    /// <summary>
    ///     Load a document from a JSON string (AOT-compatible)
    /// </summary>
    public static ConsoleImageDocument FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, ConsoleImageJsonContext.Default.ConsoleImageDocument)
               ?? throw new InvalidOperationException("Failed to deserialize document");
    }

    /// <summary>
    ///     Add a frame to the document (for streaming use)
    /// </summary>
    public void AddFrame(DocumentFrame frame)
    {
        Frames.Add(frame);
    }

    /// <summary>
    ///     Add a frame from content string (for streaming use)
    /// </summary>
    public void AddFrame(string content, int delayMs = 0, int width = 0, int height = 0)
    {
        if (width <= 0 || height <= 0)
        {
            var (w, h) = DocumentFrame.GetContentDimensions(content);
            if (width <= 0) width = w;
            if (height <= 0) height = h;
        }

        Frames.Add(new DocumentFrame
        {
            Content = content,
            DelayMs = delayMs,
            Width = width,
            Height = height
        });
    }

    /// <summary>
    ///     Create a document from ASCII frames
    /// </summary>
    public static ConsoleImageDocument FromAsciiFrames(
        IEnumerable<AsciiFrame> frames,
        RenderOptions options,
        string? sourceFile = null)
    {
        var doc = new ConsoleImageDocument
        {
            RenderMode = "ASCII",
            SourceFile = sourceFile,
            Settings = DocumentRenderSettings.FromRenderOptions(options)
        };

        foreach (var frame in frames) doc.Frames.Add(DocumentFrame.FromAsciiFrame(frame, options));

        return doc;
    }

    /// <summary>
    ///     Create a document from ColorBlock frames
    /// </summary>
    public static ConsoleImageDocument FromColorBlockFrames(
        IEnumerable<ColorBlockFrame> frames,
        RenderOptions options,
        string? sourceFile = null)
    {
        var doc = new ConsoleImageDocument
        {
            RenderMode = "ColorBlocks",
            SourceFile = sourceFile,
            Settings = DocumentRenderSettings.FromRenderOptions(options)
        };

        foreach (var frame in frames) doc.Frames.Add(DocumentFrame.FromColorBlockFrame(frame));

        return doc;
    }

    /// <summary>
    ///     Create a document from Braille frames
    /// </summary>
    public static ConsoleImageDocument FromBrailleFrames(
        IEnumerable<BrailleFrame> frames,
        RenderOptions options,
        string? sourceFile = null)
    {
        var doc = new ConsoleImageDocument
        {
            RenderMode = "Braille",
            SourceFile = sourceFile,
            Settings = DocumentRenderSettings.FromRenderOptions(options)
        };

        foreach (var frame in frames) doc.Frames.Add(DocumentFrame.FromBrailleFrame(frame));

        return doc;
    }

    /// <summary>
    ///     Create a document from Matrix frames
    /// </summary>
    public static ConsoleImageDocument FromMatrixFrames(
        IEnumerable<MatrixFrame> frames,
        RenderOptions options,
        string? sourceFile = null)
    {
        var doc = new ConsoleImageDocument
        {
            RenderMode = "Matrix",
            SourceFile = sourceFile,
            Settings = DocumentRenderSettings.FromRenderOptions(options)
        };

        foreach (var frame in frames) doc.Frames.Add(DocumentFrame.FromMatrixFrame(frame));

        return doc;
    }

    /// <summary>
    ///     Create a document from a single static image
    /// </summary>
    public static ConsoleImageDocument FromSingleFrame(
        string content,
        string renderMode,
        RenderOptions options,
        string? sourceFile = null)
    {
        var doc = new ConsoleImageDocument
        {
            RenderMode = renderMode,
            SourceFile = sourceFile,
            Settings = DocumentRenderSettings.FromRenderOptions(options)
        };

        doc.Frames.Add(new DocumentFrame { Content = content, DelayMs = 0 });

        return doc;
    }
}

/// <summary>
///     Render settings stored in the document
/// </summary>
public class DocumentRenderSettings
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int MaxWidth { get; set; } = 120;
    public int MaxHeight { get; set; } = 60;
    public float CharacterAspectRatio { get; set; } = 0.5f;
    public float ContrastPower { get; set; } = 2.5f;
    public float Gamma { get; set; } = 0.85f;
    public bool UseColor { get; set; } = true;
    public bool Invert { get; set; } = true;
    public string? CharacterSetPreset { get; set; }
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;
    public int LoopCount { get; set; }
    public bool EnableTemporalStability { get; set; }
    public int ColorStabilityThreshold { get; set; } = 15;

    /// <summary>
    ///     Maximum number of colors in the palette.
    ///     Null = full 24-bit color, otherwise reduced palette (e.g., 4, 16, 256).
    /// </summary>
    public int? ColorCount { get; set; }

    /// <summary>
    ///     Indicates subtitles were enabled during recording.
    ///     When true, a sidecar .vtt file may exist alongside the document.
    /// </summary>
    public bool SubtitlesEnabled { get; set; }

    /// <summary>
    ///     Subtitle source type: "auto" (whisper/yt-dlp extracted), "file" (external .srt/.vtt), or null.
    /// </summary>
    public string? SubtitleSource { get; set; }

    /// <summary>
    ///     Subtitle language code (e.g., "en", "es").
    /// </summary>
    public string? SubtitleLanguage { get; set; }

    /// <summary>
    ///     Sidecar subtitle filename (e.g., "movie.vtt") for easy discovery on playback.
    /// </summary>
    public string? SubtitleFile { get; set; }

    /// <summary>
    ///     Create a shallow copy of these settings.
    /// </summary>
    public DocumentRenderSettings Clone()
    {
        return (DocumentRenderSettings)MemberwiseClone();
    }

    /// <summary>
    ///     Create settings from RenderOptions
    /// </summary>
    public static DocumentRenderSettings FromRenderOptions(RenderOptions options)
    {
        return new DocumentRenderSettings
        {
            Width = options.Width,
            Height = options.Height,
            MaxWidth = options.MaxWidth,
            MaxHeight = options.MaxHeight,
            CharacterAspectRatio = options.CharacterAspectRatio,
            ContrastPower = options.ContrastPower,
            Gamma = options.Gamma,
            UseColor = options.UseColor,
            Invert = options.Invert,
            CharacterSetPreset = options.CharacterSetPreset,
            AnimationSpeedMultiplier = options.AnimationSpeedMultiplier,
            LoopCount = options.LoopCount,
            EnableTemporalStability = options.EnableTemporalStability,
            ColorStabilityThreshold = options.ColorStabilityThreshold,
            ColorCount = options.ColorCount
        };
    }

    /// <summary>
    ///     Convert to RenderOptions, with optional overrides
    /// </summary>
    public RenderOptions ToRenderOptions(RenderOptions? overrides = null)
    {
        var options = new RenderOptions
        {
            Width = overrides?.Width ?? Width,
            Height = overrides?.Height ?? Height,
            MaxWidth = overrides?.MaxWidth ?? MaxWidth,
            MaxHeight = overrides?.MaxHeight ?? MaxHeight,
            CharacterAspectRatio = overrides?.CharacterAspectRatio ?? CharacterAspectRatio,
            ContrastPower = overrides?.ContrastPower ?? ContrastPower,
            Gamma = overrides?.Gamma ?? Gamma,
            UseColor = overrides?.UseColor ?? UseColor,
            Invert = overrides?.Invert ?? Invert,
            CharacterSetPreset = overrides?.CharacterSetPreset ?? CharacterSetPreset,
            AnimationSpeedMultiplier = overrides?.AnimationSpeedMultiplier ?? AnimationSpeedMultiplier,
            LoopCount = overrides?.LoopCount ?? LoopCount,
            EnableTemporalStability = overrides?.EnableTemporalStability ?? EnableTemporalStability,
            ColorStabilityThreshold = overrides?.ColorStabilityThreshold ?? ColorStabilityThreshold,
            ColorCount = overrides?.ColorCount ?? ColorCount
        };

        return options;
    }
}

/// <summary>
///     A single frame in the document
/// </summary>
public class DocumentFrame
{
    /// <summary>
    ///     The rendered content (ANSI-escaped string)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     Frame delay in milliseconds (for animations)
    /// </summary>
    public int DelayMs { get; set; }

    /// <summary>
    ///     Width of this frame in characters
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Height of this frame in lines
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     Subtitle text active during this frame (if any).
    /// </summary>
    public string? SubtitleText { get; set; }

    /// <summary>
    ///     Create from AsciiFrame
    /// </summary>
    public static DocumentFrame FromAsciiFrame(AsciiFrame frame, RenderOptions options)
    {
        // Get brightness thresholds
        var darkThreshold = options.Invert ? options.DarkTerminalBrightnessThreshold : null;
        var lightThreshold = !options.Invert ? options.LightTerminalBrightnessThreshold : null;

        return new DocumentFrame
        {
            Content = options.UseColor ? frame.ToAnsiString(darkThreshold, lightThreshold) : frame.ToString(),
            DelayMs = frame.DelayMs,
            Width = frame.Width,
            Height = frame.Height
        };
    }

    /// <summary>
    ///     Create from ColorBlockFrame
    /// </summary>
    public static DocumentFrame FromColorBlockFrame(ColorBlockFrame frame)
    {
        var (w, h) = GetContentDimensions(frame.Content);
        return new DocumentFrame
        {
            Content = frame.Content,
            DelayMs = frame.DelayMs,
            Width = w,
            Height = h
        };
    }

    /// <summary>
    ///     Create from BrailleFrame
    /// </summary>
    public static DocumentFrame FromBrailleFrame(BrailleFrame frame)
    {
        var (w, h) = GetContentDimensions(frame.Content);
        return new DocumentFrame
        {
            Content = frame.Content,
            DelayMs = frame.DelayMs,
            Width = w,
            Height = h
        };
    }

    public static DocumentFrame FromMatrixFrame(MatrixFrame frame)
    {
        var (w, h) = GetContentDimensions(frame.Content);
        return new DocumentFrame
        {
            Content = frame.Content,
            DelayMs = frame.DelayMs,
            Width = w,
            Height = h
        };
    }

    /// <summary>
    ///     Get frame dimensions from content without allocating a string array.
    ///     Returns (firstLineLength, lineCount) matching the behavior of content.Split('\n').
    /// </summary>
    public static (int width, int height) GetContentDimensions(string content)
    {
        if (string.IsNullOrEmpty(content))
            return (0, 1); // Split('\n') returns [""] for empty string

        var firstNewline = content.IndexOf('\n');
        var firstLineLen = firstNewline >= 0 ? firstNewline : content.Length;

        var lineCount = 1;
        for (var i = 0; i < content.Length; i++)
            if (content[i] == '\n')
                lineCount++;

        return (firstLineLen, lineCount);
    }
}