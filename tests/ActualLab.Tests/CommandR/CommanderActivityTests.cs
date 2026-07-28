namespace ActualLab.Tests.CommandR;

/// <summary>
/// xUnit test collection for tests attaching an <see cref="System.Diagnostics.ActivityListener"/>
/// to <c>CommanderInstruments.ActivitySource</c> - listeners are process-wide,
/// so such tests must not run concurrently.
/// </summary>
[CollectionDefinition(nameof(CommanderActivityTests), DisableParallelization = true)]
public class CommanderActivityTests;
