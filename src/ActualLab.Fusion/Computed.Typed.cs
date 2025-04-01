using ActualLab.Conversion;

namespace ActualLab.Fusion;

public abstract class Computed<T> : Computed, IResult<T>
{
    public new Result<T> Output => base.Output.ToTypedResult<T>();
    public new T Value => Output.Value;
    public T? ValueOrDefault => Output.ValueOrDefault;

    // Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input)
        : base(options, input)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input, Result<T> output, bool isConsistent)
        : base(options, input, output.ToUntypedResult())
        => ConsistencyState = isConsistent ? ConsistencyState.Consistent : ConsistencyState.Invalidated;

    public new Task<T> GetValuePromise()
        => (Task<T>)base.GetValuePromise();

    // IResult<T> implementation

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
        => Output.Deconstruct(out value, out error);

    public void Deconstruct(out T value, out Exception? error, out ulong version)
    {
        Output.Deconstruct(out value, out error);
        version = Version;
    }

    T IConvertibleTo<T>.Convert() => Value;
}
