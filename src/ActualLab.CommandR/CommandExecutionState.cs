namespace ActualLab.CommandR;

[StructLayout(LayoutKind.Auto)]
public readonly record struct CommandExecutionState(
    CommandHandlerChain Handlers,
    int NextHandlerIndex = 0)
{
    public bool IsFinal => NextHandlerIndex >= Handlers.Length;
    public CommandHandler NextHandler => Handlers[NextHandlerIndex];
    public CommandExecutionState NextState => this with { NextHandlerIndex = NextHandlerIndex + 1 };

    public override string ToString()
        => $"{GetType().GetName()}({NextHandlerIndex}/{Handlers.Length})";
}
