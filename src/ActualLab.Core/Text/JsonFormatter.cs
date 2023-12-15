using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Text;

public static class JsonFormatter
{
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static string Format(object value)
        => SystemJsonSerializer.Pretty.Write(value);
}
