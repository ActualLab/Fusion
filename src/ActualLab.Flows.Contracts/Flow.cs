using ActualLab.CommandR;
using ActualLab.Flows.Infrastructure;
using ActualLab.Internal;
using ActualLab.Versioning;

namespace ActualLab.Flows;

public abstract class Flow : IHasId<FlowId>, IWorkerFlow
{
    private FlowWorker? _worker;

    // Persisted to the DB directly
    [IgnoreDataMember, MemoryPackIgnore]
    public FlowId Id { get; private set; }
    [IgnoreDataMember, MemoryPackIgnore]
    public long Version { get; private set; }
    [IgnoreDataMember, MemoryPackIgnore]
    public Symbol Step { get; private set; } = FlowSteps.OnStart;

    // IWorkerFlow properties (shouldn't be persisted)
    [IgnoreDataMember, MemoryPackIgnore]
    protected FlowHost Host => Worker.Host;
    [IgnoreDataMember, MemoryPackIgnore]
    protected FlowWorker Worker => RequireWorker();
    [IgnoreDataMember, MemoryPackIgnore]
    protected FlowEventSource Event { get; private set; }

    public void Initialize(FlowId id, long version, Symbol step, FlowWorker? worker = null)
    {
        Id = id;
        Version = version;
        Step = step;
        _worker = worker;
    }

    public override string ToString()
        => $"{GetType().Name}('{Id.Value}' @ {Step}, v.{Version.FormatVersion()})";

    public virtual Flow Clone()
        => MemberwiseCloner.Invoke(this);

    public virtual async Task<FlowTransition> HandleEvent(object? evt, CancellationToken cancellationToken)
    {
        RequireWorker();
        var step = Step;
        Event = new FlowEventSource(this, evt);
        FlowTransition transition;
        try {
            transition = await FlowSteps.Invoke(this, step, cancellationToken).ConfigureAwait(false);
            if (!Event.IsUsed)
                Worker.Log.LogWarning(
                    "Flow '{FlowType}' ignored event {Event} on step '{Step}'",
                    GetType().Name, Event.Event, step);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            if (FlowSteps.Get(GetType(), FlowSteps.OnRecover) is not { } resetStep)
                throw;

            Step = step;
            Event = new FlowEventSource(this, e);
            transition = await resetStep.Invoke(this, cancellationToken).ConfigureAwait(false);
        }
        finally {
            Event = default;
        }
        await ApplyTransition(transition, cancellationToken).ConfigureAwait(false);
        return transition;
    }

    // Default options

    public virtual FlowOptions GetOptions()
        => FlowOptions.Default;

    // Default steps

    protected abstract Task<FlowTransition> OnStart(CancellationToken cancellationToken);

    protected virtual Task<FlowTransition> OnEnd(CancellationToken cancellationToken)
    {
        Event.MarkUsed();
        var removeDelay = GetOptions().RemoveDelay;
        return Task.FromResult(removeDelay <= TimeSpan.Zero
            ? Jump(FlowSteps.MustRemove)
            : Wait(nameof(OnRemove)).AddTimerEvent(removeDelay));
    }

    protected virtual Task<FlowTransition> OnRemove(CancellationToken cancellationToken)
    {
        Event.MarkUsed();
        return Task.FromResult(Jump(FlowSteps.MustRemove));
    }

    protected virtual Task<FlowTransition> OnMissingStep(CancellationToken cancellationToken)
        => throw Internal.Errors.NoStepImplementation(GetType(), Step);

    // protected virtual Task<FlowTransition> OnRecover(CancellationToken cancellationToken);

    // Transition helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected FlowTransition Wait(Symbol step, bool mustStore = true)
        => new(this, step) { MustStore = mustStore };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected FlowTransition Jump(Symbol step, bool mustStore = false)
        => new(this, step) { MustStore = mustStore, MustWait = false };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected FlowTransition JumpToEnd()
        => Jump(nameof(OnEnd));

    protected virtual async ValueTask ApplyTransition(FlowTransition transition, CancellationToken cancellationToken)
    {
        RequireWorker();
        Step = transition.Step;
        if (!transition.EffectiveMustStore)
            return;

        var storeCommand = new Flows_Store(Id, Version) {
            Flow = Clone(),
            EventBuilder = transition.EventBuilder,
        };
        Version = await Worker.Host.Commander.Call(storeCommand, cancellationToken).ConfigureAwait(false);
    }

    // IFlowImpl

    FlowHost IWorkerFlow.Host => Worker.Host;
    FlowWorker IWorkerFlow.Worker => Worker;
    FlowEventSource IWorkerFlow.Event => Event;

    // Other helpers

    public static void RequireCorrectType(Type flowType)
    {
        if (!typeof(Flow).IsAssignableFrom(flowType))
            throw Errors.MustBeAssignableTo<Flow>(flowType);
    }

    private FlowWorker RequireWorker()
    {
        if (_worker == null)
            throw Errors.NotInitialized(nameof(Worker));

        return _worker;
    }
}
