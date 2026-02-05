using Xunit;

namespace ActualLab.Testing.Collections;

/// <summary>
/// xUnit test collection definition that disables parallelization for time-sensitive tests.
/// </summary>
[CollectionDefinition(nameof(TimeSensitiveTests), DisableParallelization = true)]
public class TimeSensitiveTests;
