using ActualLab.Fusion.Tests.BenchmarkRunner;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

if (args.Length >= 2 && string.Equals(args[0], "--profile", StringComparison.OrdinalIgnoreCase)) {
    if (args.Length > 3)
        throw new ArgumentOutOfRangeException(nameof(args), "Profile mode accepts a benchmark name and an optional duration.");
    var profileSeconds = BenchmarkSettings.DefaultProfileSeconds;
    if (args.Length == 3 && (!int.TryParse(args[2], out profileSeconds) || profileSeconds <= 0))
        throw new ArgumentOutOfRangeException(nameof(args), "The profile duration must be a positive number of seconds.");
    await BenchmarkProfiler.Run(args[1], profileSeconds).ConfigureAwait(false);
    return;
}

var benchmarkSwitcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
var job = Job.ShortRun
    .WithId("Short-.NET 10.0")
    .WithRuntime(CoreRuntime.Core10_0);
var config = ManualConfig.Create(DefaultConfig.Instance).AddJob(job);

if (args.Length == 0)
    benchmarkSwitcher.RunAll(config, []);
else
    benchmarkSwitcher.Run(args, config);
