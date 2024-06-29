using ActualLab.Internal;

namespace ActualLab.Time.Testing;

public sealed class TestClock : MomentClock, IDisposable
{
    private volatile TestClockSettings _settings;

    public TestClockSettings Settings {
        get => _settings;
        set {
            if (!value.IsUsable)
                throw Errors.AlreadyUsed();
            var oldSettings = Interlocked.Exchange(ref _settings, value);
            oldSettings.Changed();
            oldSettings.Dispose();
        }
    }

    public override Moment Now
        => ToLocalTime(SystemClock.Instance.Now);

    [JsonConstructor, Newtonsoft.Json.JsonConstructor]
    public TestClock(TestClockSettings settings)
        => _settings = settings;
    public TestClock(TimeSpan localOffset = default, TimeSpan realOffset = default, double multiplier = 1)
        => _settings = new TestClockSettings(localOffset, realOffset, multiplier);
    public void Dispose() => _settings.Dispose();

    public override string ToString()
        => $"{GetType().Name}({Settings.LocalOffset} + {Settings.Multiplier} * (t - {Settings.RealOffset}))";

    // Operations

    public override Moment ToRealTime(Moment localTime) => Settings.ToRealTime(localTime);
    public override Moment ToLocalTime(Moment realTime) => Settings.ToLocalTime(realTime);
    public override TimeSpan ToRealDuration(TimeSpan localDuration) => Settings.ToLocalDuration(localDuration);
    public override TimeSpan ToLocalDuration(TimeSpan realDuration) => Settings.ToRealDuration(realDuration);

    public override async Task Delay(TimeSpan dueIn, CancellationToken cancellationToken = default)
    {
        if (dueIn == Timeout.InfiniteTimeSpan) {
            await Task.Delay(dueIn, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (dueIn < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(dueIn));

        TestClockSettings? settings = Settings;
        var dueAt = settings.Now + dueIn;
        while (true) {
            settings ??= Settings;
            var settingsChangedToken = settings.ChangedToken;
            var delta = (settings.ToRealTime(dueAt) - Moment.CpuNow).Positive();
            try {
                if (!cancellationToken.CanBeCanceled) {
                    await Task.Delay(delta, settingsChangedToken).ConfigureAwait(false);
                }
                else {
                    using var cts = cancellationToken.LinkWith(settingsChangedToken);
                    await Task.Delay(delta, cts.Token).ConfigureAwait(false);
                }
                return;
            }
            catch (OperationCanceledException) {
                if (!settingsChangedToken.IsCancellationRequested)
                    throw;
            }
            settings = null;
        }
    }

    // Helpers

    public TestClock SetTo(Moment now)
    {
        var s = Settings;
        var realNow = Moment.Now;
        var delta = now - s.ToLocalTime(realNow);
        Settings = (s.LocalOffset + delta, s.RealOffset, s.Multiplier);
        return this;
    }

    public TestClock OffsetBy(long offsetInMilliseconds)
        => OffsetBy(TimeSpan.FromMilliseconds(offsetInMilliseconds));

    public TestClock OffsetBy(TimeSpan offset)
    {
        var s = Settings;
        Settings = (offset + s.LocalOffset, s.RealOffset, s.Multiplier);
        return this;
    }

    public TestClock SpeedupBy(double multiplier)
    {
        var s = Settings;
        var realNow = Moment.Now;
        var localNow = s.ToLocalTime(realNow);
        Settings = (localNow.EpochOffset, -realNow.EpochOffset, multiplier * s.Multiplier);
        return this;
    }
}
