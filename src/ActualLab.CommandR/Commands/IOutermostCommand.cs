namespace ActualLab.CommandR.Commands;

/// <summary>
/// A tagging interface for commands which ensures they always run as the outermost ones.
/// </summary>
public interface IOutermostCommand : ICommand;
