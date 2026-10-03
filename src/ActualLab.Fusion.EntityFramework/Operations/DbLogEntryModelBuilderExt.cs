using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for the Operations Framework's own log tables.
/// </summary>
public static class DbLogEntryModelBuilderExt
{
    // Call from OnModelCreating. format must be the one the registered DbLogEntrySerializer writes
    // with: mapping away the column the writer uses loses the payload silently. It also trades the
    // migration-free format switch for the leaner schema - rows in the other format become
    // unreadable, since the column they live in is no longer part of the model.
    public static ModelBuilder IgnoreUnusedOperationsFrameworkColumns(
        this ModelBuilder modelBuilder,
        DataFormat format)
    {
        if (format is DataFormat.Bytes) {
            modelBuilder.Entity<DbOperation>()
                .Ignore(x => x.CommandJson)
                .Ignore(x => x.InvalidationCallsJson);
            modelBuilder.Entity<DbEvent>()
                .Ignore(x => x.ValueJson);
        }
        else {
            modelBuilder.Entity<DbOperation>()
                .Ignore(x => x.CommandData)
                .Ignore(x => x.InvalidationCallsData);
            modelBuilder.Entity<DbEvent>()
                .Ignore(x => x.ValueData);
        }
        return modelBuilder;
    }
}
