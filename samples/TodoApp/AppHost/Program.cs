using Aspire.Hosting;

const int tenant0Port = 5005;
const int tenantCount = 3;
var builder = DistributedApplication.CreateBuilder(args);

for (var index = 0; index < tenantCount; index++)
    AddTenantHost(index);

builder.Build().Run();

void AddTenantHost(int index)
{
    var port = tenant0Port + index;
    builder.AddProject<Projects.Host>($"host-tenant{index}", options => {
            options.ExcludeLaunchProfile = true;
        })
        .WithEnvironment("Host__IsAspireManaged", "true")
        .WithEnvironment("Host__Tenant0Port", tenant0Port.ToString())
        .WithEnvironment("Host__TenantCount", tenantCount.ToString)
        .WithEnvironment("Host__Port", port.ToString) // Required, coz the app will be running on another port
        .WithEnvironment("DOTNET_ENVIRONMENT", "Development") // Optional
        .WithHttpEndpoint(port);
}
