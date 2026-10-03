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

    [Theory]
    [InlineData(DataFormat.Bytes, "CommandData", "CommandJson")]
    [InlineData(DataFormat.Text, "CommandJson", "CommandData")]
    public void IgnoreUnusedColumnsLeavesOneColumnPerPayload(
        DataFormat format, string mapped, string ignored)
    {
        using var dbContext = new LogEntryDbContext(format);
        var operationType = dbContext.Model.FindEntityType(typeof(DbOperation))!;
        var eventType = dbContext.Model.FindEntityType(typeof(DbEvent))!;

        operationType.FindProperty(mapped).Should().NotBeNull();
        operationType.FindProperty(ignored).Should().BeNull();
        // ... and the same for the other two payloads
        operationType.FindProperty(mapped.Replace("Command", "InvalidationCalls")).Should().NotBeNull();
        operationType.FindProperty(ignored.Replace("Command", "InvalidationCalls")).Should().BeNull();
        eventType.FindProperty(mapped.Replace("Command", "Value")).Should().NotBeNull();
        eventType.FindProperty(ignored.Replace("Command", "Value")).Should().BeNull();
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

    private sealed class LogEntryDbContext(DataFormat? format) : DbContext
    {
        public DataFormat? Format { get; } = format;

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
                modelBuilder.IgnoreUnusedOperationsFrameworkColumns(format);
        }
    }

    private sealed class LogEntryModelCacheKeyFactory : IModelCacheKeyFactory
    {
#if NET6_0_OR_GREATER
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((LogEntryDbContext)context).Format, designTime);
#else
        // IModelCacheKeyFactory gained its designTime parameter in EF Core 6
        public object Create(DbContext context)
            => (context.GetType(), ((LogEntryDbContext)context).Format);
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
