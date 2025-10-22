namespace ActualLab.Pooling;

#pragma warning disable CA2002, RCS1059 // lock(this)

/// <summary>
/// A lightweight wrapper around <see cref="GCHandle"/> that implements atomic <see cref="GCHandle.Free"/>
/// operation in its <see cref="Dispose"/> method.
/// This type isn't finalizable, so you HAVE TO manually dispose it, otherwise your code will be
/// leaking <see cref="GCHandle"/> instances.
/// </summary>
public sealed class WeakReferenceSlim : IDisposable
{
#if USE_UNSAFE_ACCESSORS
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_handle")]
    internal static extern ref nint AsIntPtr(ref GCHandle handle);
#endif

    private GCHandle _handle;

    public object? Target {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            var handle = _handle;
            return handle == default ? null : handle.Target;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _handle.Target = value;
    }

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WeakReferenceSlim(object target)
        => _handle = GCHandle.Alloc(target, GCHandleType.Weak);

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WeakReferenceSlim(object target, GCHandleType handleType)
        => _handle = GCHandle.Alloc(target, handleType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
#if USE_UNSAFE_ACCESSORS
        var handle = _handle;
        if (Interlocked.CompareExchange(ref AsIntPtr(ref _handle), 0, GCHandle.ToIntPtr(handle)) != 0)
            handle.Free();
#else
        lock (this) {
            var handle = _handle;
            _handle = default;
            if (handle != default)
                handle.Free();
        }
#endif
    }
}

/// <summary>
/// A lightweight wrapper around <see cref="GCHandle"/> that implements atomic <see cref="GCHandle.Free"/>
/// operation in its <see cref="Dispose"/> method.
/// This type isn't finalizable, so you HAVE TO manually dispose it, otherwise your code will be
/// leaking <see cref="GCHandle"/> instances.
/// </summary>
public sealed class WeakReferenceSlim<T> where T : class
{
    private GCHandle _handle;

    public T? Target {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            var handle = _handle;
            return handle == default ? null : Unsafe.As<T>(handle.Target);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _handle.Target = value;
    }

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WeakReferenceSlim(T target)
        => _handle = GCHandle.Alloc(target, GCHandleType.Weak);

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WeakReferenceSlim(T target, GCHandleType handleType)
        => _handle = GCHandle.Alloc(target, handleType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
#if USE_UNSAFE_ACCESSORS
        var handle = _handle;
        if (Interlocked.CompareExchange(ref WeakReferenceSlim.AsIntPtr(ref _handle), 0, GCHandle.ToIntPtr(handle)) != 0)
            handle.Free();
#else
        lock (this) {
            var handle = _handle;
            _handle = default;
            if (handle != default)
                handle.Free();
        }
#endif
    }
}
