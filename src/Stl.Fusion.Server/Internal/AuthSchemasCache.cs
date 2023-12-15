namespace ActualLab.Fusion.Server.Internal;

public class AuthSchemasCache
{
    private string? _schemas;

    public string? Schemas {
        get => _schemas;
        set => Interlocked.Exchange(ref _schemas, value);
    }
}
