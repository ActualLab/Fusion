namespace ActualLab.Async;

public static class CancellationTokenRegistrationExt
{
#if NETSTANDARD

    extension(CancellationTokenRegistration registration)
    {
        // Cross-platform version of Unregister() from .NET Core. Unlike the real one, this still
        // waits for a concurrently running callback, since netstandard has no way to avoid it - so
        // the deadlock Unregister() exists to prevent is only fixed on the frameworks that have it.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Unregister()
        {
            registration.Dispose();
            return true;
        }
    }

#endif
}
