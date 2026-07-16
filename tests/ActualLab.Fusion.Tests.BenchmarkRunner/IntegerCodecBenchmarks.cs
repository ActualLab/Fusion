using System.Buffers;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using ActualLab.Collections;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using MessagePack;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public enum IntegerDistribution
{
    RpcCallId,
    Small,
    Mixed,
    Large,
}

[MemoryDiagnoser]
[HardwareCounters(HardwareCounter.BranchInstructions, HardwareCounter.BranchMispredictions)]
[DisassemblyDiagnoser(maxDepth: 2, printSource: true)]
public class IntegerCodecBenchmarks
{
    private const ulong PayloadMask = 0x7F7F7F7F7F7F7F7F;
    private const ulong StopBitMask = 0x8080808080808080;

    private readonly ArrayBufferWriter<byte> _messagePackWriteBuffer = new(BenchmarkSettings.CodecOperationCount * 9);
    private byte[] _messagePackData = null!;
    private byte[] _varUInt32Data = null!;
    private byte[] _varUInt32WriteBuffer = null!;
    private byte[] _varIntData = null!;
    private byte[] _varIntWriteBuffer = null!;
    private uint[] _uintValues = null!;
    private long[] _values = null!;

    [ParamsAllValues]
    public IntegerDistribution Distribution { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _values = CreateValues(Distribution);
        _uintValues = CreateUInt32Values(Distribution);
        _varUInt32Data = new byte[BenchmarkSettings.CodecOperationCount * 5 + 4];
        var uintOffset = 0;
        foreach (var value in _uintValues)
            uintOffset = _varUInt32Data.AsSpan().WriteVarUInt32(value, uintOffset);
        Array.Resize(ref _varUInt32Data, uintOffset + 4);
        ValidateVarUInt32Readers();
        _varUInt32WriteBuffer = new byte[BenchmarkSettings.CodecOperationCount * 5];

        _varIntData = new byte[BenchmarkSettings.CodecOperationCount * 10 + 16];
        var offset = 0;
        foreach (var value in _values)
            offset = _varIntData.AsSpan().WriteVarUInt64((ulong)value, offset);
        Array.Resize(ref _varIntData, offset + 16);
        ValidateVarUInt64Readers();
        _varIntWriteBuffer = new byte[BenchmarkSettings.CodecOperationCount * 10];

        var writer = new MessagePackWriter(_messagePackWriteBuffer);
        foreach (var value in _values)
            writer.Write(value);
        writer.Flush();
        _messagePackData = _messagePackWriteBuffer.WrittenSpan.ToArray();
        _messagePackWriteBuffer.Clear();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public uint VarUInt32ReadLegacy()
    {
        ReadOnlySpan<byte> data = _varUInt32Data;
        var offset = 0;
        var checksum = 0u;
        for (var i = 0; i < BenchmarkSettings.CodecOperationCount; i++) {
            var (value, nextOffset) = ReadVarUInt32Legacy(data, offset);
            checksum ^= value;
            offset = nextOffset;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public uint VarUInt32Read()
    {
        ReadOnlySpan<byte> data = _varUInt32Data;
        var offset = 0;
        var checksum = 0u;
        for (var i = 0; i < BenchmarkSettings.CodecOperationCount; i++) {
            var (value, nextOffset) = data.ReadVarUInt32(offset);
            checksum ^= value;
            offset = nextOffset;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public int VarUInt32WriteLegacy()
    {
        var data = _varUInt32WriteBuffer.AsSpan();
        var offset = 0;
        foreach (var value in _uintValues)
            offset = WriteVarUInt32Legacy(data, value, offset);
        return offset;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public int VarUInt32Write()
    {
        var data = _varUInt32WriteBuffer.AsSpan();
        var offset = 0;
        foreach (var value in _uintValues)
            offset = data.WriteVarUInt32(value, offset);
        return offset;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public ulong VarUInt64ReadLegacy()
    {
        ReadOnlySpan<byte> data = _varIntData;
        var offset = 0;
        var checksum = 0ul;
        for (var i = 0; i < BenchmarkSettings.CodecOperationCount; i++) {
            var (value, nextOffset) = ReadVarUInt64Legacy(data, offset);
            checksum ^= value;
            offset = nextOffset;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public ulong VarUInt64Read()
    {
        ReadOnlySpan<byte> data = _varIntData;
        var offset = 0;
        var checksum = 0ul;
        for (var i = 0; i < BenchmarkSettings.CodecOperationCount; i++) {
            var (value, nextOffset) = data.ReadVarUInt64(offset);
            checksum ^= value;
            offset = nextOffset;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public ulong VarUInt64ReadBranchless()
    {
        ReadOnlySpan<byte> data = _varIntData;
        var offset = 0;
        var checksum = 0ul;
        for (var i = 0; i < BenchmarkSettings.CodecOperationCount; i++) {
            var (value, nextOffset) = ReadVarUInt64Branchless(data, offset);
            checksum ^= value;
            offset = nextOffset;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public ulong VarUInt64ReadHybrid()
    {
        ReadOnlySpan<byte> data = _varIntData;
        var offset = 0;
        var checksum = 0ul;
        for (var i = 0; i < BenchmarkSettings.CodecOperationCount; i++) {
            var (value, nextOffset) = ReadVarUInt64Hybrid(data, offset);
            checksum ^= value;
            offset = nextOffset;
        }
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public int VarUInt64WriteLegacy()
    {
        var data = _varIntWriteBuffer.AsSpan();
        var offset = 0;
        foreach (var value in _values)
            offset = WriteVarUInt64Legacy(data, (ulong)value, offset);
        return offset;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public int VarUInt64Write()
    {
        var data = _varIntWriteBuffer.AsSpan();
        var offset = 0;
        foreach (var value in _values)
            offset = data.WriteVarUInt64((ulong)value, offset);
        return offset;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public long MessagePackInt64Read()
    {
        var reader = new MessagePackReader(_messagePackData);
        var checksum = 0L;
        for (var i = 0; i < BenchmarkSettings.CodecOperationCount; i++)
            checksum ^= reader.ReadInt64();
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.CodecOperationCount)]
    public int MessagePackInt64Write()
    {
        _messagePackWriteBuffer.Clear();
        var writer = new MessagePackWriter(_messagePackWriteBuffer);
        foreach (var value in _values)
            writer.Write(value);
        writer.Flush();
        return _messagePackWriteBuffer.WrittenCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteVarUInt32Legacy(Span<byte> span, uint source, int offset)
    {
        while (source >= 0x80) {
            span[offset++] = (byte)(source | 0x80);
            source >>= 7;
        }
        span[offset] = (byte)source;
        return offset + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteVarUInt64Legacy(Span<byte> span, ulong source, int offset)
    {
        while (source >= 0x80) {
            span[offset++] = (byte)(source | 0x80);
            source >>= 7;
        }
        span[offset] = (byte)source;
        return offset + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (uint Value, int Offset) ReadVarUInt32Legacy(ReadOnlySpan<byte> span, int offset)
    {
        var value = 0u;
        for (var shift = 0; shift < 28; shift += 7) {
            var b = span[offset++];
            value |= (uint)(b & 0x7F) << shift;
            if (b <= 0x7F)
                return (value, offset);
        }
        var last = span[offset++];
        return (value | ((uint)last << 28), offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (ulong Value, int Offset) ReadVarUInt64Hybrid(ReadOnlySpan<byte> span, int offset)
    {
        var first = span[offset];
        return first < 0x80
            ? (first, offset + 1)
            : ReadVarUInt64Branchless(span, offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (ulong Value, int Offset) ReadVarUInt64Legacy(ReadOnlySpan<byte> span, int offset)
    {
        var value = 0ul;
        for (var shift = 0; shift < 63; shift += 7) {
            var b = span[offset++];
            value |= (ulong)(b & 0x7F) << shift;
            if (b <= 0x7F)
                return (value, offset);
        }
        var last = span[offset++];
        return (value | ((ulong)last << 63), offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (ulong Value, int Offset) ReadVarUInt64Branchless(ReadOnlySpan<byte> span, int offset)
    {
        if (Bmi2.X64.IsSupported && span.Length - offset >= 10) {
            var data = span.ReadUnchecked<ulong>(offset);
            var tail = span.ReadUnchecked<ushort>(offset + 8);
            var stopBits = Bmi2.X64.ParallelBitExtract(~data, StopBitMask)
                | ((uint)(~tail & 0x80) << 1)
                | ((uint)(~tail & 0x8000) >> 6);
            var length = BitOperations.TrailingZeroCount(stopBits) + 1;
            var value = Bmi2.X64.ParallelBitExtract(data, PayloadMask)
                | ((ulong)(tail & 0x7F) << 56)
                | ((ulong)(tail & 0x0100) << 55);
            var bitCount = Math.Min(length * 7, 64);
            value &= ulong.MaxValue >> (64 - bitCount);
            return (value, offset + length);
        }
        return span.ReadVarUInt64(offset);
    }

    private static long[] CreateValues(IntegerDistribution distribution)
    {
        var values = new long[BenchmarkSettings.CodecOperationCount];
        var random = new Random(42);
        for (var i = 0; i < values.Length; i++)
            values[i] = distribution switch {
                IntegerDistribution.RpcCallId => CreateRpcCallId(random),
                IntegerDistribution.Small => random.Next(0, 128),
                IntegerDistribution.Mixed => CreateMixedValue(random),
                IntegerDistribution.Large => random.NextInt64(1L << 56, long.MaxValue),
                _ => throw new ArgumentOutOfRangeException(nameof(distribution)),
            };
        return values;
    }

    private static uint[] CreateUInt32Values(IntegerDistribution distribution)
    {
        var values = new uint[BenchmarkSettings.CodecOperationCount];
        var random = new Random(42);
        for (var i = 0; i < values.Length; i++)
            values[i] = distribution switch {
                IntegerDistribution.RpcCallId => (uint)CreateRpcCallId(random),
                IntegerDistribution.Small => (uint)random.Next(0, 128),
                IntegerDistribution.Mixed => CreateMixedUInt32Value(random),
                IntegerDistribution.Large => (uint)random.NextInt64(1L << 28, 1L << 32),
                _ => throw new ArgumentOutOfRangeException(nameof(distribution)),
            };
        return values;
    }

    private static uint CreateMixedUInt32Value(Random random)
        => random.Next(5) switch {
            0 => (uint)random.Next(0, 1 << 7),
            1 => (uint)random.Next(1 << 7, 1 << 14),
            2 => (uint)random.Next(1 << 14, 1 << 21),
            3 => (uint)random.Next(1 << 21, 1 << 28),
            _ => (uint)random.NextInt64(1L << 28, 1L << 32),
        };

    private static long CreateMixedValue(Random random)
        => random.Next(9) switch {
            0 => random.Next(0, 1 << 7),
            1 => random.Next(1 << 7, 1 << 14),
            2 => random.Next(1 << 14, 1 << 21),
            3 => random.Next(1 << 21, 1 << 28),
            4 => random.NextInt64(1L << 28, 1L << 35),
            5 => random.NextInt64(1L << 35, 1L << 42),
            6 => random.NextInt64(1L << 42, 1L << 49),
            7 => random.NextInt64(1L << 49, 1L << 56),
            _ => random.NextInt64(1L << 56, long.MaxValue),
        };

    private static long CreateRpcCallId(Random random)
        => random.Next(100) switch {
            < 5 => random.Next(0, 1 << 7),
            < 50 => random.Next(1 << 7, 1 << 14),
            < 95 => random.Next(1 << 14, 1 << 21),
            _ => random.NextInt64(1L << 21, long.MaxValue),
        };

    private void ValidateVarUInt32Readers()
    {
        ReadOnlySpan<byte> data = _varUInt32Data;
        var offset = 0;
        foreach (var expected in _uintValues) {
            var (legacyValue, legacyOffset) = ReadVarUInt32Legacy(data, offset);
            var (value, valueOffset) = data.ReadVarUInt32(offset);
            if (legacyValue != expected || value != expected || legacyOffset != valueOffset)
                throw new InvalidOperationException("VarUInt32 reader validation failed.");
            offset = valueOffset;
        }
    }

    private void ValidateVarUInt64Readers()
    {
        ReadOnlySpan<byte> data = _varIntData;
        var offset = 0;
        foreach (var expected in _values) {
            var (branchlessValue, branchlessOffset) = ReadVarUInt64Branchless(data, offset);
            var (hybridValue, hybridOffset) = ReadVarUInt64Hybrid(data, offset);
            var (value, valueOffset) = data.ReadVarUInt64(offset);
            if (branchlessValue != (ulong)expected
                || hybridValue != (ulong)expected
                || value != (ulong)expected
                || branchlessOffset != hybridOffset
                || branchlessOffset != valueOffset)
                throw new InvalidOperationException("VarUInt64 reader validation failed.");
            offset = branchlessOffset;
        }
    }
}
