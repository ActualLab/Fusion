// using ActualLab.Resilience;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartORP;

// ============================================================================
// PartO-RP.md snippets: Operation Reprocessing
// ============================================================================

public class ReprocessorSetup
{
    #region PartORP_EnableReprocessor
    // var fusion = services.AddFusion();
    // fusion.AddOperationReprocessor();  // Enable operation reprocessing
    #endregion

    #region PartORP_Configuration
    // fusion.AddOperationReprocessor(_ => new() {
    //     MaxRetryCount = 3,  // Default: 3
    //     RetryDelays = RetryDelaySeq.Exp(0.50, 3, 0.33),  // Exponential backoff
    //     Filter = (command, context) => /* custom filter */,
    // });
    #endregion

    #region PartORP_RetryDelays
    // RetryDelaySeq.Exp(0.50, 3, 0.33)
    // Base: 0.5 seconds
    // Max multiplier: 3x
    // Jitter: ±33%
    //
    // Produces delays approximately:
    // Retry 1: ~0.5s (0.33s - 0.67s)
    // Retry 2: ~1.65s (1.1s - 2.2s)
    // Retry 3: ~5.45s (3.6s - 7.3s)
    #endregion
}

#region PartORP_DefaultFilter
// public static bool DefaultFilter(ICommand command, CommandContext context)
// {
//     // Only on server
//     if (!RuntimeInfo.IsServer)
//         return false;
//
//     // Skip delegating commands (proxies)
//     if (command is IDelegatingCommand)
//         return false;
//
//     // Skip scoped Commander commands (UI commands)
//     if (context.Commander.Hub.IsScoped)
//         return false;
//
//     // Only root-level commands
//     return true;
// }
#endregion

public class SuperTransientExample
{
    #region PartORP_SuperTransient
    // Super-transient errors retry without limit
    // if (transiency == Transiency.SuperTransient)
    //     return true;  // Always retry
    #endregion
}

#region PartORP_CustomTransiencyResolver
// services.AddSingleton<ITransiencyResolver<IOperationReprocessor>, MyTransiencyResolver>();
//
// public class MyTransiencyResolver : ITransiencyResolver<IOperationReprocessor>
// {
//     public Transiency GetTransiency(Exception error)
//     {
//         if (error is MyCustomRetryableException)
//             return Transiency.Transient;
//
//         if (error is MyRateLimitException)
//             return Transiency.SuperTransient;  // Retry indefinitely
//
//         return Transiency.Unknown;  // Fall through to other resolvers
//     }
// }
#endregion

#region PartORP_CustomRetryPolicy
// fusion.AddOperationReprocessor(_ => new() {
//     MaxRetryCount = 5,
//     RetryDelays = RetryDelaySeq.Exp(
//         TimeSpan.FromSeconds(1),     // Base delay
//         TimeSpan.FromSeconds(30)),   // Max delay
//     Filter = (command, context) => {
//         // Don't retry admin commands
//         if (command is IAdminCommand)
//             return false;
//
//         // Don't retry commands from specific users
//         if (command is IUserCommand userCmd && userCmd.UserId == SpecialUserId)
//             return false;
//
//         return OperationReprocessor.DefaultFilter(command, context);
//     },
// });
#endregion

// Helper types for examples
public interface IAdminCommand : ICommand;
public interface IUserCommand : ICommand
{
    long UserId { get; }
}
public static class SpecialUsers
{
    public static readonly long SpecialUserId = 0;
}
public class MyCustomRetryableException : Exception;
public class MyRateLimitException : Exception;
