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
/// Delegating command handlers are guaranteed to:
/// - Have no Operation Framework handlers in their pipeline -
///   they are filtered out, coz such commands can't make any changes directly.
/// - <c>InvalidatingCommandCompletionHandler</c> assumes
///   delegating commands don't require invalidation, so they aren't logged (even as the nested ones).
/// - And thus they also don't need <c>if (Invalidation.IsActive) { ... }</c> blocks.
/// </remarks>
public interface IDelegatingCommand : IOutermostCommand;

public interface IDelegatingCommand<TResult> : ICommand<TResult>, IDelegatingCommand;
