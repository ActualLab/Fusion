namespace ActualLab.Resilience;

public delegate bool ExceptionFilter(Exception error, Transiency transiency);

public static class ExceptionFilterExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Invoke(this ExceptionFilter filter, Exception error, TransiencyResolver transiencyResolver)
        => filter.Invoke(error, transiencyResolver.Invoke(error));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Invoke(this ExceptionFilter filter, Exception error, TransiencyResolver transiencyResolver, out Transiency transiency)
    {
        transiency = transiencyResolver.Invoke(error);
        return filter.Invoke(error, transiency);
    }
}
