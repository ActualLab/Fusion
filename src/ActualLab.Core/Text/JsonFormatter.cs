namespace ActualLab.Text;

/// <summary>
/// Provides a simple helper for formatting objects as pretty-printed JSON strings.
/// </summary>
public static class JsonFormatter
{
    public static string Format(object value)
        => SystemJsonSerializer.Pretty.Write(value);
}
