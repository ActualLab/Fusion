using ActualLab.Conversion;

namespace ActualLab.Fusion.Internal;

internal abstract class TypedStateTemplate<T> : State, IResult<T>
{
    public override Type OutputType => typeof(T);

    // IState<T> implementation
    public new Computed<T> Computed {
        get => (Computed<T>)UntypedComputed;
        protected set => UntypedComputed = value;
    }
    public T? ValueOrDefault => Computed.ValueOrDefault;
    public new T Value => Computed.Value;
    public new T LastNonErrorValue => ((Computed<T>)Snapshot.LastNonErrorComputed).Value;

    // ReSharper disable once ConvertToPrimaryConstructor
    protected TypedStateTemplate(StateOptions options, IServiceProvider services, bool initialize = true)
        : base(options, services, initialize)
    { }

    // IResult<T> implementation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
        => Computed.Deconstruct(out value, out error);
    T IConvertibleTo<T>.Convert() => Value;

    // Protected methods

    protected override Computed CreateComputed()
        => new StateBoundComputed<T>(ComputedOptions, this);
}
