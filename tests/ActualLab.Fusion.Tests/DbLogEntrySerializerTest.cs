using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Interception;
using ActualLab.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ActualLab.Fusion.Tests;

// Pure serialization - no host, no DI. Every case uses its own serializer instance rather than
// DbLogEntrySerializer.Default, so nothing here perturbs the tests running beside it.
public class DbLogEntrySerializerTest
{
    private static readonly DbLogEntrySerializer Bytes =
        DbLogEntrySerializer.Default with { Format = DataFormat.Bytes };
    private static readonly DbLogEntrySerializer Text =
        DbLogEntrySerializer.Default with { Format = DataFormat.Text };

    [Fact]
    public void BinaryIsTheDefault()
        => DbLogEntrySerializer.Default.Format.Should().Be(DataFormat.Bytes);

    [Theory]
    [InlineData(DataFormat.Bytes)]
    [InlineData(DataFormat.Text)]
    public void OperationRoundTrips(DataFormat format)
    {
        var serializer = Get(format);
        var dbOperation = new DbOperation(NewOperation(), serializer);

        // Only the column the format names is written
        if (format is DataFormat.Bytes) {
            dbOperation.CommandData.Should().NotBeNullOrEmpty();
            dbOperation.InvalidationCallsData.Should().NotBeNullOrEmpty();
            dbOperation.CommandJson.Should().BeNull();
            dbOperation.InvalidationCallsJson.Should().BeNull();
        }
        else {
            dbOperation.CommandJson.Should().NotBeNullOrEmpty();
            dbOperation.InvalidationCallsJson.Should().NotBeNullOrEmpty();
            dbOperation.CommandData.Should().BeNull();
            dbOperation.InvalidationCallsData.Should().BeNull();
        }

        AssertRoundTripped(dbOperation.ToModel(serializer));
    }

    [Theory]
    [InlineData(DataFormat.Bytes, DataFormat.Text)]
    [InlineData(DataFormat.Text, DataFormat.Bytes)]
    public void ARowSurvivesAFormatChange(DataFormat writeFormat, DataFormat readFormat)
    {
        // Changing the format must not strand the rows written before it: a read takes
        // whichever column carries the payload, not the one Format names
        var dbOperation = new DbOperation(NewOperation(), Get(writeFormat));

        AssertRoundTripped(dbOperation.ToModel(Get(readFormat)));
    }

    [Theory]
    [InlineData(DataFormat.Bytes)]
    [InlineData(DataFormat.Text)]
    public void EventRoundTrips(DataFormat format)
    {
        var serializer = Get(format);
        var @event = new OperationEvent("e-1", new KeyValueService_Set<string>("k", "v"));

        var restored = new DbEvent(@event, serializer: serializer).ToModel(serializer);

        restored.Uuid.Should().Be("e-1");
        restored.Value.Should().BeOfType<KeyValueService_Set<string>>()
            .Which.Key.Should().Be("k");
    }

    [Theory]
    [InlineData(DataFormat.Bytes)]
    [InlineData(DataFormat.Text)]
    public void AnEventWithNoValueRoundTrips(DataFormat format)
    {
        // The commit-verifier row writes neither column
        var serializer = Get(format);
        var dbEvent = new DbEvent(NewOperation());

        dbEvent.ValueJson.Should().BeNull();
        dbEvent.ValueData.Should().BeNull();
        dbEvent.ToModel(serializer).Value.Should().BeNull();
    }

    // A 14.x row is text-only, and 15.0 has to keep reading it: a delayed event can sit in
    // _Events with DelayUntil far in the future, so "drain before upgrading" isn't something a
    // deployment can always do. These build the row the way 14.x's own code did - its
    // ITextSerializer was NewtonsoftJsonSerializer.Default and it always passed typeof(object) -
    // rather than by round-tripping 15.0's own writer, so the cross-version contract is pinned
    // even if 15.0's text path later changes.

    [Fact]
    public void ALegacyTextOnlyEventIsReadable()
    {
        var value = new KeyValueService_Set<string>("k", "v");
        var dbEvent = new DbEvent {
            Uuid = "e-1",
            ValueJson = NewtonsoftJsonSerializer.Default.Write(value, typeof(object)),
            ValueData = null, // 14.x had no such column
        };

        // Read with the default serializer, i.e. what an upgraded app has: Format = Bytes
        var restored = dbEvent.ToModel(Bytes);

        restored.Value.Should().BeOfType<KeyValueService_Set<string>>()
            .Which.Key.Should().Be("k");
    }

    [Fact]
    public void ALegacyTextOnlyOperationIsReadable()
    {
        var command = new KeyValueService_Set<string>("k", "v");
        var dbOperation = new DbOperation {
            Uuid = "op-1",
            HostId = "host-1",
            CommandJson = NewtonsoftJsonSerializer.Default.Write(command, typeof(ICommand)),
            CommandData = null,
        };

        var restored = dbOperation.ToModel(Bytes);

        restored.Command.Should().BeOfType<KeyValueService_Set<string>>()
            .Which.Key.Should().Be("k");
        // 14.x kept its invalidations in ItemsJson, which 15.0 neither reads nor has. The row is
        // inert rather than broken - it applies nothing, which is right: every host restarts
        // during the upgrade, so there is no warm cache for it to invalidate.
        restored.InvalidationCalls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DataFormat.Bytes, "CommandData", "CommandJson")]
    [InlineData(DataFormat.Text, "CommandJson", "CommandData")]
    public void IgnoreUnusedColumnsLeavesOneColumnPerOperationPayload(
        DataFormat format, string mapped, string ignored)
    {
        // An operation row is history the trimmer removes on age, so one column per payload is
        // safe for it in either format - unlike an event, see below
        using var dbContext = new LogEntryDbContext(format);
        var operationType = dbContext.Model.FindEntityType(typeof(DbOperation))!;

        operationType.FindProperty(mapped).Should().NotBeNull();
        operationType.FindProperty(ignored).Should().BeNull();
        operationType.FindProperty(mapped.Replace("Command", "InvalidationCalls")).Should().NotBeNull();
        operationType.FindProperty(ignored.Replace("Command", "InvalidationCalls")).Should().BeNull();
    }

    [Fact]
    public void ValueJsonSurvivesIgnoreUnusedColumnsByDefault()
    {
        // The point of MustDeserializeLegacyEvents: a delayed event can outlive any upgrade
        // window, so dropping the column its payload lives in would strand it
        using var dbContext = new LogEntryDbContext(DataFormat.Bytes);
        var eventType = dbContext.Model.FindEntityType(typeof(DbEvent))!;

        eventType.FindProperty("ValueData").Should().NotBeNull();
        eventType.FindProperty("ValueJson").Should().NotBeNull();
    }

    [Fact]
    public void ValueJsonIsDroppedOnlyWhenTheAppSaysSo()
    {
        using var dbContext = new LogEntryDbContext(DataFormat.Bytes, mustDeserializeLegacyEvents: false);
        var eventType = dbContext.Model.FindEntityType(typeof(DbEvent))!;

        eventType.FindProperty("ValueData").Should().NotBeNull();
        eventType.FindProperty("ValueJson").Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheFlagDoesNothingUnderText(bool mustDeserializeLegacyEvents)
    {
        // Text writes ValueJson, so it's mapped either way and the flag has nothing to decide
        using var dbContext = new LogEntryDbContext(DataFormat.Text, mustDeserializeLegacyEvents);
        var eventType = dbContext.Model.FindEntityType(typeof(DbEvent))!;

        eventType.FindProperty("ValueJson").Should().NotBeNull();
        eventType.FindProperty("ValueData").Should().BeNull();
    }

    [Fact]
    public void ALegacyEventIsStillReadableAfterIgnoreUnusedColumns()
    {
        // The two halves together: the column stays mapped, and a Bytes-format read still falls
        // back to it. This is the case the property exists for.
        var value = new KeyValueService_Set<string>("k", "v");
        var dbEvent = new DbEvent {
            Uuid = "e-1",
            ValueJson = NewtonsoftJsonSerializer.Default.Write(value, typeof(object)),
            ValueData = null,
        };

        using var dbContext = new LogEntryDbContext(DataFormat.Bytes);
        dbContext.Model.FindEntityType(typeof(DbEvent))!.FindProperty("ValueJson").Should().NotBeNull();
        dbEvent.ToModel(Bytes).Value.Should().BeOfType<KeyValueService_Set<string>>()
            .Which.Key.Should().Be("k");
    }

    [Fact]
    public void BothColumnsAreMappedByDefault()
    {
        // Not calling the helper is what keeps a format change migration-free
        using var dbContext = new LogEntryDbContext(format: null);
        var operationType = dbContext.Model.FindEntityType(typeof(DbOperation))!;

        operationType.FindProperty("CommandJson").Should().NotBeNull();
        operationType.FindProperty("CommandData").Should().NotBeNull();
    }

    // Nested types

    private sealed class LogEntryDbContext(DataFormat? format, bool mustDeserializeLegacyEvents = true)
        : DbContext
    {
        public DataFormat? Format { get; } = format;
        public bool MustDeserializeLegacyEvents { get; } = mustDeserializeLegacyEvents;

        public DbSet<DbOperation> Operations { get; set; } = null!;
        public DbSet<DbEvent> Events { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseSqlite("DataSource=:memory:")
                // EF caches one model per context type, and the format changes the model -
                // so it has to be part of the cache key
                .ReplaceService<IModelCacheKeyFactory, LogEntryModelCacheKeyFactory>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (Format is { } format)
                modelBuilder.IgnoreUnusedOperationsFrameworkColumns(format, MustDeserializeLegacyEvents);
        }
    }

    private sealed class LogEntryModelCacheKeyFactory : IModelCacheKeyFactory
    {
#if NET6_0_OR_GREATER
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((LogEntryDbContext)context).Format,
                ((LogEntryDbContext)context).MustDeserializeLegacyEvents, designTime);
#else
        // IModelCacheKeyFactory gained its designTime parameter in EF Core 6
        public object Create(DbContext context)
            => (context.GetType(), ((LogEntryDbContext)context).Format,
                ((LogEntryDbContext)context).MustDeserializeLegacyEvents);
#endif
    }

    // Private methods

    private static DbLogEntrySerializer Get(DataFormat format)
        => format is DataFormat.Bytes ? Bytes : Text;

    private static Operation NewOperation()
        => new Operation("op-1", "host-1", Moment.EpochStart, new KeyValueService_Set<string>("k", "v"))
            .AddInvalidationCalls(
                NewInvocation("Get", ArgumentList.New("ab", default(CancellationToken))),
                NewInvocation("CountOfLength", ArgumentList.New(2, default(CancellationToken))));

    private static ServiceCall NewInvocation(string methodName, ArgumentList arguments)
        => new() {
            ServiceType = new TypeRef(typeof(LocalDeferredDeferredInvalidationModeService))
                .WithoutAssemblyVersions(),
            MethodName = $"{methodName}:{arguments.Length}",
            Arguments = arguments,
        };

    private static void AssertRoundTripped(Operation operation)
    {
        operation.Uuid.Should().Be("op-1");
        operation.HostId.Should().Be("host-1");
        operation.Command.Should().BeOfType<KeyValueService_Set<string>>()
            .Which.Key.Should().Be("k");

        var calls = operation.InvalidationCalls;
        calls.Select(x => x.MethodName).Should().Equal("Get:2", "CountOfLength:2");
        calls[0].Arguments.Get<string>(0).Should().Be("ab");
        calls[1].Arguments.Get<int>(0).Should().Be(2);
    }
}
