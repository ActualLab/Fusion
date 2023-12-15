namespace ActualLab.Tests.CommandR.Services;

public record LogCommand : ICommand<Unit>
{
    public string Message { get; init; } = "";
}

public record LogEvent : IEventCommand
{
    public Symbol ChainId { get; init; }
    public string Message { get; init; } = "";
}

public record DivCommand : ICommand<double>
{
    public double Divisible { get; init; }
    public double Divisor { get; init; }
}

public record RecSumCommand : ICommand<double>
{
    public static readonly AsyncLocal<object> Tag = new();

    public double[] Arguments { get; init; } = Array.Empty<double>();
}

public record RecAddUsersCommand : ICommand<Unit>
{
    public User[] Users { get; init; } = Array.Empty<User>();
}

public record IncSetFailCommand : IEventCommand
{
    public Symbol ChainId { get; init; }
    public int IncrementDelay { get; init; }
    public int SetDelay { get; init; }
    public int FailDelay { get; init; }
    public long IncrementBy { get; init; }
    public long? SetValue { get; init; }
    public bool MustFail { get; init; }
}
