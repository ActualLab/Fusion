using System.Text;
using Pastel;

namespace Samples.MeshRpc.Services;

public enum TestCallOutcome
{
    Ok = 0,
    Warn, // The value was correct, but only after a retry
    Failed, // Still stale once the retries ran out
    Error, // The call threw
}

/// <summary>
/// Process-wide call counters: every host's <see cref="TestRunner"/> reports into these,
/// and <c>Program.cs</c> prints the totals every <see cref="TestSettings.StatsPeriod"/>.
/// </summary>
public static class TestStats
{
    private static readonly ConcurrentDictionary<string, CallCounters> Counters = new(StringComparer.Ordinal);

    public static void Register(string callKind, TestCallOutcome outcome)
        => Counters.GetOrAdd(callKind, static _ => new CallCounters()).Register(outcome);

    public static string Format(TimeSpan elapsed)
    {
        var rows = Counters.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        var sb = new StringBuilder();
        sb.Append($"Call stats @ {elapsed.ToShortString()}:".Pastel(ConsoleColor.Cyan));
        if (rows.Count == 0)
            return sb.Append(" no calls yet").ToString();

        var callKindWidth = rows.Max(x => x.Key.Length);
        foreach (var (callKind, counters) in rows) {
            var ok = counters[TestCallOutcome.Ok];
            var warn = counters[TestCallOutcome.Warn];
            var failed = counters[TestCallOutcome.Failed];
            var error = counters[TestCallOutcome.Error];
            var row = $"  {callKind.PadRight(callKindWidth)} : "
                + $"{ok,6} ok, {warn,4} warn, {failed,4} failed, {error,4} error";
            sb.AppendLine().Append(failed + error > 0
                ? row.Pastel(ConsoleColor.Red)
                : warn > 0 ? row.Pastel(ConsoleColor.Yellow) : row);
        }
        return sb.ToString();
    }

    // Nested types

    private sealed class CallCounters
    {
        // Indexed by TestCallOutcome, so a new outcome needs no extra field
        private readonly long[] _counts = new long[4];

        public long this[TestCallOutcome outcome] => Volatile.Read(ref _counts[(int)outcome]);

        public void Register(TestCallOutcome outcome)
            => Interlocked.Increment(ref _counts[(int)outcome]);
    }
}
