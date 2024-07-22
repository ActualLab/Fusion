using Cysharp.Text;

namespace ActualLab.Async;

[StructLayout(LayoutKind.Auto)]
public record struct Temporary<T>(T Value, CancellationToken GoneToken)
{
    public void Deconstruct(out T value, out CancellationToken goneToken)
    {
        value = Value;
        goneToken = GoneToken;
    }

    public override string ToString() =>
        ZString.Concat('(',
            Value?.ToString() ?? "null",
            GoneToken.IsCancellationRequested ? ", gone)" : ", alive)");

    public static implicit operator Temporary<T>((T Value, CancellationToken GoneToken) value)
        => new(value.Value, value.GoneToken);
}
