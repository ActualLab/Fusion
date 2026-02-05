using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ActualLab.Fusion.EntityFramework.Internal;

/// <summary>
/// An <see cref="IDbContextOptionsExtension"/> that registers a specific
/// <see cref="IDbHintFormatter"/> implementation into the EF Core service provider.
/// </summary>
public class DbHintFormatterOptionsExtension(Type dbHintFormatterType) : IDbContextOptionsExtension
{
    public Type DbHintFormatterType { get; } = dbHintFormatterType;

    public DbContextOptionsExtensionInfo Info
        => new DbHintFormatterExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
        var hintFormatter = (IDbHintFormatter)DbHintFormatterType.CreateInstance();
        services.AddSingleton(typeof(IDbHintFormatter), DbHintFormatterType);
        hintFormatter.Configure(services);
    }

    public void Validate(IDbContextOptions options)
    { }
}
