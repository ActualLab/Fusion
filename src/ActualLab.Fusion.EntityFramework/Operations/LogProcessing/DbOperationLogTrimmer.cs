using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

/// <summary>
/// Trims old <see cref="DbOperation"/> log entries that exceed the configured maximum age.
/// </summary>
public class DbOperationLogTrimmer<TDbContext>
    : DbOperationLogTrimmer<TDbContext, DbOperation, DbOperationLogTrimmer<TDbContext>.Options>
    where TDbContext : DbContext
{
    /// <summary>
    /// Configuration options for <see cref="DbOperationLogTrimmer{TDbContext}"/>.
    /// </summary>
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
