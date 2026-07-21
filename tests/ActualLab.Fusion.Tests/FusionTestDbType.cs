using System.Net.Sockets;

namespace ActualLab.Fusion.Tests;

public enum FusionTestDbType
{
    Sqlite = 0,
    PostgreSql = 1,
    MariaDb = 2,
    SqlServer = 3,
    InMemory = 4,
}

public static class FusionTestDbTypeExt
{
    private static readonly HashSet<string>? EnabledTypes = GetEnabledTypes();
    private static readonly ConcurrentDictionary<int, bool> ProbeCache = new();

    public static bool IsAvailable(this FusionTestDbType dbType)
        => TestRunnerInfo.IsBuildAgent()
            ? dbType.IsAvailableOnBuildAgent()
            : dbType.IsAvailableLocally();

    public static bool IsAvailableLocally(this FusionTestDbType dbType)
    {
        if (EnabledTypes is not null)
            return EnabledTypes.Contains(dbType.ToString());

        return dbType switch {
            FusionTestDbType.InMemory or FusionTestDbType.Sqlite => true,
            FusionTestDbType.PostgreSql => CanConnect(5432),
            FusionTestDbType.MariaDb => CanConnect(3306),
            FusionTestDbType.SqlServer => CanConnect(1433),
            _ => false,
        };
    }

    public static bool IsAvailableOnBuildAgent(this FusionTestDbType dbType)
        => dbType is FusionTestDbType.InMemory;

    // Private methods

    private static HashSet<string>? GetEnabledTypes()
    {
        var value = Environment.GetEnvironmentVariable("ActualLab_Tests_DbTypes");
        if (value.IsNullOrEmpty())
            return null;

        return value.Split(',')
            .Select(x => x.Trim())
            .Where(x => x.Length != 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool CanConnect(int port)
        => ProbeCache.GetOrAdd(port, static p => {
            try {
                using var client = new TcpClient();
                return client.ConnectAsync("127.0.0.1", p).Wait(250);
            }
            catch {
                return false;
            }
        });
}
