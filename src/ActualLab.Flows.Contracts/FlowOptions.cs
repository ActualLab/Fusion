namespace ActualLab.Flows;

public record FlowOptions
{
    public static FlowOptions Default { get; set; } = new();

    public TimeSpan KeepAliveFor { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan GracefulDisposeDelay { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RemoveDelay { get; init; } = TimeSpan.FromHours(1);
}
