namespace ActualLab.Fusion;

public interface IStateOptions
{
    public ComputedOptions ComputedOptions { get; init; }
    public Action<State>? EventConfigurator { get; init; }
    public string? Category { get; init; }

    public Result InitialOutput { get; init; }
    public object? InitialValue { get; init; }
}

public interface IStateOptions<T> : IStateOptions
{
    public new Result<T> InitialOutput { get; init; }
    public new T InitialValue { get; init; }
}

public abstract record StateOptions(Result InitialOutput) : IStateOptions
{
    public ComputedOptions ComputedOptions { get; init; } = ComputedOptions.Default;
    public Action<State>? EventConfigurator { get; init; }
    public string? Category { get; init; }

    public object? InitialValue {
        get => InitialOutput.Value;
        init => InitialOutput = new Result(value, null);
    }
}

public record StateOptions<T>() : StateOptions(Computed<T>.DefaultResult), IStateOptions<T>
{
    public new Result<T> InitialOutput {
        get => base.InitialOutput.ToTypedResult<T>();
        init => base.InitialOutput = value.ToUntypedResult();
    }

    public new T InitialValue {
        get => (T)base.InitialOutput.Value!;
        init => base.InitialOutput = new Result(value, null);
    }
}
