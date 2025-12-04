#if !NETFRAMEWORK

using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Hosting;

namespace ActualLab.Testing.Logging;

public class ApplicationPartsLogger(
    ApplicationPartManager partManager,
    ILogger<ApplicationPartsLogger>? log = null
    ) : IHostedService
{
    private readonly ILogger _log = log ?? NullLogger<ApplicationPartsLogger>.Instance;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var applicationParts = partManager.ApplicationParts.Select(x => x.Name);
        var controllerFeature = new ControllerFeature();
        partManager.PopulateFeature(controllerFeature);
        var controllers = controllerFeature.Controllers.Select(x => x.Name);

        _log.LogInformation("Application parts: {ApplicationParts}", string.Join(", ", applicationParts));
        _log.LogInformation("Controllers: {Controllers}", string.Join(", ", controllers));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}

#endif
