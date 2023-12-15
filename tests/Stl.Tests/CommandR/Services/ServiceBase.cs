namespace ActualLab.Tests.CommandR.Services;

public abstract class ServiceBase
{
    protected IServiceProvider Services { get; }
    protected ILogger Log { get; }

    protected ServiceBase(IServiceProvider services)
    {
        Services = services;
        Log = services.LogFor(GetType());
    }
}
