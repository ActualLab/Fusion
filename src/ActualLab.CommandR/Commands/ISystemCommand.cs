namespace ActualLab.CommandR.Commands;

// A tagging interface for any command that's triggered
// as a consequence of another command, i.e. for
// "second-order" commands
public interface ISystemCommand : ICommand<Unit>;
