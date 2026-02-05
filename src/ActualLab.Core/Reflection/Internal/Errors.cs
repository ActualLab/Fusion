namespace ActualLab.Reflection.Internal;

/// <summary>
/// Factory methods for exceptions used internally in the Reflection namespace.
/// </summary>
public static class Errors
{
    public static Exception PropertyOrFieldInfoExpected(string paramName)
        => new ArgumentException("PropertyInfo or FieldInfo expected.", paramName);

    public static Exception TypeNotFound(string assemblyQualifiedName)
        => new KeyNotFoundException($"Type '{assemblyQualifiedName}' is not found.");
}
