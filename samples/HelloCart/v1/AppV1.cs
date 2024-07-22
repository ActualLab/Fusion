namespace Samples.HelloCart.V1;

public class AppV1 : AppBase
{
    public AppV1()
    {
        var services = new ServiceCollection();
        AppLogging.Configure(services);
        services.AddFusion(fusion => {
            fusion.AddService<IProductService, InMemoryProductService>();
            fusion.AddService<ICartService, InMemoryCartService>();
        });
        ClientServices = ServerServices = services.BuildServiceProvider();
    }
}
