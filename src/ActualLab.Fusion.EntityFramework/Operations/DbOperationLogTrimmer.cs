using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationLogTrimmer<TDbContext>
    : DbLogTrimmer<TDbContext, DbOperation, DbOperationLogTrimmer<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbLogTrimmerOptions;

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationLogTrimmer(Options settings, IServiceProvider services)
        : base(settings, services)
    { }
}
