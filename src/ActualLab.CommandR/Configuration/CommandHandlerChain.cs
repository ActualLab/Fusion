namespace ActualLab.CommandR.Configuration;

public readonly struct CommandHandlerChain : IEquatable<CommandHandlerChain>
{
    public static readonly CommandHandlerChain Empty = new([]);

    private readonly CommandHandler[]? _items;
    private readonly int _finalHandlerIndex;

    public CommandHandler[] Items => _items ?? [];
    public bool IsEmpty => _items == null || _items.Length == 0;
    public int Length => Items.Length;
    public int FinalHandlerIndex => _items == null ? -1 : _finalHandlerIndex;
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

    // Equality
    public bool Equals(CommandHandlerChain other) => Equals(Items, other.Items);
    public override bool Equals(object? obj) => obj is CommandHandlerChain other && Equals(other);
    public override int GetHashCode() => Items.GetHashCode();
    public static bool operator ==(CommandHandlerChain left, CommandHandlerChain right) => left.Equals(right);
    public static bool operator !=(CommandHandlerChain left, CommandHandlerChain right) => !left.Equals(right);
}
