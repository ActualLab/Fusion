using System.Reflection;
using ActualLab.Fusion.EntityFramework.Npgsql;
using ActualLab.Fusion.EntityFramework.Npgsql.Internal;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.Internal;

public class PersistenceProviderAuditRegressionTest
{
    [Fact]
    public void NpgsqlHintsShouldBeOrderedByClauseKind()
    {
        var extract = typeof(NpgsqlHintQuerySqlGenerator).GetMethod(
            "TryExtractHints",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var tags = new HashSet<string>(StringComparer.Ordinal) {
            "HINTS:2:SKIP LOCKED,1:UPDATE",
        };

        var result = (string)extract.Invoke(null, [tags])!;

        result.Should().Be("FOR UPDATE SKIP LOCKED");
    }

    [Fact]
    public void EmptyNpgsqlCustomHintShouldBeIgnored()
    {
        var extract = typeof(NpgsqlHintQuerySqlGenerator).GetMethod(
            "TryExtractHints",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var tags = new HashSet<string>(StringComparer.Ordinal) {
            "HINTS:",
        };

        var result = (string)extract.Invoke(null, [tags])!;

        result.Should().BeEmpty();
    }

    [Fact]
    public void DefaultNpgsqlChannelNamesShouldBeValidUnquotedIdentifiers()
    {
        var channelName = NpgsqlDbLogWatcherOptions<AuditDbContext>.DefaultChannelNameFormatter(
            "us-west",
            typeof(string));

        channelName.Should().MatchRegex("^[A-Za-z_][A-Za-z0-9_$]*$");
    }

    private sealed class AuditDbContext : DbContext;
}
