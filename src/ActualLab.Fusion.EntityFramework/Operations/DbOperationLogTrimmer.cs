using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationLogTrimmer<TDbContext>(
    DbOperationLogTrimmer<TDbContext>.Options settings,
    IServiceProvider services
    ) : DbLogTrimmer<TDbContext, DbOperation>(settings, services)
    where TDbContext : DbContext
{
    public record Options : DbLogTrimmerOptions;

    protected new Options Settings { get; } = settings;
}
