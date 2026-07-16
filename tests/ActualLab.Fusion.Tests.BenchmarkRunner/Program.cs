using BenchmarkDotNet.Running;

var benchmarkSwitcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
if (args.Length == 0)
    benchmarkSwitcher.RunAll();
else
    benchmarkSwitcher.Run(args);
