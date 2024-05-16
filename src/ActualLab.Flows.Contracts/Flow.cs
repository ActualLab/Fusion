using ActualLab.CommandR;
using ActualLab.CommandR.Operations;
using ActualLab.Flows.Infrastructure;
using ActualLab.Internal;
using ActualLab.Versioning;

namespace ActualLab.Flows;

public abstract class Flow : IHasId<FlowId>, IWorkerFlow
{
    private FlowWorker? _worker;

    [IgnoreDataMember, MemoryPackIgnore]
    public FlowId Id { get; private set; }
    [IgnoreDataMember, MemoryPackIgnore]
    public long Version { get; private set; }

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public Symbol Step { get; private set; } = FlowSteps.OnStart;

    // Computed
    [IgnoreDataMember, MemoryPackIgnore]
    protected FlowHost Host => Worker.Host;
    [IgnoreDataMember, MemoryPackIgnore]
    protected FlowWorker Worker => RequireWorker();
    [IgnoreDataMember, MemoryPackIgnore]
    protected FlowEventSource Event { get; private set; }

    public void Initialize(FlowId id, long version, FlowWorker? worker = null)
    {
        Id = id;
        Version = version;
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
            await Apply(transition, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Step = step;
            Event = new FlowEventSource(this, e);
            transition = await FlowSteps.Invoke(this, FlowSteps.OnError, cancellationToken).ConfigureAwait(false);
            if (transition.Step == FlowSteps.OnError)
                throw;

            await Apply(transition, cancellationToken).ConfigureAwait(false);
        }
        finally {
            Event = default;
        }
        return transition;
    }

    // Default options

    public virtual FlowOptions GetOptions()
        => FlowOptions.Default;

    // Default steps

    protected abstract Task<FlowTransition> OnStart(CancellationToken cancellationToken);

    protected virtual Task<FlowTransition> OnEnd(CancellationToken cancellationToken)
    {
        var removeDelay = GetOptions().RemoveDelay;
        return Task.FromResult(removeDelay <= TimeSpan.Zero
            ? Goto(FlowSteps.MustRemove)
            : Goto(nameof(OnEndRemove)).AddTimerEvent(removeDelay));
    }

    protected virtual Task<FlowTransition> OnEndRemove(CancellationToken cancellationToken)
    {
        Event.MarkUsed();
        return Task.FromResult(Goto(FlowSteps.MustRemove));
    }

    protected virtual Task<FlowTransition> OnMissingStep(CancellationToken cancellationToken)
        => throw Internal.Errors.NoStepImplementation(GetType(), Step);

    protected virtual Task<FlowTransition> OnError(CancellationToken cancellationToken)
        => Task.FromResult(Goto(nameof(OnError)) with { MustSave = false });

    // Transition helpers

    protected FlowTransition Goto(Symbol step)
    {
        RequireWorker();
        return new FlowTransition(this, step);
    }

    protected Task Save(CancellationToken cancellationToken = default)
        => Save(null, cancellationToken);
    protected async Task Save(Action<Operation>? eventBuilder, CancellationToken cancellationToken = default)
    {
        RequireWorker();
        var saveCommand = new Flows_Save(this, Version) {
            EventBuilder = eventBuilder,
        };
        Version = await Worker.Host.Commander.Call(saveCommand, cancellationToken).ConfigureAwait(false);
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

    private Task Apply(FlowTransition transition, CancellationToken cancellationToken)
    {
        Step = transition.Step;
        return transition.MustSave
            ? Save(transition.EventBuilder, cancellationToken)
            : Task.CompletedTask;
    }
}
