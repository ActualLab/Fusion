namespace ActualLab.Time.Internal;

/// <summary>
/// Internal error factory methods for the Time namespace.
/// </summary>
public static class Errors
{
    public static Exception UnusableClock()
        => new NotSupportedException("These clock cannot be used.");
}
