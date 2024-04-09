namespace ActualLab.Async;

public static partial class TaskExt
{
    public static async Task YieldDelay()
        => await Task.Yield();

    public static async Task YieldDelay(int yieldCount)
    {
        for (var i = 0; i < yieldCount; i++)
            await Task.Yield();
    }
}
