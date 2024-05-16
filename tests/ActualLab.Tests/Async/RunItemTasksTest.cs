namespace ActualLab.Tests.Async;

public class RunItemTasksTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await GetItems().RunItemTasks(
            async (i, ct) => {
                Out.WriteLine($"++ {i}");
                await TaskExt.NewNeverEndingUnreferenced().WaitAsync(ct).SilentAwait();
                Out.WriteLine($"-? {i}");
                await Task.Delay(300);
                Out.WriteLine($"-- {i}");
            },
            (active, _, _) => Out.WriteLine($"Change to: {active.ToDelimitedString()}"),
            cts.Token
            ).SilentAwait();
    }

    // Private methods

    private async IAsyncEnumerable<List<int>> GetItems()
    {
        for (var i = 0;; i++) {
            var bits = GetBits(i);
            yield return bits;
            await Task.Delay(100);
        }
    }

    private static List<int> GetBits(int x)
    {
        var bits = new List<int>();
        var mask = 1;
        for (var i = 0; mask <= x; i++, mask <<= 1)
            if ((x & mask) != 0)
                bits.Add(i);
        return bits;
    }
}
