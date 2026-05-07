using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Vellum.Benchmarks;

if (args.Any(static arg => string.Equals(arg, "--smoke", StringComparison.OrdinalIgnoreCase)))
{
    SceneBenchmarks.Smoke();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--loop", StringComparison.OrdinalIgnoreCase))
{
    string scene = args.Length > 1 ? args[1] : "Labels100";
    int iterations = args.Length > 2 && int.TryParse(args[2], out int parsedIterations)
        ? parsedIterations
        : 100_000;

    Environment.ExitCode = SceneBenchmarks.RunLoop(scene, iterations);
    return;
}

var config = DefaultConfig.Instance.WithArtifactsPath(Path.Combine("artifacts", "benchmarks"));
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
