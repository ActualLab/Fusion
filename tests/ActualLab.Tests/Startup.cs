using Xunit.DependencyInjection.Logging;

namespace ActualLab.Tests;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddDebug();
            logging.AddXunitOutput(options => options.Filter = (_, level) => level >= LogLevel.Debug);
        });
    }
}
