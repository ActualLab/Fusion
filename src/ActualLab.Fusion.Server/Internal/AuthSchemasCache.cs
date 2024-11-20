namespace ActualLab.Fusion.Server.Internal;

public class AuthSchemasCache
{
    public string? Schemas {
        get;
        set => Interlocked.Exchange(ref field, value);
    }
}
