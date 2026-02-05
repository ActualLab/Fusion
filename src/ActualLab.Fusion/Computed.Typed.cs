using ActualLab.Conversion;
using ActualLab.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// A strongly-typed <see cref="Computed"/> that holds a <see cref="Result{T}"/> output value.
/// </summary>
/// <typeparam name="T">The type of the computed value.</typeparam>
public abstract class Computed<T> : Computed, IResult<T>
{
    public static readonly Result DefaultResult = new(default(T));

    public new Result<T> Output => UntypedOutput.ToTypedResult<T>();
    public new T Value => (T)UntypedOutput.Value!;
    public T? ValueOrDefault {
        get {
            var output = UntypedOutput.GetUntypedValueOrErrorBox();
            return output is ErrorBox ? default : (T)output!;
        }
    }

    // Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input)
        : base(options, input, DefaultResult)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input, Result output, bool isConsistent)
        : base(options, input, output, isConsistent)
    { }

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

    // Protected methods

    protected override Task CreateValuePromise()
    {
        var (value, error) = UntypedOutput;
        return error is null
            ? Task.FromResult((T)value!)
            : Task.FromException<T>(error);
    }
}
