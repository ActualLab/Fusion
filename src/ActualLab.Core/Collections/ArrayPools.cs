using System.Buffers;

namespace ActualLab.Collections;

public static class ArrayPools
{
    public static readonly ArrayPool<byte> SharedBytePool = ArrayPool<byte>.Shared;
    public static readonly ArrayPool<char> SharedCharPool = ArrayPool<char>.Shared;
    public static readonly ArrayPool<int> SharedInt32Pool = ArrayPool<int>.Shared;
    public static readonly ArrayPool<long> SharedInt64Pool = ArrayPool<long>.Shared;
    public static readonly ArrayPool<string> SharedStringPool = ArrayPool<string>.Shared;
}
