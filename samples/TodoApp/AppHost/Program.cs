using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

const int tenant0Port = 5005;
const int tenantCount = 3;
const int backendPortOffset = 1000;
var builder = DistributedApplication.CreateBuilder(args);

for (var index = 0; index < tenantCount; index++)
    AddTenantHost(index);

var app = builder.Build();
app.Run();

void AddTenantHost(int index, bool useRemoteBackend = true)
{
    var port = tenant0Port + index;
    var apiHost = AddHost($"tenant{index}-api", port, useRemoteBackend ? "ApiServer" : "SingleServer");
    if (useRemoteBackend) {
        var backendHost = AddHost($"tenant{index}-backend", port + backendPortOffset, "BackendServer");
        apiHost.WithReference(backendHost);
        apiHost.WithEnvironment("Host__BackendUrl", backendHost.GetEndpoint("http"));
    }
}

IResourceBuilder<ProjectResource> AddHost(string name, int port, string hostKind)
    => builder.AddProject<Projects.Host>(name, options => { options.ExcludeLaunchProfile = true; })
        .WithEnvironment("Host__IsAspireManaged", "true")
        .WithEnvironment("Host__HostKind", hostKind)
        .WithEnvironment("Host__Tenant0Port", tenant0Port.ToString())
        .WithEnvironment("Host__TenantCount", tenantCount.ToString())
        .WithEnvironment("Host__Port", port.ToString()) // Required, coz the app will be running on another port
        .WithEnvironment("DOTNET_ENVIRONMENT", "Development") // Optional
        .WithHttpEndpoint(port);
