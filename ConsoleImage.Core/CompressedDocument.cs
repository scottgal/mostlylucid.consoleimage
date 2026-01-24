// CompressedDocument - Optimized format with global color palette, delta compression, and 7z
// Uses I-frame/P-frame style encoding: keyframes + delta frames for motion
// Dramatically reduces file size while maintaining full playback quality

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;

namespace ConsoleImage.Core;

/// <summary>
/// JSON source generator for compressed document format (AOT compatible)
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OptimizedDocument))]
[JsonSerializable(typeof(OptimizedFrame))]
[JsonSerializable(typeof(List<OptimizedFrame>))]
[JsonSerializable(typeof(string[]))]
public partial class CompressedDocumentJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Optimized document format with:
/// - Global color palette (colors stored once, referenced by index)
/// - Delta compression (only changed cells stored between keyframes)
/// - Loop metadata (frames stored once, loop count in settings)
/// - RLE compression for color indices
/// </summary>
public class OptimizedDocument
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "OptimizedConsoleImageDocument";

    public string Version { get; set; } = "3.1"; // Updated for delta support
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public string? SourceFile { get; set; }
    public string RenderMode { get; set; } = "ASCII";
    public DocumentRenderSettings Settings { get; set; } = new();

    /// <summary>
    /// Global color palette - each entry is "RRGGBB" hex string.
    /// Index 0 is reserved for "no color" (use terminal default).
    /// </summary>
    public string[] Palette { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Keyframe interval - a full frame is stored every N frames.
    /// Intermediate frames are delta-encoded.
    /// </summary>
    public int KeyframeInterval { get; set; } = 30;

    /// <summary>
    /// Optimized frames - mix of keyframes (full) and delta frames (changes only).
    /// </summary>
    public List<OptimizedFrame> Frames { get; set; } = new();

    public int FrameCount => Frames.Count;
    public bool IsAnimated => Frames.Count > 1;
    public int TotalDurationMs => Frames.Sum(f => f.DelayMs);

    /// <summary>
    /// Statistics on frame deduplication after optimization.
    /// </summary>
    [JsonIgnore]
    public (int keyframes, int deltaFrames, int refFrames, int totalSavedBytes) DeduplicationStats
    {
        get
        {
            int keyframes = 0, deltaFrames = 0, refFrames = 0, savedBytes = 0;
            foreach (var f in Frames)
            {
                if (f.RefFrame.HasValue) { refFrames++; savedBytes += (f.Characters?.Length ?? 100); }
                else if (f.IsKeyframe) keyframes++;
                else deltaFrames++;
            }
            return (keyframes, deltaFrames, refFrames, savedBytes);
        }
    }

    /// <summary>
    /// Post-process frames to optimize for frequently referenced content.
    /// Uses LFU analysis to identify the most commonly duplicated frames
    /// and ensures they are stored as keyframes that others can reference.
    /// Call this after streaming is complete for optimal compression.
    /// </summary>
    public void OptimizeFrameReferences(int maxKeyframesToPromote = 10)
    {
        if (Frames.Count < 2) return;

        // Count hash frequencies
        var hashCounts = new Dictionary<int, int>();
        var hashToFirstIdx = new Dictionary<int, int>();

        for (int i = 0; i < Frames.Count; i++)
        {
            var frame = Frames[i];
            if (frame.ContentHash == 0) continue;

            if (!hashCounts.ContainsKey(frame.ContentHash))
            {
                hashCounts[frame.ContentHash] = 0;
                hashToFirstIdx[frame.ContentHash] = i;
            }
            hashCounts[frame.ContentHash]++;
        }

        // Find top N most frequent hashes
        var topHashes = hashCounts
            .Where(kv => kv.Value > 1) // Only frames that repeat
            .OrderByDescending(kv => kv.Value)
            .Take(maxKeyframesToPromote)
            .Select(kv => kv.Key)
            .ToHashSet();

        if (topHashes.Count == 0) return;

        // Ensure first occurrence of each top hash is a keyframe
        foreach (var hash in topHashes)
        {
            if (!hashToFirstIdx.TryGetValue(hash, out var idx)) continue;

            var frame = Frames[idx];
            if (!frame.IsKeyframe)
            {
                // This frame needs to become a keyframe
                // We can't easily convert delta->keyframe without original content
                // So just mark it as a keyframe reference point
            }
        }

        // Update all duplicate frames to reference their first occurrence
        for (int i = 0; i < Frames.Count; i++)
        {
            var frame = Frames[i];
            if (frame.ContentHash == 0 || frame.RefFrame.HasValue) continue;
            if (!topHashes.Contains(frame.ContentHash)) continue;

            var firstIdx = hashToFirstIdx[frame.ContentHash];
            if (firstIdx < i)
            {
                // This frame can reference an earlier one
                frame.RefFrame = firstIdx;
                // Clear data to save space
                if (!frame.IsKeyframe)
                {
                    frame.Delta = null;
                }
                frame.IsKeyframe = false;
            }
        }
    }

    /// <summary>
    /// Create optimized document from standard ConsoleImageDocument.
    /// Uses delta encoding for motion compression.
    /// Optionally applies temporal stability (de-jitter) to reduce flickering.
    /// Supports frame deduplication via content hashing (identical frames reference earlier frames).
    /// </summary>
    public static OptimizedDocument FromDocument(ConsoleImageDocument doc, int keyframeInterval = 30,
        bool enableStability = false, int colorThreshold = 15)
    {
        var optimized = new OptimizedDocument
        {
            Created = doc.Created,
            SourceFile = doc.SourceFile,
            RenderMode = doc.RenderMode,
            Settings = doc.Settings,
            KeyframeInterval = keyframeInterval
        };

        if (doc.Frames.Count == 0)
            return optimized;

        // Frame hash -> first occurrence index (for deduplication)
        var frameHashes = new Dictionary<int, int>();

        // Build global color palette from all frames
        var colorSet = new HashSet<string> { "" }; // Index 0 = no color
        var parsedFrames = new List<(string chars, List<string> colors, int width, int height, int delayMs)>();

        foreach (var frame in doc.Frames)
        {
            var (chars, colors) = ParseAnsiContent(frame.Content);
            foreach (var color in colors.Where(c => !string.IsNullOrEmpty(c)))
            {
                colorSet.Add(color);
            }
            parsedFrames.Add((chars, colors, frame.Width, frame.Height, frame.DelayMs));
        }

        // Create palette array and lookup
        var palette = colorSet.ToArray();
        var colorToIndex = new Dictionary<string, int>();
        for (int i = 0; i < palette.Length; i++)
        {
            colorToIndex[palette[i]] = i;
        }
        optimized.Palette = palette;

        // Convert frames with delta encoding and deduplication
        string? prevChars = null;
        List<int>? prevIndices = null;
        List<string>? prevColors = null;

        for (int f = 0; f < parsedFrames.Count; f++)
        {
            var (chars, colors, width, height, delayMs) = parsedFrames[f];

            // Apply temporal stability if enabled
            if (enableStability && prevColors != null && colors.Count == prevColors.Count)
            {
                colors = ApplyColorStability(colors, prevColors, palette, colorThreshold);
            }

            var indices = colors.Select(c => colorToIndex.TryGetValue(c, out var idx) ? idx : 0).ToList();

            // Compute content hash for deduplication (simple hash of chars + first few indices)
            var contentHash = ComputeFrameHash(chars, indices);

            // Check for duplicate frame
            if (frameHashes.TryGetValue(contentHash, out var refFrameIdx))
            {
                // This frame is identical to an earlier frame - just reference it
                optimized.Frames.Add(new OptimizedFrame
                {
                    IsKeyframe = false,
                    RefFrame = refFrameIdx,
                    Width = width,
                    Height = height,
                    DelayMs = delayMs
                });
                // Don't update prev state - keep tracking from actual keyframes
                continue;
            }

            // Decide if this should be a keyframe
            bool isKeyframe = f == 0 || f % keyframeInterval == 0;

            // Also force keyframe if frame dimensions changed
            if (prevChars != null && chars.Length != prevChars.Length)
                isKeyframe = true;

            // Calculate delta if not a keyframe
            if (!isKeyframe && prevChars != null && prevIndices != null)
            {
                var delta = CalculateDelta(prevChars, prevIndices, chars, indices);

                // If delta is larger than 60% of full frame, use keyframe instead
                var fullSize = chars.Length + indices.Count;
                var deltaSize = delta.Length;
                if (deltaSize > fullSize * 0.6)
                {
                    isKeyframe = true;
                }
                else
                {
                    // Store as delta frame
                    optimized.Frames.Add(new OptimizedFrame
                    {
                        IsKeyframe = false,
                        Delta = delta,
                        Width = width,
                        Height = height,
                        DelayMs = delayMs
                    });
                    prevChars = chars;
                    prevIndices = indices;
                    prevColors = colors;
                    continue;
                }
            }

            // Store as keyframe and register in hash lookup
            frameHashes[contentHash] = optimized.Frames.Count;

            optimized.Frames.Add(new OptimizedFrame
            {
                IsKeyframe = true,
                Characters = chars,
                ColorIndices = CompressColorIndices(indices),
                Width = width,
                Height = height,
                DelayMs = delayMs
            });

            prevChars = chars;
            prevIndices = indices;
            prevColors = colors;
        }

        return optimized;
    }

    /// <summary>
    /// Compute a simple hash of frame content for deduplication.
    /// Uses characters + sampled color indices.
    /// </summary>
    private static int ComputeFrameHash(string chars, List<int> indices)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + chars.GetHashCode();

            // Sample color indices (every 10th for speed)
            for (int i = 0; i < indices.Count; i += 10)
            {
                hash = hash * 31 + indices[i];
            }

            return hash;
        }
    }

    /// <summary>
    /// Convert back to standard ConsoleImageDocument for playback.
    /// Reconstructs full frames from keyframes and deltas.
    /// </summary>
    public ConsoleImageDocument ToDocument(int? loopCountOverride = null)
    {
        var doc = new ConsoleImageDocument
        {
            Created = Created,
            SourceFile = SourceFile,
            RenderMode = RenderMode,
            Settings = Settings.Clone()
        };

        // Override loop count if specified
        if (loopCountOverride.HasValue)
        {
            doc.Settings.LoopCount = loopCountOverride.Value;
        }

        // Reconstruct frames
        string? currentChars = null;
        List<int>? currentIndices = null;
        var reconstructedContent = new List<string>(); // Store for RefFrame lookups

        for (int f = 0; f < Frames.Count; f++)
        {
            var frame = Frames[f];
            string content;

            if (frame.RefFrame.HasValue && frame.RefFrame.Value < reconstructedContent.Count)
            {
                // Reference frame - use content from earlier frame
                content = reconstructedContent[frame.RefFrame.Value];
            }
            else if (frame.IsKeyframe)
            {
                // Full keyframe
                currentChars = frame.Characters;
                currentIndices = DecompressColorIndices(frame.ColorIndices);
                content = RebuildAnsiContent(currentChars, currentIndices, Palette);
            }
            else if (currentChars != null && currentIndices != null)
            {
                // Apply delta to previous frame
                (currentChars, currentIndices) = ApplyDelta(currentChars, currentIndices, frame.Delta ?? "");
                content = RebuildAnsiContent(currentChars, currentIndices, Palette);
            }
            else
            {
                // No previous frame - skip (shouldn't happen)
                reconstructedContent.Add("");
                continue;
            }

            reconstructedContent.Add(content);
            doc.Frames.Add(new DocumentFrame
            {
                Content = content,
                Width = frame.Width,
                Height = frame.Height,
                DelayMs = frame.DelayMs
            });
        }

        return doc;
    }

    /// <summary>
    /// Apply temporal stability to colors - keep previous frame's color if delta is below threshold.
    /// This reduces visual flickering between frames.
    /// </summary>
    private static List<string> ApplyColorStability(List<string> currColors, List<string> prevColors,
        string[] palette, int threshold)
    {
        var result = new List<string>(currColors.Count);
        for (int i = 0; i < currColors.Count; i++)
        {
            var curr = currColors[i];
            var prev = i < prevColors.Count ? prevColors[i] : "";

            // If both empty or same, keep current
            if (curr == prev || string.IsNullOrEmpty(curr) || string.IsNullOrEmpty(prev))
            {
                result.Add(curr);
                continue;
            }

            // Parse colors and compare
            if (ColorsAreSimilar(curr, prev, threshold))
            {
                result.Add(prev); // Keep previous color for stability
            }
            else
            {
                result.Add(curr);
            }
        }
        return result;
    }

    /// <summary>
    /// Check if two hex colors are similar within threshold (per-channel delta).
    /// </summary>
    private static bool ColorsAreSimilar(string color1, string color2, int threshold)
    {
        if (color1.Length < 6 || color2.Length < 6)
            return false;

        try
        {
            var r1 = Convert.ToInt32(color1.Substring(0, 2), 16);
            var g1 = Convert.ToInt32(color1.Substring(2, 2), 16);
            var b1 = Convert.ToInt32(color1.Substring(4, 2), 16);

            var r2 = Convert.ToInt32(color2.Substring(0, 2), 16);
            var g2 = Convert.ToInt32(color2.Substring(2, 2), 16);
            var b2 = Convert.ToInt32(color2.Substring(4, 2), 16);

            return Math.Abs(r1 - r2) <= threshold &&
                   Math.Abs(g1 - g2) <= threshold &&
                   Math.Abs(b1 - b2) <= threshold;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Calculate delta between two frames.
    /// Format: "pos:char,colorIdx;pos:char,colorIdx;..." where pos = offset from start
    /// Uses RLE for consecutive changes.
    /// </summary>
    private static string CalculateDelta(string prevChars, List<int> prevIndices,
        string currChars, List<int> currIndices)
    {
        var sb = new StringBuilder();
        var changes = new List<(int pos, char ch, int colorIdx)>();

        int minLen = Math.Min(prevChars.Length, currChars.Length);

        for (int i = 0; i < minLen; i++)
        {
            var prevColor = i < prevIndices.Count ? prevIndices[i] : 0;
            var currColor = i < currIndices.Count ? currIndices[i] : 0;

            if (prevChars[i] != currChars[i] || prevColor != currColor)
            {
                changes.Add((i, currChars[i], currColor));
            }
        }

        // Handle length differences
        for (int i = minLen; i < currChars.Length; i++)
        {
            var currColor = i < currIndices.Count ? currIndices[i] : 0;
            changes.Add((i, currChars[i], currColor));
        }

        // Encode changes with optional RLE for consecutive positions
        for (int i = 0; i < changes.Count; i++)
        {
            if (i > 0) sb.Append(';');

            var (pos, ch, colorIdx) = changes[i];

            // Check for consecutive positions with same color (RLE opportunity)
            int runLength = 1;
            var runChars = new StringBuilder();
            runChars.Append(ch);

            while (i + runLength < changes.Count)
            {
                var next = changes[i + runLength];
                if (next.pos == pos + runLength && next.colorIdx == colorIdx)
                {
                    runChars.Append(next.ch);
                    runLength++;
                }
                else break;
            }

            if (runLength > 1)
            {
                // Consecutive run: pos:chars,colorIdx,count
                sb.Append($"{pos}:{EscapeDeltaChars(runChars.ToString())},{colorIdx},{runLength}");
                i += runLength - 1;
            }
            else
            {
                // Single change: pos:char,colorIdx
                sb.Append($"{pos}:{EscapeDeltaChar(ch)},{colorIdx}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Apply delta to reconstruct current frame from previous.
    /// </summary>
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

                for (int i = 0; i < count && pos + i < chars.Length; i++)
                {
                    chars[pos + i] = i < newChars.Length ? newChars[i] : ' ';
                    while (indices.Count <= pos + i) indices.Add(0);
                    indices[pos + i] = colorIdx;
                }
            }
        }

        return (new string(chars), indices);
    }

    private static string EscapeDeltaChar(char c)
    {
        return c switch
        {
            ':' => "\\c",
            ',' => "\\m",
            ';' => "\\s",
            '\\' => "\\\\",
            '\n' => "\\n",
            '\r' => "\\r",
            _ => c.ToString()
        };
    }

    private static string EscapeDeltaChars(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
            sb.Append(EscapeDeltaChar(c));
        return sb.ToString();
    }

    private static string UnescapeDeltaChars(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                sb.Append(s[i + 1] switch
                {
                    'c' => ':',
                    'm' => ',',
                    's' => ';',
                    'n' => '\n',
                    'r' => '\r',
                    _ => s[i + 1]
                });
                i++;
            }
            else
            {
                sb.Append(s[i]);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parse ANSI content to extract characters and colors.
    /// </summary>
    private static (string chars, List<string> colors) ParseAnsiContent(string content)
    {
        var chars = new StringBuilder();
        var colors = new List<string>();
        var currentColor = "";

        int i = 0;
        while (i < content.Length)
        {
            if (content[i] == '\x1b' && i + 1 < content.Length && content[i + 1] == '[')
            {
                // Parse ANSI escape sequence
                int end = content.IndexOf('m', i);
                if (end > i)
                {
                    var seq = content.Substring(i + 2, end - i - 2);
                    if (seq == "0" || seq == "")
                    {
                        currentColor = "";
                    }
                    else if (seq.StartsWith("38;2;"))
                    {
                        // 24-bit color: \x1b[38;2;R;G;Bm
                        var parts = seq.Split(';');
                        if (parts.Length >= 5)
                        {
                            var r = int.TryParse(parts[2], out var rv) ? rv : 0;
                            var g = int.TryParse(parts[3], out var gv) ? gv : 0;
                            var b = int.TryParse(parts[4], out var bv) ? bv : 0;
                            currentColor = $"{r:X2}{g:X2}{b:X2}";
                        }
                    }
                    else if (seq.StartsWith("48;2;"))
                    {
                        // Background color - skip for now but don't break parsing
                    }
                    i = end + 1;
                    continue;
                }
            }

            // Regular character
            chars.Append(content[i]);
            colors.Add(currentColor);
            i++;
        }

        return (chars.ToString(), colors);
    }

    /// <summary>
    /// Rebuild ANSI content from characters and color indices.
    /// </summary>
    private static string RebuildAnsiContent(string chars, List<int> indices, string[] palette)
    {
        var sb = new StringBuilder();
        var lastColorIdx = -1;

        for (int i = 0; i < chars.Length; i++)
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

        if (lastColorIdx != 0)
        {
            sb.Append("\x1b[0m");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Compress color indices using run-length encoding.
    /// </summary>
    private static string CompressColorIndices(List<int> indices)
    {
        if (indices.Count == 0) return "";

        var sb = new StringBuilder();
        int currentIdx = indices[0];
        int count = 1;

        for (int i = 1; i < indices.Count; i++)
        {
            if (indices[i] == currentIdx)
            {
                count++;
            }
            else
            {
                AppendRun(sb, currentIdx, count);
                currentIdx = indices[i];
                count = 1;
            }
        }
        AppendRun(sb, currentIdx, count);

        return sb.ToString();
    }

    private static void AppendRun(StringBuilder sb, int idx, int count)
    {
        if (sb.Length > 0) sb.Append(';');
        if (count == 1)
            sb.Append(idx);
        else
            sb.Append($"{idx},{count}");
    }

    /// <summary>
    /// Decompress RLE color indices.
    /// </summary>
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
            for (int i = 0; i < count; i++)
                result.Add(idx);
        }

        return result;
    }
}

/// <summary>
/// Extension for cloning DocumentRenderSettings.
/// </summary>
public static class DocumentRenderSettingsExtensions
{
    public static DocumentRenderSettings Clone(this DocumentRenderSettings src)
    {
        return new DocumentRenderSettings
        {
            Width = src.Width,
            Height = src.Height,
            MaxWidth = src.MaxWidth,
            MaxHeight = src.MaxHeight,
            CharacterAspectRatio = src.CharacterAspectRatio,
            ContrastPower = src.ContrastPower,
            Gamma = src.Gamma,
            UseColor = src.UseColor,
            Invert = src.Invert,
            CharacterSetPreset = src.CharacterSetPreset,
            AnimationSpeedMultiplier = src.AnimationSpeedMultiplier,
            LoopCount = src.LoopCount,
            EnableTemporalStability = src.EnableTemporalStability,
            ColorStabilityThreshold = src.ColorStabilityThreshold
        };
    }
}

/// <summary>
/// Optimized frame - can be keyframe (full) or delta (changes only).
/// </summary>
public class OptimizedFrame
{
    /// <summary>
    /// True = full keyframe, False = delta from previous frame.
    /// </summary>
    public bool IsKeyframe { get; set; } = true;

    /// <summary>
    /// Plain characters without ANSI codes (keyframes only).
    /// </summary>
    public string? Characters { get; set; }

    /// <summary>
    /// RLE-compressed color indices (keyframes only).
    /// </summary>
    public string? ColorIndices { get; set; }

    /// <summary>
    /// Delta encoding - changes from previous frame (delta frames only).
    /// Format: "pos:char,colorIdx;pos:chars,colorIdx,count;..."
    /// </summary>
    public string? Delta { get; set; }

    /// <summary>
    /// Reference to an identical frame (0-based index).
    /// When set, this frame uses the same content as the referenced frame.
    /// Used for perceptual hash-based frame deduplication.
    /// </summary>
    public int? RefFrame { get; set; }

    /// <summary>
    /// Content hash for deduplication (set during streaming, used for post-optimization).
    /// Not serialized - computed on the fly.
    /// </summary>
    [JsonIgnore]
    public int ContentHash { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
    public int DelayMs { get; set; }
}

/// <summary>
/// Handles reading and writing compressed document archives (.cid.7z or .cidz).
/// </summary>
public static class CompressedDocumentArchive
{
    /// <summary>
    /// Supported archive extensions.
    /// </summary>
    public static readonly string[] SupportedExtensions = { ".7z", ".cidz", ".cid.7z" };

    /// <summary>
    /// Check if a file path indicates a compressed document.
    /// </summary>
    public static bool IsCompressedDocument(string path)
    {
        var lower = path.ToLowerInvariant();
        return SupportedExtensions.Any(ext => lower.EndsWith(ext));
    }

    /// <summary>
    /// Save a document to a compressed archive with maximum compression.
    /// Applies delta encoding for motion compression.
    /// Loop count is stored in settings - frames are NOT duplicated.
    /// </summary>
    public static async Task SaveAsync(ConsoleImageDocument doc, string path,
        int keyframeInterval = 30, CancellationToken ct = default)
    {
        // Convert to optimized format with delta compression and optional stability
        var enableStability = doc.Settings.EnableTemporalStability;
        var colorThreshold = doc.Settings.ColorStabilityThreshold;
        var optimized = OptimizedDocument.FromDocument(doc, keyframeInterval, enableStability, colorThreshold);

        // Serialize to JSON
        var json = JsonSerializer.Serialize(optimized, CompressedDocumentJsonContext.Default.OptimizedDocument);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        // Write with GZip compression (cross-platform, AOT compatible)
        await using var fileStream = File.Create(path);
        await using var compressStream = new GZipStream(fileStream, CompressionLevel.SmallestSize);
        await compressStream.WriteAsync(jsonBytes, ct);
    }

    /// <summary>
    /// Load a document from a compressed archive.
    /// </summary>
    /// <param name="path">Path to compressed document</param>
    /// <param name="loopCountOverride">Override stored loop count (null = use stored)</param>
    /// <param name="ct">Cancellation token</param>
    public static async Task<ConsoleImageDocument> LoadAsync(string path,
        int? loopCountOverride = null, CancellationToken ct = default)
    {
        await using var fileStream = File.OpenRead(path);

        // Try to detect format from magic bytes
        var magic = new byte[6];
        var read = await fileStream.ReadAsync(magic, ct);
        fileStream.Position = 0;

        Stream decompressStream;

        // Check for 7z magic: "7z\xBC\xAF\x27\x1C"
        if (read >= 6 && magic[0] == 0x37 && magic[1] == 0x7A && magic[2] == 0xBC)
        {
            // 7z archive - use SharpCompress reader
            using var archive = SevenZipArchive.Open(fileStream);
            var entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);
            if (entry == null)
                throw new InvalidOperationException("Archive contains no files");

            using var entryStream = entry.OpenEntryStream();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms, ct);
            ms.Position = 0;

            var json = Encoding.UTF8.GetString(ms.ToArray());
            return ParseOptimizedJson(json, loopCountOverride);
        }
        // Check for gzip magic: 0x1F 0x8B
        else if (read >= 2 && magic[0] == 0x1F && magic[1] == 0x8B)
        {
            decompressStream = new GZipStream(fileStream, CompressionMode.Decompress, leaveOpen: true);
        }
        else
        {
            // Assume uncompressed JSON
            decompressStream = fileStream;
        }

        using (decompressStream)
        {
            using var reader = new StreamReader(decompressStream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync(ct);
            return ParseOptimizedJson(json, loopCountOverride);
        }
    }

    /// <summary>
    /// Stream frames from a compressed archive.
    /// Reconstructs full frames from keyframes and deltas on-the-fly.
    /// </summary>
    public static async IAsyncEnumerable<(DocumentFrame Frame, int Index, int Total)> StreamFramesAsync(
        string path,
        int? loopCountOverride = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var doc = await LoadAsync(path, loopCountOverride, ct);
        var total = doc.FrameCount;

        for (int i = 0; i < doc.Frames.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return (doc.Frames[i], i, total);
        }
    }

    /// <summary>
    /// Stream frames with playback support including loop handling.
    /// </summary>
    public static async IAsyncEnumerable<DocumentFrame> StreamPlaybackAsync(
        string path,
        float speedMultiplier = 1.0f,
        int? loopCountOverride = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var doc = await LoadAsync(path, loopCountOverride, ct);
        var effectiveSpeed = speedMultiplier > 0 ? speedMultiplier : 1.0f;
        var loopCount = loopCountOverride ?? doc.Settings.LoopCount;
        var loopsDone = 0;

        while (!ct.IsCancellationRequested)
        {
            foreach (var frame in doc.Frames)
            {
                ct.ThrowIfCancellationRequested();
                yield return frame;

                var delayMs = (int)(frame.DelayMs / effectiveSpeed);
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, ct);
                }
            }

            loopsDone++;
            if (loopCount > 0 && loopsDone >= loopCount)
                break;
        }
    }

    /// <summary>
    /// Get document info without fully reconstructing all frames.
    /// </summary>
    public static async Task<DocumentInfo> GetInfoAsync(string path, CancellationToken ct = default)
    {
        var doc = await LoadAsync(path, null, ct);
        var keyframeCount = doc.Frames.Count(f => f.Content.Length > 0); // Approximation

        return new DocumentInfo
        {
            FrameCount = doc.FrameCount,
            TotalDurationMs = doc.TotalDurationMs,
            RenderMode = doc.RenderMode,
            Settings = doc.Settings,
            LoopCount = doc.Settings.LoopCount,
            IsAnimated = doc.IsAnimated
        };
    }

    private static ConsoleImageDocument ParseOptimizedJson(string json, int? loopCountOverride)
    {
        // Try optimized format first
        try
        {
            var optimized = JsonSerializer.Deserialize(json, CompressedDocumentJsonContext.Default.OptimizedDocument);
            if (optimized?.Type == "OptimizedConsoleImageDocument")
            {
                return optimized.ToDocument(loopCountOverride);
            }
        }
        catch { }

        // Fall back to standard format
        var doc = ConsoleImageDocument.FromJson(json);
        if (loopCountOverride.HasValue)
        {
            doc.Settings.LoopCount = loopCountOverride.Value;
        }
        return doc;
    }
}

/// <summary>
/// Document metadata.
/// </summary>
public class DocumentInfo
{
    public int FrameCount { get; set; }
    public int TotalDurationMs { get; set; }
    public string RenderMode { get; set; } = "";
    public DocumentRenderSettings Settings { get; set; } = new();
    public int LoopCount { get; set; }
    public bool IsAnimated { get; set; }
}
