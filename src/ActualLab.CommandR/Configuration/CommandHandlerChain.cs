namespace ActualLab.CommandR.Configuration;

/// <summary>
/// An ordered chain of <see cref="CommandHandler"/> instances (filters and a final handler)
/// that form the execution pipeline for a command.
/// </summary>
public sealed class CommandHandlerChain
{
    public static readonly CommandHandlerChain Empty = new([]);

    private readonly CommandHandler[]? _items;
    private readonly int _finalHandlerIndex;

    public CommandHandler[] Items => _items ?? [];
    public int Length => Items.Length;
    public int FinalHandlerIndex => _items is null ? -1 : _finalHandlerIndex;
    public CommandHandler this[int index] => Items[index];
    public CommandHandler? FinalHandler => _finalHandlerIndex < 0 ? null : _items?[_finalHandlerIndex];

    public CommandHandlerChain(CommandHandler[] items)
    {
        _items = items;
        _finalHandlerIndex = -1;
        for (var index = 0; index < Items.Length; index++) {
            var handler = Items[index];
            if (handler.IsFilter)
                continue;

            if (FinalHandlerIndex == -1)
                _finalHandlerIndex = index;
            else {
                _finalHandlerIndex = -2;
                return;
            }
        }
    }

    public override string ToString()
        => $"[ {Items.ToDelimitedString()} ]";
}
