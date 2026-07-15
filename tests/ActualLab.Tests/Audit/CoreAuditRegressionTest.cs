using System.Buffers;
using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Channels;
using ActualLab.Api;
using ActualLab.Collections;
using ActualLab.Conversion;
using ActualLab.DependencyInjection;
using ActualLab.IO;
using ActualLab.Mathematics;
using ActualLab.Scalability;
using ActualLab.Scalability.Internal;
using ActualLab.Channels;
using ActualLab.Time;

namespace ActualLab.Tests.Audit;

public class CoreAuditRegressionTest
{
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
    public void PoolBufferMustRejectUnsatisfiedSizeHint()
    {
        using var buffer = new ArrayPoolBuffer<byte>(16, mustClear: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetSpan(int.MaxValue));
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
        var array = ApiArray<int>.Empty.WithMany(1, 2);

        array.Should().Equal(1, 2);
    }

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
        var formatted = MathExt.Format(long.MinValue, 10);

        formatted.Should().Be(long.MinValue.ToString(CultureInfo.InvariantCulture));
        MathExt.TryParseInt64(formatted, 10, out var parsed).Should().BeTrue();
        parsed.Should().Be(long.MinValue);
        MathExt.TryParseInt64("9223372036854775808", 10, out _).Should().BeFalse();
    }

    [Fact]
    public void DescendantTryConvertMustReturnNoneOnMismatch()
    {
        var converter = ConverterProvider.Default.From<object>().To<string>();

        converter.TryConvert(new object()).Should().Be(Option.None<string>());
    }

    [Fact]
    public void GenericActivationMustForwardExplicitArguments()
    {
        var value = ActualLab.DependencyInjection.ServiceProviderExt.Empty
            .GetServiceOrCreateInstance<ArgumentService>(123);

        value.Value.Should().Be(123);
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
    public async Task EnumerableIntervalMustBeColdPerSubscription()
    {
        var interval = SystemClock.Instance.Interval([TimeSpan.Zero, TimeSpan.Zero]);

        var first = await interval.ToList().ToTask().ConfigureAwait(false);
        var second = await interval.ToList().ToTask().ConfigureAwait(false);

        first.Should().Equal(0, 1);
        second.Should().Equal(0, 1);
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

    private sealed class ArgumentService(int value)
    {
        public int Value { get; } = value;
    }

    private sealed class RecordingPool<T> : ArrayPool<T>
    {
        public List<bool> ClearFlags { get; } = [];

        public override T[] Rent(int minimumLength)
            => new T[minimumLength];

        public override void Return(T[] array, bool clearArray = false)
            => ClearFlags.Add(clearArray);
    }
}
