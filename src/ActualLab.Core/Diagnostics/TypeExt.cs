using ActualLab.OS;

namespace ActualLab.Diagnostics;

/// <summary>
/// Extension methods for <see cref="Type"/> to construct diagnostics operation names.
/// </summary>
public static class TypeExt
{
    private static readonly ConcurrentDictionary<(object, object), string> OperationNameCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static string GetOperationName(this Type type, [CallerMemberName] string operation = "")
        => OperationNameCache.GetOrAdd((type, operation),
            static key => {
                var type = (Type)key.Item1;
                var operation = (string)key.Item2;
                return $"{DiagnosticsExt.FixName(type.NonProxyType().GetName())}/{operation}";
            });
}
