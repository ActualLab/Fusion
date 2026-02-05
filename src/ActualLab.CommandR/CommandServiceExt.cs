using ActualLab.Interception;

namespace ActualLab.CommandR;

/// <summary>
/// Extension methods for <see cref="ICommandService"/>.
/// </summary>
public static class CommandServiceExt
{
    public static IServiceProvider GetServices(this ICommandService service)
        => ProxyExt.GetServices(service);

    public static ICommander GetCommander(this ICommandService service)
        => service.GetServices().Commander();
}
