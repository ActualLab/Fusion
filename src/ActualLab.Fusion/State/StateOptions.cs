namespace ActualLab.Fusion;

/// <summary>
/// Base configuration options for <see cref="IState"/>, including computed options,
/// initial output, and event configuration.
/// </summary>
public interface IStateOptions
{
    public ComputedOptions ComputedOptions { get; init; }
    public Action<State>? EventConfigurator { get; init; }
    public string? Category { get; init; }

    public Result InitialOutput { get; init; }
    public object? InitialValue { get; init; }
}

/// <summary>
/// Strongly-typed <see cref="IStateOptions"/> with initial output of type <typeparamref name="T"/>.
/// </summary>
public interface IStateOptions<T> : IStateOptions
{
    public new Result<T> InitialOutput { get; init; }
    public new T InitialValue { get; init; }
}

/// <summary>
/// Base record for state configuration options, providing computed options and initial output.
/// </summary>
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

/// <summary>
/// Strongly-typed <see cref="StateOptions"/> with initial output of type <typeparamref name="T"/>.
/// </summary>
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
