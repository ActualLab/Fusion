using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Security.Cryptography;
using System.Threading.Channels;
using ActualLab.Api;
using ActualLab.Async;
using ActualLab.Collections;
using ActualLab.Conversion;
using ActualLab.DependencyInjection;
using ActualLab.Generators;
using ActualLab.IO;
using ActualLab.Mathematics;
using ActualLab.Scalability;
using ActualLab.Scalability.Internal;
using ActualLab.Channels;
using ActualLab.Text;
using ActualLab.Time;

namespace ActualLab.Tests.Audit;

public class CoreAuditRegressionTest
{
    [Fact]
    public async Task SafeAsyncDisposableMustPublishSynchronousFailure()
    {
        var disposable = new SynchronouslyThrowingAsyncDisposable();

        var firstTask = disposable.DisposeAsync().AsTask();
        var secondTask = disposable.DisposeAsync().AsTask();

        await Assert.ThrowsAsync<InvalidOperationException>(() => firstTask).ConfigureAwait(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => secondTask).ConfigureAwait(false);
        secondTask.Should().BeSameAs(firstTask);
        disposable.WhenDisposed.Should().BeSameAs(firstTask);
        disposable.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task SynchronousDisposeMustPublishAsynchronousDisposalFailure()
    {
        var disposable = new SynchronouslyThrowingAsyncDisposable();

        disposable.Dispose();

        var disposeTask = disposable.WhenDisposed;
        disposeTask.Should().NotBeNull();
        await Assert.ThrowsAsync<InvalidOperationException>(() => disposeTask!).ConfigureAwait(false);
        disposable.Dispose();
        disposable.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void ArrayBufferCopyToMustCopyLogicalItemsOnly()
    {
        var buffer = ArrayBuffer<int>.Lease(false);
        try {
            buffer.Add(1);
            buffer.Add(2);
            var target = new int[2];

            buffer.CopyTo(target, 0);

            target.Should().Equal(1, 2);
        }
        finally {
            buffer.Release();
        }
    }

    [Fact]
    public void BinaryHeapSourceConstructorMustUseCustomComparer()
    {
        var descending = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var heap = new BinaryHeap<int, int>([(1, 1), (2, 2), (3, 3)], descending);

        heap.PeekMin().Value.Priority.Should().Be(3);
    }

    [Fact]
    public void PoolBufferResizeMustHonorMustClear()
    {
        var pool = new RecordingPool<string>();
        using (var buffer = new ArrayPoolBuffer<string>(pool, 16, mustClear: true)) {
            for (var i = 0; i < 17; i++)
                buffer.Add(i.ToString(CultureInfo.InvariantCulture));
        }

        pool.ClearFlags.Should().Equal(true, true);
    }

    [Fact]
    public void RefPoolBufferResizeMustHonorMustClear()
    {
        var pool = new RecordingPool<string>();
        var buffer = new RefArrayPoolBuffer<string>(pool, 16, mustClear: true);
        try {
            for (var i = 0; i < 17; i++)
                buffer.Add(i.ToString(CultureInfo.InvariantCulture));
        }
        finally {
            buffer.Release();
        }

        pool.ClearFlags.Should().Equal(true, true);
    }

    [Fact]
    public void PoolBufferMustRejectUnsatisfiedSizeHint()
    {
        using var buffer = new ArrayPoolBuffer<byte>(16, mustClear: false);
        buffer.Position = 1;

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetSpan(int.MaxValue));
    }

    [Fact]
    public void RefPoolBufferMustRejectUnsatisfiedSizeHint()
    {
        var buffer = new RefArrayPoolBuffer<byte>(16, mustClear: false);
        try {
            buffer.Position = 1;
            try {
                buffer.GetSpan(int.MaxValue);
                Assert.Fail("Expected an oversized hint to be rejected.");
            }
            catch (ArgumentOutOfRangeException) { }
        }
        finally {
            buffer.Release();
        }
    }

    [Fact]
    public void RingBufferMustEnumerateEveryWrappedItem()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.PushTail(1);
        buffer.PushTail(2);
        buffer.PushTail(3);
        _ = buffer.PullHead();
        buffer.PushTail(4);

        buffer.Should().Equal(2, 3, 4);
    }

    [Fact]
    public void EmptyApiArrayMustSupportWithMany()
    {
        var appended = ApiArray<int>.Empty.WithMany(1, 2);
        var prepended = default(ApiArray<int>).WithMany(true, 1, 2);

        appended.Should().Equal(1, 2);
        prepended.Should().Equal(1, 2);
    }

    [Fact]
    public void EmptyApiMapMustReleaseStringBuilder()
        => MustReleaseStringBuilder(static () => new ApiMap<int, int>().ToString()).Should().BeTrue();

    [Fact]
    public void EmptyApiSetMustReleaseStringBuilder()
        => MustReleaseStringBuilder(static () => new ApiSet<int>().ToString()).Should().BeTrue();

    [Fact]
    public void HashRingMustHandleExtremeSignedHashes()
    {
        var ring = new HashRing<int>([int.MinValue, int.MaxValue], static value => value);

        ring.FindNode(-1).Should().Be(int.MaxValue);
        ring.FindNode(int.MaxValue).Should().Be(int.MaxValue);
    }

    [Fact]
    public void MaglevMustSupportOneShard()
    {
        var map = new MaglevShardMapBuilder().Build(1, ["node"]);

        map.Should().Equal(0);
    }

    [Fact]
    public void RadixConversionMustHandleInt64Boundaries()
    {
        foreach (var radix in new[] { 2, 10, 64 }) {
            var minFormatted = MathExt.Format(long.MinValue, radix);
            var maxFormatted = MathExt.Format(long.MaxValue, radix);

            MathExt.TryParseInt64(minFormatted, radix, out var minParsed).Should().BeTrue();
            MathExt.TryParseInt64(maxFormatted, radix, out var maxParsed).Should().BeTrue();
            minParsed.Should().Be(long.MinValue);
            maxParsed.Should().Be(long.MaxValue);
        }

        MathExt.Format(long.MinValue, 10).Should().Be(long.MinValue.ToString(CultureInfo.InvariantCulture));
        MathExt.TryParseInt64("9223372036854775808", 10, out _).Should().BeFalse();
        MathExt.TryParseInt64("-9223372036854775809", 10, out _).Should().BeFalse();
        MathExt.TryParseInt64("-", 10, out _).Should().BeFalse();
    }

    [Fact]
    public void FactorialMustResumeFromNearestCachedPrefix()
    {
        const int prefixIndex = 24;
        var cacheField = typeof(MathExt).GetField("Factorials", BindingFlags.Static | BindingFlags.NonPublic)!;
        var cache = (Dictionary<int, BigInteger>)cacheField.GetValue(null)!;
        var prefix = Enumerable.Range(2, prefixIndex - 1).Aggregate(
            BigInteger.One,
            static (factorial, value) => factorial * value);

        lock (cache) {
            var saved = cache.ToArray();
            try {
                cache.Clear();
                cache.Add(prefixIndex, prefix);

                MathExt.Factorial(prefixIndex + 1).Should().Be(prefix * (prefixIndex + 1));
                cache.Keys.Should().BeEquivalentTo([prefixIndex, prefixIndex + 1]);
            }
            finally {
                cache.Clear();
                foreach (var (key, value) in saved)
                    cache.Add(key, value);
            }
        }
    }

    [Fact]
    public void RandomStringGeneratorMustUseEveryByteValueWithoutModuloBias()
    {
        using var rng = new SequenceRandomNumberGenerator(Enumerable.Range(0, 256).Select(static x => (byte)x).ToArray());
        using var generator = new RandomStringGenerator(rng: rng);

        var value = generator.Next(510, "abc");

        value.Count(static c => c == 'a').Should().Be(170);
        value.Count(static c => c == 'b').Should().Be(170);
        value.Count(static c => c == 'c').Should().Be(170);
        rng.CallCount.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void RandomStringGeneratorMustReachWideAlphabet()
    {
        var alphabet = new string(Enumerable.Range(0, 257).Select(static x => (char)x).ToArray());
        using var rng = new SequenceRandomNumberGenerator([0, 1, 0, 0]);
        using var generator = new RandomStringGenerator(rng: rng);

        generator.Next(1, alphabet).Should().Be(alphabet[256].ToString());
    }

    [Fact]
    public void RandomStringPowerOfTwoPathMustRemainBatchedAndAllocationBounded()
    {
        const int length = 512;
        using var rng = new SequenceRandomNumberGenerator([0x5A]);
        using var generator = new RandomStringGenerator(rng: rng);
        _ = generator.Next(length);
        rng.ResetCallCount();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var value = generator.Next(length);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        value.Length.Should().Be(length);
        rng.CallCount.Should().Be(1);
        allocated.Should().BeLessThanOrEqualTo(2048);
    }

    [Fact]
    public void DescendantTryConvertMustReturnNoneOnMismatch()
    {
        var descendantConverter = ConverterProvider.Default.From<object>().To<string>();
        var baseConverter = ConverterProvider.Default.From<int>().To<object>();

        descendantConverter.TryConvert(new object()).Should().Be(Option.None<string>());
        descendantConverter.TryConvertUntyped(new object()).Should().Be(Option.None<object?>());
        baseConverter.TryConvertUntyped("not an int").Should().Be(Option.None<object?>());
        Assert.Throws<InvalidCastException>(() => baseConverter.ConvertUntyped("not an int"));
    }

    [Fact]
    public void GenericActivationMustForwardExplicitArguments()
    {
        var genericValue = ActualLab.DependencyInjection.ServiceProviderExt.Empty
            .GetServiceOrCreateInstance<ArgumentService>(123);
        var nonGenericValue = (ArgumentService)ActualLab.DependencyInjection.ServiceProviderExt.Empty
            .GetServiceOrCreateInstance(typeof(ArgumentService), 456);

        genericValue.Value.Should().Be(123);
        nonGenericValue.Value.Should().Be(456);
    }

    [Fact]
    public async Task ConcurrentTransformMustHandlePreCancellationInsideCopyPolicy()
    {
        var source = Channel.CreateUnbounded<int>();
        var target = Channel.CreateUnbounded<int>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await source.Reader.ConcurrentTransform(
            target.Writer,
            static value => value,
            2,
            ChannelCopyMode.CopyAllSilently,
            cts.Token).ConfigureAwait(false);

        target.Reader.Completion.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentAsyncTransformMustHandlePreCancellationInsideCopyPolicy()
    {
        var source = Channel.CreateUnbounded<int>();
        var target = Channel.CreateUnbounded<int>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await source.Reader.ConcurrentTransform(
            target.Writer,
            static value => new ValueTask<int>(value),
            2,
            ChannelCopyMode.CopyAllSilently,
            cts.Token).ConfigureAwait(false);

        target.Reader.Completion.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task EnumerableIntervalMustBeColdPerSubscription()
    {
        var interval = SystemClock.Instance.Interval([TimeSpan.Zero, TimeSpan.Zero]);

        var first = await interval.ToList().ToTask().ConfigureAwait(false);
        var second = await interval.ToList().ToTask().ConfigureAwait(false);

        first.Should().Equal(0, 1);
        second.Should().Equal(0, 1);
    }

    [Fact]
    public async Task TimerSetPriorityCursorMustCrossInt32Boundary()
    {
        var clock = MomentClockSet.Default.CpuClock;
        var options = new TimerSetOptions {
            Clock = clock,
            TickSource = new TickSource(TimerSetOptions.MinQuanta),
        };
        await using var timerSet = new TimerSet<string>(options, start: clock.Now + TimeSpan.FromDays(1));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var minPriorityField = typeof(TimerSet<string>).GetField("_minPriority", flags)!;
        var timersField = typeof(TimerSet<string>).GetField("_timers", flags)!;
        var timers = (RadixHeapSet<string>)timersField.GetValue(timerSet)!;

        minPriorityField.SetValue(timerSet, (long)int.MaxValue);
        timerSet.AddOrUpdate("before", 0);
        timers.ExtractMinSet(int.MaxValue).Should().ContainKey("before");

        minPriorityField.SetValue(timerSet, (long)int.MaxValue + 1);
        timerSet.AddOrUpdate("after", 0);
        timers.ExtractMinSet((long)int.MaxValue + 1).Should().ContainKey("after");
    }

    [Fact]
    public void TimerSetOptionsMustRejectUnsupportedQuanta()
    {
        var createOptions = () => new TimerSetOptions {
            TickSource = new TickSource(TimerSetOptions.MinQuanta - TimeSpan.FromTicks(1)),
        };

        createOptions.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task WriteLinesMustTruncateExistingFile()
    {
        var path = new FilePath(Path.Combine(Path.GetTempPath(), $"fusion-core-audit-{Guid.NewGuid():N}.txt"));
        try {
            await File.WriteAllTextAsync(path, "this is much longer").ConfigureAwait(false);
            await path.WriteLines(Lines(), encoding: null, cancellationToken: default).ConfigureAwait(false);

            (await File.ReadAllTextAsync(path).ConfigureAwait(false)).Should().Be($"x{Environment.NewLine}");
        }
        finally {
            File.Delete(path);
        }

        static async IAsyncEnumerable<string> Lines()
        {
            await Task.Yield();
            yield return "x";
        }
    }

    private static bool MustReleaseStringBuilder(Func<string> format)
    {
        var isReleased = false;
        var thread = new Thread(() => {
            var builder = StringBuilderExt.Acquire();
            builder.Release();
            _ = format.Invoke();
            var reusedBuilder = StringBuilderExt.Acquire();
            isReleased = ReferenceEquals(builder, reusedBuilder);
            reusedBuilder.Release();
        });
        thread.Start();
        thread.Join();
        return isReleased;
    }

    private sealed class ArgumentService(int value)
    {
        public int Value { get; } = value;
    }

    private sealed class SynchronouslyThrowingAsyncDisposable : SafeAsyncDisposableBase
    {
        public int DisposeCount { get; private set; }

        protected override Task DisposeAsync(bool disposing)
        {
            DisposeCount++;
            throw new InvalidOperationException();
        }
    }

    private sealed class RecordingPool<T> : ArrayPool<T>
    {
        public List<bool> ClearFlags { get; } = [];

        public override T[] Rent(int minimumLength)
            => new T[minimumLength];

        public override void Return(T[] array, bool clearArray = false)
            => ClearFlags.Add(clearArray);
    }

    private sealed class SequenceRandomNumberGenerator(byte[] sequence) : RandomNumberGenerator
    {
        private int _position;

        public int CallCount { get; private set; }

        public void ResetCallCount()
            => CallCount = 0;

        public override void GetBytes(byte[] data)
            => FillData(data);

        public override void GetBytes(byte[] data, int offset, int count)
            => FillData(data.AsSpan(offset, count));

        public override void GetBytes(Span<byte> data)
            => FillData(data);

        public override void GetNonZeroBytes(byte[] data)
        {
            FillData(data);
            for (var i = 0; i < data.Length; i++)
                if (data[i] == 0)
                    data[i] = 1;
        }

        private void FillData(Span<byte> data)
        {
            CallCount++;
            foreach (ref var item in data) {
                item = sequence[_position];
                _position = (_position + 1) % sequence.Length;
            }
        }
    }
}
