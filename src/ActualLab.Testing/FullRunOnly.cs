using Xunit;

namespace ActualLab.Testing;

// Set ActualLab_FullTestRun=1 to run tests marked with these attributes -
// they're skipped by default. Use them for benchmarks, performance tests,
// and other expensive tests that don't have to run on every test pass.

public sealed class FullRunOnlyFact : FactAttribute
{
    public FullRunOnlyFact()
    {
        if (!TestRunnerInfo.IsFullRun())
            Skip = "Full-run only (set ActualLab_FullTestRun=1 to run)";
    }
}

public sealed class FullRunOnlyTheory : TheoryAttribute
{
    public FullRunOnlyTheory()
    {
        if (!TestRunnerInfo.IsFullRun())
            Skip = "Full-run only (set ActualLab_FullTestRun=1 to run)";
    }
}
