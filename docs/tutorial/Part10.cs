using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
using Microsoft.EntityFrameworkCore;
using static System.Console;

namespace Tutorial;

public static class Part10{
    public class AppDbContext(DbContextOptions options) : DbContextBase(options)
    {
        #region Part10_DbSet
        public DbSet<DbOperation> Operations { get; protected set; } = null!;
        #endregion
        
    }
    
    // public static void Configure(IServiceCollection services){
    //    services.AddDbContextServices<AppDbContext>(db => {
    //         // Uncomment if you'll be using AddRedisOperationLogChangeTracking 
    //         // db.AddRedisDb("localhost", "Fusion.Tutorial.Part10");
            
    //         db.AddOperations(operations => {
    //             // This call enabled Operations Framework (OF) for AppDbContext. 
    //             operations.ConfigureOperationLogReader(_ => new() {
    //                 // We use FileBasedDbOperationLogChangeTracking, so unconditional wake up period
    //                 // can be arbitrary long - all depends on the reliability of Notifier-Monitor chain.
    //                 // See what .ToRandom does - most of timeouts in Fusion settings are RandomTimeSpan-s,
    //                 // but you can provide a normal one too - there is an implicit conversion from it.
    //                 UnconditionalCheckPeriod = TimeSpan.FromSeconds(Env.IsDevelopment() ? 60 : 5).ToRandom(0.05),
    //             });
    //             // Optionally enable file-based log change tracker 
    //             operations.AddFileBasedOperationLogChangeTracking();
                
    //             // Or, if you use PostgreSQL, use this instead of above line
    //             // operations.AddNpgsqlOperationLogChangeTracking();
                
    //             // Or, if you use Redis, use this instead of above line
    //             // operations.AddRedisOperationLogChangeTracking();
    //         });
    //     });
    // }

    // public async Task<ChatMessage> PostMessage(Session session, string text, CancellationToken cancellationToken = default)
    // {
    //     await using var dbContext = CreateDbContext().ReadWrite();
    //     // Actual code...

    //     // Invalidation
    //     using (Invalidation.Begin())
    //     _ = PseudoGetAnyChatTail();
    //     return message;
    // }

    // public virtual async Task SignOut(SignOutCommand command, CancellationToken cancellationToken = default)
    // {
    //     // ...
    //     var context = CommandContext.GetCurrent();
    //     if (Invalidation.IsActive) {
    //         // Fetch operation item
    //         var invSessionInfo = context.Operation.Items.Get<SessionInfo>();
    //         if (invSessionInfo != null) {
    //             // Use it
    //             _ = GetUser(invSessionInfo.UserId, default);
    //             _ = GetUserSessions(invSessionInfo.UserId, default);
    //         }
    //         return;
    //     }

    //     await using var dbContext = await CreateOperationDbContext(cancellationToken).ConfigureAwait(false);

    //     var dbSessionInfo = await Sessions.FindOrCreate(dbContext, session, cancellationToken).ConfigureAwait(false);
    //     var sessionInfo = dbSessionInfo.ToModel();
    //     if (sessionInfo.IsSignOutForced)
    //         return;
        
    //     // Store operation item for invalidation logic
    //     context.Operation.Items.Set(sessionInfo);
    //     // ... 
    // }

    // public record PostMessageCommand(Session Session, string Text) : ICommand<ChatMessage>
    // {
    //     // Newtonsoft.Json needs this constructor to deserialize this record
    //     public PostMessageCommand() : this(Session.Null, "") { } 
    // }
}