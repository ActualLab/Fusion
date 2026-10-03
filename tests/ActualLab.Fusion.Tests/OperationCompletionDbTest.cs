using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

// ICompletion<TCommand> is the escape hatch for a command whose invalidations can't all be declared
// up front, so it has to reach every host - which is only testable against a real operation log.
public class OperationCompletionDbTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task ICompletionRunsOnTheOriginAndOnTheOtherHost()
    {
        if (MustSkip()) return;

        await using var serving = await WebHost.Serve();
        var origin = WebServices.GetRequiredService<CompletionEchoCatcher>();
        var other = Services.GetRequiredService<CompletionEchoCatcher>();
        var originHostId = WebServices.GetRequiredService<HostId>().Id;

        await WebServices.Commander().Call(new CompletionEcho_Set("ab", 1));

        // The origin runs it from CompletionProducer right after the commit...
        await WhenCaught(origin, (originHostId, "ab"), 5);
        // ...and the other host runs it when its operation log reader picks the row up
        await WhenCaught(other, (originHostId, "ab"), 10);
    }

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (isClient)
            return;

        var commander = services.AddCommander();
        services.AddFusion().AddService<CompletionEchoService>();
        services.AddSingleton<CompletionEchoCatcher>();
        commander.AddHandlers<CompletionEchoCatcher>();
    }

    // Private methods

    // The catcher is a plain list rather than a reactive state, so there is nothing for
    // ComputedTest.When to wait on here
    private async Task WhenCaught(
        CompletionEchoCatcher catcher, (string HostId, string Key) expected, double timeout)
    {
        var endsAt = CpuTimestamp.Now + TimeSpan.FromSeconds(timeout);
        while (true) {
            var completions = catcher.Completions;
            if (completions.Count != 0) {
                completions.Should().Equal(expected);
                return;
            }
            if (CpuTimestamp.Now > endsAt)
                throw new TimeoutException($"No completion was caught in {timeout}s.");

            await Delay(0.1);
        }
    }
}
