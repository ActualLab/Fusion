using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Host>("web")
    .WithExternalHttpEndpoints();

builder.Build().Run();
