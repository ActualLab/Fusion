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

    private volatile nint _handle;

    public object? Target {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            var handle = _handle;
            return handle == 0 ? null : GCHandle.FromIntPtr(handle).Target;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set {
            var handle = GCHandle.FromIntPtr(_handle);
            handle.Target = value;
        }
    }

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WeakReferenceSlim(object target)
        => _handle = GCHandle.ToIntPtr(GCHandle.Alloc(target, GCHandleType.Weak));

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WeakReferenceSlim(object target, GCHandleType handleType)
        => _handle = GCHandle.ToIntPtr(GCHandle.Alloc(target, handleType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
#if USE_UNSAFE_ACCESSORS
        var handle = _handle;
        if (Interlocked.CompareExchange(ref _handle, 0, handle) != 0)
            GCHandle.FromIntPtr(handle).Free();
#else
        lock (this) {
            var handle = _handle;
            _handle = 0;
            if (handle != 0)
                GCHandle.FromIntPtr(handle).Free();
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
    private volatile nint _handle;

    public T? Target {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            var handle = _handle;
            return handle == 0 ? null : Unsafe.As<T>(GCHandle.FromIntPtr(handle).Target);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set {
            var handle = GCHandle.FromIntPtr(_handle);
            handle.Target = value;
        }
    }

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WeakReferenceSlim(T target)
        => _handle = GCHandle.ToIntPtr(GCHandle.Alloc(target, GCHandleType.Weak));

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WeakReferenceSlim(T target, GCHandleType handleType)
        => _handle = GCHandle.ToIntPtr(GCHandle.Alloc(target, handleType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
#if USE_UNSAFE_ACCESSORS
        var handle = _handle;
        if (Interlocked.CompareExchange(ref _handle, 0, handle) != 0)
            GCHandle.FromIntPtr(handle).Free();
#else
        lock (this) {
            var handle = _handle;
            _handle = 0;
            if (handle != 0)
                GCHandle.FromIntPtr(handle).Free();
        }
#endif
    }
}
