using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Tests.Internal;

#pragma warning disable CA2002, RCS1059 // lock(this)

/// <summary>
/// A lightweight wrapper around <see cref="GCHandle"/> that implements delayed
/// <see cref="GCHandle.Free"/> operation in its <see cref="Dispose"/> method.
/// This type isn't finalizable, so you HAVE TO manually dispose it, otherwise your code will be
/// leaking <see cref="GCHandle"/> instances.
/// </summary>
public sealed class WeakReferenceSlim : IDisposable
{
    public static readonly TimeSpan FreeDelay = TimeSpan.FromSeconds(3);

    private nint _handle;

    public object? Target {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            var handle = _handle;
            if (handle == 0)
                return null;

            try {
                var target = GCHandle.FromIntPtr(handle).Target;
                return Volatile.Read(ref _handle) == handle ? target : null;
            }
            catch (InvalidOperationException) {
                return null;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetTarget([NotNullWhen(true)] out object? target)
    {
        target = Target;
        return target != null;
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
        if (Interlocked.CompareExchange(ref _handle, 0, handle) != 0)
            GCHandle.FromIntPtr(handle).Free();
    }
}

/// <summary>
/// A lightweight wrapper around <see cref="GCHandle"/> that implements delayed
/// <see cref="GCHandle.Free"/> operation in its <see cref="Dispose"/> method.
/// This type isn't finalizable, so you HAVE TO manually dispose it, otherwise your code will be
/// leaking <see cref="GCHandle"/> instances.
/// </summary>
public sealed class WeakReferenceSlim<T> : IDisposable
    where T : class
{
    private nint _handle;

    public T? Target {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            var handle = _handle;
            if (handle == 0)
                return null;

            try {
                var target = GCHandle.FromIntPtr(handle).Target;
                return Volatile.Read(ref _handle) == handle ? Unsafe.As<T>(target) : null;
            }
            catch (InvalidOperationException) {
                return null;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetTarget([NotNullWhen(true)] out T? target)
    {
        target = Target;
        return target != null;
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
        if (Interlocked.CompareExchange(ref _handle, 0, handle) != 0)
            GCHandle.FromIntPtr(handle).Free();
    }
}
