using ActualLab.Resilience;

namespace ActualLab.Fusion.Tests;

public class RetryDelayAwareErrorTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static readonly TimeSpan ErrorInvalidationDelay = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private sealed class RetryLaterException(TimeSpan retryDelay)
        : InvalidOperationException("Retry later."), IHasRetryDelay
    {
        public TimeSpan RetryDelay { get; } = retryDelay;
    }

    [Fact]
    public async Task DefersInvalidationToErrorRetryDelay()
    {
        // arrange
        var computed = await GetErrorComputed(new RetryLaterException(RetryDelay));
        var whenInvalidated = computed.WhenInvalidated();

        // act
        await Task.Delay(ErrorInvalidationDelay * 2.5);

        // assert
        whenInvalidated.IsCompleted.Should().BeFalse();
        await whenInvalidated.WaitAsync(RetryDelay);
    }

    [Fact]
    public async Task ErrorWithoutRetryDelayUsesTheConfiguredDelay()
    {
        // arrange
        var computed = await GetErrorComputed(new InvalidOperationException("No retry hint."));
        var whenInvalidated = computed.WhenInvalidated();

        // act, assert
        await whenInvalidated.WaitAsync(ErrorInvalidationDelay * 4);
    }

    // Private methods

    private async Task<Computed<int>> GetErrorComputed(Exception error)
    {
        var services = CreateServices();
        var state = services.StateFactory().NewComputed(
            new ComputedState<int>.Options() {
                InitialValue = -1,
                TryComputeSynchronously = false,
                ComputedOptions = ComputedOptions.Default with {
                    NonTransientErrorInvalidationDelay = ErrorInvalidationDelay,
                },
            },
            Computer);
        await state.WhenNonInitial().WaitAsync(TimeSpan.FromSeconds(5));
        var computed = state.Computed;
        computed.Error.Should().BeSameAs(error);
        return computed;

        Task<int> Computer(CancellationToken cancellationToken)
            => throw error;
    }
}
