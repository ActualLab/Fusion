using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationEventLogTrimmer<TDbContext>(
    DbOperationEventLogTrimmer<TDbContext>.Options settings,
    IServiceProvider services
) : DbLogTrimmer<TDbContext, DbOperationEvent>(settings, services)
    where TDbContext : DbContext
{
    public record Options : DbLogTrimmerOptions
    {
        public Options()
        {
            MaxEntryAge = TimeSpan.FromDays(1);
        }
    }

    protected new Options Settings { get; } = settings;
}
