using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Host;

public class HostSettings
{
    public bool IsAspireManaged { get; set; }
    public HostKind HostKind { get; set; } = HostKind.SingleServer;
    public bool UseTenants { get; set; }
    public int? TenantIndex { get; set; }
    public int TenantCount { get; set; } = 3;
    public int Tenant0Port { get; set; } = 5005;
    public int? Port { get; set; }
    public string BackendUrl { get; set; } = "";

    // DBs
    public bool MustRecreateDb { get; set; } = false;
    public string UsePostgreSql { get; set; } = "";
        // "Server=localhost;Database=fusion_todoapp1_{0:Shard};Port=5432;User Id=postgres;Password=postgres";
    public string UseSqlServer { get; set; } = "";
        // "Data Source=localhost;Initial Catalog=fusion_todoapp1_{0:Shard};Integrated Security=False;User ID=sa;Password=SqlServer1";

    // Auth
    public string MicrosoftAccountClientId { get; set; } = "6839dbf7-d1d3-4eb2-a7e1-ce8d48f34d00";
    public string MicrosoftAccountClientSecret { get; set; } =
        Encoding.UTF8.GetString(Convert.FromBase64String(
            "YnlCOFF+QkdyUk9VcUlQWHdrYWxFQVNrQmpoaC0zdGdFU1JncGNMVA=="));

    public string GitHubClientId { get; set; } = "7a38bc415f7e1200fee2";
    public string GitHubClientSecret { get; set; } =
        Encoding.UTF8.GetString(Convert.FromBase64String(
            "OGNkMTAzM2JmZjljOTk3ODc5MjhjNTNmMmE3Y2Q1NWU0ZmNlNjU0OA=="));
}
