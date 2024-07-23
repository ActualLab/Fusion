using Aspire.Hosting;

const int tenant0Port = 6005;
const int tenantCount = 1;
var builder = DistributedApplication.CreateBuilder(args);

for (var index = 0; index < tenantCount; index++)
    AddTenantHost(index);

builder.Build().Run();

void AddTenantHost(int index)
{
    var port = tenant0Port + index;
    builder.AddProject<Projects.Host>($"host{index}")
        .WithEnvironment("Host__IsAspireManaged", "true")
        .WithEnvironment("Host__Tenant0Port", tenant0Port.ToString())
        .WithEnvironment("Host__TenantCount", tenantCount.ToString)
        .WithHttpEndpoint(port);
}
