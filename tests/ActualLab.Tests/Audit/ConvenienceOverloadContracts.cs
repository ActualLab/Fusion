using ActualLab.IO;
using ActualLab.Locking;
using ActualLab.Resilience;

namespace ActualLab.Tests.Audit;
internal static class ConvenienceOverloadContracts
{
    public static void Verify(FilePath path, IRetryPolicy retryPolicy)
    {
        _ = path.WriteText("");
        _ = path.WriteLines(GetLines());
        _ = FileLock.Lock(path);
        _ = retryPolicy.Run<int>(static _ => Task.FromResult(0));
        _ = retryPolicy.RunIsolated<int>(static _ => Task.FromResult(0));
    }

    private static async IAsyncEnumerable<string> GetLines()
    {
        await Task.Yield();
        yield return "";
    }
}
