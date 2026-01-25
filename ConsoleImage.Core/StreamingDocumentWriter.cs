// StreamingDocumentWriter - Write JSON-LD frames incrementally for long videos
// Uses JSON Lines (NDJSON) format so document is always valid and can be stopped at any point

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleImage.Core.Subtitles;

namespace ConsoleImage.Core;

/// <summary>
///     JSON source generator for streaming document types (AOT compatible)
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StreamingDocumentHeader))]
[JsonSerializable(typeof(StreamingDocumentFrame))]
[JsonSerializable(typeof(StreamingDocumentFooter))]
[JsonSerializable(typeof(SubtitleTrackData))]
[JsonSerializable(typeof(SubtitleEntryData))]
[JsonSerializable(typeof(List<SubtitleEntryData>))]
public partial class StreamingDocumentJsonContext : JsonSerializerContext
{
}

/// <summary>
///     Header record for streaming document (first line of NDJSON)
/// </summary>
public class StreamingDocumentHeader
{
    [JsonPropertyName("@context")] public string Context { get; set; } = "https://schema.org/";

    [JsonPropertyName("@type")] public string Type { get; set; } = "ConsoleImageDocumentHeader";

    public string Version { get; set; } = "2.0";
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public string? SourceFile { get; set; }
    public string RenderMode { get; set; } = "ASCII";
    public DocumentRenderSettings Settings { get; set; } = new();

    /// <summary>
    /// Subtitle track data (stored separately, not embedded in frames).
    /// </summary>
    public SubtitleTrackData? Subtitles { get; set; }
}

/// <summary>
///     Frame record for streaming document (each frame line of NDJSON)
/// </summary>
public class StreamingDocumentFrame
{
    [JsonPropertyName("@type")] public string Type { get; set; } = "Frame";

    public int Index { get; set; }
    public string Content { get; set; } = string.Empty;
    public int DelayMs { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
///     Footer record for streaming document (last line of NDJSON, written on finalize)
/// </summary>
public class StreamingDocumentFooter
{
    [JsonPropertyName("@type")] public string Type { get; set; } = "ConsoleImageDocumentFooter";

    public int FrameCount { get; set; }
    public int TotalDurationMs { get; set; }
    public DateTime Completed { get; set; } = DateTime.UtcNow;
    public bool IsComplete { get; set; } = true;
}

/// <summary>
///     Writes a ConsoleImageDocument incrementally using JSON Lines (NDJSON) format.
///     Each line is valid JSON, so the document can be stopped at any point.
///     Format:
///     Line 1: Header with metadata and settings
///     Lines 2-N: Individual frames
///     Last line: Footer with summary (written on dispose/finalize)
/// </summary>
public class StreamingDocumentWriter : IDisposable, IAsyncDisposable
{
    private readonly StreamingDocumentHeader _header;
    private readonly string _path;
    private readonly StreamWriter _writer;
    private bool _disposed;
    private bool _finalized;
    private bool _headerWritten;

    /// <summary>
    ///     Create a new streaming document writer
    /// </summary>
    public StreamingDocumentWriter(
        string path,
        string renderMode,
        RenderOptions options,
        string? sourceFile = null)
    {
        _path = path;
        _writer = new StreamWriter(path, false, Encoding.UTF8);
        _header = new StreamingDocumentHeader
        {
            RenderMode = renderMode,
            SourceFile = sourceFile,
            Settings = DocumentRenderSettings.FromRenderOptions(options)
        };
    }

    /// <summary>
    ///     Number of frames written so far
    /// </summary>
    public int FrameCount { get; private set; }

    /// <summary>
    ///     Total duration of all frames in milliseconds
    /// </summary>
    public int TotalDurationMs { get; private set; }

    /// <summary>
    ///     Set subtitle track data (must be called before WriteHeader)
    /// </summary>
    public void SetSubtitles(SubtitleTrackData? subtitles)
    {
        if (_headerWritten)
            throw new InvalidOperationException("Cannot set subtitles after header has been written");
        _header.Subtitles = subtitles;
    }

    /// <summary>
    ///     Set subtitle metadata for sidecar file discovery (must be called before WriteHeader).
    /// </summary>
    public void SetSubtitleMetadata(string subtitleFile, string? language = null, string? source = null)
    {
        if (_headerWritten)
            throw new InvalidOperationException("Cannot set subtitle metadata after header has been written");
        _header.Settings.SubtitlesEnabled = true;
        _header.Settings.SubtitleFile = subtitleFile;
        _header.Settings.SubtitleLanguage = language;
        _header.Settings.SubtitleSource = source;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        // Auto-finalize on dispose if not already done
        if (_headerWritten && !_finalized) await FinalizeAsync(false);

        await _writer.DisposeAsync();
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Auto-finalize on dispose if not already done
        if (_headerWritten && !_finalized) Finalize(false);

        _writer.Dispose();
        _disposed = true;
    }

    /// <summary>
    ///     Write the header (called automatically on first frame if not called explicitly)
    /// </summary>
    public void WriteHeader()
    {
        if (_headerWritten) return;

        var json = JsonSerializer.Serialize(_header, StreamingDocumentJsonContext.Default.StreamingDocumentHeader);
        _writer.WriteLine(json);
        _headerWritten = true;
    }

    /// <summary>
    ///     Write the header asynchronously
    /// </summary>
    public async Task WriteHeaderAsync(CancellationToken ct = default)
    {
        if (_headerWritten) return;

        var json = JsonSerializer.Serialize(_header, StreamingDocumentJsonContext.Default.StreamingDocumentHeader);
        await _writer.WriteLineAsync(json.AsMemory(), ct);
        _headerWritten = true;
    }

    /// <summary>
    ///     Write a frame to the document
    /// </summary>
    public void WriteFrame(string content, int delayMs, int width = 0, int height = 0)
    {
        EnsureNotDisposed();
        if (!_headerWritten) WriteHeader();

        var lines = content.Split('\n');
        var frame = new StreamingDocumentFrame
        {
            Index = FrameCount,
            Content = content,
            DelayMs = delayMs,
            Width = width > 0 ? width : lines.Length > 0 ? lines[0].Length : 0,
            Height = height > 0 ? height : lines.Length
        };

        var json = JsonSerializer.Serialize(frame, StreamingDocumentJsonContext.Default.StreamingDocumentFrame);
        _writer.WriteLine(json);

        FrameCount++;
        TotalDurationMs += delayMs;
    }

    /// <summary>
    ///     Write a frame asynchronously
    /// </summary>
    public async Task WriteFrameAsync(string content, int delayMs, int width = 0, int height = 0,
        CancellationToken ct = default)
    {
        EnsureNotDisposed();
        if (!_headerWritten) await WriteHeaderAsync(ct);

        var lines = content.Split('\n');
        var frame = new StreamingDocumentFrame
        {
            Index = FrameCount,
            Content = content,
            DelayMs = delayMs,
            Width = width > 0 ? width : lines.Length > 0 ? lines[0].Length : 0,
            Height = height > 0 ? height : lines.Length
        };

        var json = JsonSerializer.Serialize(frame, StreamingDocumentJsonContext.Default.StreamingDocumentFrame);
        await _writer.WriteLineAsync(json.AsMemory(), ct);

        FrameCount++;
        TotalDurationMs += delayMs;
    }

    /// <summary>
    ///     Write an AsciiFrame to the document
    /// </summary>
    public void WriteFrame(AsciiFrame frame, RenderOptions options)
    {
        var darkThreshold = options.Invert ? options.DarkTerminalBrightnessThreshold : null;
        var lightThreshold = !options.Invert ? options.LightTerminalBrightnessThreshold : null;
        var content = options.UseColor ? frame.ToAnsiString(darkThreshold, lightThreshold) : frame.ToString();
        WriteFrame(content, frame.DelayMs, frame.Width, frame.Height);
    }

    /// <summary>
    ///     Write an AsciiFrame asynchronously
    /// </summary>
    public Task WriteFrameAsync(AsciiFrame frame, RenderOptions options, CancellationToken ct = default)
    {
        var darkThreshold = options.Invert ? options.DarkTerminalBrightnessThreshold : null;
        var lightThreshold = !options.Invert ? options.LightTerminalBrightnessThreshold : null;
        var content = options.UseColor ? frame.ToAnsiString(darkThreshold, lightThreshold) : frame.ToString();
        return WriteFrameAsync(content, frame.DelayMs, frame.Width, frame.Height, ct);
    }

    /// <summary>
    ///     Write a ColorBlockFrame to the document
    /// </summary>
    public void WriteFrame(ColorBlockFrame frame)
    {
        WriteFrame(frame.Content, frame.DelayMs);
    }

    /// <summary>
    ///     Write a ColorBlockFrame asynchronously
    /// </summary>
    public Task WriteFrameAsync(ColorBlockFrame frame, CancellationToken ct = default)
    {
        return WriteFrameAsync(frame.Content, frame.DelayMs, ct: ct);
    }

    /// <summary>
    ///     Write a BrailleFrame to the document
    /// </summary>
    public void WriteFrame(BrailleFrame frame)
    {
        WriteFrame(frame.Content, frame.DelayMs);
    }

    /// <summary>
    ///     Write a BrailleFrame asynchronously
    /// </summary>
    public Task WriteFrameAsync(BrailleFrame frame, CancellationToken ct = default)
    {
        return WriteFrameAsync(frame.Content, frame.DelayMs, ct: ct);
    }

    /// <summary>
    ///     Finalize the document by writing the footer
    /// </summary>
    public void Finalize(bool isComplete = true)
    {
        if (_finalized) return;
        if (!_headerWritten) WriteHeader();

        var footer = new StreamingDocumentFooter
        {
            FrameCount = FrameCount,
            TotalDurationMs = TotalDurationMs,
            IsComplete = isComplete
        };

        var json = JsonSerializer.Serialize(footer, StreamingDocumentJsonContext.Default.StreamingDocumentFooter);
        _writer.WriteLine(json);
        _writer.Flush();
        _finalized = true;
    }

    /// <summary>
    ///     Finalize the document asynchronously
    /// </summary>
    public async Task FinalizeAsync(bool isComplete = true, CancellationToken ct = default)
    {
        if (_finalized) return;
        if (!_headerWritten) await WriteHeaderAsync(ct);

        var footer = new StreamingDocumentFooter
        {
            FrameCount = FrameCount,
            TotalDurationMs = TotalDurationMs,
            IsComplete = isComplete
        };

        var json = JsonSerializer.Serialize(footer, StreamingDocumentJsonContext.Default.StreamingDocumentFooter);
        await _writer.WriteLineAsync(json.AsMemory(), ct);
        await _writer.FlushAsync(ct);
        _finalized = true;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamingDocumentWriter));
    }
}

/// <summary>
///     Reads a streaming document (NDJSON format) created by StreamingDocumentWriter
/// </summary>
public static class StreamingDocumentReader
{
    /// <summary>
    ///     Load a streaming document and convert to ConsoleImageDocument for playback
    /// </summary>
    public static async Task<ConsoleImageDocument> LoadAsync(string path, CancellationToken ct = default)
    {
        var doc = new ConsoleImageDocument();

        await foreach (var line in ReadLinesAsync(path, ct))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Detect type from @type field
            if (line.Contains("\"@type\":\"ConsoleImageDocumentHeader\"") ||
                line.Contains("\"@type\": \"ConsoleImageDocumentHeader\""))
            {
                var header =
                    JsonSerializer.Deserialize(line, StreamingDocumentJsonContext.Default.StreamingDocumentHeader);
                if (header != null)
                {
                    doc.SourceFile = header.SourceFile;
                    doc.RenderMode = header.RenderMode;
                    doc.Settings = header.Settings;
                    doc.Created = header.Created;
                    doc.Subtitles = header.Subtitles;
                }
            }
            else if (line.Contains("\"@type\":\"Frame\"") ||
                     line.Contains("\"@type\": \"Frame\""))
            {
                var frame = JsonSerializer.Deserialize(line,
                    StreamingDocumentJsonContext.Default.StreamingDocumentFrame);
                if (frame != null)
                    doc.Frames.Add(new DocumentFrame
                    {
                        Content = frame.Content,
                        DelayMs = frame.DelayMs,
                        Width = frame.Width,
                        Height = frame.Height
                    });
            }
            // Footer is informational, we don't need to process it
        }

        return doc;
    }

    /// <summary>
    ///     Stream frames from a document without loading everything into memory
    /// </summary>
    public static async IAsyncEnumerable<(StreamingDocumentHeader? Header, StreamingDocumentFrame? Frame,
            StreamingDocumentFooter? Footer)>
        StreamAsync(string path, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var line in ReadLinesAsync(path, ct))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.Contains("\"@type\":\"ConsoleImageDocumentHeader\"") ||
                line.Contains("\"@type\": \"ConsoleImageDocumentHeader\""))
            {
                var header =
                    JsonSerializer.Deserialize(line, StreamingDocumentJsonContext.Default.StreamingDocumentHeader);
                yield return (header, null, null);
            }
            else if (line.Contains("\"@type\":\"Frame\"") ||
                     line.Contains("\"@type\": \"Frame\""))
            {
                var frame = JsonSerializer.Deserialize(line,
                    StreamingDocumentJsonContext.Default.StreamingDocumentFrame);
                yield return (null, frame, null);
            }
            else if (line.Contains("\"@type\":\"ConsoleImageDocumentFooter\"") ||
                     line.Contains("\"@type\": \"ConsoleImageDocumentFooter\""))
            {
                var footer =
                    JsonSerializer.Deserialize(line, StreamingDocumentJsonContext.Default.StreamingDocumentFooter);
                yield return (null, null, footer);
            }
        }
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        string path,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) != null) yield return line;
    }

    /// <summary>
    ///     Check if a file is a streaming document (NDJSON format) vs regular JSON
    /// </summary>
    public static async Task<bool> IsStreamingDocumentAsync(string path, CancellationToken ct = default)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        var firstLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(firstLine)) return false;

        return firstLine.Contains("\"@type\":\"ConsoleImageDocumentHeader\"") ||
               firstLine.Contains("\"@type\": \"ConsoleImageDocumentHeader\"");
    }
}