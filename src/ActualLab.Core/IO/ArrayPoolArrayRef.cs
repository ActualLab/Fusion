namespace ActualLab.IO;

public readonly struct ArrayPoolArrayRef<T> : IDisposable, ICanBeNone<ArrayPoolArrayRef<T>>
{
    private readonly ArrayPoolArrayHandle<T>? _handle;

    public static ArrayPoolArrayRef<T> None => default;

    public ArrayPoolArrayHandle<T>? Handle => _handle;
    public bool IsNone => _handle is null;

    internal ArrayPoolArrayRef(ArrayPoolArrayHandle<T> handle)
        => _handle = handle;

    public void Dispose()
        => _handle?.ReleaseRef();
}
