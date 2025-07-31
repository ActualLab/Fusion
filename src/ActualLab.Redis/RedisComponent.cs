using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;

namespace ActualLab.Redis;

public sealed class RedisComponent<T>(RedisConnector connector, Func<IConnectionMultiplexer, T> factory)
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private volatile Task<Temporary<T>>? _resultTask;

    public RedisComponent(RedisDb redisDb, Func<IConnectionMultiplexer, T> factory)
        : this(redisDb.Connector, factory)
    { }

    public ValueTask<T> Get(CancellationToken cancellationToken = default)
    {
        var valueTask = GetTemporary(cancellationToken);
        return valueTask.IsCompletedSuccessfully
            ? new ValueTask<T>(valueTask.Result.Value)
            : Unwrap(valueTask);

        static async ValueTask<T> Unwrap(ValueTask<Temporary<T>> valueTask1)
        {
            var (result, _) = await valueTask1.ConfigureAwait(false);
            return result;
        }
    }

    public ValueTask<Temporary<T>> GetTemporary(CancellationToken cancellationToken = default)
    {
        if (TryGetTask(out var resultTask))
            return resultTask.WaitAsync(cancellationToken).ToValueTask();

        lock (_lock) {
            if (TryGetTask(out resultTask))
                return resultTask.WaitAsync(cancellationToken).ToValueTask();

            return (_resultTask = NewTask()).ToValueTask();
        }
    }

    // Private methods

    private bool TryGetTask([NotNullWhen(true)] out Task<Temporary<T>>? resultTask)
    {
        resultTask = _resultTask;
        if (resultTask is null)
            return false;

        if (!resultTask.IsCompletedSuccessfully())
            return true;

        var (_, goneToken) = resultTask.GetAwaiter().GetResult();
        return !goneToken.IsCancellationRequested;
    }

    private async Task<Temporary<T>> NewTask()
    {
        while (true) {
            try {
                var (multiplexer, goneToken) = await connector.GetMultiplexer().ConfigureAwait(false);
                var result = factory.Invoke(multiplexer);
                return (result, goneToken);
            }
            catch {
                // Intended
            }
        }
    }
}
