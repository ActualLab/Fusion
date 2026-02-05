using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ActualLab.Fusion.EntityFramework.Internal;

/// <summary>
/// Extension methods for <see cref="DatabaseFacade"/> providing helpers to detect
/// in-memory providers and disable auto-transactions and savepoints.
/// </summary>
public static class DatabaseFacadeExt
{
    public static bool IsInMemory(this DatabaseFacade database)
        => database.ProviderName?.EndsWith(".InMemory", StringComparison.Ordinal) ?? false;

    public static void DisableAutoTransactionsAndSavepoints(this DatabaseFacade database)
    {
#if NET7_0_OR_GREATER
        database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
#else
        database.AutoTransactionsEnabled = false;
#endif
#if NET6_0_OR_GREATER
        database.AutoSavepointsEnabled = false;
#endif
    }
}
