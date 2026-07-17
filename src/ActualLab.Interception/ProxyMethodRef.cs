namespace ActualLab.Interception;

/// <summary>
/// A table-qualified reference to a proxy method slot.
/// A bare slot index is meaningless without its <see cref="ProxyMethodTable"/>.
/// </summary>
public readonly record struct ProxyMethodRef(ProxyMethodTable Table, int Index)
{
    public MethodInfo Method {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Table.Methods[Index];
    }

    public override string ToString()
        => $"{nameof(ProxyMethodRef)}({Table.ProxyType.GetName()}, {Index})";
}
