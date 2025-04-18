using ActualLab.Redis;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Redis;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RedisQueueTest(ITestOutputHelper @out) : RedisTestBase(@out)
{
    [SkipOnGitHubFact]
    public async Task BasicTest()
    {
        await using var queue = await CreateQueue<string>("q", true).ConfigureAwait(false);
        var sw = new Stopwatch();
        sw.Start();

        Out.WriteLine($"{sw.ElapsedMilliseconds}: <- 1");
        await queue.Enqueue("1");
        Out.WriteLine($"{sw.ElapsedMilliseconds}: -> {await queue.Dequeue()}");

        Out.WriteLine($"{sw.ElapsedMilliseconds}: <- 2");
        await queue.Enqueue("2");
        Out.WriteLine($"{sw.ElapsedMilliseconds}: -> {await queue.Dequeue()}");
    }

    [SkipOnGitHubFact]
    public async Task DistributionTest()
    {
        var queueName = "iq";
        await using var queue = await CreateQueue<int>(queueName, true).ConfigureAwait(false);

        var itemCount = 10_000;
        var writerCount = 4;
        var readerCount = 8;

        async Task Writer(int writerIndex) {
            await using var q = await CreateQueue<int>(queueName).ConfigureAwait(false);
            var rnd = new Random();
            for (var i = writerIndex; i < itemCount; i += writerCount) {
                await q.Enqueue(i).ConfigureAwait(false);
                var delay = rnd.Next(10);
                if (delay < 3)
                    await Task.Delay(delay);
            }
        }

        var writeTasks = Enumerable.Range(0, writerCount)
            .Select(i => Task.Run(() => Writer(i)))
            .ToArray();
        var writeTask = Task.WhenAll(writeTasks);

        async Task<List<int>> Reader() {
            await using var q = await CreateQueue<int>(queueName).ConfigureAwait(false);
            var rnd = new Random();
            var list = new List<int>();
            while (true) {
                var valueResult = await q.Dequeue()
                    .WaitResultAsync(TimeSpan.FromSeconds(1))
                    .ConfigureAwait(false);
                if (!valueResult.IsValue(out var value)) {
                    writeTask.IsCompleted.Should().BeTrue();
                    break;
                }
                list.Add(value);
                var delay = rnd.Next(10);
                if (delay < 2)
                    await Task.Delay(delay);
            }
            return list;
        }

        var sw = new Stopwatch();
        sw.Start();

        var readTasks = Enumerable.Range(0, readerCount)
            .Select(_ => Task.Run(Reader))
            .ToArray();
        var lists = await Task.WhenAll(readTasks).ConfigureAwait(false);
        Out.WriteLine($"{sw.Elapsed}: completed");

        var items = lists.SelectMany(i => i).ToImmutableHashSet();
        items.Count.Should().Be(itemCount);
    }

    private async Task<RedisQueue<T>> CreateQueue<T>(string id, bool reset = false)
    {
        var queue = GetRedisDb().GetQueue<T>(id, new() {
            EnqueueCheckPeriod = TimeSpan.FromSeconds(2),
        });
        if (reset)
            await queue.Remove().ConfigureAwait(false);
        return queue;
    }
}
