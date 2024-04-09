using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ActualLab.Fusion.EntityFramework.Internal;

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
