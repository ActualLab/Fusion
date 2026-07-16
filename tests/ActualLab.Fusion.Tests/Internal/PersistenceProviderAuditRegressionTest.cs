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
        var format = NpgsqlDbLogWatcherOptions<AuditDbContext>.DefaultChannelNameFormatter;
        var channelNames = new[] {
            format("us-west", typeof(string)),
            format("us'west", typeof(string)),
            format(new string('x', 100), typeof(string)),
        };

        channelNames.Should().OnlyContain(x => x.Length <= 63);
        foreach (var channelName in channelNames)
            channelName.Should().MatchRegex("^[A-Za-z_][A-Za-z0-9_$]*$");
        channelNames.Should().OnlyHaveUniqueItems();
    }

    private sealed class AuditDbContext : DbContext;
}
