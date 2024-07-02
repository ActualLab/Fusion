using System.Diagnostics.CodeAnalysis;
using ActualLab.Conversion;
using ActualLab.Fusion.Internal;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Versioning;
using Errors = ActualLab.Fusion.Internal.Errors;

namespace ActualLab.Fusion;

public abstract class Computed<T> : Computed, IResult<T>
{
    private Result<T> _output;
    private Task<T>? _outputAsTask;

    // IComputed properties

    public IComputeFunction<T> Function => (IComputeFunction<T>)Input.Function;

    public sealed override Type OutputType {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(T);
    }

    public new Result<T> Output {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            this.AssertConsistencyStateIsNot(ConsistencyState.Computing);
            return _output;
        }
    }

    public Task<T> OutputAsTask {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if (_outputAsTask != null)
                return _outputAsTask;

            lock (Lock) {
                this.AssertConsistencyStateIsNot(ConsistencyState.Computing);
                return _outputAsTask ??= _output.AsTask();
            }
        }
    }

    // IResult<T> properties
    public T? ValueOrDefault => Output.ValueOrDefault;
    public T Value => Output.Value;
    public sealed override Exception? Error => Output.Error;
    public sealed override bool HasValue => Output.HasValue;
    public sealed override bool HasError => Output.HasError;
    public sealed override object? UntypedValue => Output.Value;
    public sealed override Result<TOther> Cast<TOther>()
        => Output.Cast<TOther>();

    // IResult<T> methods

    public bool IsValue([MaybeNullWhen(false)] out T value)
        => Output.IsValue(out value);
    public bool IsValue([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out Exception error)
        => Output.IsValue(out value, out error!);
    public Result<T> AsResult()
        => Output.AsResult();
    T IConvertibleTo<T>.Convert() => Value;
    Result<T> IConvertibleTo<Result<T>>.Convert() => AsResult();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input)
        : base(options, input)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input, Result<T> output, bool isConsistent)
        : base(options, input)
    {
        ConsistencyState = isConsistent ? ConsistencyState.Consistent : ConsistencyState.Invalidated;
        _output = output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
        => Output.Deconstruct(out value, out error);

    public void Deconstruct(out T value, out Exception? error, out ulong version)
    {
        Output.Deconstruct(out value, out error);
        version = Version;
    }

    public override string ToString()
        => $"{GetType().GetName()}({Input} v.{Version.FormatVersion()}, State: {ConsistencyState})";

    // GetHashCode

    public sealed override int GetHashCode()
        => (int)Version;

    // Update & use

    public sealed override async ValueTask<Computed> UpdateUntyped(CancellationToken cancellationToken = default)
        => await Update(cancellationToken).ConfigureAwait(false);

    public async ValueTask<Computed<T>> Update(CancellationToken cancellationToken = default)
    {
        if (this.IsConsistent())
            return this;

        using var scope = Computed.BeginIsolation();
        return await Function.Invoke(Input, scope.Context, cancellationToken).ConfigureAwait(false);
    }

    public sealed override async ValueTask UseUntyped(CancellationToken cancellationToken = default)
        => await Use(cancellationToken).ConfigureAwait(false);

    public async ValueTask<T> Use(CancellationToken cancellationToken = default)
    {
        var context = ComputeContext.Current;
        if ((context.CallOptions & CallOptions.GetExisting) != 0) // Both GetExisting & Invalidate
            throw Errors.InvalidContextCallOptions(context.CallOptions);

        // Slightly faster version of this.TryUseExistingFromLock(context)
        if (this.IsConsistent()) {
            // It can become inconsistent here, but we don't care, since...
            ComputedImpl.UseNew(this, context);
            // it can also become inconsistent here & later, and UseNew handles this.
            // So overall, Use(...) guarantees the dependency chain will be there even
            // if computed is invalidated right after above "if".
            return Value;
        }

        var computed = await Function.Invoke(Input, context, cancellationToken).ConfigureAwait(false);
        return computed.Value;
    }

    // Apply

    public override TResult Apply<TArg, TResult>(IComputedApplyHandler<TArg, TResult> handler, TArg arg)
        => handler.Apply(this, arg);

    // Protected internal methods - you can call them via ComputedImpl

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected internal bool TrySetOutput(Result<T> output)
    {
        ComputedFlags flags;
        lock (Lock) {
            if (ConsistencyState != ConsistencyState.Computing)
                return false;

            ConsistencyState = ConsistencyState.Consistent;
            _output = output;
            flags = Flags;
        }

        if ((flags & ComputedFlags.InvalidateOnSetOutput) != 0) {
            Invalidate((flags & ComputedFlags.InvalidateOnSetOutputImmediately) != 0);
            return true;
        }

        StartAutoInvalidation();
        return true;
    }
}
