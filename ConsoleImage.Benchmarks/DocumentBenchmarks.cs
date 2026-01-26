// Benchmarks for document save/load, CIDZ compression, subtitle rendering,
// status line rendering, and ANSI content parsing.

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using ConsoleImage.Core;
using ConsoleImage.Core.Subtitles;

namespace ConsoleImage.Benchmarks;

public static class DocumentBenchmarks
{
    public static void RunAll()
    {
        Console.WriteLine("=== Document & Compression Benchmarks ===\n");

        RunCidzRoundtripBenchmark();
        Console.WriteLine();
        RunSubtitleRendererBenchmark();
        Console.WriteLine();
        RunStatusLineBenchmark();
        Console.WriteLine();
        RunDocumentPlayerFrameBuffer();
    }

    private static void RunCidzRoundtripBenchmark()
    {
        Console.WriteLine("--- CIDZ Save/Load Roundtrip ---");

        // Create test document with animated frames
        var doc = CreateTestDocument(100, 80, 40);

        // Create test subtitle track
        var subtitleTrack = CreateTestSubtitles(100, 33);

        var tempOptimal = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}_optimal.cidz");
        var tempSmallest = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}_smallest.cidz");

        try
        {
            const int saveIterations = 5;

            // Benchmark save with Optimal compression (default)
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < saveIterations; i++)
                CompressedDocumentArchive.SaveAsync(doc, tempOptimal, 30, subtitleTrack,
                    compressionLevel: CompressionLevel.Optimal).GetAwaiter().GetResult();
            sw.Stop();
            var optimalSize = new FileInfo(tempOptimal).Length;
            Console.WriteLine(
                $"Save Optimal   ({doc.FrameCount}f): {sw.Elapsed.TotalMilliseconds / saveIterations:F1} ms/save, {optimalSize / 1024:N0} KB");

            // Benchmark save with SmallestSize compression
            sw.Restart();
            for (var i = 0; i < saveIterations; i++)
                CompressedDocumentArchive.SaveAsync(doc, tempSmallest, 30, subtitleTrack,
                    compressionLevel: CompressionLevel.SmallestSize).GetAwaiter().GetResult();
            sw.Stop();
            var smallestSize = new FileInfo(tempSmallest).Length;
            Console.WriteLine(
                $"Save Smallest  ({doc.FrameCount}f): {sw.Elapsed.TotalMilliseconds / saveIterations:F1} ms/save, {smallestSize / 1024:N0} KB");

            // Show size comparison
            var sizeDelta = (double)(optimalSize - smallestSize) / smallestSize * 100;
            Console.WriteLine($"Size delta: Optimal is {sizeDelta:+0.0;-0.0}% vs SmallestSize");

            // Benchmark load
            sw.Restart();
            const int loadIterations = 10;
            for (var i = 0; i < loadIterations; i++)
                CompressedDocumentArchive.LoadAsync(tempOptimal).GetAwaiter().GetResult();
            sw.Stop();
            Console.WriteLine($"Load ({doc.FrameCount}f): {sw.Elapsed.TotalMilliseconds / loadIterations:F1} ms/load");

            // Compression ratio (using Optimal as the default)
            var rawSize = EstimateRawSize(doc);
            Console.WriteLine(
                $"Compression ratio (Optimal): {rawSize / (double)optimalSize:F1}:1 ({rawSize / 1024:N0} KB raw -> {optimalSize / 1024:N0} KB)");
        }
        finally
        {
            if (File.Exists(tempOptimal)) File.Delete(tempOptimal);
            if (File.Exists(tempSmallest)) File.Delete(tempSmallest);
        }
    }

    private static void RunSubtitleRendererBenchmark()
    {
        Console.WriteLine("--- SubtitleRenderer ---");

        var renderer = new SubtitleRenderer(80);
        var entry = new SubtitleEntry
        {
            Text = "This is a test subtitle line that should be balanced across two lines for display",
            StartTime = TimeSpan.FromSeconds(1),
            EndTime = TimeSpan.FromSeconds(5)
        };

        // Warm up
        for (var i = 0; i < 100; i++)
        {
            renderer.RenderEntry(entry);
            renderer.RenderEntry(null);
            renderer.RenderAtPosition(entry, 40);
        }

        // Measure GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var gen0Before = GC.CollectionCount(0);

        const int iterations = 10000;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) renderer.RenderEntry(i % 3 == 0 ? null : entry);
        sw.Stop();
        var gen0After = GC.CollectionCount(0);
        Console.WriteLine(
            $"RenderEntry:      {sw.Elapsed.TotalMicroseconds / iterations:F2} µs/call, Gen0: {gen0After - gen0Before}");

        gen0Before = GC.CollectionCount(0);
        sw.Restart();
        for (var i = 0; i < iterations; i++) renderer.RenderAtPosition(i % 3 == 0 ? null : entry, 40);
        sw.Stop();
        gen0After = GC.CollectionCount(0);
        Console.WriteLine(
            $"RenderAtPosition: {sw.Elapsed.TotalMicroseconds / iterations:F2} µs/call, Gen0: {gen0After - gen0Before}");
    }

    private static void RunStatusLineBenchmark()
    {
        Console.WriteLine("--- StatusLine ---");

        var statusLine = new StatusLine(120);
        var info = new StatusLine.StatusInfo
        {
            FileName = "test-video-file-name.mp4",
            RenderMode = "Braille",
            CurrentTime = TimeSpan.FromSeconds(30),
            TotalDuration = TimeSpan.FromSeconds(120),
            CurrentFrame = 900,
            TotalFrames = 3600,
            ClipProgress = 0.25
        };

        // Warm up
        for (var i = 0; i < 100; i++)
            statusLine.Render(info);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var gen0Before = GC.CollectionCount(0);

        const int iterations = 50000;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            info.ClipProgress = (double)i / iterations;
            statusLine.Render(info);
        }

        sw.Stop();
        var gen0After = GC.CollectionCount(0);
        Console.WriteLine(
            $"Render: {sw.Elapsed.TotalMicroseconds / iterations:F2} µs/call, Gen0: {gen0After - gen0Before} over {iterations} calls");
    }

    private static void RunDocumentPlayerFrameBuffer()
    {
        Console.WriteLine("--- DocumentPlayer Frame Buffer ---");

        // Create two frames with slight differences (simulates animation)
        var frame1 = BuildTestAnsiFrame(80, 40, 42);
        var frame2 = BuildTestAnsiFrame(80, 40, 43); // Slightly different seed

        var sb = new StringBuilder(8192);
        const int iterations = 1000;

        // Warm up
        for (var i = 0; i < 50; i++) DiffRenderWithOffsets(sb, frame1, frame2);

        // Benchmark: offset-based O(N) single-pass diff
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) DiffRenderWithOffsets(sb, frame1, frame2);
        sw.Stop();
        Console.WriteLine($"Diff render (80x40): {sw.Elapsed.TotalMicroseconds / iterations:F1} µs/frame");
    }

    /// <summary>
    ///     Simulates DocumentPlayer.BuildFrameBuffer using offset-based line access.
    /// </summary>
    private static void DiffRenderWithOffsets(StringBuilder sb, string frame1, string frame2)
    {
        sb.Clear();
        sb.Append("\x1b[?2026h");

        Span<int> currStarts = stackalloc int[301];
        Span<int> prevStarts = stackalloc int[301];
        var currLineCount = BuildLineStarts(frame1, currStarts);
        var prevLineCount = BuildLineStarts(frame2, prevStarts);
        var maxLines = Math.Max(currLineCount, prevLineCount);
        var abandonThreshold = (int)(maxLines * 0.6) + 1;

        var diffStart = sb.Length;
        var changedLines = 0;

        for (var line = 0; line < maxLines; line++)
        {
            var curr = GetLineFromStarts(frame1, currStarts, currLineCount, line);
            var prev = GetLineFromStarts(frame2, prevStarts, prevLineCount, line);
            if (!curr.SequenceEqual(prev))
            {
                changedLines++;
                if (changedLines >= abandonThreshold)
                {
                    sb.Length = diffStart;
                    sb.Append("\x1b[H");
                    sb.Append(frame1);
                    break;
                }

                sb.Append("\x1b[");
                sb.Append(line + 1);
                sb.Append(";1H");
                sb.Append(curr);
                sb.Append("\x1b[0m");
            }
        }

        sb.Append("\x1b[?2026l");
        _ = sb.ToString();
    }

    private static int BuildLineStarts(string content, Span<int> starts)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        var count = 0;
        starts[count++] = 0;
        for (var i = 0; i < content.Length && count < starts.Length; i++)
            if (content[i] == '\n')
                starts[count++] = i + 1;
        return count;
    }

    private static ReadOnlySpan<char> GetLineFromStarts(string content, Span<int> starts, int lineCount, int lineIndex)
    {
        if (lineIndex >= lineCount) return ReadOnlySpan<char>.Empty;
        var start = starts[lineIndex];
        int end;
        if (lineIndex + 1 < lineCount)
        {
            end = starts[lineIndex + 1] - 1;
            if (end > start && content[end - 1] == '\r') end--;
        }
        else
        {
            end = content.Length;
            if (end > start && content[end - 1] == '\r') end--;
        }

        return content.AsSpan(start, end - start);
    }

    // --- Helpers ---

    private static ConsoleImageDocument CreateTestDocument(int frameCount, int width, int height)
    {
        var doc = new ConsoleImageDocument
        {
            RenderMode = "Braille",
            Settings = new DocumentRenderSettings
            {
                MaxWidth = width,
                MaxHeight = height,
                UseColor = true,
                LoopCount = 0
            }
        };

        var random = new Random(42);
        for (var f = 0; f < frameCount; f++)
        {
            var content = BuildTestAnsiFrame(width, height, f);
            doc.Frames.Add(new DocumentFrame
            {
                Content = content,
                Width = width,
                Height = height,
                DelayMs = 33
            });
        }

        return doc;
    }

    private static SubtitleTrack CreateTestSubtitles(int frameCount, int frameDelayMs)
    {
        var entries = new List<SubtitleEntry>();
        var totalMs = frameCount * frameDelayMs;

        for (var i = 0; i < totalMs; i += 3000)
            entries.Add(new SubtitleEntry
            {
                Text = $"Test subtitle at {i / 1000}s - some dialog text here",
                StartTime = TimeSpan.FromMilliseconds(i),
                EndTime = TimeSpan.FromMilliseconds(i + 2500)
            });

        return new SubtitleTrack { Entries = entries };
    }

    private static string BuildTestAnsiFrame(int width, int height, int seed)
    {
        var sb = new StringBuilder(width * height * 20);
        var random = new Random(seed);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var r = (byte)((x * 3 + seed) % 256);
                var g = (byte)((y * 5 + seed) % 256);
                var b = (byte)((x + y + seed) % 256);
                sb.Append($"\x1b[38;2;{r};{g};{b}m");
                sb.Append((char)('!' + random.Next(94)));
            }

            sb.Append("\x1b[0m");
            if (y < height - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    private static long EstimateRawSize(ConsoleImageDocument doc)
    {
        long size = 0;
        foreach (var frame in doc.Frames)
            size += frame.Content.Length * 2; // UTF-16
        return size;
    }
}