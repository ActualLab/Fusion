using Stl.Fusion.EntityFramework.Operations;
using Stl.Multitenancy;

namespace Stl.Fusion.EntityFramework.Redis.Operations;

public record RedisOperationLogChangeTrackingOptions<TDbContext> : DbOperationCompletionTrackingOptions
{
    public static RedisOperationLogChangeTrackingOptions<TDbContext> Default { get; set; } = new();

    public Func<Tenant, string> PubSubKeyFactory { get; init; } = DefaultPubSubKeyFactory;

    public static string DefaultPubSubKeyFactory(Tenant tenant)
    {
        var tDbContext = typeof(TDbContext);
        var tenantSuffix = tenant == Tenant.Default ? "" : $".{tenant.Id.Value}";
        return $"{tDbContext.GetName()}{tenantSuffix}._Operations";
    }
}
