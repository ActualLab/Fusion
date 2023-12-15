using ActualLab.Interception;

namespace ActualLab.CommandR;

public static class CommandServiceExt
{
    public static IServiceProvider GetServices(this ICommandService service)
        => ProxyExt.GetServices(service);

    public static ICommander GetCommander(this ICommandService service)
        => service.GetServices().Commander();
}
