namespace ActualLab.Diagnostics;

public static class TypeExt
{
    private static readonly ConcurrentDictionary<(Type, string), string> OperationNameCache = new();

    public static string GetOperationName(this Type type, [CallerMemberName] string operation = "")
        => OperationNameCache.GetOrAdd((type, operation),
            static key => $"{DiagnosticsExt.FixName(key.Item1.NonProxyType().GetName())}/{key.Item2}");
}
