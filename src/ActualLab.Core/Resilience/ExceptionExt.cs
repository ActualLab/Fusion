namespace ActualLab.Resilience;

public static class ExceptionExt
{
    public static bool IsServiceProviderDisposedException(this Exception error)
        => error.Any(IsServiceProviderDisposedExceptionImpl);

    private static bool IsServiceProviderDisposedExceptionImpl(Exception error)
    {
        if (Equals(error.GetType().Name, "JSDisconnectedException"))
            return true; // This is specific to Blazor Server, it also indicates the scope is going to die soon
        if (error is not ObjectDisposedException ode)
            return false;

#if NETSTANDARD2_0
        return ode.ObjectName.Contains("IServiceProvider")
            || ode.Message.Contains("'IServiceProvider'");
#else
        return ode.ObjectName.Contains("IServiceProvider", StringComparison.Ordinal)
               || ode.Message.Contains("'IServiceProvider'", StringComparison.Ordinal);
#endif
    }
}
