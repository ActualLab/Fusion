namespace ActualLab.CommandR.Commands;

/// <summary>
/// A tagging interface that ensures the command always run as the outermost one.
/// </summary>
public interface IOutermostCommand : ICommand;
