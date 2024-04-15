namespace Samples.HelloCart;

public static class AppLogging
{
    public static void Configure(IServiceCollection services)
    {
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("", LogLevel.Information);
            logging.AddFilter("ActualLab.Fusion.Operations", LogLevel.Warning);
            logging.AddFilter("ActualLab.Fusion.EntityFramework.Operations", LogLevel.Information);
            logging.AddFilter("ActualLab.Fusion.EntityFramework.Operations.LogProcessing", LogLevel.Information);
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Warning);
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });
    }
}
