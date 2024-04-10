using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbOperationLogTrimmer<TDbContext>
    : DbLogTrimmer<TDbContext, DbOperation, DbOperationLogTrimmer<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbLogTrimmerOptions
    {
        public static Options Default { get; set; } = new();
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationLogTrimmer(Options settings, IServiceProvider services)
        : base(settings, services)
    { }
}
