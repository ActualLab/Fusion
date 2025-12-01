using ActualLab.Resilience.Internal;

namespace ActualLab.Resilience;

public abstract record ChaosMaker
{
    public static readonly NoneChaosMaker None = new();
    public static ChaosMaker Default { get; set; } = None;

    public bool IsNone => ReferenceEquals(None, this);
    public bool IsEnabled => !ReferenceEquals(None, this) && (this is not GatingChaosMaker g || g.IsEnabled);

    // Delay
    public static DelayChaosMaker Delay(RandomTimeSpan duration)
        => new(duration);
    public static DelayChaosMaker Delay(double duration, double maxDelta)
        => new(new RandomTimeSpan(duration, maxDelta));

    // Error
    public static ErrorChaosMaker<TimeoutException> TimeoutError { get; } = Error<TimeoutException>();
    public static ErrorChaosMaker<TransientException> TransientError { get; } = Error<TransientException>();
    public static ErrorChaosMaker<NullReferenceException> NullReferenceError { get; } = Error<NullReferenceException>();
    public static ErrorChaosMaker<TException> Error<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TException>(
        string? message = null)
        where TException : Exception
        => new(message);

    // Act
    public abstract Task Act(object context, CancellationToken cancellationToken);

    public void TryEnable(bool mustEnable = true)
    {
        if (this is GatingChaosMaker g)
            g.IsEnabled = mustEnable;
    }

    // Transforms

    public GatingChaosMaker Gated(bool isEnabled = false) => new(this) { IsEnabled = isEnabled };
    public FilteringChaosMaker Filtered(string description, Func<object, bool> filter) => new(description, filter, this);
    public SamplingChaosMaker Sampled(Sampler sampler) => new(sampler, this);
    public DelayChaosMaker Delayed(RandomTimeSpan delay) => new(delay, this);
    public DelayChaosMaker Delayed(double duration, double maxDelta) => new(new RandomTimeSpan(duration, maxDelta), this);

    public static JoinChaosMaker operator |(ChaosMaker first, ChaosMaker second) => new(first, second);
    public static SamplingChaosMaker operator *(double probability, ChaosMaker next) => new(probability, next);
    public static SamplingChaosMaker operator *(Sampler sampler, ChaosMaker next) => new(sampler, next);
}
