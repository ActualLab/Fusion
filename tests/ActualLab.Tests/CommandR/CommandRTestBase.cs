using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Tests.CommandR.Services;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace ActualLab.Tests.CommandR;

public class CommandRTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    protected bool UseLogging { get; set; } = true;
    protected bool UseDebugLog { get; set; } = true;
    protected bool UseDbContext { get; set; }
    protected Func<CommandHandler, Type, bool>? CommandHandlerFilter { get; set; }

    protected virtual IServiceProvider CreateServices()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        var services = serviceCollection.BuildServiceProvider();

        if (UseDbContext) {
            var dbContextFactory = services.GetRequiredService<IDbContextFactory<TestDbContext>>();
            using var dbContext = dbContextFactory.CreateDbContext();
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        }
        return services;
    }

    private void ConfigureServices(ServiceCollection services)
    {
        if (UseLogging)
            services.AddLogging(logging => {
                var debugCategories = new List<string> {
                    "ActualLab.CommandR",
                    "ActualLab.Tests",
                };

                bool LogFilter(string? category, LogLevel level)
                    => debugCategories.Any(x => category?.StartsWith(x) ?? false)
                        && level >= LogLevel.Debug;

                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Debug);
                if (UseDebugLog)
                    logging.AddDebug();
                // XUnit logging requires weird setup b/c otherwise it filters out
                // everything below LogLevel.Information
                logging.AddProvider(
#pragma warning disable CS0618
                    new XunitTestOutputLoggerProvider(
                        new TestOutputHelperAccessor() { Output = Out },
                        LogFilter));
#pragma warning restore CS0618
            });

        var commander = services.AddCommander();
        if (CommandHandlerFilter is not null)
            commander.AddHandlerFilter(CommandHandlerFilter);

        services.AddFusion();
        if (UseDbContext) {
            services.AddTransientDbContextFactory<TestDbContext>(db => {
                db.UseSqlite($"Data Source={GetTestSqliteFilePath()}", sqlite => { });
            });
            services.AddDbContextServices<TestDbContext>(db => {
                db.AddOperations();
                db.AddEntityResolver<string, User>();
            });
        }

        services.AddSingleton<LogCommandHandler>();
        commander.AddHandlers<LogCommandHandler>();

        services.AddSingleton<LogEnterExitService>();
        commander.AddHandlers<LogEnterExitService>();

        services.AddSingleton<UserService>();
        commander.AddHandlers<UserService>();

        commander.AddService<IMathService, MathService>();
    }
}
