namespace ActualLab.Tests.Internal;

#pragma warning disable CA2002, RCS1059 // lock(this)

/// <summary>
/// A lightweight wrapper around <see cref="GCHandle"/> that implements delayed
/// <see cref="GCHandle.Free"/> operation in its <see cref="Dispose"/> method.
/// This type isn't finalizable, so you HAVE TO manually dispose it, otherwise your code will be
/// leaking <see cref="GCHandle"/> instances.
/// </summary>
public sealed class WeakReferenceSlim : IDisposable, IGenericTimeoutHandler
{
    public static readonly TimeSpan FreeDelay = TimeSpan.FromSeconds(3);

    private volatile nint _handle;
    private volatile nint _handleToFree;

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
        var handle = _handle;
        if (Interlocked.CompareExchange(ref _handle, 0, handle) != 0) {
            Interlocked.Exchange(ref _handleToFree, handle);
            // Timeouts.Generic5S.Add(this);
            GCHandle.FromIntPtr(_handleToFree).Free();
        }
    }

    void IGenericTimeoutHandler.OnTimeout()
        => GCHandle.FromIntPtr(_handleToFree).Free();
}

/// <summary>
/// A lightweight wrapper around <see cref="GCHandle"/> that implements delayed
/// <see cref="GCHandle.Free"/> operation in its <see cref="Dispose"/> method.
/// This type isn't finalizable, so you HAVE TO manually dispose it, otherwise your code will be
/// leaking <see cref="GCHandle"/> instances.
/// </summary>
public sealed class WeakReferenceSlim<T> : IDisposable, IGenericTimeoutHandler
    where T : class
{
    private volatile nint _handle;
    private volatile nint _handleToFree;

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
        var handle = _handle;
        if (Interlocked.CompareExchange(ref _handle, 0, handle) != 0) {
            Interlocked.Exchange(ref _handleToFree, handle);
            // Timeouts.Generic5S.Add(this);
            GCHandle.FromIntPtr(_handleToFree).Free();
        }
    }

    void IGenericTimeoutHandler.OnTimeout()
        => GCHandle.FromIntPtr(_handleToFree).Free();
}
