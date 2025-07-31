namespace ActualLab;

#pragma warning disable CA2002, RCS1059

public interface ILazySlim<out TValue>
{
    public TValue Value { get; }
}

public static class LazySlim
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LazySlim<TValue> New<TValue>(TValue value)
        => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LazySlim<TValue> New<TValue>(Func<TValue> factory)
        => new(factory);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LazySlim<TArg0, TValue> New<TArg0, TValue>(
        TArg0 arg0,
        Func<TArg0, TValue> factory)
        => new(arg0, factory);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LazySlim<TArg0, TArg1, TValue> New<TArg0, TArg1, TValue>(
        TArg0 arg0, TArg1 arg1,
        Func<TArg0, TArg1, TValue> factory)
        => new(arg0, arg1, factory);
}

public sealed class LazySlim<TValue> : ILazySlim<TValue>
{
    private Func<TValue>? _factory;

    public TValue Value {
        get {
            // Double-check locking
            if (_factory is null) return field;
            lock (this) {
                if (_factory is null) return field;
                field = _factory.Invoke();
                _factory = null;
            }
            return field;
        }
    }

    public LazySlim(TValue value)
    {
        _factory = null;
        Value = value;
    }

    public LazySlim(Func<TValue> factory)
    {
        _factory = factory;
        Value = default!;
    }

    public override string ToString()
        => $"{GetType().GetName()}({(_factory is null ? Value?.ToString() : "...")})";
}

public sealed class LazySlim<TArg0, TValue> : ILazySlim<TValue>
{
    private Func<TArg0, TValue>? _factory;
    private TArg0 _arg0;

    public TValue Value {
        get {
            // Double-check locking
            if (_factory is null) return field;
            lock (this) {
                if (_factory is null) return field;
                field = _factory.Invoke(_arg0);
                _factory = null;
                _arg0 = default!;
            }
            return field;
        }
    }

    public LazySlim(TValue value)
    {
        _factory = null;
        _arg0 = default!;
        Value = value;
    }

    public LazySlim(TArg0 arg0, Func<TArg0, TValue> factory)
    {
        _factory = factory;
        _arg0 = arg0;
        Value = default!;
    }

    public override string ToString()
        => $"{GetType().GetName()}({(_factory is null ? Value?.ToString() : "...")})";
}

public sealed class LazySlim<TArg0, TArg1, TValue> : ILazySlim<TValue>
{
    private Func<TArg0, TArg1, TValue>? _factory;
    private TArg0 _arg0;
    private TArg1 _arg1;

    public TValue Value {
        get {
            // Double-check locking
            if (_factory is null) return field;
            lock (this) {
                if (_factory is null) return field;
                field = _factory.Invoke(_arg0, _arg1);
                _factory = null;
                _arg0 = default!;
                _arg1 = default!;
            }
            return field;
        }
    }

    public LazySlim(TValue value)
    {
        _factory = null;
        _arg0 = default!;
        _arg1 = default!;
        Value = value;
    }

    public LazySlim(TArg0 arg0, TArg1 arg1, Func<TArg0, TArg1, TValue> factory)
    {
        _factory = factory;
        _arg0 = arg0;
        _arg1 = arg1;
        Value = default!;
    }

    public override string ToString()
        => $"{GetType().GetName()}({(_factory is null ? Value?.ToString() : "...")})";
}
