using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationEventLogTrimmer<TDbContext>
    : DbLogTrimmer<TDbContext, DbOperation, DbOperationEventLogTrimmer<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbLogTrimmerOptions
    {
        public Options()
        {
            MaxEntryAge = TimeSpan.FromDays(1);
        }
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationEventLogTrimmer(Options settings, IServiceProvider services)
        : base(settings, services)
    { }
}
