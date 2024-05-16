namespace ActualLab.Fusion;

public static class StateFactoryExt
{
    // NewMutable

    public static MutableState<T> NewMutable<T>(
        this StateFactory factory,
        T initialValue = default!,
        string? category = null)
    {
        var options = new MutableState<T>.Options() {
            InitialValue = initialValue,
            Category = category,
        };
        return factory.NewMutable(options);
    }

    public static MutableState<T> NewMutable<T>(
        this StateFactory factory,
        Result<T> initialOutput,
        string? category = null)
    {
        var options = new MutableState<T>.Options() {
            InitialOutput = initialOutput,
            Category = category,
        };
        return factory.NewMutable(options);
    }

    // NewComputed

    public static ComputedState<T> NewComputed<T>(
        this StateFactory factory,
        Func<IComputedState<T>, CancellationToken, Task<T>> computer,
        string? category = null)
    {
        var options = new ComputedState<T>.Options() {
            Category = category,
        };
        return factory.NewComputed(options, computer);
    }

    public static ComputedState<T> NewComputed<T>(
        this StateFactory factory,
        T initialValue,
        Func<IComputedState<T>, CancellationToken, Task<T>> computer,
        string? category = null)
    {
        var options = new ComputedState<T>.Options() {
            InitialValue = initialValue,
            Category = category,
        };
        return factory.NewComputed(options, computer);
    }

    public static ComputedState<T> NewComputed<T>(
        this StateFactory factory,
        Result<T> initialOutput,
        Func<IComputedState<T>, CancellationToken, Task<T>> computer,
        string? category = null)
    {
        var options = new ComputedState<T>.Options() {
            InitialOutput = initialOutput,
            Category = category,
        };
        return factory.NewComputed(options, computer);
    }

    public static ComputedState<T> NewComputed<T>(
        this StateFactory factory,
        IUpdateDelayer updateDelayer,
        Func<IComputedState<T>, CancellationToken, Task<T>> computer,
        string? category = null)
    {
        var options = new ComputedState<T>.Options() {
            UpdateDelayer = updateDelayer,
            Category = category,
        };
        return factory.NewComputed(options, computer);
    }

    public static ComputedState<T> NewComputed<T>(
        this StateFactory factory,
        T initialValue,
        IUpdateDelayer updateDelayer,
        Func<IComputedState<T>, CancellationToken, Task<T>> computer,
        string? category = null)
    {
        var options = new ComputedState<T>.Options() {
            InitialValue = initialValue,
            UpdateDelayer = updateDelayer,
            Category = category,
        };
        return factory.NewComputed(options, computer);
    }

    public static ComputedState<T> NewComputed<T>(
        this StateFactory factory,
        Result<T> initialOutput,
        IUpdateDelayer updateDelayer,
        Func<IComputedState<T>, CancellationToken, Task<T>> computer,
        string? category = null)
    {
        var options = new ComputedState<T>.Options() {
            InitialOutput = initialOutput,
            UpdateDelayer = updateDelayer,
            Category = category,
        };
        return factory.NewComputed(options, computer);
    }
}
