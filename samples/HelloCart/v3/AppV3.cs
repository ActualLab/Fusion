namespace Samples.HelloCart.V3;

public class AppV3 : AppBase
{
    public AppV3()
    {
        var services = new ServiceCollection();
        AppLogging.Configure(services);
        AppDb.Configure(services);
        services.AddFusion(fusion => {
            fusion.AddService<IProductService, DbProductServiceUsingEntityResolver>();
            fusion.AddService<ICartService, DbCartServiceUsingEntityResolver>();
        });
        ClientServices = ServerServices = services.BuildServiceProvider();
    }
}
