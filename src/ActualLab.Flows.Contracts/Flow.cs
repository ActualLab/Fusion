using ActualLab.CommandR;
using ActualLab.Flows.Infrastructure;
using ActualLab.Internal;
using ActualLab.Versioning;

namespace ActualLab.Flows;

public abstract class Flow : IHasId<FlowId>, IHasId<Symbol>, IHasId<string>
{
    Symbol IHasId<Symbol>.Id => Id.Id;
    string IHasId<string>.Id => Id.Value;

    [IgnoreDataMember, MemoryPackIgnore]
    protected FlowWorker? Worker { get; private set; }
    [IgnoreDataMember, MemoryPackIgnore]
    protected object? Event { get; private set; }
    [IgnoreDataMember, MemoryPackIgnore]
    protected Func<CommandContext, CancellationToken, Task>? OperationBuilder { get; set; }

    [IgnoreDataMember, MemoryPackIgnore]
    public FlowId Id { get; private set; }
    [IgnoreDataMember, MemoryPackIgnore]
    public long Version { get; private set; }

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public Symbol Step { get; private init; } = FlowSteps.OnStart;

    // Computed
    [IgnoreDataMember, MemoryPackIgnore]
    protected ILogger? Log => Worker?.Log;

    public void Initialize(FlowId id, long version, FlowWorker? worker = null)
    {
        Id = id;
        Version = version;
        Worker = worker;
    }

    public override string ToString()
        => $"{GetType().Name}('{Id.Value}' @ {Step}, v.{Version.FormatVersion()})";

    public virtual Flow Clone()
        => MemberwiseCloner.Invoke(this);

    public virtual async Task<FlowTransition> MoveNext(object? @event, CancellationToken cancellationToken)
    {
        Worker.Require();
        Event = @event;
        OperationBuilder = null;
        FlowTransition result;
        try {
            result = await FlowSteps.Invoke(this, Step, cancellationToken).ConfigureAwait(false);
            if (result.MustSave)
                await Save(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Event = e;
            OperationBuilder = null;
            result = await FlowSteps.Invoke(this, FlowSteps.OnError, cancellationToken).ConfigureAwait(false);
            if (result.Step == FlowSteps.OnError)
                throw;

            if (result.MustSave)
                await Save(cancellationToken).ConfigureAwait(false);
        }
        finally {
            Event = null;
            OperationBuilder = null;
        }
        return result;
    }

    // Default options

    public virtual FlowOptions GetOptions()
        => FlowOptions.Default;

    // Default steps

    protected abstract Task<FlowTransition> OnStart(CancellationToken cancellationToken);

    protected virtual Task<FlowTransition> OnMissingStep(CancellationToken cancellationToken)
        => throw Internal.Errors.StepNotFound(GetType(), Step);

    protected virtual Task<FlowTransition> OnError(CancellationToken cancellationToken)
        => Task.FromResult(Goto(nameof(OnError), false));

    // Transition helpers

    protected void AddOperationBuilder(Func<CommandContext, CancellationToken, Task> operationBuilder)
    {
        Worker.Require();
        var oldOperationBuilder = OperationBuilder;
        OperationBuilder = async (context, ct) => {
            if (oldOperationBuilder != null)
                await oldOperationBuilder.Invoke(context, ct).ConfigureAwait(false);
            await operationBuilder.Invoke(context, ct).ConfigureAwait(false);
        };
    }

    protected FlowTransition Goto(Symbol step, bool mustCommit = true, bool mustWaitForEvent = true)
    {
        Worker.Require();
        return new FlowTransition(step, mustCommit, mustWaitForEvent);
    }

    protected Task Save(CancellationToken cancellationToken)
        => Save(false, cancellationToken);
    protected async Task Save(bool ignoreVersion, CancellationToken cancellationToken)
    {
        Worker.Require();
        var saveCommand = new Flows_Save(this, ignoreVersion ? null : Version) {
            OperationBuilder = OperationBuilder,
        };
        Version = await Worker.Host.Commander.Call(saveCommand, cancellationToken).ConfigureAwait(false);
    }

    // Other helpers

    public static void RequireCorrectType(Type flowType)
    {
        if (!typeof(Flow).IsAssignableFrom(flowType))
            throw Errors.MustBeAssignableTo<Flow>(flowType);
    }
}
