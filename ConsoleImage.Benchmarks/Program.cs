using BenchmarkDotNet.Running;
using ConsoleImage.Benchmarks;

// If --simple flag, run simple benchmarks without BenchmarkDotNet
if (args.Contains("--simple") || args.Contains("-s"))
    SimpleBenchmark.RunAll();
else
    // Run all benchmarks with BenchmarkDotNet
    BenchmarkSwitcher.FromAssembly(typeof(BrailleRendererBenchmarks).Assembly).Run(args);