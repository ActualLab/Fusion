namespace Samples.HelloCart.V2;

public class AppV2 : AppBase
{
    public AppV2()
    {
        var services = new ServiceCollection();
        AppLogging.Configure(services);
        AppDb.Configure(services);
        services.AddFusion(fusion => {
            fusion.AddService<IProductService, DbProductService>();
            fusion.AddService<ICartService, DbCartService>();
        });
        ClientServices = ServerServices = services.BuildServiceProvider();
    }
}
