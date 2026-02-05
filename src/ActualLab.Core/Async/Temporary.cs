namespace ActualLab.Async;

/// <summary>
/// Wraps a value together with a <see cref="CancellationToken"/> indicating when the value is gone.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public record struct Temporary<T>(T Value, CancellationToken GoneToken)
{
    public void Deconstruct(out T value, out CancellationToken goneToken)
    {
        value = Value;
        goneToken = GoneToken;
    }

    public override string ToString()
        => $"({Value?.ToString() ?? "null"}{(GoneToken.IsCancellationRequested ? ", gone)" : ", alive)")}";

    public static implicit operator Temporary<T>((T Value, CancellationToken GoneToken) value)
        => new(value.Value, value.GoneToken);
}
