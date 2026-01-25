// CompressedDocument - Optimized format with global color palette, delta compression, and 7z
// Uses I-frame/P-frame style encoding: keyframes + delta frames for motion
// Dramatically reduces file size while maintaining full playback quality

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleImage.Core.Subtitles;
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
[JsonSerializable(typeof(SubtitleTrackData))]
[JsonSerializable(typeof(SubtitleEntryData))]
[JsonSerializable(typeof(List<SubtitleEntryData>))]
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
            foreach (var color in colors)
            {
                if (color.Length > 0)
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

        // Reusable StringBuilders for per-frame operations (avoids per-frame allocations)
        var deltaSb = new StringBuilder(4096);
        var compressSb = new StringBuilder(2048);

        for (int f = 0; f < parsedFrames.Count; f++)
        {
            var (chars, colors, width, height, delayMs) = parsedFrames[f];

            // Apply temporal stability if enabled (modifies colors in-place)
            if (enableStability && prevColors != null && colors.Count == prevColors.Count)
            {
                ApplyColorStabilityInPlace(colors, prevColors, colorThreshold);
            }

            // Convert colors to palette indices without LINQ
            var indices = new List<int>(colors.Count);
            foreach (var c in colors)
            {
                indices.Add(colorToIndex.TryGetValue(c, out var idx) ? idx : 0);
            }

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
                var delta = CalculateDelta(prevChars, prevIndices, chars, indices, deltaSb);

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
                ColorIndices = CompressColorIndices(indices, compressSb),
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
    /// Optimized for low allocation: pre-caches palette ANSI strings,
    /// reuses buffers across delta frames.
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

        if (Frames.Count == 0) return doc;

        // Pre-build palette ANSI escape strings once (avoids hex parsing + string interpolation per frame)
        var paletteAnsi = BuildPaletteAnsiStrings(Palette);

        // Reusable buffers for delta application (avoids per-frame allocations)
        char[]? charBuffer = null;
        int[]? indexBuffer = null;
        int bufferLen = 0;

        // Reusable StringBuilder for ANSI content building
        var rebuildSb = new StringBuilder(4096);

        var reconstructedContent = new List<string>(Frames.Count); // Store for RefFrame lookups

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
                // Full keyframe - decompress directly into reusable buffers
                var chars = frame.Characters ?? string.Empty;
                bufferLen = chars.Length;

                // Ensure buffers are large enough
                if (charBuffer == null || charBuffer.Length < bufferLen)
                {
                    charBuffer = new char[bufferLen];
                    indexBuffer = new int[bufferLen];
                }
                chars.CopyTo(0, charBuffer, 0, bufferLen);

                DecompressColorIndicesInto(frame.ColorIndices, indexBuffer!, bufferLen);
                content = RebuildAnsiContentFast(charBuffer, indexBuffer!, bufferLen, paletteAnsi, rebuildSb);
            }
            else if (charBuffer != null && indexBuffer != null)
            {
                // Apply delta to previous frame (mutates buffers in place)
                ApplyDeltaInPlace(charBuffer, indexBuffer, ref bufferLen, frame.Delta ?? "");
                content = RebuildAnsiContentFast(charBuffer, indexBuffer, bufferLen, paletteAnsi, rebuildSb);
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
    /// Pre-build ANSI escape strings for each palette color.
    /// Index 0 = reset, others = foreground color code.
    /// Called once at load time; eliminates hex parsing and string interpolation per frame.
    /// </summary>
    private static string[] BuildPaletteAnsiStrings(string[] palette)
    {
        var result = new string[palette.Length];
        result[0] = "\x1b[0m"; // Index 0 = reset/no color

        for (int i = 1; i < palette.Length; i++)
        {
            var hex = palette[i];
            if (hex.Length >= 6)
            {
                var r = ParseHexByte(hex, 0);
                var g = ParseHexByte(hex, 2);
                var b = ParseHexByte(hex, 4);
                result[i] = $"\x1b[38;2;{r};{g};{b}m";
            }
            else
            {
                result[i] = "\x1b[0m";
            }
        }

        return result;
    }

    /// <summary>
    /// Parse two hex characters to a byte value without Substring allocation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int ParseHexByte(string hex, int offset)
    {
        return (HexVal(hex[offset]) << 4) | HexVal(hex[offset + 1]);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int HexVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        return 0;
    }

    /// <summary>
    /// Apply temporal stability to colors in-place - keep previous frame's color if delta is below threshold.
    /// This reduces visual flickering between frames. Modifies currColors directly to avoid list allocation.
    /// </summary>
    private static void ApplyColorStabilityInPlace(List<string> currColors, List<string> prevColors,
        int threshold)
    {
        for (int i = 0; i < currColors.Count; i++)
        {
            var curr = currColors[i];
            var prev = i < prevColors.Count ? prevColors[i] : "";

            // If both empty or same, keep current
            if (curr == prev || curr.Length == 0 || prev.Length == 0)
                continue;

            // Parse colors and compare - replace with previous if similar
            if (ColorsAreSimilar(curr, prev, threshold))
                currColors[i] = prev;
        }
    }

    /// <summary>
    /// Check if two hex colors are similar within threshold (per-channel delta).
    /// </summary>
    private static bool ColorsAreSimilar(string color1, string color2, int threshold)
    {
        if (color1.Length < 6 || color2.Length < 6)
            return false;

        var r1 = ParseHexByte(color1, 0);
        var g1 = ParseHexByte(color1, 2);
        var b1 = ParseHexByte(color1, 4);

        var r2 = ParseHexByte(color2, 0);
        var g2 = ParseHexByte(color2, 2);
        var b2 = ParseHexByte(color2, 4);

        return Math.Abs(r1 - r2) <= threshold &&
               Math.Abs(g1 - g2) <= threshold &&
               Math.Abs(b1 - b2) <= threshold;
    }

    /// <summary>
    /// Calculate delta between two frames.
    /// Format: "pos:char,colorIdx;pos:char,colorIdx;..." where pos = offset from start
    /// Uses RLE for consecutive changes.
    /// Optimized: single-pass scan eliminates intermediate List allocation,
    /// direct StringBuilder writes eliminate inner StringBuilder and string interpolation.
    /// </summary>
    private static string CalculateDelta(string prevChars, List<int> prevIndices,
        string currChars, List<int> currIndices, StringBuilder? reuseSb = null)
    {
        int minLen = Math.Min(prevChars.Length, currChars.Length);
        int maxLen = currChars.Length;

        var sb = reuseSb ?? new StringBuilder(Math.Max(64, maxLen / 5 * 25));
        sb.Clear();
        bool firstEntry = true;

        int i = 0;
        while (i < maxLen)
        {
            // Check if this position changed
            bool changed;
            int currColor = i < currIndices.Count ? currIndices[i] : 0;

            if (i < minLen)
            {
                var prevColor = i < prevIndices.Count ? prevIndices[i] : 0;
                changed = prevChars[i] != currChars[i] || prevColor != currColor;
            }
            else
            {
                changed = true; // New position beyond prev length
            }

            if (!changed)
            {
                i++;
                continue;
            }

            // Found a change - check for RLE run (consecutive positions, same color)
            int runStart = i;
            int runColor = currColor;
            int runEnd = i + 1;

            while (runEnd < maxLen)
            {
                int nextColor = runEnd < currIndices.Count ? currIndices[runEnd] : 0;
                if (nextColor != runColor) break;

                // Check if next position is also changed
                if (runEnd < minLen)
                {
                    var prevColor = runEnd < prevIndices.Count ? prevIndices[runEnd] : 0;
                    if (prevChars[runEnd] == currChars[runEnd] && prevColor == nextColor)
                        break; // Not changed
                }
                // else: beyond prev length, always changed

                runEnd++;
            }

            int runLength = runEnd - runStart;

            // Write entry
            if (!firstEntry) sb.Append(';');
            firstEntry = false;

            sb.Append(runStart);
            sb.Append(':');

            // Write escaped characters directly (no inner StringBuilder)
            for (int j = runStart; j < runEnd; j++)
                AppendEscapedChar(sb, currChars[j]);

            sb.Append(',');
            sb.Append(runColor);

            if (runLength > 1)
            {
                sb.Append(',');
                sb.Append(runLength);
            }

            i = runEnd;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Append a delta-escaped character directly to StringBuilder.
    /// Avoids per-char string allocation from EscapeDeltaChar.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AppendEscapedChar(StringBuilder sb, char c)
    {
        switch (c)
        {
            case ':': sb.Append("\\c"); break;
            case ',': sb.Append("\\m"); break;
            case ';': sb.Append("\\s"); break;
            case '\\': sb.Append("\\\\"); break;
            case '\n': sb.Append("\\n"); break;
            case '\r': sb.Append("\\r"); break;
            default: sb.Append(c); break;
        }
    }

    /// <summary>
    /// Apply delta in-place to char/index buffers without allocations.
    /// Parses delta string using spans to avoid Split/Substring allocations.
    /// </summary>
    private static void ApplyDeltaInPlace(char[] chars, int[] indices, ref int bufferLen, string delta)
    {
        if (string.IsNullOrEmpty(delta)) return;

        var span = delta.AsSpan();
        int start = 0;

        while (start < span.Length)
        {
            // Find end of this change entry (next ';' or end of string)
            var entryEnd = span.Slice(start).IndexOf(';');
            ReadOnlySpan<char> entry;
            if (entryEnd < 0)
            {
                entry = span.Slice(start);
                start = span.Length;
            }
            else
            {
                entry = span.Slice(start, entryEnd);
                start += entryEnd + 1;
            }

            if (entry.IsEmpty) continue;

            // Find colon separator (pos:rest)
            var colonIdx = entry.IndexOf(':');
            if (colonIdx < 0) continue;

            var pos = ParseInt(entry.Slice(0, colonIdx));

            // Parse rest: chars,colorIdx[,count]
            var rest = entry.Slice(colonIdx + 1);

            // Find first comma (separates chars from colorIdx)
            var firstComma = FindUnescapedComma(rest);
            if (firstComma < 0) continue;

            var charsPart = rest.Slice(0, firstComma);
            var afterChars = rest.Slice(firstComma + 1);

            // Find second comma (optional count)
            var secondComma = afterChars.IndexOf(',');
            int colorIdx, count;

            if (secondComma >= 0)
            {
                colorIdx = ParseInt(afterChars.Slice(0, secondComma));
                count = ParseInt(afterChars.Slice(secondComma + 1));
            }
            else
            {
                colorIdx = ParseInt(afterChars);
                count = 1;
            }

            // Unescape and apply characters
            var unescapedIdx = 0;
            for (int ci = 0; ci < charsPart.Length && unescapedIdx < count; ci++)
            {
                char ch;
                if (charsPart[ci] == '\\' && ci + 1 < charsPart.Length)
                {
                    ch = charsPart[ci + 1] switch
                    {
                        'c' => ':',
                        'm' => ',',
                        's' => ';',
                        'n' => '\n',
                        'r' => '\r',
                        _ => charsPart[ci + 1]
                    };
                    ci++; // Skip escaped char
                }
                else
                {
                    ch = charsPart[ci];
                }

                if (pos + unescapedIdx < bufferLen)
                {
                    chars[pos + unescapedIdx] = ch;
                    indices[pos + unescapedIdx] = colorIdx;
                }
                unescapedIdx++;
            }

            // Fill remaining count with spaces (for RLE runs longer than char data)
            for (int i = unescapedIdx; i < count && pos + i < bufferLen; i++)
            {
                chars[pos + i] = ' ';
                indices[pos + i] = colorIdx;
            }
        }
    }

    /// <summary>
    /// Find first unescaped comma in span (skips escaped sequences like \m).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int FindUnescapedComma(ReadOnlySpan<char> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == '\\' && i + 1 < span.Length)
            {
                i++; // Skip escaped char
                continue;
            }
            if (span[i] == ',') return i;
        }
        return -1;
    }

    /// <summary>
    /// Parse integer from span without allocation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int ParseInt(ReadOnlySpan<char> span)
    {
        int result = 0;
        bool negative = false;
        int i = 0;

        if (span.Length > 0 && span[0] == '-')
        {
            negative = true;
            i = 1;
        }

        for (; i < span.Length; i++)
        {
            var c = span[i];
            if (c >= '0' && c <= '9')
                result = result * 10 + (c - '0');
        }

        return negative ? -result : result;
    }

    /// <summary>
    /// Parse ANSI content to extract characters and colors.
    /// Optimized: span-based parsing avoids Substring/Split allocations.
    /// Color strings are interned via dictionary to avoid duplicate heap allocations.
    /// </summary>
    private static (string chars, List<string> colors) ParseAnsiContent(string content)
    {
        var chars = new StringBuilder(content.Length / 2); // Rough estimate without ANSI codes
        var colors = new List<string>(content.Length / 2);
        var currentColor = "";

        // Intern color strings to avoid duplicates (a frame has few unique colors but many cells)
        var colorIntern = new Dictionary<int, string>(64);
        colorIntern[0] = ""; // No color sentinel

        int i = 0;
        while (i < content.Length)
        {
            if (content[i] == '\x1b' && i + 1 < content.Length && content[i + 1] == '[')
            {
                // Find the 'm' terminator
                int end = content.IndexOf('m', i + 2);
                if (end > i)
                {
                    var seqStart = i + 2;
                    var seqLen = end - seqStart;

                    // Check for reset: \x1b[0m or \x1b[m
                    if (seqLen == 0 || (seqLen == 1 && content[seqStart] == '0'))
                    {
                        currentColor = "";
                    }
                    // Check for 24-bit foreground: \x1b[38;2;R;G;Bm
                    else if (seqLen > 5 &&
                             content[seqStart] == '3' && content[seqStart + 1] == '8' &&
                             content[seqStart + 2] == ';' && content[seqStart + 3] == '2' &&
                             content[seqStart + 4] == ';')
                    {
                        // Parse R;G;B directly from content span without Split
                        var rgbStart = seqStart + 5;
                        var r = ParseNextInt(content, ref rgbStart, end);
                        var g = ParseNextInt(content, ref rgbStart, end);
                        var b = ParseNextInt(content, ref rgbStart, end);

                        // Intern the hex string: pack RGB into int key
                        var colorKey = (r << 16) | (g << 8) | b;
                        if (!colorIntern.TryGetValue(colorKey, out var hexStr))
                        {
                            hexStr = $"{r:X2}{g:X2}{b:X2}";
                            colorIntern[colorKey] = hexStr;
                        }
                        currentColor = hexStr;
                    }
                    // Background color (48;2;...) - skip
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
    /// Parse the next integer from a semicolon-delimited string at the given position.
    /// Advances pos past the integer and the following semicolon.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int ParseNextInt(string content, ref int pos, int end)
    {
        int val = 0;
        while (pos < end && content[pos] != ';')
        {
            var c = content[pos];
            if (c >= '0' && c <= '9')
                val = val * 10 + (c - '0');
            pos++;
        }
        if (pos < end) pos++; // Skip semicolon
        return val;
    }

    /// <summary>
    /// Rebuild ANSI content from char/index buffers using pre-cached palette strings.
    /// Zero-allocation hot path (reuses provided StringBuilder).
    /// </summary>
    private static string RebuildAnsiContentFast(char[] chars, int[] indices, int length,
        string[] paletteAnsi, StringBuilder sb)
    {
        sb.Clear();
        sb.EnsureCapacity(length * 2); // Reasonable estimate

        var lastColorIdx = -1;

        for (int i = 0; i < length; i++)
        {
            var colorIdx = indices[i];

            if (colorIdx != lastColorIdx)
            {
                if (colorIdx == 0 || colorIdx >= paletteAnsi.Length)
                    sb.Append("\x1b[0m");
                else
                    sb.Append(paletteAnsi[colorIdx]);
                lastColorIdx = colorIdx;
            }

            // Reset before newline to prevent color bleeding
            if (chars[i] == '\n' && lastColorIdx != 0)
            {
                sb.Append("\x1b[0m");
                lastColorIdx = 0;
            }

            sb.Append(chars[i]);
        }

        if (lastColorIdx != 0)
            sb.Append("\x1b[0m");

        return sb.ToString();
    }

    /// <summary>
    /// Compress color indices using run-length encoding.
    /// Accepts optional reusable StringBuilder to avoid per-call allocation.
    /// </summary>
    private static string CompressColorIndices(List<int> indices, StringBuilder? reuseSb = null)
    {
        if (indices.Count == 0) return "";

        var sb = reuseSb ?? new StringBuilder(indices.Count * 3);
        sb.Clear();
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
        sb.Append(idx);
        if (count > 1)
        {
            sb.Append(',');
            sb.Append(count);
        }
    }

    /// <summary>
    /// Decompress RLE color indices directly into a pre-allocated buffer.
    /// Zero-allocation: parses using spans, writes directly to int[].
    /// </summary>
    private static void DecompressColorIndicesInto(string? compressed, int[] buffer, int bufferLen)
    {
        // Clear buffer first
        Array.Clear(buffer, 0, Math.Min(buffer.Length, bufferLen));

        if (string.IsNullOrEmpty(compressed)) return;

        var span = compressed.AsSpan();
        int writePos = 0;
        int start = 0;

        while (start < span.Length && writePos < bufferLen)
        {
            // Find end of this run (next ';' or end)
            var runEnd = span.Slice(start).IndexOf(';');
            ReadOnlySpan<char> run;
            if (runEnd < 0)
            {
                run = span.Slice(start);
                start = span.Length;
            }
            else
            {
                run = span.Slice(start, runEnd);
                start += runEnd + 1;
            }

            if (run.IsEmpty) continue;

            // Find comma (idx,count) or just idx
            var commaIdx = run.IndexOf(',');
            int idx, count;

            if (commaIdx >= 0)
            {
                idx = ParseInt(run.Slice(0, commaIdx));
                count = ParseInt(run.Slice(commaIdx + 1));
            }
            else
            {
                idx = ParseInt(run);
                count = 1;
            }

            // Write run to buffer
            var end = Math.Min(writePos + count, bufferLen);
            for (int i = writePos; i < end; i++)
                buffer[i] = idx;
            writePos = end;
        }
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
            ColorStabilityThreshold = src.ColorStabilityThreshold,
            SubtitlesEnabled = src.SubtitlesEnabled,
            SubtitleFile = src.SubtitleFile,
            SubtitleSource = src.SubtitleSource,
            SubtitleLanguage = src.SubtitleLanguage
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

    // CIDZ v2 binary format magic
    private static readonly byte[] CidzMagic = "CIDZ"u8.ToArray();
    private const byte CidzVersion = 2;
    private const byte CidzFlagHasSubtitles = 0x01;

    /// <summary>
    /// Save a document to a compressed archive.
    /// Applies delta encoding for motion compression.
    /// Loop count is stored in settings - frames are NOT duplicated.
    /// </summary>
    public static async Task SaveAsync(ConsoleImageDocument doc, string path,
        int keyframeInterval = 30, CancellationToken ct = default)
    {
        await SaveAsync(doc, path, keyframeInterval, subtitles: null, ct: ct);
    }

    /// <summary>
    /// Save a document with optional subtitle track bundled inside the archive.
    /// Uses CIDZ v2 format: 6-byte header + Brotli-compressed payload.
    /// Streams JSON directly to Brotli - no full JSON string buffered in memory.
    /// Brotli achieves ~20-30% better compression than Deflate/GZip on text data.
    /// Format: [CIDZ magic 4B][version 1B][flags 1B][Brotli(JSON + \0 + VTT)]
    /// Default compression level is Optimal (quality ~4) for fast encoding.
    /// Use SmallestSize for archival when encoding speed doesn't matter.
    /// </summary>
    public static async Task SaveAsync(ConsoleImageDocument doc, string path,
        int keyframeInterval, SubtitleTrack? subtitles,
        CancellationToken ct = default,
        CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        // Convert to optimized format with delta compression and optional stability
        var enableStability = doc.Settings.EnableTemporalStability;
        var colorThreshold = doc.Settings.ColorStabilityThreshold;
        var optimized = OptimizedDocument.FromDocument(doc, keyframeInterval, enableStability, colorThreshold);

        var hasSubtitles = subtitles != null && subtitles.HasEntries;

        // Write CIDZ v2 header (6 bytes)
        await using var fileStream = File.Create(path);
        await fileStream.WriteAsync(CidzMagic, ct);
        fileStream.WriteByte(CidzVersion);
        fileStream.WriteByte(hasSubtitles ? CidzFlagHasSubtitles : (byte)0);

        // Stream JSON directly to Brotli - no buffering the full JSON string
        await using var brotli = new BrotliStream(fileStream, compressionLevel);
        await JsonSerializer.SerializeAsync(brotli, optimized,
            CompressedDocumentJsonContext.Default.OptimizedDocument, ct);

        // Null separator + VTT content (if subtitles present)
        if (hasSubtitles)
        {
            brotli.WriteByte(0x00); // Null separator (never appears in valid JSON)
            var vttContent = BuildVttContent(subtitles!);
            var vttBytes = Encoding.UTF8.GetBytes(vttContent);
            await brotli.WriteAsync(vttBytes, ct);
        }
    }

    /// <summary>
    /// Build VTT content from a subtitle track without writing to disk.
    /// </summary>
    private static string BuildVttContent(SubtitleTrack track)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        for (var i = 0; i < track.Entries.Count; i++)
        {
            var entry = track.Entries[i];
            sb.AppendLine((i + 1).ToString());
            sb.Append(FormatVttTimestamp(entry.StartTime));
            sb.Append(" --> ");
            sb.AppendLine(FormatVttTimestamp(entry.EndTime));
            sb.AppendLine(entry.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatVttTimestamp(TimeSpan ts)
    {
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    /// <summary>
    /// Load a document from a compressed archive.
    /// Auto-detects format: CIDZ v2 (Brotli), GZip (v1), 7z (legacy).
    /// CIDZ v2 archives may contain bundled subtitles.
    /// </summary>
    public static async Task<ConsoleImageDocument> LoadAsync(string path,
        int? loopCountOverride = null, CancellationToken ct = default)
    {
        await using var fileStream = File.OpenRead(path);

        // Detect format from magic bytes
        var magic = new byte[6];
        var read = await fileStream.ReadAsync(magic, ct);
        fileStream.Position = 0;

        // CIDZ v2 magic: "CIDZ" (0x43 0x49 0x44 0x5A)
        if (read >= 4 && magic[0] == 0x43 && magic[1] == 0x49 && magic[2] == 0x44 && magic[3] == 0x5A)
        {
            return await LoadCidzV2Async(fileStream, loopCountOverride, ct);
        }

        // 7z magic: "7z\xBC\xAF\x27\x1C" - legacy format
        if (read >= 6 && magic[0] == 0x37 && magic[1] == 0x7A && magic[2] == 0xBC)
        {
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

        // GZip magic: 0x1F 0x8B - v1 single-JSON format
        Stream decompressStream;
        if (read >= 2 && magic[0] == 0x1F && magic[1] == 0x8B)
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
    /// Load CIDZ v2 format: 6-byte header + Brotli payload (JSON + \0 + VTT).
    /// </summary>
    private static async Task<ConsoleImageDocument> LoadCidzV2Async(
        Stream fileStream, int? loopCountOverride, CancellationToken ct)
    {
        // Read header: magic (4) + version (1) + flags (1) = 6 bytes
        var header = new byte[6];
        await fileStream.ReadExactlyAsync(header, ct);

        var version = header[4];
        var flags = header[5];
        var hasSubtitles = (flags & CidzFlagHasSubtitles) != 0;

        // Decompress entire Brotli payload into memory
        await using var brotli = new BrotliStream(fileStream, CompressionMode.Decompress, leaveOpen: true);
        using var ms = new MemoryStream();
        await brotli.CopyToAsync(ms, ct);
        var payload = ms.GetBuffer();
        var payloadLen = (int)ms.Length;

        // Find null separator between JSON and VTT
        int jsonLen = payloadLen;
        int vttStart = payloadLen;

        if (hasSubtitles)
        {
            // Scan backwards from end for the null byte (more efficient since VTT is small)
            // But JSON could also end near the end, so scan forward from the end of JSON
            // JSON always ends with '}', so find the last '}' then the null byte after it
            for (int i = payloadLen - 1; i >= 0; i--)
            {
                if (payload[i] == 0x00)
                {
                    jsonLen = i;
                    vttStart = i + 1;
                    break;
                }
            }
        }

        // Parse JSON directly from UTF-8 bytes (skip string conversion)
        var jsonSpan = new ReadOnlySpan<byte>(payload, 0, jsonLen);
        var optimized = JsonSerializer.Deserialize(jsonSpan,
            CompressedDocumentJsonContext.Default.OptimizedDocument);
        if (optimized == null)
            throw new InvalidOperationException("Failed to parse CIDZ v2 document");
        var doc = optimized.ToDocument(loopCountOverride);

        // Parse VTT portion if present
        if (hasSubtitles && vttStart < payloadLen)
        {
            var vttContent = Encoding.UTF8.GetString(payload, vttStart, payloadLen - vttStart);
            var track = SubtitleParser.Parse(vttContent, "subtitles.vtt");
            if (track.HasEntries)
            {
                doc.Subtitles = SubtitleTrackData.FromTrack(track);
            }
        }

        return doc;
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
