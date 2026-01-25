// Simple performance tests using Stopwatch
// Can be run directly without BenchmarkDotNet framework magic

using System.Diagnostics;
using ConsoleImage.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Benchmarks;

public static class SimpleBenchmark
{
    public static void RunAll()
    {
        Console.WriteLine("=== Simple Performance Benchmarks ===\n");

        RunBrightnessTests();
        Console.WriteLine();
        RunCharacterMatchingTests();
        Console.WriteLine();
        RunAsciiTests();
        Console.WriteLine();
        RunBrailleTests();
        Console.WriteLine();
        RunColorBlockTests();
        Console.WriteLine();
        RunMatrixTests();
        Console.WriteLine();
        RunMemoryAllocationTest();
        Console.WriteLine();
        DocumentBenchmarks.RunAll();
    }

    private static void RunBrightnessTests()
    {
        Console.WriteLine("--- Brightness Range Calculation ---");
        Console.WriteLine($"SIMD available: {System.Numerics.Vector.IsHardwareAccelerated}, Vector<float>.Count: {System.Numerics.Vector<float>.Count}");

        var random = new Random(42);
        var smallBuffer = new float[80 * 45 * 8];
        var largeBuffer = new float[320 * 90 * 8];

        for (var i = 0; i < smallBuffer.Length; i++)
            smallBuffer[i] = (float)random.NextDouble();
        for (var i = 0; i < largeBuffer.Length; i++)
            largeBuffer[i] = (float)random.NextDouble();

        // Warm up
        for (var i = 0; i < 100; i++)
        {
            GetMinMaxSimple(smallBuffer);
            GetMinMaxUnrolled(smallBuffer);
            GetMinMaxSimd(smallBuffer);
        }

        const int iterations = 10000;

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            GetMinMaxSimple(smallBuffer);
        sw.Stop();
        Console.WriteLine($"Simple (small):   {sw.Elapsed.TotalMicroseconds / iterations:F2} µs/iter");

        sw.Restart();
        for (var i = 0; i < iterations; i++)
            GetMinMaxUnrolled(smallBuffer);
        sw.Stop();
        Console.WriteLine($"Unrolled (small): {sw.Elapsed.TotalMicroseconds / iterations:F2} µs/iter");

        sw.Restart();
        for (var i = 0; i < iterations; i++)
            GetMinMaxSimd(smallBuffer);
        sw.Stop();
        Console.WriteLine($"SIMD (small):     {sw.Elapsed.TotalMicroseconds / iterations:F2} µs/iter");

        sw.Restart();
        for (var i = 0; i < iterations; i++)
            GetMinMaxSimple(largeBuffer);
        sw.Stop();
        Console.WriteLine($"Simple (large):   {sw.Elapsed.TotalMicroseconds / iterations:F2} µs/iter");

        sw.Restart();
        for (var i = 0; i < iterations; i++)
            GetMinMaxUnrolled(largeBuffer);
        sw.Stop();
        Console.WriteLine($"Unrolled (large): {sw.Elapsed.TotalMicroseconds / iterations:F2} µs/iter");

        sw.Restart();
        for (var i = 0; i < iterations; i++)
            GetMinMaxSimd(largeBuffer);
        sw.Stop();
        Console.WriteLine($"SIMD (large):     {sw.Elapsed.TotalMicroseconds / iterations:F2} µs/iter");
    }

    private static void RunCharacterMatchingTests()
    {
        Console.WriteLine("--- Character Matching (K-D Tree vs Brute Force) ---");

        var charMap = new CharacterMap();
        var random = new Random(42);

        // Generate random test vectors
        var testVectors = new ShapeVector[1000];
        for (var i = 0; i < testVectors.Length; i++)
        {
            testVectors[i] = new ShapeVector(
                (float)random.NextDouble(),
                (float)random.NextDouble(),
                (float)random.NextDouble(),
                (float)random.NextDouble(),
                (float)random.NextDouble(),
                (float)random.NextDouble()
            );
        }

        // Warm up
        for (var i = 0; i < 100; i++)
        {
            charMap.FindBestMatch(testVectors[i % testVectors.Length]);
            charMap.FindBestMatchBruteForce(testVectors[i % testVectors.Length]);
        }

        charMap.ClearCache(); // Clear cache for fair comparison

        const int iterations = 10000;

        // Test K-D Tree (with cache disabled via different quantization)
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            charMap.FindBestMatch(testVectors[i % testVectors.Length]);
        }
        sw.Stop();
        var kdTreeTime = sw.Elapsed.TotalMicroseconds / iterations;
        var (hits, misses, cacheSize, hitRate) = charMap.GetCacheStats();
        Console.WriteLine($"K-D Tree (cached): {kdTreeTime:F2} µs/lookup, cache: {hitRate:P1} hit rate ({cacheSize} entries)");

        // Test Brute Force SIMD
        sw.Restart();
        for (var i = 0; i < iterations; i++)
        {
            charMap.FindBestMatchBruteForce(testVectors[i % testVectors.Length]);
        }
        sw.Stop();
        var bruteForceTime = sw.Elapsed.TotalMicroseconds / iterations;
        Console.WriteLine($"SIMD Brute Force:  {bruteForceTime:F2} µs/lookup ({charMap.Count} characters)");

        // Verify correctness
        var matches = 0;
        for (var i = 0; i < 100; i++)
        {
            charMap.ClearCache();
            var kdResult = charMap.FindBestMatch(testVectors[i]);
            var bfResult = charMap.FindBestMatchBruteForce(testVectors[i]);
            if (kdResult == bfResult) matches++;
        }
        Console.WriteLine($"Agreement: {matches}/100 ({(matches == 100 ? "OK" : "MISMATCH")})");
    }

    private static void RunAsciiTests()
    {
        Console.WriteLine("--- ASCII Renderer ---");

        using var smallImage = CreateTestImage(320, 180);
        using var mediumImage = CreateTestImage(640, 360);

        // Use explicit dimensions for fair comparison
        var smallOptions = new RenderOptions
        {
            Width = 80,
            Height = 45,
            UseColor = true,
            UseParallelProcessing = true
        };

        var mediumOptions = new RenderOptions
        {
            Width = 160,
            Height = 90,
            UseColor = true,
            UseParallelProcessing = true
        };

        using var smallRenderer = new AsciiRenderer(smallOptions);
        using var mediumRenderer = new AsciiRenderer(mediumOptions);

        // Warm up
        for (var i = 0; i < 5; i++)
        {
            smallRenderer.RenderImage(smallImage);
            mediumRenderer.RenderImage(mediumImage);
        }

        const int iterations = 50;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            smallRenderer.RenderImage(smallImage);
        }
        sw.Stop();
        Console.WriteLine($"80x45 output:  {sw.Elapsed.TotalMilliseconds / iterations:F2} ms/frame (3600 cells)");

        sw.Restart();
        for (var i = 0; i < iterations; i++)
        {
            mediumRenderer.RenderImage(mediumImage);
        }
        sw.Stop();
        Console.WriteLine($"160x90 output: {sw.Elapsed.TotalMilliseconds / iterations:F2} ms/frame (14400 cells)");

    }

    private static void RunBrailleTests()
    {
        Console.WriteLine("--- Braille Renderer ---");

        using var smallImage = CreateTestImage(160, 90);
        using var mediumImage = CreateTestImage(320, 180);

        var options = new RenderOptions
        {
            UseColor = true,
            UseParallelProcessing = true
        };

        using var renderer = new BrailleRenderer(options);

        // Warm up
        for (var i = 0; i < 5; i++)
        {
            renderer.RenderImage(smallImage);
        }

        const int iterations = 100;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            renderer.RenderImage(smallImage);
        }
        sw.Stop();
        Console.WriteLine($"Small (160x90):  {sw.Elapsed.TotalMilliseconds / iterations:F2} ms/frame");

        sw.Restart();
        for (var i = 0; i < iterations; i++)
        {
            renderer.RenderImage(mediumImage);
        }
        sw.Stop();
        Console.WriteLine($"Medium (320x180): {sw.Elapsed.TotalMilliseconds / iterations:F2} ms/frame");

        // Test delta rendering
        var cells = renderer.RenderToCells(smallImage);
        sw.Restart();
        for (var i = 0; i < iterations; i++)
        {
            renderer.RenderWithDelta(smallImage, cells);
        }
        sw.Stop();
        Console.WriteLine($"Delta (same frame): {sw.Elapsed.TotalMilliseconds / iterations:F2} ms/frame");
    }

    private static void RunColorBlockTests()
    {
        Console.WriteLine("--- ColorBlock Renderer ---");

        using var smallImage = CreateTestImage(160, 90);
        using var mediumImage = CreateTestImage(320, 180);

        var options = new RenderOptions
        {
            UseColor = true,
            UseParallelProcessing = true
        };

        using var renderer = new ColorBlockRenderer(options);

        // Warm up
        for (var i = 0; i < 5; i++)
        {
            renderer.RenderImage(smallImage);
        }

        const int iterations = 100;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            renderer.RenderImage(smallImage);
        }
        sw.Stop();
        Console.WriteLine($"Small (160x90):  {sw.Elapsed.TotalMilliseconds / iterations:F2} ms/frame");

        sw.Restart();
        for (var i = 0; i < iterations; i++)
        {
            renderer.RenderImage(mediumImage);
        }
        sw.Stop();
        Console.WriteLine($"Medium (320x180): {sw.Elapsed.TotalMilliseconds / iterations:F2} ms/frame");
    }

    private static Image<Rgba32> CreateTestImage(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var r = (byte)(x * 255 / width);
                    var g = (byte)(y * 255 / height);
                    var b = (byte)((x + y) * 127 / (width + height));
                    row[x] = new Rgba32(r, g, b, 255);
                }
            }
        });

        return image;
    }

    private static (float min, float max) GetMinMaxSimple(float[] buffer)
    {
        var min = 1f;
        var max = 0f;
        for (var i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] < min) min = buffer[i];
            if (buffer[i] > max) max = buffer[i];
        }
        return (min, max);
    }

    private static (float min, float max) GetMinMaxUnrolled(float[] buffer)
    {
        if (buffer.Length == 0) return (0f, 1f);

        var min = buffer[0];
        var max = buffer[0];
        var len = buffer.Length;
        var i = 1;

        for (; i + 3 < len; i += 4)
        {
            var v0 = buffer[i];
            var v1 = buffer[i + 1];
            var v2 = buffer[i + 2];
            var v3 = buffer[i + 3];

            var localMin = MathF.Min(MathF.Min(v0, v1), MathF.Min(v2, v3));
            var localMax = MathF.Max(MathF.Max(v0, v1), MathF.Max(v2, v3));

            if (localMin < min) min = localMin;
            if (localMax > max) max = localMax;
        }

        for (; i < len; i++)
        {
            if (buffer[i] < min) min = buffer[i];
            if (buffer[i] > max) max = buffer[i];
        }

        return (min, max);
    }

    private static void RunMatrixTests()
    {
        Console.WriteLine("--- Matrix Renderer ---");

        using var smallImage = CreateTestImage(160, 90);
        using var mediumImage = CreateTestImage(320, 180);

        var options = new RenderOptions
        {
            UseColor = true,
            UseParallelProcessing = true
        };

        using var renderer = new MatrixRenderer(options);

        // Warm up
        for (var i = 0; i < 5; i++)
        {
            renderer.RenderImage(smallImage);
        }

        const int iterations = 100;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            renderer.RenderImage(smallImage);
        }
        sw.Stop();
        Console.WriteLine($"Small (160x90):  {sw.Elapsed.TotalMilliseconds / iterations:F2} ms/frame");

        sw.Restart();
        for (var i = 0; i < iterations; i++)
        {
            renderer.RenderImage(mediumImage);
        }
        sw.Stop();
        Console.WriteLine($"Medium (320x180): {sw.Elapsed.TotalMilliseconds / iterations:F2} ms/frame");
    }

    private static void RunMemoryAllocationTest()
    {
        Console.WriteLine("--- Memory Allocation Test (video simulation) ---");

        using var image = CreateTestImage(320, 180);
        var options = new RenderOptions { UseColor = true, UseParallelProcessing = true };

        // Measure GC collections during 100 frame simulation
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var memBefore = GC.GetTotalMemory(false);

        using var renderer = new BrailleRenderer(options);

        // Simulate 100 frames
        for (var i = 0; i < 100; i++)
        {
            renderer.RenderImage(image);
        }

        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var memAfter = GC.GetTotalMemory(false);

        Console.WriteLine($"Braille (100 frames):");
        Console.WriteLine($"  Gen0 collections: {gen0After - gen0Before}");
        Console.WriteLine($"  Gen1 collections: {gen1After - gen1Before}");
        Console.WriteLine($"  Memory delta: {(memAfter - memBefore) / 1024:N0} KB");

        // Now test ColorBlock
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        gen0Before = GC.CollectionCount(0);
        gen1Before = GC.CollectionCount(1);
        memBefore = GC.GetTotalMemory(false);

        using var blockRenderer = new ColorBlockRenderer(options);

        for (var i = 0; i < 100; i++)
        {
            blockRenderer.RenderImage(image);
        }

        gen0After = GC.CollectionCount(0);
        gen1After = GC.CollectionCount(1);
        memAfter = GC.GetTotalMemory(false);

        Console.WriteLine($"ColorBlock (100 frames):");
        Console.WriteLine($"  Gen0 collections: {gen0After - gen0Before}");
        Console.WriteLine($"  Gen1 collections: {gen1After - gen1Before}");
        Console.WriteLine($"  Memory delta: {(memAfter - memBefore) / 1024:N0} KB");
    }

    private static (float min, float max) GetMinMaxSimd(float[] buffer)
    {
        if (buffer.Length == 0) return (0f, 1f);

        var span = buffer.AsSpan();
        var vectorSize = System.Numerics.Vector<float>.Count;
        var len = span.Length;

        if (!System.Numerics.Vector.IsHardwareAccelerated || len < vectorSize * 2)
            return GetMinMaxUnrolled(buffer);

        var vectorizedLength = len - (len % vectorSize);

        var minVec = new System.Numerics.Vector<float>(span);
        var maxVec = minVec;

        for (var i = vectorSize; i < vectorizedLength; i += vectorSize)
        {
            var vec = new System.Numerics.Vector<float>(span.Slice(i));
            minVec = System.Numerics.Vector.Min(minVec, vec);
            maxVec = System.Numerics.Vector.Max(maxVec, vec);
        }

        var min = float.MaxValue;
        var max = float.MinValue;

        for (var i = 0; i < vectorSize; i++)
        {
            if (minVec[i] < min) min = minVec[i];
            if (maxVec[i] > max) max = maxVec[i];
        }

        for (var i = vectorizedLength; i < len; i++)
        {
            var v = span[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        return (min, max);
    }
}
