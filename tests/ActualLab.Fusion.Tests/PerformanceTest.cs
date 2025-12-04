using System.Text;
using System.Text.Json;
using ActualLab.Fusion.Tests.DbModel;
using ActualLab.Fusion.Tests.Services;
using ActualLab.OS;

namespace ActualLab.Fusion.Tests;

#pragma warning disable CS0162 // Unreachable code detected

public class PerformanceTest_Sqlite : PerformanceTestBase
{
    public PerformanceTest_Sqlite(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.Sqlite;
}

public class PerformanceTest_PostgreSql : PerformanceTestBase
{
    public PerformanceTest_PostgreSql(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.PostgreSql;
}

public class PerformanceTest_MariaDb : PerformanceTestBase
{
    public PerformanceTest_MariaDb(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.MariaDb;
}

public class PerformanceTest_SqlServer : PerformanceTestBase
{
    public PerformanceTest_SqlServer(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.SqlServer;
}

public class PerformanceTest_InMemoryDb : PerformanceTestBase
{
    public PerformanceTest_InMemoryDb(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.InMemory;
}

public abstract class PerformanceTestBase : FusionTestBase
{
    public int UserCount = 1000;
    public bool UseEntityResolver = false;

    protected PerformanceTestBase(ITestOutputHelper @out) : base(@out)
        => UseLogging = false;

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        var fusion = services.AddFusion();
        fusion.AddService<IUserService, UserService>();
        services.AddSingleton<IPlainUserService, UserService>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync().ConfigureAwait(false);
        var commander = Services.Commander();
        var tasks = new List<Task>();
        for (var i = 0; i < UserCount; i++)
            tasks.Add(commander.Call(new UserService_Add(new DbUser() {
                Id = i,
                Name = $"User_{i}",
            }, true)));
        await Task.WhenAll(tasks);
    }

    // [Fact]
    [Fact(Skip = "Performance")]
    public async Task ComputedPerformanceTest()
    {
        if (TestRunnerInfo.IsBuildAgent())
            return; // Shouldn't run this test on build agents

        var users = (UserService)Services.GetRequiredService<IUserService>();
        users.UseEntityResolver = UseEntityResolver;
        var plainUsers = (UserService)Services.GetRequiredService<IPlainUserService>();
        plainUsers.UseEntityResolver = UseEntityResolver;

        var fusionOpCountPerCore = 16_000_000;
        var fusionReadersPerCore = 20;
        var fusionIterationCount = fusionOpCountPerCore / fusionReadersPerCore;
        var fusionReaderCount = HardwareInfo.GetProcessorCountFactor(fusionReadersPerCore);

        var nonFusionOpCountPerCore = fusionOpCountPerCore / (UseEntityResolver ? 1000 : 2000);
        var nonFusionReadersPerCore = 40; // We need more readers here to maximize the throughput w/ parallel queries
        var nonFusionIterationCount = nonFusionOpCountPerCore / nonFusionReadersPerCore;
        var nonFusionReaderCount = HardwareInfo.GetProcessorCountFactor(nonFusionReadersPerCore);

        WriteLine($"Database: {DbType}" + (UseEntityResolver ? " (with DbEntityResolver)" : ""));
        WriteLine("With ActualLab.Fusion:");
        await Test("Multiple readers, 1 mutator", users, null, true,
            fusionReaderCount, fusionIterationCount);
        await Test("Single reader, no mutators", users, null, false,
            1, fusionOpCountPerCore);
        return;

        WriteLine("Without ActualLab.Fusion:");
        await Test("Multiple readers, 1 mutator", plainUsers, null, true,
            nonFusionReaderCount, nonFusionIterationCount);
        await Test("Single reader, no mutators", plainUsers, null, false,
            1, nonFusionOpCountPerCore);
    }

    private async Task Test(string title,
        IUserService users, Action<DbUser>? extraAction, bool enableMutations,
        int threadCount, int iterationCount, bool isWarmup = false)
    {
        if (!isWarmup)
            await Test(title, users, extraAction, enableMutations, threadCount, iterationCount / 2, true);

        var runCount = isWarmup ? 1 : 3;
        var operationCount = threadCount * iterationCount;
        WriteLine($"  {title}:");
        WriteLine($"    Setup: {FormatCount(operationCount)} calls ({threadCount} readers x {FormatCount(iterationCount)})");

        var bestElapsed = TimeSpan.MaxValue;
        var sb = new StringBuilder();
        for (var i = 0; i < runCount; i++) {
            if (i > 0)
                await Task.Delay(250).ConfigureAwait(false);
            var lastElapsed = await Run().ConfigureAwait(false);
            sb.Append(FormatCount(operationCount / lastElapsed.TotalSeconds)).Append(' ');
            bestElapsed = TimeSpanExt.Min(bestElapsed, lastElapsed);
        }
        WriteLine($"    Speed: {sb}-> {FormatCount(operationCount / bestElapsed.TotalSeconds)} calls/s");
        return;

        // ReSharper disable once LocalFunctionHidesMethod
        void WriteLine(string line) {
            if (!isWarmup)
                Out.WriteLine(line);
        }

        async Task<TimeSpan> Run() {
            using var stopCts = new CancellationTokenSource();
            var cancellationToken = stopCts.Token;
            var mutatorTask = enableMutations
                ? Task.Run(() => Mutator("W", cancellationToken), CancellationToken.None)
                : Task.CompletedTask;
            var whenReadySource = AsyncTaskMethodBuilderExt.New();
            var tasks = Enumerable
                .Range(0, threadCount)
                .Select(i => Task.Run(() => Reader($"R{i}", iterationCount, whenReadySource.Task), CancellationToken.None))
                .ToArray();
            var startedAt = CpuTimestamp.Now;
            whenReadySource.SetResult();
            var results = await Task.WhenAll(tasks);
            var elapsed = startedAt.Elapsed;

            // ReSharper disable once MethodHasAsyncOverload
            stopCts.Cancel();
            // ReSharper disable once MethodHasAsyncOverload
            await mutatorTask.SilentAwait(false);

            results.Length.Should().Be(threadCount);
            results.All(r => r == iterationCount).Should().BeTrue();
            return elapsed;
        }

        async Task Mutator(string name, CancellationToken cancellationToken) {
            var rnd = new Random();
            var count = 0L;

            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                var userId = (long)rnd.Next(UserCount);
                // Log.LogDebug($"{name}: R {userId}");
                var user = await users.Get(userId, cancellationToken).ConfigureAwait(false);
                user = user! with { Email = $"{++count}@counter.org" };
                // Log.LogDebug($"{name}: R done, U {user}");
                var updateCommand = new UserService_Update(user);
                await users.UpdateDirectly(updateCommand, cancellationToken).ConfigureAwait(false);

                // Log.LogDebug($"{name}: U {user} done");
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task<long> Reader(string name, int iterationCount1, Task whenReady) {
            var rnd = new Random();
            var count = 0L;

            await whenReady.ConfigureAwait(false);
            for (; iterationCount1 > 0; iterationCount1--) {
                var userId = (long)rnd.Next(UserCount);
                // Log.LogDebug($"{name}: R {userId}");
                var user = await users.Get(userId).ConfigureAwait(false);
                // Log.LogDebug($"{name}: R {userId} done");
                if (user!.Id == userId)
                    count++;
                extraAction?.Invoke(user);
            }
            return count;
        }

        string FormatCount(double value) {
            var scale = "";
            if (value >= 1000_000) {
                scale = "M";
                value /= 1000_000;
            }
            else if (value >= 1000) {
                scale = "K";
                value /= 1000;
            }
            return $"{value:N}{scale}";
        }
    }
}
