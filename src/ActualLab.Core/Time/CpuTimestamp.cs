using System.Diagnostics;
using ActualLab.Time.Internal;
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
        get {
#if NET5_0_OR_GREATER
            return new CpuTimestamp(Stopwatch.GetTimestamp());
#else
            return new CpuTimestamp(Constants.GetTimestamp.Invoke());
#endif
        }
    }

    [IgnoreDataMember, IgnoreMember]
    public TimeSpan Elapsed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Now - this;
    }

    public override string ToString()
        => string.Concat(Elapsed.ToShortString(), " elapsed");

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
#if !NET5_0_OR_GREATER
        public static readonly Func<long> GetTimestamp;
#endif

        static Constants()
        {
#if NET5_0_OR_GREATER
            TickFrequency = Stopwatch.Frequency;
#else
            if (RuntimeCodegen.Mode != RuntimeCodegenMode.DynamicMethods) {
                TickFrequency = Stopwatch.Frequency;
                GetTimestamp = Stopwatch.GetTimestamp;
            }
            else {
                var mQueryPerformanceCounter = typeof(Stopwatch)
                    .GetMethod(
                        "QueryPerformanceCounter",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (mQueryPerformanceCounter is not null) {
                    // .NET + .NET Core, WASM
                    TickFrequency = Stopwatch.Frequency;
                    // ReSharper disable once RedundantSuppressNullableWarningExpression
                    GetTimestamp = (Func<long>)mQueryPerformanceCounter!.CreateDelegate(typeof(Func<long>));
                }
                else {
                    // .NET Framework
                    TickFrequency = 10_000_000;
                    GetTimestamp = Stopwatch.GetTimestamp;
                }
            }
#endif
            TickDuration = 1d / TickFrequency;
        }
    }
}
