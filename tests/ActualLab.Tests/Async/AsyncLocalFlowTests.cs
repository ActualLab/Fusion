namespace ActualLab.Tests.Async;

public class AsyncLocalFlowTests(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task Test1()
    {
        var myLocal = new AsyncLocal<int>();
        var whenT3Started = TaskCompletionSourceExt.New();
        var signal = TaskCompletionSourceExt.New();
        var myLocalObservedInT3 = TaskCompletionSourceExt.New<int>();

        // Task 1
        Task? t2 = null;
        Task? t3 = null;
        Task t1 = Task.Run(async () => {
            myLocal.Value = 1;

            // Task 2 (child of task1)
            t2 = Task.Run(async () => {
                myLocal.Value = 2;

                // Task 3 (child of task2) – should see value 2
                t3 = Task.Run(async () => {
                    whenT3Started.TrySetResult();       // tell task1 that t3 exists

                    await signal.Task;               // wait for task1 to signal
                    myLocalObservedInT3.TrySetResult(myLocal.Value); // capture myLocal seen in t3
                });

                await t3;
            });

            // Ensure t3 has been created from task2's context before we change task1's value
            await whenT3Started.Task;

            // Change myLocal.value in task1 and then signal t3 to read its value
            myLocal.Value = 3;
            signal.TrySetResult();

            await t2;
        });

        await t1;

        var valueInT3 = await myLocalObservedInT3.Task;
        valueInT3.Should().Be(2);

        await t3!.ContinueWith(_ => {
            myLocal.Value.Should().Be(0);
        }, TaskScheduler.Default);
    }

    [Fact]
    public async Task Test2()
    {
        var myLocal = new AsyncLocal<int>();

        async Task Callee() {
            myLocal.Value = 2;
            myLocal.Value.Should().Be(2);
            await Task.Yield();
        }

        myLocal.Value = 1;
        await Callee();
        myLocal.Value.Should().Be(1);
    }
}
