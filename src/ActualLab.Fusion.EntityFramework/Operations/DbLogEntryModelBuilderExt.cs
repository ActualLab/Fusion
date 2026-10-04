using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for the Operations Framework's own log tables.
/// </summary>
public static class DbLogEntryModelBuilderExt
{
    // Prefer this overload: it can't disagree with the serializer the app actually registered,
    // which the DataFormat one can.
    public static ModelBuilder IgnoreUnusedOperationsFrameworkColumns(
        this ModelBuilder modelBuilder,
        DbLogEntrySerializer serializer)
        => modelBuilder.IgnoreUnusedOperationsFrameworkColumns(
            serializer.Format, serializer.MustDeserializeLegacyEvents);

    // Call from OnModelCreating. format must be the one the registered DbLogEntrySerializer writes
    // with: mapping away the column the writer uses loses the payload silently. It also trades the
    // migration-free format switch for the leaner schema - rows in the other format become
    // unreadable, since the column they live in is no longer part of the model.
    public static ModelBuilder IgnoreUnusedOperationsFrameworkColumns(
        this ModelBuilder modelBuilder,
        DataFormat format,
        bool mustDeserializeLegacyEvents = true)
    {
        if (format is DataFormat.Bytes) {
            // An operation row is history: the readers consume it, then the trimmer removes it on
            // age, so there is a point after which no older-format row is left.
            modelBuilder.Entity<DbOperation>()
                .Ignore(x => x.CommandJson)
                .Ignore(x => x.InvalidationCallsJson);
            // An event row isn't: a delayed one can outlive any upgrade window, so ValueJson stays
            // mapped unless the app says it has none left.
            if (!mustDeserializeLegacyEvents)
                modelBuilder.Entity<DbEvent>()
                    .Ignore(x => x.ValueJson);
        }
        else {
            // Text is what gets written, so ValueJson is mapped either way and the flag is moot
            modelBuilder.Entity<DbOperation>()
                .Ignore(x => x.CommandData)
                .Ignore(x => x.InvalidationCallsData);
            modelBuilder.Entity<DbEvent>()
                .Ignore(x => x.ValueData);
        }
        return modelBuilder;
    }
}
