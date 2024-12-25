using System.Diagnostics;
using ActualLab.Time.Internal;
using Cysharp.Text;
using MessagePack;

namespace ActualLab.Time;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable, MessagePackFormatter(typeof(CpuTimestampMessagePackFormatter))]
public readonly partial record struct CpuTimestamp(
    [property: DataMember(Order = 0), Key(0)] long Value
    ) : IComparable<CpuTimestamp>
{
    public static readonly CpuTimestamp PositiveInfinity = new(long.MaxValue);
    public static readonly CpuTimestamp NegativeInfinity = new(long.MinValue);

    public static long TickFrequency {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Constants.TickFrequency;
    }

    public static double TickDuration {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Constants.TickDuration;
    }

    public static CpuTimestamp Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Constants.QueryPerformanceCounter.Invoke());
    }

    [IgnoreDataMember, IgnoreMember]
    public TimeSpan Elapsed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Now - this;
    }

    public override string ToString()
        => ZString.Concat(Elapsed.ToShortString(), " elapsed");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan operator -(CpuTimestamp a, CpuTimestamp b)
        => TimeSpan.FromSeconds(Constants.TickDuration * (a.Value - b.Value));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CpuTimestamp operator +(CpuTimestamp a, TimeSpan b)
        => new(a.Value + (long)(b.TotalSeconds * Constants.TickFrequency));
    public static CpuTimestamp operator -(CpuTimestamp a, TimeSpan b)
        => new(a.Value - (long)(b.TotalSeconds * Constants.TickFrequency));

    public static bool operator >(CpuTimestamp a, CpuTimestamp b) => a.Value > b.Value;
    public static bool operator >=(CpuTimestamp a, CpuTimestamp b) => a.Value >= b.Value;
    public static bool operator <(CpuTimestamp a, CpuTimestamp b) => a.Value < b.Value;
    public static bool operator <=(CpuTimestamp a, CpuTimestamp b) => a.Value <= b.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(CpuTimestamp other)
        => Value.CompareTo(other.Value);

    // Nested types

    private static class Constants
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static readonly long TickFrequency;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static readonly double TickDuration;
        public static readonly Func<long> QueryPerformanceCounter;

        static Constants()
        {
            if (RuntimeCodegen.Mode != RuntimeCodegenMode.DynamicMethods) {
                // AOT
                TickFrequency = Stopwatch.Frequency;
                QueryPerformanceCounter = Stopwatch.GetTimestamp;
            }
            else {
                var mQueryPerformanceCounter = typeof(Stopwatch)
                    .GetMethod(
                        nameof(QueryPerformanceCounter),
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (mQueryPerformanceCounter != null) {
                    // .NET + .NET Core, WASM
                    TickFrequency = Stopwatch.Frequency;
                    QueryPerformanceCounter = (Func<long>)mQueryPerformanceCounter!
                        .CreateDelegate(typeof(Func<long>));
                }
                else {
                    // .NET Framework
                    TickFrequency = 10_000_000;
                    QueryPerformanceCounter = Stopwatch.GetTimestamp;
                }
            }
            TickDuration = 1d / TickFrequency;
        }
    }
}
