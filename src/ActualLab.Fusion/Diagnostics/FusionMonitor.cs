using System.Globalization;
using System.Text;

namespace ActualLab.Fusion.Diagnostics;

public sealed class FusionMonitor : WorkerBase
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    // Cached delegates
    private readonly Action<Computed, bool> _onAccess;
    private readonly Action<Computed> _onRegister;
    private readonly Action<Computed> _onUnregister;

    private Statistics _statistics;

    // Services
    private IServiceProvider Services { get; }
    private ILogger Log { get; }

    // Settings
    public RandomTimeSpan SleepPeriod { get; init; } = TimeSpan.Zero;
    public TimeSpan CollectPeriod { get; init; } = TimeSpan.FromMinutes(1);
    public Sampler AccessSampler { get; init; } = Sampler.EveryNth(8);
    public Func<Computed, bool> AccessFilter { get; init; } = static _ => true;
    public Sampler RegistrationSampler { get; init; } = Sampler.EveryNth(8);
    public Sampler RegistrationLogSampler { get; init; } = Sampler.Never; // Applied after RegistrationSampler!
    public Action<Dictionary<string, (int, int)>>? AccessStatisticsPreprocessor { get; init; }
    public Action<Dictionary<string, (int, int)>>? RegistrationStatisticsPreprocessor { get; init; }

    public FusionMonitor(IServiceProvider services)
    {
        Services = services;
        Log = services.LogFor(GetType());
        _statistics = new();
        _onAccess = OnAccess;
        _onRegister = OnRegistration;
        _onUnregister = OnUnregistration;
    }

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        try {
            Attach();
            Log.LogInformation("Running");

            // We want to format this log message as quickly as possible, so use StringBuilder here
            var sb = new StringBuilder();
            var fp = CultureInfo.InvariantCulture;
            while (!cancellationToken.IsCancellationRequested) {
                var sleepDelay = SleepPeriod.Next();
                if (sleepDelay > TimeSpan.Zero) {
                    Detach();
                    Log.LogInformation("Sleeping for {SleepPeriod}...", sleepDelay);
                    await Task.Delay(sleepDelay, cancellationToken).ConfigureAwait(false);
                    Attach();
                }

                Log.LogInformation("Collecting for {CollectPeriod}...", CollectPeriod);
                await Task.Delay(CollectPeriod, cancellationToken).ConfigureAwait(false);
                var (accesses, registrations, invalidationPaths) = GetAndResetStatistics();
                AccessStatisticsPreprocessor?.Invoke(accesses);
                RegistrationStatisticsPreprocessor?.Invoke(registrations);

                // Accesses
                if (accesses.Count != 0) {
                    var m = AccessSampler.InverseProbability;
                    sb.AppendFormat(fp, "Reads, sampled with {0}:", AccessSampler);
                    var hitSum = 0;
                    var missSum = 0;
                    foreach (var (key, (hits, misses)) in accesses.OrderByDescending(kv => kv.Value.Item1 + kv.Value.Item2)) {
                        hitSum += hits;
                        missSum += misses;
                        var reads = hits + misses;
                        sb.AppendFormat(fp, "\r\n- {0}: {1:F1} reads -> {2:P2} hits",
                            key, reads * m, (double)hits / reads);
                    }
                    var readSum = hitSum + missSum;
                    sb.AppendFormat(fp, "\r\nTotal: {0:F1} reads -> {1:P2} hits",
                        readSum * m, (double)hitSum / readSum);
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                    Log.LogInformation(sb.ToString());
                    sb.Clear();
                }

                // Registrations
                if (registrations.Count != 0) {
                    var m = RegistrationSampler.InverseProbability;
                    sb.AppendFormat(fp, "Updates (+) and invalidations (-), sampled with {0}:", RegistrationSampler);
                    var addSum = 0;
                    var subSum = 0;
                    foreach (var (key, (adds, subs)) in registrations.OrderByDescending(kv => kv.Value.Item1)) {
                        addSum += adds;
                        subSum += subs;
                        sb.AppendFormat(fp, "\r\n- {0}: +{1:F1} -{2:F1}", key, adds * m, subs * m);
                    }
                    sb.AppendFormat(fp, "\r\nTotal: +{0:F1} -{1:F1}", addSum * m, subSum * m);
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                    Log.LogInformation(sb.ToString());
                    sb.Clear();
                }

                // Invalidation origins
                if (invalidationPaths.Count != 0) {
                    var m = RegistrationSampler.InverseProbability;
                    sb.AppendFormat(fp, "Invalidation paths, sampled with {0}:", RegistrationSampler);
                    var valueSum = 0;
                    foreach (var (key, value) in invalidationPaths.OrderByDescending(kv => kv.Value)) {
                        valueSum += value;
                        var format = key.Category is null
                            ? "\r\n- {1} by {2}: +{3:F1}"
                            : "\r\n- {0} <- {1} by {2}: +{3:F1}";
                        sb.AppendFormat(fp, format, key.Category, key.OriginCategory, key.OriginSource.ToString(), value * m);
                    }
                    sb.AppendFormat(fp, "\r\nTotal: +{0:F1}", valueSum * m);
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                    Log.LogInformation(sb.ToString());
                    sb.Clear();
                }
            }
        }
        finally {
            Detach();
            GetAndResetStatistics();
        }
    }

    // Private methods

    private void Attach()
    {
        GetAndResetStatistics();
        ComputedRegistry.OnAccess += _onAccess;
        ComputedRegistry.OnRegister += _onRegister;
        ComputedRegistry.OnUnregister += _onUnregister;
    }

    private void Detach()
    {
        ComputedRegistry.OnAccess -= _onAccess;
        ComputedRegistry.OnRegister -= _onRegister;
        ComputedRegistry.OnUnregister -= _onUnregister;
    }

    private Statistics GetAndResetStatistics()
    {
        lock (_lock) {
            (var statistics, _statistics) = (_statistics, new());
            return statistics;
        }
    }

    // Event handlers

    private void OnAccess(Computed computed, bool isNew)
    {
        if (AccessSampler.Next())
            return;
        if (!AccessFilter.Invoke(computed))
            return;

        var input = computed.Input;
        var category = input.Category;
        var dHit = isNew ? 0 : 1;
        var dMiss = 1 - dHit;
        lock (_lock) {
            var accesses = _statistics.Accesses;
            if (accesses.TryGetValue(category, out var counts))
                accesses[category] = (counts.Item1 + dHit, counts.Item2 + dMiss);
            else
                accesses[category] = (dHit, dMiss);
        }
    }

    private void OnRegistration(Computed computed)
    {
        if (RegistrationSampler.Next())
            return;

        var input = computed.Input;
        var category = input.Category;
        lock (_lock) {
            var registrations = _statistics.Registrations;
            if (registrations.TryGetValue(category, out var counts))
                registrations[category] = (counts.Item1 + 1, counts.Item2);
            else
                registrations[category] = (1, 0);
        }

        if (RegistrationLogSampler.Next())
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.LogDebug("+ " + input);
    }

    private void OnUnregistration(Computed computed)
    {
        if (RegistrationSampler.Next())
            return;

        var input = computed.Input;
        var category = input.Category;
        lock (_lock) {
            var registrations = _statistics.Registrations;
            if (registrations.TryGetValue(category, out var counts))
                registrations[category] = (counts.Item1, counts.Item2 + 1);
            else
                registrations[category] = (0, 0);

            var origin = computed.GetInvalidationOrigin();
            var atOrigin = ReferenceEquals(computed, origin);
            var key = (
                atOrigin ? null : category,
                origin.Input.Category,
                origin.InvalidationSource);
            if (!MustIgnore(atOrigin, key.InvalidationSource)) {
                var invalidationPaths = _statistics.InvalidationPaths;
                if (invalidationPaths.TryGetValue(key, out var count))
                    invalidationPaths[key] = count + 1;
                else
                    invalidationPaths[key] = 1;
            }
        }

        if (RegistrationLogSampler.Next())
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.LogDebug("- " + input);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MustIgnore(bool atOrigin, InvalidationSource source)
        => atOrigin && (source.IsNone || source.Equals(InvalidationSource.InitialState));

    // Nested types

    private sealed record Statistics(
        Dictionary<string, (int, int)> Accesses,
        Dictionary<string, (int, int)> Registrations,
        Dictionary<(string? Category, string OriginCategory, InvalidationSource OriginSource), int> InvalidationPaths)
    {
        public Statistics()
            : this(new(StringComparer.Ordinal), new(StringComparer.Ordinal), new())
        { }
    }
}
