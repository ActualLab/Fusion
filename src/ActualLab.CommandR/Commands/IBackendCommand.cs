namespace ActualLab.CommandR.Commands;

/// <summary>
/// A tagging interface for commands that can't be initiated by the client.
/// </summary>
/// <remarks>
/// Fusion doesn't do anything special for such commands,
/// but some of its own commands are decorated with this interface.
/// </remarks>
public interface IBackendCommand : ICommand;
