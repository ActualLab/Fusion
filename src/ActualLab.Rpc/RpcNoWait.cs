using MessagePack;

namespace ActualLab.Rpc;

#pragma warning disable CS0169 // Field is never used

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
[DataContract, MemoryPackable, MessagePackObject]
public readonly partial struct RpcNoWait : IEquatable<RpcNoWait>
{
    // See https://github.com/dotnet/runtime/pull/107198
    [Obsolete("This member exists solely to make Mono AOT work. Don't use it!")]
    private readonly byte _dummyValue;

    // Equality
    public bool Equals(RpcNoWait other) => true;
    public override bool Equals(object? obj) => obj is RpcNoWait;
    public override int GetHashCode() => 0;
    public static bool operator ==(RpcNoWait left, RpcNoWait right) => left.Equals(right);
    public static bool operator !=(RpcNoWait left, RpcNoWait right) => !left.Equals(right);

    // Nested types

    public static class Tasks
    {
        public static readonly Task<RpcNoWait> Completed = Task.FromResult(default(RpcNoWait));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<RpcNoWait> From(Task task)
            => task.IsCompletedSuccessfully ? Completed : FromAsync(task);

        private static async Task<RpcNoWait> FromAsync(Task task)
        {
            await task.ConfigureAwait(false);
            return default;
        }
    }

    public static class ValueTasks
    {
        public static readonly ValueTask<RpcNoWait> Completed = ValueTaskExt.FromResult(default(RpcNoWait));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<RpcNoWait> From(ValueTask task)
            => task.IsCompletedSuccessfully ? Completed : FromAsync(task);

        private static async ValueTask<RpcNoWait> FromAsync(ValueTask task)
        {
            await task.ConfigureAwait(false);
            return default;
        }
    }

    public static class TaskSources
    {
        public static readonly TaskCompletionSource<RpcNoWait> Completed =
            new TaskCompletionSource<RpcNoWait>().WithResult(default);
    }
}
