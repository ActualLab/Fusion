namespace ActualLab.CommandR.Commands;

/// <summary>
/// A tagging interface for any command that is triggered as a consequence of another command,
/// i.e., for "second-order" commands.
/// </summary>
public interface ISystemCommand : ICommand<Unit>;
