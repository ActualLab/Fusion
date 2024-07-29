using System.Diagnostics;
using ActualLab.Resilience;

namespace ActualLab.Async;

public static class AsyncChainExt
{
    // RunXxx

    public static Task Run(
        this IEnumerable<AsyncChain> chains,
        CancellationToken cancellationToken = default)
        => chains.Run(false, cancellationToken);

    public static Task RunIsolated(
        this IEnumerable<AsyncChain> chains,
        CancellationToken cancellationToken = default)
        => chains.Run(true, cancellationToken);

    public static Task Run(
        this IEnumerable<AsyncChain> chains,
        bool isolate,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();
        using (isolate ? ExecutionContextExt.TrySuppressFlow() : default)
            foreach (var chain in chains)
                tasks.Add(chain.Run(cancellationToken));
        return Task.WhenAll(tasks);
    }

    // Construction primitives

    public static AsyncChain Rename(this AsyncChain asyncChain, string name)
        => asyncChain with { Name = name };

    public static AsyncChain WithTransiencyResolver(this AsyncChain asyncChain, TransiencyResolver transiencyResolver)
        => asyncChain with { TransiencyResolver = transiencyResolver };

    public static AsyncChain Append(this AsyncChain asyncChain, AsyncChain suffixChain)
        => new($"{asyncChain.Name} & {suffixChain.Name}", async cancellationToken => {
            await asyncChain.Start(cancellationToken).ConfigureAwait(false);
            await suffixChain.Start(cancellationToken).ConfigureAwait(false);
        }, asyncChain.TransiencyResolver);

    public static AsyncChain Prepend(this AsyncChain asyncChain, AsyncChain prefixChain)
        => new($"{prefixChain.Name} & {asyncChain.Name}", async cancellationToken => {
            await prefixChain.Start(cancellationToken).ConfigureAwait(false);
            await asyncChain.Start(cancellationToken).ConfigureAwait(false);
        }, asyncChain.TransiencyResolver);

    public static AsyncChain LogError(this AsyncChain asyncChain, ILogger? log)
    {
        if (log == null)
            return asyncChain;

        return asyncChain with {
            Start = async cancellationToken => {
                try {
                    await asyncChain.Start(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                    if (asyncChain.TransiencyResolver.Invoke(e).IsTerminal())
                        throw;

                    log.LogError(e, "{ChainName} failed", asyncChain.Name);
                }
            }
        };
    }

    public static AsyncChain Log(this AsyncChain asyncChain, ILogger? log)
        => asyncChain.Log(LogLevel.Information, log);
    public static AsyncChain Log(this AsyncChain asyncChain, LogLevel logLevel, ILogger? log)
    {
        if (log == null)
            return asyncChain;

        return asyncChain with {
            Start = async cancellationToken => {
                log.IfEnabled(logLevel)?.Log(logLevel, "{ChainName} started", asyncChain.Name);
                try {
                    await asyncChain.Start(cancellationToken).ConfigureAwait(false);
                    log.IfEnabled(logLevel)?.Log(logLevel,
                        "{ChainName} completed", asyncChain.Name);
                }
                catch (Exception e) {
                    if (e.IsCancellationOf(cancellationToken))
                        log.IfEnabled(logLevel)?.Log(logLevel,
                            "{ChainName} completed (cancelled)", asyncChain.Name);
                    else if (asyncChain.TransiencyResolver.Invoke(e).IsTerminal())
                        log.IfEnabled(logLevel)?.Log(logLevel,
                            "{ChainName} completed (terminal error)", asyncChain.Name);
                    else
                        log.LogError(e,
                            "{ChainName} failed", asyncChain.Name);
                    throw;
                }
            }
        };
    }

    public static AsyncChain Trace(this AsyncChain asyncChain, Func<Activity?>? activityFactory, ILogger? log = null)
    {
        if (activityFactory == null)
            return asyncChain.LogError(log);

        return asyncChain with {
            Start = async cancellationToken => {
                var activity = activityFactory.Invoke();
                try {
                    await asyncChain.LogError(log).Start(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) {
                    activity?.MaybeSetError(e, cancellationToken);
                    throw;
                }
                finally {
                    activity?.Dispose();
                }
            }
        };
    }

    public static AsyncChain Silence(this AsyncChain asyncChain, ILogger? log = null)
        => new($"{asyncChain.Name}.Silence()", async cancellationToken => {
            try {
                await asyncChain.Start(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                if (asyncChain.TransiencyResolver.Invoke(e).IsTerminal())
                    throw;

                log?.IfEnabled(LogLevel.Error)?.LogError(e,
                    "{ChainName} failed, the error is silenced", asyncChain.Name);
            }
        }, asyncChain.TransiencyResolver);

    public static AsyncChain AppendDelay(this AsyncChain asyncChain, Func<RandomTimeSpan> delayFactory, MomentClock? clock = null)
        => asyncChain.AppendDelay(() => delayFactory.Invoke().Next());
    public static AsyncChain AppendDelay(this AsyncChain asyncChain, Func<TimeSpan> delayFactory, MomentClock? clock = null)
    {
        clock ??= MomentClockSet.Default.CpuClock;
        return new($"{asyncChain.Name}.AppendDelay(?)", async cancellationToken => {
            await asyncChain.Start(cancellationToken).ConfigureAwait(false);
            await clock.Delay(delayFactory.Invoke(), cancellationToken).ConfigureAwait(false);
        }, asyncChain.TransiencyResolver);
    }
    public static AsyncChain AppendDelay(this AsyncChain asyncChain, RandomTimeSpan delay, MomentClock? clock = null)
    {
        clock ??= MomentClockSet.Default.CpuClock;
        return new($"{asyncChain.Name}.AppendDelay({delay})", async cancellationToken => {
            await asyncChain.Start(cancellationToken).ConfigureAwait(false);
            await clock.Delay(delay.Next(), cancellationToken).ConfigureAwait(false);
        }, asyncChain.TransiencyResolver);
    }

    public static AsyncChain PrependDelay(this AsyncChain asyncChain, Func<RandomTimeSpan> delayFactory, MomentClock? clock = null)
        => asyncChain.PrependDelay(() => delayFactory.Invoke().Next());
    public static AsyncChain PrependDelay(this AsyncChain asyncChain, Func<TimeSpan> delayFactory, MomentClock? clock = null)
    {
        clock ??= MomentClockSet.Default.CpuClock;
        return new($"{asyncChain.Name}.PrependDelay(?)", async cancellationToken => {
            await clock.Delay(delayFactory.Invoke(), cancellationToken).ConfigureAwait(false);
            await asyncChain.Start(cancellationToken).ConfigureAwait(false);
        }, asyncChain.TransiencyResolver);
    }
    public static AsyncChain PrependDelay(this AsyncChain asyncChain, RandomTimeSpan delay, MomentClock? clock = null)
    {
        clock ??= MomentClockSet.Default.CpuClock;
        return new($"{asyncChain.Name}.PrependDelay({delay})", async cancellationToken => {
            await clock.Delay(delay.Next(), cancellationToken).ConfigureAwait(false);
            await asyncChain.Start(cancellationToken).ConfigureAwait(false);
        }, asyncChain.TransiencyResolver);
    }

    public static AsyncChain RetryForever(this AsyncChain asyncChain, RetryDelaySeq retryDelays, ILogger? log = null)
        => asyncChain.RetryForever(retryDelays, null, log);
    public static AsyncChain RetryForever(this AsyncChain asyncChain, RetryDelaySeq retryDelays, MomentClock? clock, ILogger? log = null)
        => new($"{asyncChain.Name}.RetryForever({retryDelays}", async cancellationToken => {
            clock ??= MomentClockSet.Default.CpuClock;
            var tryIndex = 0;
            while (true) {
                try {
                    if (tryIndex >= 1)
                        log?.IfEnabled(LogLevel.Information)?.LogInformation(
                            "Retrying {ChainName} (#{TryIndex})",
                            asyncChain.Name, tryIndex);
                    await asyncChain.Start(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                    var transiency = asyncChain.TransiencyResolver.Invoke(e);
                    if (transiency.IsTerminal())
                        throw;

                    if (!transiency.IsSuperTransient())
                        tryIndex++;
                }
                var retryDelay = retryDelays[Math.Max(1, tryIndex)];
                await clock.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }, asyncChain.TransiencyResolver);

    public static AsyncChain Retry(this AsyncChain asyncChain, RetryDelaySeq retryDelays, int? tryCount, ILogger? log = null)
        => asyncChain.Retry(retryDelays, tryCount, null, log);
    public static AsyncChain Retry(this AsyncChain asyncChain, RetryDelaySeq retryDelays, int? tryCount, MomentClock? clock, ILogger? log = null)
    {
        if (tryCount is not { } vTryCount)
            return asyncChain.RetryForever(retryDelays, log);

        return new($"{asyncChain.Name}.Retry({retryDelays}, {tryCount})",
            async cancellationToken => {
                clock ??= MomentClockSet.Default.CpuClock;
                var tryIndex = 0;
                while (true) {
                    try {
                        if (tryIndex >= 1)
                            log?.IfEnabled(LogLevel.Information)?.LogInformation(
                                "Retrying {ChainName} (#{TryIndex}/{MaxRetryCount})",
                                asyncChain.Name, tryIndex, vTryCount);
                        await asyncChain.Start(cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                        var transiency = asyncChain.TransiencyResolver.Invoke(e);
                        if (transiency.IsTerminal())
                            throw;

                        if (!transiency.IsSuperTransient())
                            tryIndex++;
                        if (tryIndex >= vTryCount)
                            throw;
                    }
                    var retryDelay = retryDelays[Math.Max(1, tryIndex)];
                    await clock.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
            }, asyncChain.TransiencyResolver);
    }

    public static AsyncChain CycleForever(this AsyncChain asyncChain)
        => new($"{asyncChain.Name}.CycleForever()", async cancellationToken => {
            while (true) {
                await asyncChain.Start(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            // ReSharper disable once FunctionNeverReturns
        }, asyncChain.TransiencyResolver);
}
