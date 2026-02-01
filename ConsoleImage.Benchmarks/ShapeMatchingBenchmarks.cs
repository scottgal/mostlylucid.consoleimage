using BenchmarkDotNet.Attributes;
using ConsoleImage.Core;

namespace ConsoleImage.Benchmarks;

/// <summary>
///     Benchmarks for BrailleCharacterMap shape vector matching.
///     Run with: dotnet run -c Release -- --filter *ShapeMatching*
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class BrailleShapeMatchingBenchmarks
{
    private BrailleCharacterMap _brailleMap = null!;
    private float[][] _randomVectors = null!;
    private float[][] _sparseVectors = null!;
    private float[][] _denseVectors = null!;

    [GlobalSetup]
    public void Setup()
    {
        _brailleMap = new BrailleCharacterMap();

        var random = new Random(42);

        // Random 8D vectors (covers full range)
        _randomVectors = new float[1000][];
        for (var i = 0; i < _randomVectors.Length; i++)
        {
            _randomVectors[i] = new float[8];
            for (var j = 0; j < 8; j++)
                _randomVectors[i][j] = (float)random.NextDouble();
        }

        // Sparse vectors (mostly 0, few dots on - like thin lines)
        _sparseVectors = new float[1000][];
        for (var i = 0; i < _sparseVectors.Length; i++)
        {
            _sparseVectors[i] = new float[8];
            for (var j = 0; j < 8; j++)
                _sparseVectors[i][j] = random.NextDouble() < 0.25 ? (float)random.NextDouble() : 0f;
        }

        // Dense vectors (mostly 1, few dots off - like filled areas)
        _denseVectors = new float[1000][];
        for (var i = 0; i < _denseVectors.Length; i++)
        {
            _denseVectors[i] = new float[8];
            for (var j = 0; j < 8; j++)
                _denseVectors[i][j] = random.NextDouble() < 0.25 ? (float)random.NextDouble() : 1f;
        }
    }

    [Benchmark(Baseline = true)]
    public char FindBestMatch_Random_Cached()
    {
        char result = ' ';
        for (var i = 0; i < _randomVectors.Length; i++)
            result = _brailleMap.FindBestMatch(_randomVectors[i]);
        return result;
    }

    [Benchmark]
    public char FindBestMatch_Random_BruteForce()
    {
        char result = ' ';
        for (var i = 0; i < _randomVectors.Length; i++)
            result = _brailleMap.FindBestMatchBruteForce(_randomVectors[i]);
        return result;
    }

    [Benchmark]
    public char FindBestMatch_Sparse()
    {
        char result = ' ';
        for (var i = 0; i < _sparseVectors.Length; i++)
            result = _brailleMap.FindBestMatch(_sparseVectors[i]);
        return result;
    }

    [Benchmark]
    public char FindBestMatch_Dense()
    {
        char result = ' ';
        for (var i = 0; i < _denseVectors.Length; i++)
            result = _brailleMap.FindBestMatch(_denseVectors[i]);
        return result;
    }
}

/// <summary>
///     Benchmarks for ASCII CharacterMap with different character set sizes.
///     Measures the impact of the expanded full-printable character set.
///     Run with: dotnet run -c Release -- --filter *CharacterSet*
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class CharacterSetBenchmarks
{
    private CharacterMap _classicMap = null!;    // 70 chars (old default)
    private CharacterMap _fullMap = null!;       // 95 chars (new default)
    private CharacterMap _extendedMap = null!;   // 93 chars
    private ShapeVector[] _testVectors = null!;

    [GlobalSetup]
    public void Setup()
    {
        _classicMap = new CharacterMap(CharacterMap.ClassicCharacterSet);
        _fullMap = new CharacterMap(CharacterMap.DefaultCharacterSet);
        _extendedMap = new CharacterMap(CharacterMap.ExtendedCharacterSet);

        var random = new Random(42);
        _testVectors = new ShapeVector[1000];
        for (var i = 0; i < _testVectors.Length; i++)
            _testVectors[i] = new ShapeVector(
                (float)random.NextDouble(),
                (float)random.NextDouble(),
                (float)random.NextDouble(),
                (float)random.NextDouble(),
                (float)random.NextDouble(),
                (float)random.NextDouble()
            );
    }

    [Benchmark(Baseline = true)]
    public char Classic_70Chars()
    {
        char result = ' ';
        for (var i = 0; i < _testVectors.Length; i++)
            result = _classicMap.FindBestMatch(_testVectors[i]);
        return result;
    }

    [Benchmark]
    public char Full_95Chars()
    {
        char result = ' ';
        for (var i = 0; i < _testVectors.Length; i++)
            result = _fullMap.FindBestMatch(_testVectors[i]);
        return result;
    }

    [Benchmark]
    public char Extended_93Chars()
    {
        char result = ' ';
        for (var i = 0; i < _testVectors.Length; i++)
            result = _extendedMap.FindBestMatch(_testVectors[i]);
        return result;
    }

    [Benchmark]
    public char Classic_BruteForce()
    {
        char result = ' ';
        for (var i = 0; i < _testVectors.Length; i++)
            result = _classicMap.FindBestMatchBruteForce(_testVectors[i]);
        return result;
    }

    [Benchmark]
    public char Full_BruteForce()
    {
        char result = ' ';
        for (var i = 0; i < _testVectors.Length; i++)
            result = _fullMap.FindBestMatchBruteForce(_testVectors[i]);
        return result;
    }
}

/// <summary>
///     Benchmarks for disk cache performance (CharacterMap startup).
///     Run with: dotnet run -c Release -- --filter *DiskCache*
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[IterationCount(5)]
[WarmupCount(2)]
public class DiskCacheBenchmarks
{
    [Benchmark(Baseline = true)]
    public CharacterMap CreateMap_Classic_CacheHit()
    {
        // Second creation should hit disk cache
        return new CharacterMap(CharacterMap.ClassicCharacterSet);
    }

    [Benchmark]
    public CharacterMap CreateMap_Full_CacheHit()
    {
        return new CharacterMap(CharacterMap.DefaultCharacterSet);
    }

    [Benchmark]
    public BrailleCharacterMap CreateBrailleMap()
    {
        // Mathematical generation - no disk cache needed
        return new BrailleCharacterMap();
    }
}
