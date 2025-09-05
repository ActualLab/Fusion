using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbOperationLogTrimmer<TDbContext>
    : DbOperationLogTrimmer<TDbContext, DbOperation, DbOperationLogTrimmer<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbLogTrimmerOptions
    {
        public static Options Default { get; set; } = new();

        // Trim condition:
        // ~ .Where(e => e.LoggedAt < minLoggedAt)
        public Options()
            => MaxEntryAge = TimeSpan.FromMinutes(30);
    }

    public override DbLogKind LogKind => DbLogKind.Operations;

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationLogTrimmer(Options settings, IServiceProvider services)
        : base(settings, services)
    { }
}
