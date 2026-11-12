namespace ActualLab.CommandR.Commands;

/// <summary>
/// A tagging interface that marks a command delegating its work to other commands.
/// Such commands aren't supposed to make any changes directly - all they can do is to
/// either read the data or invoke other commands to make the changes.
/// </summary>
/// <remarks>
/// Delegating commands:
/// - Always execute as the outermost ones.
/// - Always execute nested commands as the outermost ones.
/// - Are never reprocessed by <c>OperationReprocessor</c>; the commands they invoke are.
/// "No direct changes" is a convention, nothing enforces it: Operations Framework handlers
/// stay in the pipeline, so a delegating command handler that uses an operation scope
/// gets an operation and may defer invalidation like any other handler.
/// </remarks>
public interface IDelegatingCommand : IOutermostCommand;

/// <summary>
/// A generic variant of <see cref="IDelegatingCommand"/> that produces a typed result.
/// </summary>
public interface IDelegatingCommand<TResult> : ICommand<TResult>, IDelegatingCommand;
