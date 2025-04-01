using ActualLab.Conversion;
using ActualLab.Internal;

namespace ActualLab.Fusion;

#pragma warning disable MA0095 // Should override Equals

public abstract class State<T> : State, IResult<T>, IEquatable<State<T>>
{
    public new record Options : State.Options
    {
        public new Result<T> InitialOutput {
            get => base.InitialOutput.ToTypedResult<T>();
            init => base.InitialOutput = value.ToUntypedResult();
        }

        public new T InitialValue {
            get => (T)base.InitialOutput.Value!;
            init => base.InitialOutput = new Result(value, null);
        }
    }

    public override Type OutputType => typeof(T);

    public new StateSnapshot<T> Snapshot
        => (StateSnapshot<T>)base.Snapshot;

    public new Computed<T> Computed {
        get => (Computed<T>)UntypedComputed;
        protected set => UntypedComputed = value;
    }

    public T? ValueOrDefault => Computed.ValueOrDefault;
    public new T Value => Computed.Value;
    public new T LastNonErrorValue => Snapshot.LastNonErrorComputed.Value;

    // ReSharper disable once ConvertToPrimaryConstructor
    protected State(Options settings, IServiceProvider services, bool initialize = true)
        : base(settings, services, initialize)
    { }

    // IResult<T> implementation

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
        => Computed.Deconstruct(out value, out error);
    T IConvertibleTo<T>.Convert() => Value;

    // Equality

    public bool Equals(State<T>? other)
        => ReferenceEquals(this, other);

    // Protected methods

    protected override StateSnapshot CreateSnapshot(State state, StateSnapshot? prevSnapshot, Computed computed)
        => new StateSnapshot<T>((State<T>)state, (StateSnapshot<T>?)prevSnapshot, (Computed<T>)computed);

    protected override Computed CreateComputed()
        => new StateBoundComputed<T>(ComputedOptions, this);

    protected override object? ExtractComputeTaskResult(Task computeTask)
    {
        if (computeTask.IsCompletedSuccessfully())
            return ((Task<T>)computeTask).GetAwaiter().GetResult();

        if (computeTask.IsFaulted || computeTask.IsCanceled) {
            computeTask.GetAwaiter().GetResult();
            throw Errors.TaskAwaiterMustThrow();
        }

        throw Errors.TaskIsNotCompleted();
    }
}
