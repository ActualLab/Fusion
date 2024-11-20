using System.Diagnostics.CodeAnalysis;
using ActualLab.Resilience;

namespace ActualLab.Async;

[StructLayout(LayoutKind.Auto)]
public readonly record struct AsyncChain
{
    public static readonly AsyncChain None = new("(no-operation)",
        _ => Task.CompletedTask);
    public static readonly AsyncChain NeverEnding = new("(never-ending)",
        cancellationToken => TaskExt.NewNeverEndingUnreferenced().WaitAsync(cancellationToken));

    public string Name { get; init; }
    public Func<CancellationToken, Task> Start { get; init; }

    [field: AllowNull, MaybeNull]
    public TransiencyResolver TransiencyResolver {
        get => field ?? TransiencyResolvers.PreferTransient;
        init;
    }

    // Constructor-like methods

    public static AsyncChain From(
        Func<CancellationToken, Task> start,
#if NETCOREAPP3_1_OR_GREATER
        [CallerArgumentExpression(nameof(start))]
#endif
        string name = "Unknown")
        => new(name, start);

    public static AsyncChain Delay(TimeSpan timeSpan, MomentClock? clock = null)
        => new($"Delay({timeSpan.ToString()})",
            ct => (clock ?? MomentClockSet.Default.CpuClock).Delay(timeSpan, ct));

    public static AsyncChain Delay(RandomTimeSpan delay, MomentClock? clock = null)
        => new($"Delay({delay.ToString()})",
            ct => (clock ?? MomentClockSet.Default.CpuClock).Delay(delay.Next(), ct));

    // Constructors

    public AsyncChain(Func<CancellationToken, Task> start)
        : this("(unnamed)", start) { }
    public AsyncChain(string name,
        Func<CancellationToken, Task> start,
        TransiencyResolver? transiencyResolver = null)
    {
        Name = name;
        Start = start;
        TransiencyResolver = transiencyResolver!;
    }

    // Conversion

    public void Deconstruct(out string name, out Func<CancellationToken, Task> start)
    {
        name = Name;
        start = Start;
    }

    public void Deconstruct(out string name, out Func<CancellationToken, Task> start, out TransiencyResolver transiencyResolver)
    {
        name = Name;
        start = Start;
        transiencyResolver = TransiencyResolver;
    }

    public override string ToString() => Name;

    public static implicit operator AsyncChain(Func<CancellationToken, Task> start) => new(start);
    public static implicit operator AsyncChain(RandomTimeSpan value) => Delay(value);
    public static implicit operator AsyncChain(TimeSpan value) => Delay(value);

    // Operators

    public static AsyncChain operator &(AsyncChain first, AsyncChain second) => first.Append(second);

    // Other methods

    public Task Run(CancellationToken cancellationToken = default)
    {
        var start = Start;
        return Task.Run(() => start.Invoke(cancellationToken), cancellationToken);
    }

    public Task RunIsolated(CancellationToken cancellationToken = default)
    {
        using var _ = ExecutionContextExt.TrySuppressFlow();
        return Run(cancellationToken);
    }
}
