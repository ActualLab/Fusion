using ActualLab.Comparison;

namespace ActualLab.Rpc;

public sealed class LegacyNames
{
    private readonly LegacyName[]? _items;

    public int Count => _items?.Length ?? 0;
    public IReadOnlyList<LegacyName> Items => _items ?? [];

    public LegacyName? this[Version version] {
        get {
            if (_items is null || _items.Length == 0)
                return null;

            var index = Array.BinarySearch(_items, new LegacyName("", version), LegacyName.MaxVersionComparer);
            if (index < 0) {
                index = ~index;
                if (index >= _items.Length)
                    return null;
            }
            return _items[index];
        }
    }

    public LegacyNames(LegacyName[] items)
        => _items = items;

    public LegacyNames(IEnumerable<LegacyName> items)
        => _items = items.ToImmutableSortedDictionary(x => x.MaxVersion ?? VersionExt.MaxValue, x => x).Values.ToArray();

    public override string ToString()
        => $"[ {Items.ToDelimitedString(", ")} ]";
}
