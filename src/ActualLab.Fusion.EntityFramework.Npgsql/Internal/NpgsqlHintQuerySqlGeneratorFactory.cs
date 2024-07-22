using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

namespace ActualLab.Fusion.EntityFramework.Npgsql.Internal;

#pragma warning disable EF1001

public class NpgsqlHintQuerySqlGeneratorFactory : IQuerySqlGeneratorFactory
{
    private readonly QuerySqlGeneratorDependencies _dependencies;
#if NET8_0_OR_GREATER
    private readonly IRelationalTypeMappingSource _typeMappingSource;
    private readonly INpgsqlSingletonOptions _npgsqlSingletonOptions;
#elif NET6_0_OR_GREATER
    private readonly INpgsqlSingletonOptions _npgsqlSingletonOptions;
#else
    [NotNull] private readonly INpgsqlOptions _npgsqlOptions;
#endif

    // ReSharper disable once ConvertToPrimaryConstructor
    public NpgsqlHintQuerySqlGeneratorFactory(
        QuerySqlGeneratorDependencies dependencies,
#if NET8_0_OR_GREATER
        IRelationalTypeMappingSource typeMappingSource,
        INpgsqlSingletonOptions npgsqlSingletonOptions)
#elif NET6_0_OR_GREATER
        INpgsqlSingletonOptions npgsqlSingletonOptions)
#else
        INpgsqlOptions npgsqlOptions)
#endif
    {
        _dependencies = dependencies;
#if NET8_0_OR_GREATER
        _typeMappingSource = typeMappingSource;
        _npgsqlSingletonOptions = npgsqlSingletonOptions;
#elif NET6_0_OR_GREATER
        _npgsqlSingletonOptions = npgsqlSingletonOptions;
#else
        _npgsqlOptions = npgsqlOptions;
#endif
    }

    public virtual QuerySqlGenerator Create()
        => new NpgsqlHintQuerySqlGenerator(
            _dependencies,
#if NET8_0_OR_GREATER
            _typeMappingSource,
#endif
#if NET6_0_OR_GREATER
            _npgsqlSingletonOptions.ReverseNullOrderingEnabled,
            _npgsqlSingletonOptions.PostgresVersion);
#else
            _npgsqlOptions.ReverseNullOrderingEnabled,
            _npgsqlOptions.PostgresVersion!);
#endif
}
