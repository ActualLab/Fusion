using Xunit;

namespace ActualLab.Testing.Collections;

/// <summary>
/// xUnit test collection definition that disables parallelization for performance tests.
/// </summary>
[CollectionDefinition(nameof(PerformanceTests), DisableParallelization = true)]
public class PerformanceTests;
