using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ConsoleImage.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleImage.Benchmarks;

/// <summary>
///     Benchmarks for BrailleRenderer performance.
///     Run with: dotnet run -c Release -- --filter *Braille*
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class BrailleRendererBenchmarks
{
    private Image<Rgba32> _largeImage = null!;
    private Image<Rgba32> _mediumImage = null!;
    private CellData[,]? _previousCells;
    private BrailleRenderer _renderer = null!;
    private BrailleRenderer _rendererNoColor = null!;
    private Image<Rgba32> _smallImage = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create test images of various sizes
        _smallImage = CreateTestImage(160, 90); // 80x23 chars
        _mediumImage = CreateTestImage(320, 180); // 160x45 chars
        _largeImage = CreateTestImage(640, 360); // 320x90 chars

        _renderer = new BrailleRenderer(new RenderOptions
        {
            UseColor = true,
            UseParallelProcessing = true
        });

        _rendererNoColor = new BrailleRenderer(new RenderOptions
        {
            UseColor = false
        });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _smallImage.Dispose();
        _mediumImage.Dispose();
        _largeImage.Dispose();
        _renderer.Dispose();
        _rendererNoColor.Dispose();
    }

    private static Image<Rgba32> CreateTestImage(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);

        // Create a gradient pattern for realistic benchmarking
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

    [Benchmark(Baseline = true)]
    public string RenderSmall_Color()
    {
        return _renderer.RenderImage(_smallImage);
    }

    [Benchmark]
    public string RenderMedium_Color()
    {
        return _renderer.RenderImage(_mediumImage);
    }

    [Benchmark]
    public string RenderLarge_Color()
    {
        return _renderer.RenderImage(_largeImage);
    }

    [Benchmark]
    public string RenderSmall_NoColor()
    {
        return _rendererNoColor.RenderImage(_smallImage);
    }

    [Benchmark]
    public CellData[,] RenderToCells_Small()
    {
        return _renderer.RenderToCells(_smallImage);
    }

    [Benchmark]
    public CellData[,] RenderToCells_Medium()
    {
        return _renderer.RenderToCells(_mediumImage);
    }

    [Benchmark]
    public (string output, CellData[,] cells) RenderWithDelta_FirstFrame()
    {
        return _renderer.RenderWithDelta(_smallImage, null);
    }

    [Benchmark]
    public (string output, CellData[,] cells) RenderWithDelta_SameFrame()
    {
        // Same image = minimal delta output
        _previousCells ??= _renderer.RenderToCells(_smallImage);
        return _renderer.RenderWithDelta(_smallImage, _previousCells);
    }
}

/// <summary>
///     Benchmarks for brightness calculation optimizations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class BrightnessCalculationBenchmarks
{
    private float[] _largeBuffer = null!;
    private float[] _smallBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);

        _smallBuffer = new float[80 * 45 * 8]; // Small image cells
        _largeBuffer = new float[320 * 90 * 8]; // Large image cells

        for (var i = 0; i < _smallBuffer.Length; i++)
            _smallBuffer[i] = (float)random.NextDouble();

        for (var i = 0; i < _largeBuffer.Length; i++)
            _largeBuffer[i] = (float)random.NextDouble();
    }

    [Benchmark(Baseline = true)]
    public (float min, float max) MinMax_Simple_Small()
    {
        return GetMinMaxSimple(_smallBuffer);
    }

    [Benchmark]
    public (float min, float max) MinMax_Unrolled_Small()
    {
        return GetMinMaxUnrolled(_smallBuffer);
    }

    [Benchmark]
    public (float min, float max) MinMax_Simple_Large()
    {
        return GetMinMaxSimple(_largeBuffer);
    }

    [Benchmark]
    public (float min, float max) MinMax_Unrolled_Large()
    {
        return GetMinMaxUnrolled(_largeBuffer);
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

        // Process 4 at a time
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
}

/// <summary>
///     Benchmarks for ANSI escape sequence generation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class AnsiEscapeBenchmarks
{
    private static readonly string[] GreyscaleEscapes = InitGreyscale();
    private byte[] _colors = null!;

    private static string[] InitGreyscale()
    {
        var escapes = new string[256];
        for (var i = 0; i < 256; i++)
            escapes[i] = $"\x1b[38;2;{i};{i};{i}m";
        return escapes;
    }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _colors = new byte[1000 * 3];
        random.NextBytes(_colors);
    }

    [Benchmark(Baseline = true)]
    public string InterpolatedStrings()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _colors.Length; i += 3)
            sb.Append($"\x1b[38;2;{_colors[i]};{_colors[i + 1]};{_colors[i + 2]}m");
        return sb.ToString();
    }

    [Benchmark]
    public string ManualAppend()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _colors.Length; i += 3)
        {
            sb.Append("\x1b[38;2;");
            sb.Append(_colors[i]);
            sb.Append(';');
            sb.Append(_colors[i + 1]);
            sb.Append(';');
            sb.Append(_colors[i + 2]);
            sb.Append('m');
        }

        return sb.ToString();
    }

    [Benchmark]
    public string CachedGreyscale()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _colors.Length; i += 3)
        {
            var r = _colors[i];
            var g = _colors[i + 1];
            var b = _colors[i + 2];

            if (r == g && g == b)
            {
                sb.Append(GreyscaleEscapes[r]);
            }
            else
            {
                sb.Append("\x1b[38;2;");
                sb.Append(r);
                sb.Append(';');
                sb.Append(g);
                sb.Append(';');
                sb.Append(b);
                sb.Append('m');
            }
        }

        return sb.ToString();
    }
}