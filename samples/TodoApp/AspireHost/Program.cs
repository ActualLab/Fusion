using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

const int tenant0Port = 5005;
const int tenantCount = 3;
const int backendPortOffset = 1000;
var builder = DistributedApplication.CreateBuilder(args);

for (var tenantIndex = 0; tenantIndex < tenantCount; tenantIndex++)
    AddTenantHost(tenantIndex);

var app = builder.Build();
app.Run();

void AddTenantHost(int tenantIndex, bool useRemoteBackend = true)
{
    var port = tenant0Port + tenantIndex;
    var apiHost = AddHost($"tenant{tenantIndex}-api", tenantIndex, port, useRemoteBackend ? "ApiServer" : "SingleServer");
    if (useRemoteBackend) {
        var backendHost = AddHost($"tenant{tenantIndex}-backend", tenantIndex, port + backendPortOffset, "BackendServer");
        apiHost.WithReference(backendHost);
        apiHost.WithEnvironment("Host__BackendUrl", backendHost.GetEndpoint("http"));
    }
}

IResourceBuilder<ProjectResource> AddHost(string name, int tenantIndex, int port, string hostKind)
    => builder.AddProject<Projects.Host>(name, options => { options.ExcludeLaunchProfile = true; })
        .WithEnvironment("Host__IsAspireManaged", "true")
        .WithEnvironment("Host__HostKind", hostKind)
        .WithEnvironment("Host__TenantIndex", tenantIndex.ToString())
        .WithEnvironment("Host__Tenant0Port", tenant0Port.ToString())
        .WithEnvironment("Host__TenantCount", tenantCount.ToString())
        .WithEnvironment("Host__Port", port.ToString()) // Required, coz the app will be running on another port
        .WithEnvironment("DOTNET_ENVIRONMENT", "Development") // Optional
        .WithHttpEndpoint(port);
