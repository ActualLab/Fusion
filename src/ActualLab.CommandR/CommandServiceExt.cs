using ActualLab.Interception;

namespace ActualLab.CommandR;

public static class CommandServiceExt
{
    extension(ICommandService service)
    {
        public IServiceProvider GetServices()
            => ProxyExt.GetServices(service);

        public ICommander GetCommander()
            => service.GetServices().Commander();
    }
}
