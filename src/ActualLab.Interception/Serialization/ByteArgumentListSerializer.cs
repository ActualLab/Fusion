using System.Buffers;
using System.Diagnostics;
using ActualLab.Interception.Serialization.Internal;
using ActualLab.OS;

namespace ActualLab.Interception.Serialization;

/// <summary>
/// V4 binary <see cref="ArgumentListSerializer"/> that supports polymorphic argument serialization.
/// </summary>
public sealed class ByteArgumentListSerializer(IByteSerializer baseSerializer) : ArgumentListSerializer
{
    [ThreadStatic]
    private static SerializeVisitor? _serializeVisitor;
    [ThreadStatic]
    private static DeserializeVisitor? _deserializeVisitor;

    private readonly ConcurrentDictionary<ArgumentListType, ItemSerializer?[]> _itemSerializerCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    private IByteSerializer BaseSerializer { get; } = baseSerializer;

    public override void Serialize(ArgumentList arguments, bool needsPolymorphism, ArrayPoolBuffer<byte> buffer)
    {
        if (needsPolymorphism) { // Rare case
            PolySerialize(arguments, buffer);
            return;
        }

        // Frequent case
        var visitor = _serializeVisitor ?? new SerializeVisitor();
        // Nothing in Fusion nests non-polymorphic calls (ServiceCall nests a polymorphic one),
        // but an item's own serializer may, and such a call must get its own visitor
        _serializeVisitor = null;
        visitor.ItemSerializers = GetItemSerializers(arguments.Type);
        visitor.Buffer = buffer;
        try {
            arguments.Read(visitor);
        }
        finally {
            visitor.Buffer = null!;
            _serializeVisitor = visitor;
        }
    }

    public override void Deserialize(ref ArgumentList arguments, bool needsPolymorphism, ReadOnlyMemory<byte> data)
    {
        if (needsPolymorphism) { // Rare case
            PolyDeserialize(arguments, data);
            return;
        }

        // Frequent case
        var visitor = _deserializeVisitor ?? new DeserializeVisitor();
        _deserializeVisitor = null; // Same as in Serialize
        visitor.ItemSerializers = GetItemSerializers(arguments.Type);
        visitor.Data = data;
        try {
            arguments.Write(visitor);
        }
        finally {
            visitor.Data = default;
            _deserializeVisitor = visitor;
        }
    }

    // Private methods

    private void PolySerialize(ArgumentList arguments, ArrayPoolBuffer<byte> buffer)
    {
        var itemTypes = arguments.Type.ItemTypes;
        for (var i = 0; i < itemTypes.Length; i++) {
            var type = itemTypes[i];
            if (type == typeof(CancellationToken))
                continue;

            var item = arguments.GetUntyped(i);
            if (IsPolymorphic(type)) {
                if (item is null)
                    ByteTypeSerializer.WriteDerivedItemType(buffer, type, typeof(NullValue));
                else {
                    var itemType = item.GetType();
                    ByteTypeSerializer.WriteDerivedItemType(buffer, type, itemType);
                    BaseSerializer.Write(buffer, item, itemType);
                }
            }
            else
                BaseSerializer.Write(buffer, item, type);
        }
    }

    private void PolyDeserialize(ArgumentList arguments, ReadOnlyMemory<byte> data)
    {
        var itemTypes = arguments.Type.ItemTypes;
        for (var i = 0; i < itemTypes.Length; i++) {
            var type = itemTypes[i];
            if (type == typeof(CancellationToken)) {
                arguments.SetCancellationToken(i, default);
                continue;
            }

            object? item;
            if (IsPolymorphic(type)) {
                var itemType = ByteTypeSerializer.ReadDerivedItemType(ref data, type);
                item = itemType != typeof(NullValue)
                    ? BaseSerializer.Read(ref data, itemType)
                    : null;
            }
            else
                item = BaseSerializer.Read(ref data, type);
            arguments.SetUntyped(i, item);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ItemSerializer?[] GetItemSerializers(ArgumentListType argumentListType)
        => _itemSerializerCache.TryGetValue(argumentListType, out var result)
            ? result
            : _itemSerializerCache.GetOrAdd(
                argumentListType,
                static (type, self) => ItemSerializer.NewArray(self.BaseSerializer, type),
                this);

    // Nested types

    private delegate object? UntypedItemReader(ref ReadOnlyMemory<byte> data);
    private delegate T ItemReader<out T>(ReadOnlyMemory<byte> data, out int readLength);

    /// <summary>
    /// Serializer of a single <see cref="ArgumentList"/> item. All of its operations are delegates,
    /// so an item costs one delegate call rather than a serializer lookup plus a virtual call.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume ArgumentList code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
    private abstract class ItemSerializer
    {
        private static readonly MethodInfo NewTypedMethod = typeof(ItemSerializer)
            .GetMethod(nameof(NewTyped), BindingFlags.Static | BindingFlags.NonPublic)!;

        public Action<IBufferWriter<byte>, object?> WriteUntyped { get; protected init; } = null!;
        public UntypedItemReader ReadUntyped { get; protected init; } = null!;

        // No bounds check: index is an item position in the ArgumentListType the array was built for
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ItemSerializer? GetUnchecked(ItemSerializer?[] serializers, int index)
#if NET6_0_OR_GREATER
            => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(serializers), index);
#else
            => serializers[index];
#endif

        // The result is indexed by item position; CancellationToken items have no serializer
        public static ItemSerializer?[] NewArray(IByteSerializer baseSerializer, ArgumentListType argumentListType)
        {
            var itemTypes = argumentListType.ItemTypes;
            var result = new ItemSerializer?[itemTypes.Length];
            for (var i = 0; i < itemTypes.Length; i++) {
                var itemType = itemTypes[i];
                if (itemType == typeof(CancellationToken))
                    continue;

                // Only generic lists use typed serializers, and they exist only where
                // generic instantiation over an arbitrary item type is available
                result[i] = argumentListType.GenericItemCount != 0
                    ? (ItemSerializer)NewTypedMethod.MakeGenericMethod(itemType).Invoke(null, [baseSerializer])!
                    : new UntypedItemSerializer(baseSerializer, itemType);
            }
            return result;
        }

        private static ItemSerializer NewTyped<T>(IByteSerializer baseSerializer)
            => new ItemSerializer<T>(baseSerializer.ToTyped<T>());
    }

    private sealed class ItemSerializer<T> : ItemSerializer
    {
        public readonly Action<IBufferWriter<byte>, T> Write;
        public readonly ItemReader<T> Read;

        // No type check: the item at this position is T, so its serializer is ItemSerializer<T>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new static ItemSerializer<T> GetUnchecked(ItemSerializer?[] serializers, int index)
        {
            var serializer = ItemSerializer.GetUnchecked(serializers, index);
            Debug.Assert(serializer is ItemSerializer<T>);
            return Unsafe.As<ItemSerializer<T>>(serializer!);
        }

        public ItemSerializer(IByteSerializer<T> serializer)
        {
            Write = serializer.Write;
            Read = serializer.Read;
            WriteUntyped = WriteUntypedImpl;
            ReadUntyped = ReadUntypedImpl;
        }

        private void WriteUntypedImpl(IBufferWriter<byte> buffer, object? item)
            => Write.Invoke(buffer, (T)item!);

        private object? ReadUntypedImpl(ref ReadOnlyMemory<byte> data)
        {
            var item = Read.Invoke(data, out var readLength);
            data = data[readLength..];
            return item;
        }
    }

    private sealed class UntypedItemSerializer : ItemSerializer
    {
        private readonly IByteSerializer _serializer;
        private readonly Type _itemType;

        public UntypedItemSerializer(IByteSerializer serializer, Type itemType)
        {
            _serializer = serializer;
            _itemType = itemType;
            WriteUntyped = WriteUntypedImpl;
            ReadUntyped = ReadUntypedImpl;
        }

        private void WriteUntypedImpl(IBufferWriter<byte> buffer, object? item)
            => _serializer.Write(buffer, item, _itemType);

        private object? ReadUntypedImpl(ref ReadOnlyMemory<byte> data)
            => _serializer.Read(ref data, _itemType);
    }

    private sealed class SerializeVisitor : ArgumentListReader
    {
        public ItemSerializer?[] ItemSerializers = null!;
        public ArrayPoolBuffer<byte> Buffer = null!;

        public override void OnStruct<T>(T item, int index)
        {
            if (typeof(T) == typeof(CancellationToken))
                return;

            ItemSerializer<T>.GetUnchecked(ItemSerializers, index).Write.Invoke(Buffer, item);
        }

        public override void OnClass(Type type, object? item, int index)
            => ItemSerializer.GetUnchecked(ItemSerializers, index)!.WriteUntyped.Invoke(Buffer, item);

        public override void OnAny(Type type, object? item, int index)
            => ItemSerializer.GetUnchecked(ItemSerializers, index)?.WriteUntyped.Invoke(Buffer, item);
    }

    private sealed class DeserializeVisitor : ArgumentListWriter
    {
        public ItemSerializer?[] ItemSerializers = null!;
        public ReadOnlyMemory<byte> Data;

        public override T OnStruct<T>(int index)
        {
            if (typeof(T) == typeof(CancellationToken))
                return default!;

            var item = ItemSerializer<T>.GetUnchecked(ItemSerializers, index).Read.Invoke(Data, out var readLength);
            Data = Data[readLength..];
            return item;
        }

        public override object? OnClass(Type type, int index)
            => ItemSerializer.GetUnchecked(ItemSerializers, index)!.ReadUntyped.Invoke(ref Data);

        public override object? OnAny(Type type, int index, object? defaultValue)
            => ItemSerializer.GetUnchecked(ItemSerializers, index) is { } serializer
                ? serializer.ReadUntyped.Invoke(ref Data)
                : defaultValue;
    }
}
