using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

namespace ActualLab.Fusion.EntityFramework.Npgsql.Internal;

#pragma warning disable EF1001

/// <summary>
/// A PostgreSQL SQL generator that appends row-level locking hints (e.g., FOR UPDATE)
/// extracted from query tags to the generated SQL.
/// </summary>
public class NpgsqlHintQuerySqlGenerator : NpgsqlQuerySqlGenerator
{
    // ReSharper disable once ConvertToPrimaryConstructor
    public NpgsqlHintQuerySqlGenerator(
        QuerySqlGeneratorDependencies dependencies,
#if NET8_0_OR_GREATER
        IRelationalTypeMappingSource typeMappingSource,
#endif
        bool reverseNullOrderingEnabled,
        Version postgresVersion)
#if NET8_0_OR_GREATER
        : base(dependencies, typeMappingSource, reverseNullOrderingEnabled, postgresVersion)
#else
        : base(dependencies, reverseNullOrderingEnabled, postgresVersion)
#endif
    { }

    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        var hints = TryExtractHints(selectExpression.Tags);
        var result = base.VisitSelect(selectExpression);
        if (!hints.IsNullOrEmpty()) {
            Sql.AppendLine();
            Sql.Append(hints);
        }
        return result;
    }

    private static string TryExtractHints(ISet<string> tags)
    {
        var hints = (HashSet<string>?)null;
        foreach (var tag in tags) {
            if (!tag.StartsWith("HINTS:", StringComparison.Ordinal))
                continue;

            hints ??= new HashSet<string>(StringComparer.Ordinal);
            foreach (var hint in tag[6..].Split(','))
                hints.Add(hint);
        }
        if (hints is null)
            return "";

        var sb = StringBuilderExt.Acquire();
        try {
            sb.Append("FOR");
            foreach (var g in hints.GroupBy(static x => x[0])) {
                if (!char.IsDigit(g.Key))
                    return ""; // Invalid hints

                var hint = (string?)null;
                foreach (var h in g) {
                    if (hint is not null && !string.Equals(hint, h, StringComparison.Ordinal))
                        return ""; // Invalid hints

                    hint = h;
                }
                sb.Append(' ');
                sb.Append(hint![2..]);
            }
            return sb.ToString();
        }
        finally {
            sb.Release();
        }
    }
}
