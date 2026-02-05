namespace ActualLab.Fusion.Extensions.Internal;

/// <summary>
/// Factory methods for exceptions used in the Fusion extensions module.
/// </summary>
public static class Errors
{
    public static Exception KeyViolatesSandboxedKeyValueStoreConstraints()
        => throw new InvalidOperationException("Key violates sandboxed key-value store constraints.");
}
