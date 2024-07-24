using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.Diagnostics;

public static class TypeExt
{
    public static string GetOperationName(this Type type, string operation)
        => $"{operation}@{type.NonProxyType().GetName()}";
}
