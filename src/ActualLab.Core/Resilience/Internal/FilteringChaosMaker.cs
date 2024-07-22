namespace ActualLab.Resilience.Internal;

public record FilteringChaosMaker(string Description, Func<object, bool> Filter, ChaosMaker Target) : ChaosMaker
{
    public override string ToString()
        => $"\"{Description}\" ? {Target}";

    public override Task Act(object context, CancellationToken cancellationToken)
    {
        return Filter.Invoke(context)
            ? Target.Act(context, cancellationToken)
            : Task.CompletedTask;
    }
}
