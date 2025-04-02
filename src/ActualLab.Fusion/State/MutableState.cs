using System.Diagnostics.CodeAnalysis;
using ActualLab.Conversion;
using ActualLab.Fusion.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion;

// Interfaces

public interface IMutableStateOptions : IStateOptions;
public interface IMutableState : IState, IMutableResult;
public interface IMutableState<T> : IState<T>, IMutableResult<T>, IMutableState;

// Classes

public abstract class MutableState : State, IMutableState
{
    protected Result NextOutput;

    public new object? Value {
        get => base.Value;
        set => Set(Result.NewUntyped(value));
    }

    public new Exception? Error {
        get => base.Error;
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume all used constructors are preserved")]
        set {
            var result = value == null
                ? Result.NewUntyped(OutputType.GetDefaultValue(), null)
                : Result.NewUntypedError(value!);
            Set(result);
        }
    }

    protected MutableState(IMutableStateOptions options, IServiceProvider services, bool initialize = true)
        : base(options, services, false)
    {
        NextOutput = options.InitialOutput;

        // ReSharper disable once VirtualMemberCallInConstructor
        if (initialize)
            Initialize(options);
    }

    // Set overloads

    public void Set(Result result)
    {
        lock (Lock) {
            if (NextOutput == result)
                return;

            var snapshot = Snapshot;
            NextOutput = result;
            // We do this inside the lock by a few reasons:
            // 1. Otherwise, the lock will be acquired twice -
            //    see OnInvalidated & Invoke overloads below.
            // 2. It's quite convenient if Set, while being
            //    non-async, synchronously updates the mutable
            //    state.
            // 3. If all the updates are synchronous, we don't
            //    need async lock that's used by regular
            //    IComputed instances.
            snapshot.Computed.Invalidate();
        }
    }

    // Protected methods

    protected internal override void OnInvalidated(Computed computed)
    {
        base.OnInvalidated(computed);

        if (Snapshot.Computed != computed)
            return;

        var updateTask = computed.UpdateUntyped();
        if (!updateTask.IsCompleted)
            throw Errors.InternalError("Update() task must complete synchronously here.");
    }

    protected override Task<Computed> ProduceComputed(ComputeContext context, CancellationToken cancellationToken = default)
    {
        // The same logic as in base method, but relying on lock instead of AsyncLock
        lock (Lock) {
            var computed = UntypedComputed;
            if (ComputedImpl.TryUseExistingFromLock(computed, context))
                return Task.FromResult(computed);

            OnUpdating(computed);
            computed = CreateComputed();
            ComputedImpl.UseNew(computed, context);
            return Task.FromResult(computed);
        }
    }

    protected override Task Compute(CancellationToken cancellationToken)
        => throw Errors.InternalError("This method should never be called.");
}

public class MutableState<T> : MutableState, IMutableState<T>
{
    public record Options : StateOptions<T>, IMutableStateOptions
    {
        public Options()
            => ComputedOptions = ComputedOptions.MutableStateDefault;
    }

    public override Type OutputType => typeof(T);

    // IState<T> implementation
    public new Computed<T> Computed {
        get => (Computed<T>)UntypedComputed;
        protected set => UntypedComputed = value;
    }

    public T? ValueOrDefault => Computed.ValueOrDefault;
    public new T LastNonErrorValue => ((Computed<T>)Snapshot.LastNonErrorComputed).Value;

    public new T Value {
        get => Computed.Value;
        set => Set(Result.NewUntyped(value));
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    public MutableState(Options options, IServiceProvider services, bool initialize = true)
        : base(options, services, initialize)
    { }

    // IResult<T> implementation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
        => Computed.Deconstruct(out value, out error);
    T IConvertibleTo<T>.Convert() => Value;

    public void Set(Result<T> result)
        => Set(result.ToUntypedResult());

    public void Set(Func<Result<T>, Result<T>> updater, bool throwOnError = false)
    {
        lock (Lock) {
            var snapshot = Snapshot;
            Result<T> result;
            try {
                result = updater.Invoke(((Computed<T>)snapshot.Computed).Output);
            }
            catch (Exception e) when (!throwOnError) {
                result = Result.NewError<T>(e);
            }
            NextOutput = result.ToUntypedResult();
            snapshot.Computed.Invalidate();
        }
    }

    public void Set<TState>(TState state, Func<TState, Result<T>, Result<T>> updater, bool throwOnError = false)
    {
        lock (Lock) {
            var snapshot = Snapshot;
            Result<T> result;
            try {
                result = updater.Invoke(state, ((Computed<T>)snapshot.Computed).Output);
            }
            catch (Exception e) when (!throwOnError) {
                result = Result.NewError<T>(e);
            }
            NextOutput = result.ToUntypedResult();
            snapshot.Computed.Invalidate();
        }
    }

    // Protected methods

    protected override Computed CreateComputed()
    {
        var computed = new StateBoundComputed<T>(ComputedOptions, this);
        computed.TrySetOutput(NextOutput);
        UntypedComputed = computed;
        return computed;
    }
}
