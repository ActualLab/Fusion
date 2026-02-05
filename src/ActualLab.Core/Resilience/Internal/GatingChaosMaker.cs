namespace ActualLab.Resilience.Internal;

/// <summary>
/// A <see cref="ChaosMaker"/> that can be enabled or disabled at runtime.
/// </summary>
public sealed record GatingChaosMaker(ChaosMaker Next) : ChaosMaker
{
    public new bool IsEnabled { get; set; } = false;

    public override string ToString()
        => $"[{(IsEnabled ? "+" : "-")}] {Next}";

    public override Task Act(object context, CancellationToken cancellationToken)
        => IsEnabled ? Next.Act(context, cancellationToken) : Task.CompletedTask;
}
