namespace ActualLab.CommandR.Commands;

/// <summary>
/// A tagging interface for commands that can be initiated by the client
/// and aren't supposed to make any changes directly - i.e. w/o invoking
/// other commands.
/// </summary>
/// <remarks>
/// API commands:
/// - Always execute as the outermost ones.
/// - Always execute other commands as the outermost ones.
/// API command handlers are guaranteed to:
/// - Have less Fusion handlers in their pipelines - namely,
///   the Operation Framework handlers are filtered out for them,
///   since they anyway can't change the data directly.
/// - PostCompletionInvalidator assumes any IApiCommand doesn't require
///   invalidation, coz it can't change anything directly,
///   thus such commands aren't logged as nested ones,
///   and don't need to have "if (Invalidation.IsOn() ...)" block.
/// </remarks>
public interface IApiCommand : ICommand;
