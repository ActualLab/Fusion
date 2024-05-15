using ActualLab.Flows.Infrastructure;
using ActualLab.Internal;
using ActualLab.Versioning;

namespace ActualLab.Flows;

public abstract class Flow : IHasId<FlowId>, IHasId<Symbol>, IHasId<string>
{
    Symbol IHasId<Symbol>.Id => Id.Id;
    string IHasId<string>.Id => Id.Value;

    [IgnoreDataMember, MemoryPackIgnore]
    public FlowId Id { get; private set; }
    [IgnoreDataMember, MemoryPackIgnore]
    public long Version { get; private set; }
    [IgnoreDataMember, MemoryPackIgnore]
    public RunningFlow? Runner { get; private set; }

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public Symbol NextStep { get; internal set; }

    // Computed
    [IgnoreDataMember, MemoryPackIgnore]
    protected ILogger? Log => Runner?.Log;

    public void Initialize(FlowId id, long version, RunningFlow? runner = null)
    {
        Id = id;
        Version = version;
        Runner = runner;
    }

    public override string ToString()
        => $"{GetType().Name}('{Id.Value}' @ {NextStep}, v.{Version.FormatVersion()})";

    public virtual Flow Clone()
        => MemberwiseCloner.Invoke(this);

    public virtual async Task MoveNext(object? @event, CancellationToken cancellationToken)
    {
        try {
            await FlowSteps.Invoke(this, NextStep, @event, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var task = FlowSteps.Invoke(this, FlowSteps.Error, e, cancellationToken);
            if (task is Task<bool> isHandledTask) {
                var isHandled = await isHandledTask.ConfigureAwait(false);
                if (isHandled)
                    return;
            }
            else
                await task.ConfigureAwait(false);
            throw;
        }
    }

    // Default options

    public virtual FlowOptions GetOptions()
        => FlowOptions.Default;

    // Default steps

    protected abstract Task Start(object? @event, CancellationToken cancellationToken);

    protected virtual Task NoStep(object? @event, CancellationToken cancellationToken)
        => throw Internal.Errors.NoStep(GetType(), NextStep);

    protected virtual Task UnsupportedEvent(object? @event, CancellationToken cancellationToken)
        => throw Internal.Errors.UnsupportedEvent(GetType(), NextStep, @event?.GetType() ?? typeof(object));

    protected virtual Task<bool> Error(Exception error, CancellationToken cancellationToken)
        => TaskExt.FalseTask;

    // Helpers

    public static void RequireFlowType(Type flowType)
    {
        if (!typeof(Flow).IsAssignableFrom(flowType))
            throw Errors.MustBeAssignableTo<Flow>(flowType);
    }
}
