using ActualLab.Fusion.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion;

public interface IMutableState : IState, IMutableResult
{
    public new interface IOptions : IState.IOptions;
}

public interface IMutableState<T> : IState<T>, IMutableResult<T>, IMutableState;

public class MutableState<T> : State<T>, IMutableState<T>
{
    public new record Options : State<T>.Options, IMutableState.IOptions
    {
        public Options()
            => ComputedOptions = ComputedOptions.MutableStateDefault;
    }

    protected Result<T> NextOutput;

    public new T Value {
        get => base.Value;
        set => Set(Result.Value(value));
    }
    public new Exception? Error {
        get => base.Error;
        set => Set(Result.Error<T>(value!));
    }
    object? IMutableResult.UntypedValue {
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        get => Value;
        set => Set(Result.Value((T) value!));
    }

    public MutableState(Options settings, IServiceProvider services, bool initialize = true)
        : base(settings, services, false)
    {
        NextOutput = settings.InitialOutput;

        // ReSharper disable once VirtualMemberCallInConstructor
        if (initialize)
            Initialize(settings);
    }

    // Set overloads

    void IMutableResult.Set(IResult result)
        => Set(result.Cast<T>());
    public void Set(Result<T> result)
    {
        lock (Lock) {
            if (NextOutput == result)
                return;

            var snapshot = Snapshot;
            NextOutput = result;
            // We do this inside the lock by a few reasons:
            // 1. Otherwise the lock will be acquired twice -
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

    public void Set(Func<Result<T>, Result<T>> updater)
    {
        lock (Lock) {
            var snapshot = Snapshot;
            T result;
            try {
                result = updater.Invoke(snapshot.Computed.Output);
            }
            catch (Exception e) {
                result = Result.Error<T>(e);
            }
            NextOutput = result;
            snapshot.Computed.Invalidate();
        }
    }

    public void Set<TState>(TState state, Func<TState, Result<T>, Result<T>> updater)
    {
        lock (Lock) {
            var snapshot = Snapshot;
            T result;
            try {
                result = updater.Invoke(state, snapshot.Computed.Output);
            }
            catch (Exception e) {
                result = Result.Error<T>(e);
            }
            NextOutput = result;
            snapshot.Computed.Invalidate();
        }
    }

    // Protected methods

    protected internal override void OnInvalidated(Computed<T> computed)
    {
        base.OnInvalidated(computed);

        if (Snapshot.Computed != computed)
            return;

        var updateTask = computed.Update();
        if (!updateTask.IsCompleted)
            throw Errors.InternalError("Update() task must complete synchronously here.");
    }

    protected override ValueTask<Computed<T>> Invoke(
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        var computed = Computed;
        if (ComputedHelpers.TryUseExisting(computed, context))
            return ValueTaskExt.FromResult(computed);

        // Double-check locking
        lock (Lock) {
            computed = Computed;
            if (ComputedHelpers.TryUseExistingFromLock(computed, context))
                return ValueTaskExt.FromResult(computed);

            OnUpdating(computed);
            computed = CreateComputed();
            ComputedHelpers.UseNew(computed, context);
            return ValueTaskExt.FromResult(computed);
        }
    }

    protected override Task<T> InvokeAndStrip(
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        var computed = Computed;
        if (ComputedHelpers.TryUseExisting(computed, context))
            return ComputedHelpers.StripToTask(computed, context);

        // Double-check locking
        lock (Lock) {
            computed = Computed;
            if (ComputedHelpers.TryUseExistingFromLock(computed, context))
                return ComputedHelpers.StripToTask(computed, context);

            OnUpdating(computed);
            computed = CreateComputed();
            ComputedHelpers.UseNew(computed, context);
            return ComputedHelpers.StripToTask(computed, context);
        }
    }

    protected override StateBoundComputed<T> CreateComputed()
    {
        var computed = base.CreateComputed();
        computed.TrySetOutput(NextOutput);
        Computed = computed;
        return computed;
    }

    protected override Task<T> Compute(CancellationToken cancellationToken)
        => throw Errors.InternalError("This method should never be called.");
}
