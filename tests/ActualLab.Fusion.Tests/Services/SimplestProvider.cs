using ActualLab.Resilience;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

#pragma warning disable CA1024, CA1067

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true, SuppressSourceGeneration = true)]
public partial record SetValueCommand : ICommand<Unit>
{
    [DataMember, MemoryPackOrder(0)]
    public string Value { get; init; } = "";
}

public interface ISimpleProviderImpl
{
    // These two properties are here solely for testing purposes
    public int GetValueCallCount { get; }
    public int GetCharCountCallCount { get; }

    public void SetValue(string? value);
}

public interface ISimplestProvider : IComputeService
{
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<string?> GetValue();
    [ComputeMethod(MinCacheDuration = 0.5, TransientErrorInvalidationDelay = 0.5)]
    public Task<int> GetCharCount();
    [ComputeMethod(TransientErrorInvalidationDelay = 0.5)]
    public Task<int> Fail(Type exceptionType);

    [CommandHandler]
    public Task SetValue(SetValueCommand command, CancellationToken cancellationToken = default);
}

public class SimplestProvider : ISimplestProvider, ISimpleProviderImpl, IHasId<Type>, IHasIsDisposed
{
    private static volatile string? _value;
    private readonly bool _isCaching;

    public Type Id => GetType();
    public int GetValueCallCount { get; private set; }
    public int GetCharCountCallCount { get; private set; }
    public bool IsDisposed => false;

    public SimplestProvider()
        => _isCaching = GetType().Name.EndsWith("Proxy");

    public void SetValue(string? value)
    {
        Interlocked.Exchange(ref _value, value);
        Invalidate();
    }

    public virtual Task<string?> GetValue()
    {
        GetValueCallCount++;
        return Task.FromResult(_value);
    }

    public virtual async Task<int> GetCharCount()
    {
        GetCharCountCallCount++;
        try {
            var value = await GetValue().ConfigureAwait(false);
            return value!.Length;
        }
        catch (NullReferenceException e) {
            throw new TransientException(null, e);
        }
    }

    public virtual Task<int> Fail(Type exceptionType)
    {
        var e = new ExceptionInfo(exceptionType, "Fail!");
        throw e.ToException()!;
    }

    public virtual Task SetValue(SetValueCommand command, CancellationToken cancellationToken = default)
    {
        SetValue(command.Value);
        return Task.CompletedTask;
    }

    protected virtual void Invalidate()
    {
        if (!_isCaching)
            return;

        using (Invalidation.Begin())
            _ = GetValue().AssertCompleted();

        // No need to invalidate GetCharCount,
        // since it will be invalidated automatically.
    }
}
