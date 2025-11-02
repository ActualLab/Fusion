using ActualLab.Conversion;
using ActualLab.Fusion.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion;

// Interfaces

public interface IMutableStateOptions : IStateOptions;

public interface IMutableState : IState
{
    // Set(...) from IMutableResult, but with InvalidationSource argument

    public void Set(Result result,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0);

    public void Set(Result result, InvalidationSource source);

    // SetError(...) from IMutableResult, but with InvalidationSource argument

    public void SetError(Exception error,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0);

    public void SetError(Exception error, InvalidationSource source);
}

public interface IMutableState<T> : IState<T>, IMutableState
{
    // Set(...) from IMutableResult<T>, but with InvalidationSource argument

    public void Set(Result<T> result,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0);

    public void Set(Result<T> result, InvalidationSource source);

    public void Set(Func<Result<T>, Result<T>> updater, bool throwOnError = false,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0);

    public void Set(Func<Result<T>, Result<T>> updater, bool throwOnError, InvalidationSource source);

    public void Set<TState>(TState state, Func<TState, Result<T>, Result<T>> updater, bool throwOnError = false,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0);

    public void Set<TState>(TState state, Func<TState, Result<T>, Result<T>> updater, bool throwOnError, InvalidationSource source);
}

// Classes

public abstract class MutableState : State, IMutableState, IMutableResult
{
    protected Result NextOutput;

    protected MutableState(IMutableStateOptions options, IServiceProvider services, bool initialize = true)
        : base(options, services, initialize: false)
    {
        NextOutput = options.InitialOutput;

        // ReSharper disable once VirtualMemberCallInConstructor
        if (initialize)
            Initialize(options);
    }

    // IMutableResult implementation
    void IMutableResult.Set(Result result)
        => Set(result, InvalidationSource.ForCurrentLocation());
    void IMutableResult.SetError(Exception error)
        => Set(new Result(null, error), InvalidationSource.ForCurrentLocation());

    // Set overloads

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(Result result,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => Set(result, new InvalidationSource(file, member, line));

    public void Set(Result result, InvalidationSource source)
    {
        lock (Lock) {
            if (NextOutput == result)
                return;

            var snapshot = Snapshot;
            NextOutput = result;
            // We do this inside the lock for a few reasons:
            // 1. Otherwise, the lock will be acquired twice -
            //    see OnInvalidated & Invoke overloads below.
            // 2. It's quite convenient if Set, while being
            //    non-async, synchronously updates the mutable
            //    state.
            // 3. If all the updates are synchronous, we don't
            //    need async lock that's used by regular
            //    IComputed instances.
            snapshot.Computed.Invalidate(source);
        }
    }

    // SetError overloads

    public void SetError(Exception error,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => Set(new Result(null, error), new InvalidationSource(file, member, line));

    public void SetError(Exception error, InvalidationSource source)
        => Set(new Result(null, error), source);

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
        // The same logic as in the base method, but relying on lock instead of AsyncLock
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

public class MutableState<T> : MutableState, IMutableState<T>, IMutableResult<T>
{
    public record Options : StateOptions<T>, IMutableStateOptions
    {
        public Options()
            => ComputedOptions = ComputedOptions.MutableStateDefault;
    }

    public override Type OutputType => typeof(T);

    // IState<T> implementation
    public new Computed<T> Computed => Unsafe.As<Computed<T>>(UntypedComputed);

    public T? ValueOrDefault => Computed.ValueOrDefault;
    public new T LastNonErrorValue => Unsafe.As<Computed<T>>(Snapshot.LastNonErrorComputed).Value;
    public new T Value => Computed.Value;

    // ReSharper disable once ConvertToPrimaryConstructor
    public MutableState(Options options, IServiceProvider services, bool initialize = true)
        : base(options, services, initialize)
    { }

    // IResult<T> implementation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
        => Computed.Deconstruct(out value, out error);
    T IConvertibleTo<T>.Convert() => Value;

    // IMutableResult<T> implementation
    void IMutableResult<T>.Set(Result<T> result)
        => Set(result.ToUntypedResult(), InvalidationSource.ForCurrentLocation());
    void IMutableResult<T>.Set(Func<Result<T>, Result<T>> updater, bool throwOnError)
        => Set(updater, throwOnError, InvalidationSource.ForCurrentLocation());
    void IMutableResult<T>.Set<TState>(TState state, Func<TState, Result<T>, Result<T>> updater, bool throwOnError)
        => Set(state, updater, throwOnError, InvalidationSource.ForCurrentLocation());

    // Set(...) overloads

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(Result<T> result,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => Set(result.ToUntypedResult(), new InvalidationSource(file, member, line));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(Result<T> result, InvalidationSource source)
        => Set(result.ToUntypedResult(), source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(Func<Result<T>, Result<T>> updater, bool throwOnError = false,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => Set(updater, throwOnError, new InvalidationSource(file, member, line));

    public void Set(Func<Result<T>, Result<T>> updater, bool throwOnError, InvalidationSource source)
    {
        lock (Lock) {
            var snapshot = Snapshot;
            Result<T> result;
            try {
                result = updater.Invoke(Unsafe.As<Computed<T>>(snapshot.Computed).Output);
            }
            catch (Exception e) when (!throwOnError) {
                result = Result.NewError<T>(e);
            }
            NextOutput = result.ToUntypedResult();
            snapshot.Computed.Invalidate(source);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set<TState>(TState state, Func<TState, Result<T>, Result<T>> updater, bool throwOnError = false,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => Set(state, updater, throwOnError, new InvalidationSource(file, member, line));

    public void Set<TState>(TState state, Func<TState, Result<T>, Result<T>> updater, bool throwOnError, InvalidationSource source)
    {
        lock (Lock) {
            var snapshot = Snapshot;
            Result<T> result;
            try {
                result = updater.Invoke(state, Unsafe.As<Computed<T>>(snapshot.Computed).Output);
            }
            catch (Exception e) when (!throwOnError) {
                result = Result.NewError<T>(e);
            }
            NextOutput = result.ToUntypedResult();
            snapshot.Computed.Invalidate(source);
        }
    }

    // Useful helpers

    public bool IsInitial(out T value)
    {
        var snapshot = Snapshot;
        value = Unsafe.As<Computed<T>>(snapshot.LastNonErrorComputed).Value;
        return snapshot.IsInitial;
    }

    public bool IsInitial(out T value, out Exception? error)
    {
        var snapshot = Snapshot;
        value = Unsafe.As<Computed<T>>(snapshot.LastNonErrorComputed).Value;
        error = snapshot.Computed.Error;
        return snapshot.IsInitial;
    }

    // Protected methods

    protected override Computed CreateComputed()
    {
        var computed = new StateBoundComputed<T>(ComputedOptions, this);
        computed.TrySetOutput(NextOutput);
        SetComputed(computed, InvalidationSource.MutableStateCreateComputed);
        return computed;
    }
}
