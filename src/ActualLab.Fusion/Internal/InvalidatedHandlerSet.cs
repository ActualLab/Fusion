namespace ActualLab.Fusion.Internal;

/// <summary>
/// A compact collection of <see cref="Computed"/> invalidation handlers that progressively
/// upgrades its internal storage from a single delegate to an array to a hash set.
/// </summary>
public struct InvalidatedHandlerSet
{
    private const int ListSize = 5;

    private object? _storage;

    public InvalidatedHandlerSet(Action<Computed> item)
        => _storage = item;

    public InvalidatedHandlerSet(IEnumerable<Action<Computed>> items)
    {
        foreach (var item in items)
            Add(item);
    }

    public void Add(Action<Computed> item)
    {
        if (ReferenceEquals(item, null))
            return;

        switch (_storage) {
            case null:
                _storage = item;
                return;

            case Action<Computed> anotherItem:
                if (anotherItem == item)
                    return;

                var newList = new Action<Computed>[ListSize];
                newList[0] = anotherItem;
                newList[1] = item;
                _storage = newList;
                return;

            case Action<Computed>[] list:
                for (var i = 0; i < list.Length; i++) {
                    var listItem = list[i];
                    if (ReferenceEquals(listItem, null)) {
                        list[i] = item;
                        return;
                    }
                    if (listItem == item)
                        return;
                }
                _storage = new HashSet<Action<Computed>>(list) { item };
                return;

            case HashSet<Action<Computed>> set:
                set.Add(item);
                return;

            default:
                throw ActualLab.Internal.Errors.InternalError(
                    $"{nameof(InvalidatedHandlerSet)} structure is corrupted.");
        }
    }

    public void Remove(Action<Computed> item)
    {
        if (ReferenceEquals(item, null))
            return;

        switch (_storage) {
            case null:
                return;

            case Action<Computed> anotherItem:
                if (anotherItem == item)
                    _storage = null;
                return;

            case Action<Computed>[] list:
                for (var i = 0; i < list.Length; i++) {
                    var listItem = list[i];
                    if (ReferenceEquals(listItem, null))
                        return;

                    if (listItem == item) {
                        list.AsSpan(i + 1).CopyTo(list.AsSpan(i));
                        list[^1] = null!;
                        return;
                    }
                }
                return;

            case HashSet<Action<Computed>> set:
                set.Remove(item);
                return;

            default:
                throw ActualLab.Internal.Errors.InternalError(
                    $"{nameof(InvalidatedHandlerSet)} structure is corrupted.");
        }
    }

    public void Clear()
        => _storage = null;

    public readonly void Invoke(Computed computed)
    {
        switch (_storage) {
            case null:
                return;

            case Action<Computed> item:
                try {
                    item.Invoke(computed);
                }
                catch (Exception e) {
                    LogHandlerError(computed, e);
                }
                return;

            case Action<Computed>[] list:
                foreach (var item in list) {
                    if (ReferenceEquals(item, null))
                        return;

                    try {
                        item.Invoke(computed);
                    }
                    catch (Exception e) {
                        LogHandlerError(computed, e);
                    }
                }
                return;

            case HashSet<Action<Computed>> set:
                foreach (var item in set) {
                    try {
                        item.Invoke(computed);
                    }
                    catch (Exception e) {
                        LogHandlerError(computed, e);
                    }
                }
                return;

            default:
                throw ActualLab.Internal.Errors.InternalError(
                    $"{nameof(InvalidatedHandlerSet)} structure is corrupted.");
        }
    }

    private static void LogHandlerError(Computed computed, Exception error)
    {
        try {
            computed.Input.Function.Services.LogFor(computed.GetType())
                .LogError(error, "Invalidated handler failed for {Category}", computed.Input.Category);
        }
        catch {
            // Intended: Invalidate doesn't throw!
        }
    }
}
