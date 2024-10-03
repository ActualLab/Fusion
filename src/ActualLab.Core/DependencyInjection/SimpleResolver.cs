using System.Diagnostics.CodeAnalysis;

namespace ActualLab.DependencyInjection;

public record SimpleResolver<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> Items) : IResolver<TKey, TValue>
    where TKey : notnull
{
    public override string ToString()
        => $"{GetType().GetName()}([{Items.Keys.ToDelimitedString()}])";

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
        => Items.TryGetValue(key, out value);
}
