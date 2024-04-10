namespace ActualLab.Resilience.Internal;

public sealed record GatingChaosMaker(ChaosMaker Next) : ChaosMaker
{
    public new bool IsEnabled { get; set; } = false;

    public override string ToString()
        => $"[{(IsEnabled ? "+" : "-")}] {Next}";

    public override Task Act(object context, CancellationToken cancellationToken)
        => IsEnabled ? Next.Act(context, cancellationToken) : Task.CompletedTask;
}
