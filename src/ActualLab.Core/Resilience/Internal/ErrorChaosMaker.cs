namespace ActualLab.Resilience.Internal;

public record ErrorChaosMaker<TException>(string? Message = null, Func<TException>? Factory = null) : ChaosMaker
    where TException : Exception
{
    public override string ToString()
        => $"Error<{typeof(TException).GetName()}>({Message})";

    public override Task Act(object context, CancellationToken cancellationToken)
        => throw CreateException();

    public Exception CreateException()
        => Factory is { } factory
            ? factory.Invoke()
            : (Exception)typeof(TException).CreateInstance(Message ?? "ChaosMaker-caused failure.");
}
