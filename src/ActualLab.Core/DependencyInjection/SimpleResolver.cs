using System.Diagnostics.CodeAnalysis;

namespace ActualLab.DependencyInjection;

public class SimpleResolver<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> items) : IResolver<TKey, TValue>
    where TKey : notnull
{
    public IReadOnlyDictionary<TKey, TValue> Items { get; } = items;

    public override string ToString()
        => $"{GetType().GetName()}([{Items.Keys.ToDelimitedString()}])";

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
        => Items.TryGetValue(key, out value);
}
