// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ArrangeConstructorOrDestructorBody
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection.Emit;
using ActualLab.Internal;

namespace ActualLab.Interception;

#pragma warning disable MA0012
#pragma warning disable CA2201
#pragma warning disable IL2046

public abstract partial record ArgumentList
{
    public const int NativeTypeCount = 8;
#if NET5_0_OR_GREATER
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentList<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentList<, >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentList<, , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentList<, , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentList<, , , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentList<, , , , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentList<, , , , , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentList<, , , , , , , >))]
#endif
    public static readonly ImmutableArray<Type> NativeTypes = ImmutableArray.Create(new [] {
        typeof(ArgumentList0),
        typeof(ArgumentList<>),
        typeof(ArgumentList<, >),
        typeof(ArgumentList<, , >),
        typeof(ArgumentList<, , , >),
        typeof(ArgumentList<, , , , >),
        typeof(ArgumentList<, , , , , >),
        typeof(ArgumentList<, , , , , , >),
        typeof(ArgumentList<, , , , , , , >),
    });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList<T0> New<T0>(T0 item0)
        => new(item0);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList<T0, T1> New<T0, T1>(T0 item0, T1 item1)
        => new(item0, item1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList<T0, T1, T2> New<T0, T1, T2>(T0 item0, T1 item1, T2 item2)
        => new(item0, item1, item2);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList<T0, T1, T2, T3> New<T0, T1, T2, T3>(T0 item0, T1 item1, T2 item2, T3 item3)
        => new(item0, item1, item2, item3);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList<T0, T1, T2, T3, T4> New<T0, T1, T2, T3, T4>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4)
        => new(item0, item1, item2, item3, item4);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList<T0, T1, T2, T3, T4, T5> New<T0, T1, T2, T3, T4, T5>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
        => new(item0, item1, item2, item3, item4, item5);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList<T0, T1, T2, T3, T4, T5, T6> New<T0, T1, T2, T3, T4, T5, T6>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
        => new(item0, item1, item2, item3, item4, item5, item6);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList<T0, T1, T2, T3, T4, T5, T6, T7> New<T0, T1, T2, T3, T4, T5, T6, T7>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
        => new(item0, item1, item2, item3, item4, item5, item6, item7);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentListPair< ArgumentList<T0, T1, T2, T3, T4, T5, T6, T7>, ArgumentList<T8> > New<T0, T1, T2, T3, T4, T5, T6, T7, T8>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
        => new(new(item0, item1, item2, item3, item4, item5, item6, item7), new(item8));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentListPair< ArgumentList<T0, T1, T2, T3, T4, T5, T6, T7>, ArgumentList<T8, T9> > New<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8, T9 item9)
        => new(new(item0, item1, item2, item3, item4, item5, item6, item7), new(item8, item9));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentListPair< ArgumentList<T0, T1, T2, T3, T4, T5, T6, T7>, ArgumentList<T8, T9, T10> > New<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8, T9 item9, T10 item10)
        => new(new(item0, item1, item2, item3, item4, item5, item6, item7), new(item8, item9, item10));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentListPair< ArgumentList<T0, T1, T2, T3, T4, T5, T6, T7>, ArgumentList<T8, T9, T10, T11> > New<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8, T9 item9, T10 item10, T11 item11)
        => new(new(item0, item1, item2, item3, item4, item5, item6, item7), new(item8, item9, item10, item11));

    public virtual T Get0<T>() => throw new IndexOutOfRangeException();
    public virtual T Get1<T>() => throw new IndexOutOfRangeException();
    public virtual T Get2<T>() => throw new IndexOutOfRangeException();
    public virtual T Get3<T>() => throw new IndexOutOfRangeException();
    public virtual T Get4<T>() => throw new IndexOutOfRangeException();
    public virtual T Get5<T>() => throw new IndexOutOfRangeException();
    public virtual T Get6<T>() => throw new IndexOutOfRangeException();
    public virtual T Get7<T>() => throw new IndexOutOfRangeException();
}

public abstract record ArgumentList1 : ArgumentListNative
{
    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[1];

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public override int Length => 1;
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentList<T0> : ArgumentList1
{
    private T0 _item0;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T0 Item0 { get => _item0; init => _item0 = value; }

    // Constructors

    public ArgumentList()
    {
        _item0 = default!;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public ArgumentList(T0 item0)
    {
        _item0 = item0;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentList<T0>(Item0);

    // ToString & ToArray

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('(');
        sb.Append(Item0 is CancellationToken ct0 ? ct0.Format() : Item0);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex == 0
            ? Array.Empty<object?>()
            : throw new ArgumentOutOfRangeException(nameof(skipIndex));

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType != null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => typeof(T0),
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;

    public override T Get<T>(int index)
        => index switch {
            0 => Item0 is T value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override object? GetUntyped(int index)
        => index switch {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            0 => Item0,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override CancellationToken GetCancellationToken(int index)
        => index switch {
            0 => Item0 is CancellationToken value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        _item0 = other.Get0<T0>();
    }

    // Insert

    public override ArgumentList Insert<T>(int index, T item)
        => index switch {
            0 => New(item, Item0),
            1 => New(Item0, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override ArgumentList InsertCancellationToken(int index, CancellationToken item)
        => index switch {
            0 => New(item, Item0),
            1 => New(Item0, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Remove

    public override ArgumentList Remove(int index)
        => index switch {
            0 => New(),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 1)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(listType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, listType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item0")!.GetGetMethod()!);

                // Call method
                il.Emit(method1.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method1);

                // Box return type
                if (method1.ReturnType == typeof(void))
                    il.Emit(OpCodes.Ldnull);
                else if (method1.ReturnType.IsValueType)
                    il.Emit(OpCodes.Box, method1.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object?, ArgumentList, object?>)m.CreateDelegate(typeof(Func<object, ArgumentList, object?>));
            }
            : static key => { // Expression trees
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 1)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(listType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, listType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.PropertyOrField(vList, "Item0")
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnObject(typeof(T0), _item0, 0);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, offset + 0);
        else
            reader.OnObject(typeof(T0), _item0, offset + 0);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), 0)!;
    }


    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(offset + 0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), offset + 0)!;
    }

    // Equality

    public bool Equals(ArgumentList<T0>? other)
    {
        if (other == null)
            return false;

        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentList<T0> vOther)
            return false;

        if (skipIndex != 0 && !EqualityComparer<T0>.Default.Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode = EqualityComparer<T0>.Default.GetHashCode(Item0!);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode = skipIndex == 0 ? 0 : EqualityComparer<T0>.Default.GetHashCode(Item0!);
            return hashCode;
        }
    }
}

public abstract record ArgumentList2 : ArgumentListNative
{
    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[2];

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public override int Length => 2;
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentList<T0, T1> : ArgumentList2
{
    private T0 _item0;
    private T1 _item1;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T0 Item0 { get => _item0; init => _item0 = value; }
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public T1 Item1 { get => _item1; init => _item1 = value; }

    // Constructors

    public ArgumentList()
    {
        _item0 = default!;
        _item1 = default!;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public ArgumentList(T0 item0, T1 item1)
    {
        _item0 = item0;
        _item1 = item1;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentList<T0, T1>(Item0, Item1);

    // ToString & ToArray

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('(');
        sb.Append(Item0 is CancellationToken ct0 ? ct0.Format() : Item0);
        sb.Append(", ");
        sb.Append(Item1 is CancellationToken ct1 ? ct1.Format() : Item1);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1 },
            1 => new object?[] { Item0 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType != null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType != null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => typeof(T0),
            1 => typeof(T1),
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    public override T Get1<T>() => Item1 is T value ? value : default!;

    public override T Get<T>(int index)
        => index switch {
            0 => Item0 is T value ? value : default!,
            1 => Item1 is T value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override object? GetUntyped(int index)
        => index switch {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            0 => Item0,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            1 => Item1,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override CancellationToken GetCancellationToken(int index)
        => index switch {
            0 => Item0 is CancellationToken value ? value : default!,
            1 => Item1 is CancellationToken value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        _item0 = other.Get0<T0>();
        _item1 = other.Get1<T1>();
    }

    // Insert

    public override ArgumentList Insert<T>(int index, T item)
        => index switch {
            0 => New(item, Item0, Item1),
            1 => New(Item0, item, Item1),
            2 => New(Item0, Item1, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override ArgumentList InsertCancellationToken(int index, CancellationToken item)
        => index switch {
            0 => New(item, Item0, Item1),
            1 => New(Item0, item, Item1),
            2 => New(Item0, Item1, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Remove

    public override ArgumentList Remove(int index)
        => index switch {
            0 => New(Item1),
            1 => New(Item0),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 2)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(listType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, listType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item1")!.GetGetMethod()!);

                // Call method
                il.Emit(method1.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method1);

                // Box return type
                if (method1.ReturnType == typeof(void))
                    il.Emit(OpCodes.Ldnull);
                else if (method1.ReturnType.IsValueType)
                    il.Emit(OpCodes.Box, method1.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object?, ArgumentList, object?>)m.CreateDelegate(typeof(Func<object, ArgumentList, object?>));
            }
            : static key => { // Expression trees
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 2)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(listType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, listType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.PropertyOrField(vList, "Item0")
                                , Expression.PropertyOrField(vList, "Item1")
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnObject(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnObject(typeof(T1), _item1, 1);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, offset + 0);
        else
            reader.OnObject(typeof(T0), _item0, offset + 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, offset + 1);
        else
            reader.OnObject(typeof(T1), _item1, offset + 1);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), 1)!;
    }


    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(offset + 0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), offset + 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(offset + 1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), offset + 1)!;
    }

    // Equality

    public bool Equals(ArgumentList<T0, T1>? other)
    {
        if (other == null)
            return false;

        if (!EqualityComparer<T1>.Default.Equals(Item1, other.Item1))
            return false;
        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentList<T0, T1> vOther)
            return false;

        if (skipIndex != 1 && !EqualityComparer<T1>.Default.Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !EqualityComparer<T0>.Default.Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode = EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + EqualityComparer<T1>.Default.GetHashCode(Item1!);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode = skipIndex == 0 ? 0 : EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + (skipIndex == 1 ? 0 : EqualityComparer<T1>.Default.GetHashCode(Item1!));
            return hashCode;
        }
    }
}

public abstract record ArgumentList3 : ArgumentListNative
{
    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[3];

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public override int Length => 3;
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentList<T0, T1, T2> : ArgumentList3
{
    private T0 _item0;
    private T1 _item1;
    private T2 _item2;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T0 Item0 { get => _item0; init => _item0 = value; }
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public T1 Item1 { get => _item1; init => _item1 = value; }
    [DataMember(Order = 2), MemoryPackOrder(2)]
    public T2 Item2 { get => _item2; init => _item2 = value; }

    // Constructors

    public ArgumentList()
    {
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public ArgumentList(T0 item0, T1 item1, T2 item2)
    {
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentList<T0, T1, T2>(Item0, Item1, Item2);

    // ToString & ToArray

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('(');
        sb.Append(Item0 is CancellationToken ct0 ? ct0.Format() : Item0);
        sb.Append(", ");
        sb.Append(Item1 is CancellationToken ct1 ? ct1.Format() : Item1);
        sb.Append(", ");
        sb.Append(Item2 is CancellationToken ct2 ? ct2.Format() : Item2);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2 },
            1 => new object?[] { Item0, Item2 },
            2 => new object?[] { Item0, Item1 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType != null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType != null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType != null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => typeof(T0),
            1 => typeof(T1),
            2 => typeof(T2),
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    public override T Get2<T>() => Item2 is T value ? value : default!;

    public override T Get<T>(int index)
        => index switch {
            0 => Item0 is T value ? value : default!,
            1 => Item1 is T value ? value : default!,
            2 => Item2 is T value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override object? GetUntyped(int index)
        => index switch {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            0 => Item0,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            1 => Item1,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            2 => Item2,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override CancellationToken GetCancellationToken(int index)
        => index switch {
            0 => Item0 is CancellationToken value ? value : default!,
            1 => Item1 is CancellationToken value ? value : default!,
            2 => Item2 is CancellationToken value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        _item0 = other.Get0<T0>();
        _item1 = other.Get1<T1>();
        _item2 = other.Get2<T2>();
    }

    // Insert

    public override ArgumentList Insert<T>(int index, T item)
        => index switch {
            0 => New(item, Item0, Item1, Item2),
            1 => New(Item0, item, Item1, Item2),
            2 => New(Item0, Item1, item, Item2),
            3 => New(Item0, Item1, Item2, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override ArgumentList InsertCancellationToken(int index, CancellationToken item)
        => index switch {
            0 => New(item, Item0, Item1, Item2),
            1 => New(Item0, item, Item1, Item2),
            2 => New(Item0, Item1, item, Item2),
            3 => New(Item0, Item1, Item2, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Remove

    public override ArgumentList Remove(int index)
        => index switch {
            0 => New(Item1, Item2),
            1 => New(Item0, Item2),
            2 => New(Item0, Item1),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 3)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(listType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, listType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item2")!.GetGetMethod()!);

                // Call method
                il.Emit(method1.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method1);

                // Box return type
                if (method1.ReturnType == typeof(void))
                    il.Emit(OpCodes.Ldnull);
                else if (method1.ReturnType.IsValueType)
                    il.Emit(OpCodes.Box, method1.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object?, ArgumentList, object?>)m.CreateDelegate(typeof(Func<object, ArgumentList, object?>));
            }
            : static key => { // Expression trees
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 3)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(listType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, listType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.PropertyOrField(vList, "Item0")
                                , Expression.PropertyOrField(vList, "Item1")
                                , Expression.PropertyOrField(vList, "Item2")
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnObject(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnObject(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnObject(typeof(T2), _item2, 2);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, offset + 0);
        else
            reader.OnObject(typeof(T0), _item0, offset + 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, offset + 1);
        else
            reader.OnObject(typeof(T1), _item1, offset + 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, offset + 2);
        else
            reader.OnObject(typeof(T2), _item2, offset + 2);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), 2)!;
    }


    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(offset + 0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), offset + 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(offset + 1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), offset + 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(offset + 2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), offset + 2)!;
    }

    // Equality

    public bool Equals(ArgumentList<T0, T1, T2>? other)
    {
        if (other == null)
            return false;

        if (!EqualityComparer<T2>.Default.Equals(Item2, other.Item2))
            return false;
        if (!EqualityComparer<T1>.Default.Equals(Item1, other.Item1))
            return false;
        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentList<T0, T1, T2> vOther)
            return false;

        if (skipIndex != 2 && !EqualityComparer<T2>.Default.Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !EqualityComparer<T1>.Default.Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !EqualityComparer<T0>.Default.Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode = EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + EqualityComparer<T1>.Default.GetHashCode(Item1!);
            hashCode = 397*hashCode + EqualityComparer<T2>.Default.GetHashCode(Item2!);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode = skipIndex == 0 ? 0 : EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + (skipIndex == 1 ? 0 : EqualityComparer<T1>.Default.GetHashCode(Item1!));
            hashCode = 397*hashCode + (skipIndex == 2 ? 0 : EqualityComparer<T2>.Default.GetHashCode(Item2!));
            return hashCode;
        }
    }
}

public abstract record ArgumentList4 : ArgumentListNative
{
    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[4];

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public override int Length => 4;
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentList<T0, T1, T2, T3> : ArgumentList4
{
    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T0 Item0 { get => _item0; init => _item0 = value; }
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public T1 Item1 { get => _item1; init => _item1 = value; }
    [DataMember(Order = 2), MemoryPackOrder(2)]
    public T2 Item2 { get => _item2; init => _item2 = value; }
    [DataMember(Order = 3), MemoryPackOrder(3)]
    public T3 Item3 { get => _item3; init => _item3 = value; }

    // Constructors

    public ArgumentList()
    {
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public ArgumentList(T0 item0, T1 item1, T2 item2, T3 item3)
    {
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentList<T0, T1, T2, T3>(Item0, Item1, Item2, Item3);

    // ToString & ToArray

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('(');
        sb.Append(Item0 is CancellationToken ct0 ? ct0.Format() : Item0);
        sb.Append(", ");
        sb.Append(Item1 is CancellationToken ct1 ? ct1.Format() : Item1);
        sb.Append(", ");
        sb.Append(Item2 is CancellationToken ct2 ? ct2.Format() : Item2);
        sb.Append(", ");
        sb.Append(Item3 is CancellationToken ct3 ? ct3.Format() : Item3);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2, Item3 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2, Item3 },
            1 => new object?[] { Item0, Item2, Item3 },
            2 => new object?[] { Item0, Item1, Item3 },
            3 => new object?[] { Item0, Item1, Item2 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType != null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType != null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType != null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType != null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => typeof(T0),
            1 => typeof(T1),
            2 => typeof(T2),
            3 => typeof(T3),
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    public override T Get3<T>() => Item3 is T value ? value : default!;

    public override T Get<T>(int index)
        => index switch {
            0 => Item0 is T value ? value : default!,
            1 => Item1 is T value ? value : default!,
            2 => Item2 is T value ? value : default!,
            3 => Item3 is T value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override object? GetUntyped(int index)
        => index switch {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            0 => Item0,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            1 => Item1,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            2 => Item2,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            3 => Item3,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override CancellationToken GetCancellationToken(int index)
        => index switch {
            0 => Item0 is CancellationToken value ? value : default!,
            1 => Item1 is CancellationToken value ? value : default!,
            2 => Item2 is CancellationToken value ? value : default!,
            3 => Item3 is CancellationToken value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        _item0 = other.Get0<T0>();
        _item1 = other.Get1<T1>();
        _item2 = other.Get2<T2>();
        _item3 = other.Get3<T3>();
    }

    // Insert

    public override ArgumentList Insert<T>(int index, T item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3),
            1 => New(Item0, item, Item1, Item2, Item3),
            2 => New(Item0, Item1, item, Item2, Item3),
            3 => New(Item0, Item1, Item2, item, Item3),
            4 => New(Item0, Item1, Item2, Item3, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override ArgumentList InsertCancellationToken(int index, CancellationToken item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3),
            1 => New(Item0, item, Item1, Item2, Item3),
            2 => New(Item0, Item1, item, Item2, Item3),
            3 => New(Item0, Item1, Item2, item, Item3),
            4 => New(Item0, Item1, Item2, Item3, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Remove

    public override ArgumentList Remove(int index)
        => index switch {
            0 => New(Item1, Item2, Item3),
            1 => New(Item0, Item2, Item3),
            2 => New(Item0, Item1, Item3),
            3 => New(Item0, Item1, Item2),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 4)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(listType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, listType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item3")!.GetGetMethod()!);

                // Call method
                il.Emit(method1.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method1);

                // Box return type
                if (method1.ReturnType == typeof(void))
                    il.Emit(OpCodes.Ldnull);
                else if (method1.ReturnType.IsValueType)
                    il.Emit(OpCodes.Box, method1.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object?, ArgumentList, object?>)m.CreateDelegate(typeof(Func<object, ArgumentList, object?>));
            }
            : static key => { // Expression trees
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 4)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(listType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, listType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.PropertyOrField(vList, "Item0")
                                , Expression.PropertyOrField(vList, "Item1")
                                , Expression.PropertyOrField(vList, "Item2")
                                , Expression.PropertyOrField(vList, "Item3")
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnObject(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnObject(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnObject(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnObject(typeof(T3), _item3, 3);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, offset + 0);
        else
            reader.OnObject(typeof(T0), _item0, offset + 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, offset + 1);
        else
            reader.OnObject(typeof(T1), _item1, offset + 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, offset + 2);
        else
            reader.OnObject(typeof(T2), _item2, offset + 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, offset + 3);
        else
            reader.OnObject(typeof(T3), _item3, offset + 3);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), 3)!;
    }


    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(offset + 0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), offset + 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(offset + 1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), offset + 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(offset + 2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), offset + 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(offset + 3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), offset + 3)!;
    }

    // Equality

    public bool Equals(ArgumentList<T0, T1, T2, T3>? other)
    {
        if (other == null)
            return false;

        if (!EqualityComparer<T3>.Default.Equals(Item3, other.Item3))
            return false;
        if (!EqualityComparer<T2>.Default.Equals(Item2, other.Item2))
            return false;
        if (!EqualityComparer<T1>.Default.Equals(Item1, other.Item1))
            return false;
        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentList<T0, T1, T2, T3> vOther)
            return false;

        if (skipIndex != 3 && !EqualityComparer<T3>.Default.Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !EqualityComparer<T2>.Default.Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !EqualityComparer<T1>.Default.Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !EqualityComparer<T0>.Default.Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode = EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + EqualityComparer<T1>.Default.GetHashCode(Item1!);
            hashCode = 397*hashCode + EqualityComparer<T2>.Default.GetHashCode(Item2!);
            hashCode = 397*hashCode + EqualityComparer<T3>.Default.GetHashCode(Item3!);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode = skipIndex == 0 ? 0 : EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + (skipIndex == 1 ? 0 : EqualityComparer<T1>.Default.GetHashCode(Item1!));
            hashCode = 397*hashCode + (skipIndex == 2 ? 0 : EqualityComparer<T2>.Default.GetHashCode(Item2!));
            hashCode = 397*hashCode + (skipIndex == 3 ? 0 : EqualityComparer<T3>.Default.GetHashCode(Item3!));
            return hashCode;
        }
    }
}

public abstract record ArgumentList5 : ArgumentListNative
{
    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[5];

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public override int Length => 5;
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentList<T0, T1, T2, T3, T4> : ArgumentList5
{
    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private T4 _item4;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T0 Item0 { get => _item0; init => _item0 = value; }
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public T1 Item1 { get => _item1; init => _item1 = value; }
    [DataMember(Order = 2), MemoryPackOrder(2)]
    public T2 Item2 { get => _item2; init => _item2 = value; }
    [DataMember(Order = 3), MemoryPackOrder(3)]
    public T3 Item3 { get => _item3; init => _item3 = value; }
    [DataMember(Order = 4), MemoryPackOrder(4)]
    public T4 Item4 { get => _item4; init => _item4 = value; }

    // Constructors

    public ArgumentList()
    {
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = default!;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public ArgumentList(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4)
    {
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentList<T0, T1, T2, T3, T4>(Item0, Item1, Item2, Item3, Item4);

    // ToString & ToArray

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('(');
        sb.Append(Item0 is CancellationToken ct0 ? ct0.Format() : Item0);
        sb.Append(", ");
        sb.Append(Item1 is CancellationToken ct1 ? ct1.Format() : Item1);
        sb.Append(", ");
        sb.Append(Item2 is CancellationToken ct2 ? ct2.Format() : Item2);
        sb.Append(", ");
        sb.Append(Item3 is CancellationToken ct3 ? ct3.Format() : Item3);
        sb.Append(", ");
        sb.Append(Item4 is CancellationToken ct4 ? ct4.Format() : Item4);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2, Item3, Item4 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2, Item3, Item4 },
            1 => new object?[] { Item0, Item2, Item3, Item4 },
            2 => new object?[] { Item0, Item1, Item3, Item4 },
            3 => new object?[] { Item0, Item1, Item2, Item4 },
            4 => new object?[] { Item0, Item1, Item2, Item3 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType != null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType != null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType != null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType != null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        if (!typeof(T4).IsValueType) {
            itemType = _item4?.GetType();
            if (itemType != null && itemType != typeof(T4)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => typeof(T0),
            1 => typeof(T1),
            2 => typeof(T2),
            3 => typeof(T3),
            4 => typeof(T4),
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    public override T Get4<T>() => Item4 is T value ? value : default!;

    public override T Get<T>(int index)
        => index switch {
            0 => Item0 is T value ? value : default!,
            1 => Item1 is T value ? value : default!,
            2 => Item2 is T value ? value : default!,
            3 => Item3 is T value ? value : default!,
            4 => Item4 is T value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override object? GetUntyped(int index)
        => index switch {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            0 => Item0,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            1 => Item1,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            2 => Item2,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            3 => Item3,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            4 => Item4,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override CancellationToken GetCancellationToken(int index)
        => index switch {
            0 => Item0 is CancellationToken value ? value : default!,
            1 => Item1 is CancellationToken value ? value : default!,
            2 => Item2 is CancellationToken value ? value : default!,
            3 => Item3 is CancellationToken value ? value : default!,
            4 => Item4 is CancellationToken value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        _item0 = other.Get0<T0>();
        _item1 = other.Get1<T1>();
        _item2 = other.Get2<T2>();
        _item3 = other.Get3<T3>();
        _item4 = other.Get4<T4>();
    }

    // Insert

    public override ArgumentList Insert<T>(int index, T item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3, Item4),
            1 => New(Item0, item, Item1, Item2, Item3, Item4),
            2 => New(Item0, Item1, item, Item2, Item3, Item4),
            3 => New(Item0, Item1, Item2, item, Item3, Item4),
            4 => New(Item0, Item1, Item2, Item3, item, Item4),
            5 => New(Item0, Item1, Item2, Item3, Item4, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override ArgumentList InsertCancellationToken(int index, CancellationToken item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3, Item4),
            1 => New(Item0, item, Item1, Item2, Item3, Item4),
            2 => New(Item0, Item1, item, Item2, Item3, Item4),
            3 => New(Item0, Item1, Item2, item, Item3, Item4),
            4 => New(Item0, Item1, Item2, Item3, item, Item4),
            5 => New(Item0, Item1, Item2, Item3, Item4, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Remove

    public override ArgumentList Remove(int index)
        => index switch {
            0 => New(Item1, Item2, Item3, Item4),
            1 => New(Item0, Item2, Item3, Item4),
            2 => New(Item0, Item1, Item3, Item4),
            3 => New(Item0, Item1, Item2, Item4),
            4 => New(Item0, Item1, Item2, Item3),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 5)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != typeof(T4))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(listType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, listType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item4")!.GetGetMethod()!);

                // Call method
                il.Emit(method1.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method1);

                // Box return type
                if (method1.ReturnType == typeof(void))
                    il.Emit(OpCodes.Ldnull);
                else if (method1.ReturnType.IsValueType)
                    il.Emit(OpCodes.Box, method1.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object?, ArgumentList, object?>)m.CreateDelegate(typeof(Func<object, ArgumentList, object?>));
            }
            : static key => { // Expression trees
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 5)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != typeof(T4))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(listType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, listType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.PropertyOrField(vList, "Item0")
                                , Expression.PropertyOrField(vList, "Item1")
                                , Expression.PropertyOrField(vList, "Item2")
                                , Expression.PropertyOrField(vList, "Item3")
                                , Expression.PropertyOrField(vList, "Item4")
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnObject(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnObject(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnObject(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnObject(typeof(T3), _item3, 3);
        if (typeof(T4).IsValueType)
            reader.OnStruct(_item4, 4);
        else
            reader.OnObject(typeof(T4), _item4, 4);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, offset + 0);
        else
            reader.OnObject(typeof(T0), _item0, offset + 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, offset + 1);
        else
            reader.OnObject(typeof(T1), _item1, offset + 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, offset + 2);
        else
            reader.OnObject(typeof(T2), _item2, offset + 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, offset + 3);
        else
            reader.OnObject(typeof(T3), _item3, offset + 3);
        if (typeof(T4).IsValueType)
            reader.OnStruct(_item4, offset + 4);
        else
            reader.OnObject(typeof(T4), _item4, offset + 4);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), 3)!;
        if (typeof(T4).IsValueType)
            _item4 = writer.OnStruct<T4>(4);
        else
            _item4 = (T4)writer.OnObject(typeof(T4), 4)!;
    }


    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(offset + 0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), offset + 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(offset + 1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), offset + 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(offset + 2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), offset + 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(offset + 3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), offset + 3)!;
        if (typeof(T4).IsValueType)
            _item4 = writer.OnStruct<T4>(offset + 4);
        else
            _item4 = (T4)writer.OnObject(typeof(T4), offset + 4)!;
    }

    // Equality

    public bool Equals(ArgumentList<T0, T1, T2, T3, T4>? other)
    {
        if (other == null)
            return false;

        if (!EqualityComparer<T4>.Default.Equals(Item4, other.Item4))
            return false;
        if (!EqualityComparer<T3>.Default.Equals(Item3, other.Item3))
            return false;
        if (!EqualityComparer<T2>.Default.Equals(Item2, other.Item2))
            return false;
        if (!EqualityComparer<T1>.Default.Equals(Item1, other.Item1))
            return false;
        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentList<T0, T1, T2, T3, T4> vOther)
            return false;

        if (skipIndex != 4 && !EqualityComparer<T4>.Default.Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !EqualityComparer<T3>.Default.Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !EqualityComparer<T2>.Default.Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !EqualityComparer<T1>.Default.Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !EqualityComparer<T0>.Default.Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode = EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + EqualityComparer<T1>.Default.GetHashCode(Item1!);
            hashCode = 397*hashCode + EqualityComparer<T2>.Default.GetHashCode(Item2!);
            hashCode = 397*hashCode + EqualityComparer<T3>.Default.GetHashCode(Item3!);
            hashCode = 397*hashCode + EqualityComparer<T4>.Default.GetHashCode(Item4!);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode = skipIndex == 0 ? 0 : EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + (skipIndex == 1 ? 0 : EqualityComparer<T1>.Default.GetHashCode(Item1!));
            hashCode = 397*hashCode + (skipIndex == 2 ? 0 : EqualityComparer<T2>.Default.GetHashCode(Item2!));
            hashCode = 397*hashCode + (skipIndex == 3 ? 0 : EqualityComparer<T3>.Default.GetHashCode(Item3!));
            hashCode = 397*hashCode + (skipIndex == 4 ? 0 : EqualityComparer<T4>.Default.GetHashCode(Item4!));
            return hashCode;
        }
    }
}

public abstract record ArgumentList6 : ArgumentListNative
{
    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[6];

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public override int Length => 6;
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentList<T0, T1, T2, T3, T4, T5> : ArgumentList6
{
    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private T4 _item4;
    private T5 _item5;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T0 Item0 { get => _item0; init => _item0 = value; }
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public T1 Item1 { get => _item1; init => _item1 = value; }
    [DataMember(Order = 2), MemoryPackOrder(2)]
    public T2 Item2 { get => _item2; init => _item2 = value; }
    [DataMember(Order = 3), MemoryPackOrder(3)]
    public T3 Item3 { get => _item3; init => _item3 = value; }
    [DataMember(Order = 4), MemoryPackOrder(4)]
    public T4 Item4 { get => _item4; init => _item4 = value; }
    [DataMember(Order = 5), MemoryPackOrder(5)]
    public T5 Item5 { get => _item5; init => _item5 = value; }

    // Constructors

    public ArgumentList()
    {
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = default!;
        _item5 = default!;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public ArgumentList(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
    {
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
        _item5 = item5;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentList<T0, T1, T2, T3, T4, T5>(Item0, Item1, Item2, Item3, Item4, Item5);

    // ToString & ToArray

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('(');
        sb.Append(Item0 is CancellationToken ct0 ? ct0.Format() : Item0);
        sb.Append(", ");
        sb.Append(Item1 is CancellationToken ct1 ? ct1.Format() : Item1);
        sb.Append(", ");
        sb.Append(Item2 is CancellationToken ct2 ? ct2.Format() : Item2);
        sb.Append(", ");
        sb.Append(Item3 is CancellationToken ct3 ? ct3.Format() : Item3);
        sb.Append(", ");
        sb.Append(Item4 is CancellationToken ct4 ? ct4.Format() : Item4);
        sb.Append(", ");
        sb.Append(Item5 is CancellationToken ct5 ? ct5.Format() : Item5);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2, Item3, Item4, Item5 },
            1 => new object?[] { Item0, Item2, Item3, Item4, Item5 },
            2 => new object?[] { Item0, Item1, Item3, Item4, Item5 },
            3 => new object?[] { Item0, Item1, Item2, Item4, Item5 },
            4 => new object?[] { Item0, Item1, Item2, Item3, Item5 },
            5 => new object?[] { Item0, Item1, Item2, Item3, Item4 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType != null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType != null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType != null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType != null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        if (!typeof(T4).IsValueType) {
            itemType = _item4?.GetType();
            if (itemType != null && itemType != typeof(T4)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        if (!typeof(T5).IsValueType) {
            itemType = _item5?.GetType();
            if (itemType != null && itemType != typeof(T5)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => typeof(T0),
            1 => typeof(T1),
            2 => typeof(T2),
            3 => typeof(T3),
            4 => typeof(T4),
            5 => typeof(T5),
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    public override T Get5<T>() => Item5 is T value ? value : default!;

    public override T Get<T>(int index)
        => index switch {
            0 => Item0 is T value ? value : default!,
            1 => Item1 is T value ? value : default!,
            2 => Item2 is T value ? value : default!,
            3 => Item3 is T value ? value : default!,
            4 => Item4 is T value ? value : default!,
            5 => Item5 is T value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override object? GetUntyped(int index)
        => index switch {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            0 => Item0,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            1 => Item1,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            2 => Item2,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            3 => Item3,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            4 => Item4,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            5 => Item5,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override CancellationToken GetCancellationToken(int index)
        => index switch {
            0 => Item0 is CancellationToken value ? value : default!,
            1 => Item1 is CancellationToken value ? value : default!,
            2 => Item2 is CancellationToken value ? value : default!,
            3 => Item3 is CancellationToken value ? value : default!,
            4 => Item4 is CancellationToken value ? value : default!,
            5 => Item5 is CancellationToken value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        case 5:
            _item5 = item is T5 item5 ? item5 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        case 5:
            _item5 = item is T5 item5 ? item5 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        case 5:
            _item5 = item is T5 item5 ? item5 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        _item0 = other.Get0<T0>();
        _item1 = other.Get1<T1>();
        _item2 = other.Get2<T2>();
        _item3 = other.Get3<T3>();
        _item4 = other.Get4<T4>();
        _item5 = other.Get5<T5>();
    }

    // Insert

    public override ArgumentList Insert<T>(int index, T item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3, Item4, Item5),
            1 => New(Item0, item, Item1, Item2, Item3, Item4, Item5),
            2 => New(Item0, Item1, item, Item2, Item3, Item4, Item5),
            3 => New(Item0, Item1, Item2, item, Item3, Item4, Item5),
            4 => New(Item0, Item1, Item2, Item3, item, Item4, Item5),
            5 => New(Item0, Item1, Item2, Item3, Item4, item, Item5),
            6 => New(Item0, Item1, Item2, Item3, Item4, Item5, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override ArgumentList InsertCancellationToken(int index, CancellationToken item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3, Item4, Item5),
            1 => New(Item0, item, Item1, Item2, Item3, Item4, Item5),
            2 => New(Item0, Item1, item, Item2, Item3, Item4, Item5),
            3 => New(Item0, Item1, Item2, item, Item3, Item4, Item5),
            4 => New(Item0, Item1, Item2, Item3, item, Item4, Item5),
            5 => New(Item0, Item1, Item2, Item3, Item4, item, Item5),
            6 => New(Item0, Item1, Item2, Item3, Item4, Item5, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Remove

    public override ArgumentList Remove(int index)
        => index switch {
            0 => New(Item1, Item2, Item3, Item4, Item5),
            1 => New(Item0, Item2, Item3, Item4, Item5),
            2 => New(Item0, Item1, Item3, Item4, Item5),
            3 => New(Item0, Item1, Item2, Item4, Item5),
            4 => New(Item0, Item1, Item2, Item3, Item5),
            5 => New(Item0, Item1, Item2, Item3, Item4),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 6)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != typeof(T4))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != typeof(T5))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(listType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, listType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item4")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item5")!.GetGetMethod()!);

                // Call method
                il.Emit(method1.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method1);

                // Box return type
                if (method1.ReturnType == typeof(void))
                    il.Emit(OpCodes.Ldnull);
                else if (method1.ReturnType.IsValueType)
                    il.Emit(OpCodes.Box, method1.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object?, ArgumentList, object?>)m.CreateDelegate(typeof(Func<object, ArgumentList, object?>));
            }
            : static key => { // Expression trees
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 6)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != typeof(T4))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != typeof(T5))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(listType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, listType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.PropertyOrField(vList, "Item0")
                                , Expression.PropertyOrField(vList, "Item1")
                                , Expression.PropertyOrField(vList, "Item2")
                                , Expression.PropertyOrField(vList, "Item3")
                                , Expression.PropertyOrField(vList, "Item4")
                                , Expression.PropertyOrField(vList, "Item5")
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnObject(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnObject(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnObject(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnObject(typeof(T3), _item3, 3);
        if (typeof(T4).IsValueType)
            reader.OnStruct(_item4, 4);
        else
            reader.OnObject(typeof(T4), _item4, 4);
        if (typeof(T5).IsValueType)
            reader.OnStruct(_item5, 5);
        else
            reader.OnObject(typeof(T5), _item5, 5);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, offset + 0);
        else
            reader.OnObject(typeof(T0), _item0, offset + 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, offset + 1);
        else
            reader.OnObject(typeof(T1), _item1, offset + 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, offset + 2);
        else
            reader.OnObject(typeof(T2), _item2, offset + 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, offset + 3);
        else
            reader.OnObject(typeof(T3), _item3, offset + 3);
        if (typeof(T4).IsValueType)
            reader.OnStruct(_item4, offset + 4);
        else
            reader.OnObject(typeof(T4), _item4, offset + 4);
        if (typeof(T5).IsValueType)
            reader.OnStruct(_item5, offset + 5);
        else
            reader.OnObject(typeof(T5), _item5, offset + 5);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), 3)!;
        if (typeof(T4).IsValueType)
            _item4 = writer.OnStruct<T4>(4);
        else
            _item4 = (T4)writer.OnObject(typeof(T4), 4)!;
        if (typeof(T5).IsValueType)
            _item5 = writer.OnStruct<T5>(5);
        else
            _item5 = (T5)writer.OnObject(typeof(T5), 5)!;
    }


    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(offset + 0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), offset + 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(offset + 1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), offset + 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(offset + 2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), offset + 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(offset + 3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), offset + 3)!;
        if (typeof(T4).IsValueType)
            _item4 = writer.OnStruct<T4>(offset + 4);
        else
            _item4 = (T4)writer.OnObject(typeof(T4), offset + 4)!;
        if (typeof(T5).IsValueType)
            _item5 = writer.OnStruct<T5>(offset + 5);
        else
            _item5 = (T5)writer.OnObject(typeof(T5), offset + 5)!;
    }

    // Equality

    public bool Equals(ArgumentList<T0, T1, T2, T3, T4, T5>? other)
    {
        if (other == null)
            return false;

        if (!EqualityComparer<T5>.Default.Equals(Item5, other.Item5))
            return false;
        if (!EqualityComparer<T4>.Default.Equals(Item4, other.Item4))
            return false;
        if (!EqualityComparer<T3>.Default.Equals(Item3, other.Item3))
            return false;
        if (!EqualityComparer<T2>.Default.Equals(Item2, other.Item2))
            return false;
        if (!EqualityComparer<T1>.Default.Equals(Item1, other.Item1))
            return false;
        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentList<T0, T1, T2, T3, T4, T5> vOther)
            return false;

        if (skipIndex != 5 && !EqualityComparer<T5>.Default.Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !EqualityComparer<T4>.Default.Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !EqualityComparer<T3>.Default.Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !EqualityComparer<T2>.Default.Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !EqualityComparer<T1>.Default.Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !EqualityComparer<T0>.Default.Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode = EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + EqualityComparer<T1>.Default.GetHashCode(Item1!);
            hashCode = 397*hashCode + EqualityComparer<T2>.Default.GetHashCode(Item2!);
            hashCode = 397*hashCode + EqualityComparer<T3>.Default.GetHashCode(Item3!);
            hashCode = 397*hashCode + EqualityComparer<T4>.Default.GetHashCode(Item4!);
            hashCode = 397*hashCode + EqualityComparer<T5>.Default.GetHashCode(Item5!);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode = skipIndex == 0 ? 0 : EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + (skipIndex == 1 ? 0 : EqualityComparer<T1>.Default.GetHashCode(Item1!));
            hashCode = 397*hashCode + (skipIndex == 2 ? 0 : EqualityComparer<T2>.Default.GetHashCode(Item2!));
            hashCode = 397*hashCode + (skipIndex == 3 ? 0 : EqualityComparer<T3>.Default.GetHashCode(Item3!));
            hashCode = 397*hashCode + (skipIndex == 4 ? 0 : EqualityComparer<T4>.Default.GetHashCode(Item4!));
            hashCode = 397*hashCode + (skipIndex == 5 ? 0 : EqualityComparer<T5>.Default.GetHashCode(Item5!));
            return hashCode;
        }
    }
}

public abstract record ArgumentList7 : ArgumentListNative
{
    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[7];

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public override int Length => 7;
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentList<T0, T1, T2, T3, T4, T5, T6> : ArgumentList7
{
    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private T4 _item4;
    private T5 _item5;
    private T6 _item6;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T0 Item0 { get => _item0; init => _item0 = value; }
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public T1 Item1 { get => _item1; init => _item1 = value; }
    [DataMember(Order = 2), MemoryPackOrder(2)]
    public T2 Item2 { get => _item2; init => _item2 = value; }
    [DataMember(Order = 3), MemoryPackOrder(3)]
    public T3 Item3 { get => _item3; init => _item3 = value; }
    [DataMember(Order = 4), MemoryPackOrder(4)]
    public T4 Item4 { get => _item4; init => _item4 = value; }
    [DataMember(Order = 5), MemoryPackOrder(5)]
    public T5 Item5 { get => _item5; init => _item5 = value; }
    [DataMember(Order = 6), MemoryPackOrder(6)]
    public T6 Item6 { get => _item6; init => _item6 = value; }

    // Constructors

    public ArgumentList()
    {
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = default!;
        _item5 = default!;
        _item6 = default!;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public ArgumentList(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
    {
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
        _item5 = item5;
        _item6 = item6;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentList<T0, T1, T2, T3, T4, T5, T6>(Item0, Item1, Item2, Item3, Item4, Item5, Item6);

    // ToString & ToArray

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('(');
        sb.Append(Item0 is CancellationToken ct0 ? ct0.Format() : Item0);
        sb.Append(", ");
        sb.Append(Item1 is CancellationToken ct1 ? ct1.Format() : Item1);
        sb.Append(", ");
        sb.Append(Item2 is CancellationToken ct2 ? ct2.Format() : Item2);
        sb.Append(", ");
        sb.Append(Item3 is CancellationToken ct3 ? ct3.Format() : Item3);
        sb.Append(", ");
        sb.Append(Item4 is CancellationToken ct4 ? ct4.Format() : Item4);
        sb.Append(", ");
        sb.Append(Item5 is CancellationToken ct5 ? ct5.Format() : Item5);
        sb.Append(", ");
        sb.Append(Item6 is CancellationToken ct6 ? ct6.Format() : Item6);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2, Item3, Item4, Item5, Item6 },
            1 => new object?[] { Item0, Item2, Item3, Item4, Item5, Item6 },
            2 => new object?[] { Item0, Item1, Item3, Item4, Item5, Item6 },
            3 => new object?[] { Item0, Item1, Item2, Item4, Item5, Item6 },
            4 => new object?[] { Item0, Item1, Item2, Item3, Item5, Item6 },
            5 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item6 },
            6 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType != null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType != null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType != null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType != null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        if (!typeof(T4).IsValueType) {
            itemType = _item4?.GetType();
            if (itemType != null && itemType != typeof(T4)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        if (!typeof(T5).IsValueType) {
            itemType = _item5?.GetType();
            if (itemType != null && itemType != typeof(T5)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        if (!typeof(T6).IsValueType) {
            itemType = _item6?.GetType();
            if (itemType != null && itemType != typeof(T6)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[6] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => typeof(T0),
            1 => typeof(T1),
            2 => typeof(T2),
            3 => typeof(T3),
            4 => typeof(T4),
            5 => typeof(T5),
            6 => typeof(T6),
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    public override T Get6<T>() => Item6 is T value ? value : default!;

    public override T Get<T>(int index)
        => index switch {
            0 => Item0 is T value ? value : default!,
            1 => Item1 is T value ? value : default!,
            2 => Item2 is T value ? value : default!,
            3 => Item3 is T value ? value : default!,
            4 => Item4 is T value ? value : default!,
            5 => Item5 is T value ? value : default!,
            6 => Item6 is T value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override object? GetUntyped(int index)
        => index switch {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            0 => Item0,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            1 => Item1,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            2 => Item2,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            3 => Item3,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            4 => Item4,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            5 => Item5,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            6 => Item6,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override CancellationToken GetCancellationToken(int index)
        => index switch {
            0 => Item0 is CancellationToken value ? value : default!,
            1 => Item1 is CancellationToken value ? value : default!,
            2 => Item2 is CancellationToken value ? value : default!,
            3 => Item3 is CancellationToken value ? value : default!,
            4 => Item4 is CancellationToken value ? value : default!,
            5 => Item5 is CancellationToken value ? value : default!,
            6 => Item6 is CancellationToken value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        case 5:
            _item5 = item is T5 item5 ? item5 : default!;
            break;
        case 6:
            _item6 = item is T6 item6 ? item6 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        case 5:
            _item5 = item is T5 item5 ? item5 : default!;
            break;
        case 6:
            _item6 = item is T6 item6 ? item6 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        case 5:
            _item5 = item is T5 item5 ? item5 : default!;
            break;
        case 6:
            _item6 = item is T6 item6 ? item6 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        _item0 = other.Get0<T0>();
        _item1 = other.Get1<T1>();
        _item2 = other.Get2<T2>();
        _item3 = other.Get3<T3>();
        _item4 = other.Get4<T4>();
        _item5 = other.Get5<T5>();
        _item6 = other.Get6<T6>();
    }

    // Insert

    public override ArgumentList Insert<T>(int index, T item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3, Item4, Item5, Item6),
            1 => New(Item0, item, Item1, Item2, Item3, Item4, Item5, Item6),
            2 => New(Item0, Item1, item, Item2, Item3, Item4, Item5, Item6),
            3 => New(Item0, Item1, Item2, item, Item3, Item4, Item5, Item6),
            4 => New(Item0, Item1, Item2, Item3, item, Item4, Item5, Item6),
            5 => New(Item0, Item1, Item2, Item3, Item4, item, Item5, Item6),
            6 => New(Item0, Item1, Item2, Item3, Item4, Item5, item, Item6),
            7 => New(Item0, Item1, Item2, Item3, Item4, Item5, Item6, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override ArgumentList InsertCancellationToken(int index, CancellationToken item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3, Item4, Item5, Item6),
            1 => New(Item0, item, Item1, Item2, Item3, Item4, Item5, Item6),
            2 => New(Item0, Item1, item, Item2, Item3, Item4, Item5, Item6),
            3 => New(Item0, Item1, Item2, item, Item3, Item4, Item5, Item6),
            4 => New(Item0, Item1, Item2, Item3, item, Item4, Item5, Item6),
            5 => New(Item0, Item1, Item2, Item3, Item4, item, Item5, Item6),
            6 => New(Item0, Item1, Item2, Item3, Item4, Item5, item, Item6),
            7 => New(Item0, Item1, Item2, Item3, Item4, Item5, Item6, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Remove

    public override ArgumentList Remove(int index)
        => index switch {
            0 => New(Item1, Item2, Item3, Item4, Item5, Item6),
            1 => New(Item0, Item2, Item3, Item4, Item5, Item6),
            2 => New(Item0, Item1, Item3, Item4, Item5, Item6),
            3 => New(Item0, Item1, Item2, Item4, Item5, Item6),
            4 => New(Item0, Item1, Item2, Item3, Item5, Item6),
            5 => New(Item0, Item1, Item2, Item3, Item4, Item6),
            6 => New(Item0, Item1, Item2, Item3, Item4, Item5),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 7)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != typeof(T4))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != typeof(T5))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != typeof(T6))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(listType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, listType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item4")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item5")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item6")!.GetGetMethod()!);

                // Call method
                il.Emit(method1.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method1);

                // Box return type
                if (method1.ReturnType == typeof(void))
                    il.Emit(OpCodes.Ldnull);
                else if (method1.ReturnType.IsValueType)
                    il.Emit(OpCodes.Box, method1.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object?, ArgumentList, object?>)m.CreateDelegate(typeof(Func<object, ArgumentList, object?>));
            }
            : static key => { // Expression trees
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 7)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != typeof(T4))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != typeof(T5))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != typeof(T6))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(listType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, listType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.PropertyOrField(vList, "Item0")
                                , Expression.PropertyOrField(vList, "Item1")
                                , Expression.PropertyOrField(vList, "Item2")
                                , Expression.PropertyOrField(vList, "Item3")
                                , Expression.PropertyOrField(vList, "Item4")
                                , Expression.PropertyOrField(vList, "Item5")
                                , Expression.PropertyOrField(vList, "Item6")
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnObject(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnObject(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnObject(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnObject(typeof(T3), _item3, 3);
        if (typeof(T4).IsValueType)
            reader.OnStruct(_item4, 4);
        else
            reader.OnObject(typeof(T4), _item4, 4);
        if (typeof(T5).IsValueType)
            reader.OnStruct(_item5, 5);
        else
            reader.OnObject(typeof(T5), _item5, 5);
        if (typeof(T6).IsValueType)
            reader.OnStruct(_item6, 6);
        else
            reader.OnObject(typeof(T6), _item6, 6);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, offset + 0);
        else
            reader.OnObject(typeof(T0), _item0, offset + 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, offset + 1);
        else
            reader.OnObject(typeof(T1), _item1, offset + 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, offset + 2);
        else
            reader.OnObject(typeof(T2), _item2, offset + 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, offset + 3);
        else
            reader.OnObject(typeof(T3), _item3, offset + 3);
        if (typeof(T4).IsValueType)
            reader.OnStruct(_item4, offset + 4);
        else
            reader.OnObject(typeof(T4), _item4, offset + 4);
        if (typeof(T5).IsValueType)
            reader.OnStruct(_item5, offset + 5);
        else
            reader.OnObject(typeof(T5), _item5, offset + 5);
        if (typeof(T6).IsValueType)
            reader.OnStruct(_item6, offset + 6);
        else
            reader.OnObject(typeof(T6), _item6, offset + 6);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), 3)!;
        if (typeof(T4).IsValueType)
            _item4 = writer.OnStruct<T4>(4);
        else
            _item4 = (T4)writer.OnObject(typeof(T4), 4)!;
        if (typeof(T5).IsValueType)
            _item5 = writer.OnStruct<T5>(5);
        else
            _item5 = (T5)writer.OnObject(typeof(T5), 5)!;
        if (typeof(T6).IsValueType)
            _item6 = writer.OnStruct<T6>(6);
        else
            _item6 = (T6)writer.OnObject(typeof(T6), 6)!;
    }


    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(offset + 0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), offset + 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(offset + 1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), offset + 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(offset + 2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), offset + 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(offset + 3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), offset + 3)!;
        if (typeof(T4).IsValueType)
            _item4 = writer.OnStruct<T4>(offset + 4);
        else
            _item4 = (T4)writer.OnObject(typeof(T4), offset + 4)!;
        if (typeof(T5).IsValueType)
            _item5 = writer.OnStruct<T5>(offset + 5);
        else
            _item5 = (T5)writer.OnObject(typeof(T5), offset + 5)!;
        if (typeof(T6).IsValueType)
            _item6 = writer.OnStruct<T6>(offset + 6);
        else
            _item6 = (T6)writer.OnObject(typeof(T6), offset + 6)!;
    }

    // Equality

    public bool Equals(ArgumentList<T0, T1, T2, T3, T4, T5, T6>? other)
    {
        if (other == null)
            return false;

        if (!EqualityComparer<T6>.Default.Equals(Item6, other.Item6))
            return false;
        if (!EqualityComparer<T5>.Default.Equals(Item5, other.Item5))
            return false;
        if (!EqualityComparer<T4>.Default.Equals(Item4, other.Item4))
            return false;
        if (!EqualityComparer<T3>.Default.Equals(Item3, other.Item3))
            return false;
        if (!EqualityComparer<T2>.Default.Equals(Item2, other.Item2))
            return false;
        if (!EqualityComparer<T1>.Default.Equals(Item1, other.Item1))
            return false;
        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentList<T0, T1, T2, T3, T4, T5, T6> vOther)
            return false;

        if (skipIndex != 6 && !EqualityComparer<T6>.Default.Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !EqualityComparer<T5>.Default.Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !EqualityComparer<T4>.Default.Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !EqualityComparer<T3>.Default.Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !EqualityComparer<T2>.Default.Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !EqualityComparer<T1>.Default.Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !EqualityComparer<T0>.Default.Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode = EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + EqualityComparer<T1>.Default.GetHashCode(Item1!);
            hashCode = 397*hashCode + EqualityComparer<T2>.Default.GetHashCode(Item2!);
            hashCode = 397*hashCode + EqualityComparer<T3>.Default.GetHashCode(Item3!);
            hashCode = 397*hashCode + EqualityComparer<T4>.Default.GetHashCode(Item4!);
            hashCode = 397*hashCode + EqualityComparer<T5>.Default.GetHashCode(Item5!);
            hashCode = 397*hashCode + EqualityComparer<T6>.Default.GetHashCode(Item6!);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode = skipIndex == 0 ? 0 : EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + (skipIndex == 1 ? 0 : EqualityComparer<T1>.Default.GetHashCode(Item1!));
            hashCode = 397*hashCode + (skipIndex == 2 ? 0 : EqualityComparer<T2>.Default.GetHashCode(Item2!));
            hashCode = 397*hashCode + (skipIndex == 3 ? 0 : EqualityComparer<T3>.Default.GetHashCode(Item3!));
            hashCode = 397*hashCode + (skipIndex == 4 ? 0 : EqualityComparer<T4>.Default.GetHashCode(Item4!));
            hashCode = 397*hashCode + (skipIndex == 5 ? 0 : EqualityComparer<T5>.Default.GetHashCode(Item5!));
            hashCode = 397*hashCode + (skipIndex == 6 ? 0 : EqualityComparer<T6>.Default.GetHashCode(Item6!));
            return hashCode;
        }
    }
}

public abstract record ArgumentList8 : ArgumentListNative
{
    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[8];

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public override int Length => 8;
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentList<T0, T1, T2, T3, T4, T5, T6, T7> : ArgumentList8
{
    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private T4 _item4;
    private T5 _item5;
    private T6 _item6;
    private T7 _item7;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T0 Item0 { get => _item0; init => _item0 = value; }
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public T1 Item1 { get => _item1; init => _item1 = value; }
    [DataMember(Order = 2), MemoryPackOrder(2)]
    public T2 Item2 { get => _item2; init => _item2 = value; }
    [DataMember(Order = 3), MemoryPackOrder(3)]
    public T3 Item3 { get => _item3; init => _item3 = value; }
    [DataMember(Order = 4), MemoryPackOrder(4)]
    public T4 Item4 { get => _item4; init => _item4 = value; }
    [DataMember(Order = 5), MemoryPackOrder(5)]
    public T5 Item5 { get => _item5; init => _item5 = value; }
    [DataMember(Order = 6), MemoryPackOrder(6)]
    public T6 Item6 { get => _item6; init => _item6 = value; }
    [DataMember(Order = 7), MemoryPackOrder(7)]
    public T7 Item7 { get => _item7; init => _item7 = value; }

    // Constructors

    public ArgumentList()
    {
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = default!;
        _item5 = default!;
        _item6 = default!;
        _item7 = default!;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public ArgumentList(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
    {
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
        _item5 = item5;
        _item6 = item6;
        _item7 = item7;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentList<T0, T1, T2, T3, T4, T5, T6, T7>(Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7);

    // ToString & ToArray

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('(');
        sb.Append(Item0 is CancellationToken ct0 ? ct0.Format() : Item0);
        sb.Append(", ");
        sb.Append(Item1 is CancellationToken ct1 ? ct1.Format() : Item1);
        sb.Append(", ");
        sb.Append(Item2 is CancellationToken ct2 ? ct2.Format() : Item2);
        sb.Append(", ");
        sb.Append(Item3 is CancellationToken ct3 ? ct3.Format() : Item3);
        sb.Append(", ");
        sb.Append(Item4 is CancellationToken ct4 ? ct4.Format() : Item4);
        sb.Append(", ");
        sb.Append(Item5 is CancellationToken ct5 ? ct5.Format() : Item5);
        sb.Append(", ");
        sb.Append(Item6 is CancellationToken ct6 ? ct6.Format() : Item6);
        sb.Append(", ");
        sb.Append(Item7 is CancellationToken ct7 ? ct7.Format() : Item7);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7 },
            1 => new object?[] { Item0, Item2, Item3, Item4, Item5, Item6, Item7 },
            2 => new object?[] { Item0, Item1, Item3, Item4, Item5, Item6, Item7 },
            3 => new object?[] { Item0, Item1, Item2, Item4, Item5, Item6, Item7 },
            4 => new object?[] { Item0, Item1, Item2, Item3, Item5, Item6, Item7 },
            5 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item6, Item7 },
            6 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item7 },
            7 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType != null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType != null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType != null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType != null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        if (!typeof(T4).IsValueType) {
            itemType = _item4?.GetType();
            if (itemType != null && itemType != typeof(T4)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        if (!typeof(T5).IsValueType) {
            itemType = _item5?.GetType();
            if (itemType != null && itemType != typeof(T5)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        if (!typeof(T6).IsValueType) {
            itemType = _item6?.GetType();
            if (itemType != null && itemType != typeof(T6)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[6] = itemType;
            }
        }
        if (!typeof(T7).IsValueType) {
            itemType = _item7?.GetType();
            if (itemType != null && itemType != typeof(T7)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[7] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => typeof(T0),
            1 => typeof(T1),
            2 => typeof(T2),
            3 => typeof(T3),
            4 => typeof(T4),
            5 => typeof(T5),
            6 => typeof(T6),
            7 => typeof(T7),
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    public override T Get6<T>() => Item6 is T value ? value : default!;
    public override T Get7<T>() => Item7 is T value ? value : default!;

    public override T Get<T>(int index)
        => index switch {
            0 => Item0 is T value ? value : default!,
            1 => Item1 is T value ? value : default!,
            2 => Item2 is T value ? value : default!,
            3 => Item3 is T value ? value : default!,
            4 => Item4 is T value ? value : default!,
            5 => Item5 is T value ? value : default!,
            6 => Item6 is T value ? value : default!,
            7 => Item7 is T value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override object? GetUntyped(int index)
        => index switch {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            0 => Item0,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            1 => Item1,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            2 => Item2,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            3 => Item3,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            4 => Item4,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            5 => Item5,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            6 => Item6,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            7 => Item7,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override CancellationToken GetCancellationToken(int index)
        => index switch {
            0 => Item0 is CancellationToken value ? value : default!,
            1 => Item1 is CancellationToken value ? value : default!,
            2 => Item2 is CancellationToken value ? value : default!,
            3 => Item3 is CancellationToken value ? value : default!,
            4 => Item4 is CancellationToken value ? value : default!,
            5 => Item5 is CancellationToken value ? value : default!,
            6 => Item6 is CancellationToken value ? value : default!,
            7 => Item7 is CancellationToken value ? value : default!,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        case 5:
            _item5 = item is T5 item5 ? item5 : default!;
            break;
        case 6:
            _item6 = item is T6 item6 ? item6 : default!;
            break;
        case 7:
            _item7 = item is T7 item7 ? item7 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        case 5:
            _item5 = item is T5 item5 ? item5 : default!;
            break;
        case 6:
            _item6 = item is T6 item6 ? item6 : default!;
            break;
        case 7:
            _item7 = item is T7 item7 ? item7 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = item is T0 item0 ? item0 : default!;
            break;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            break;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            break;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            break;
        case 4:
            _item4 = item is T4 item4 ? item4 : default!;
            break;
        case 5:
            _item5 = item is T5 item5 ? item5 : default!;
            break;
        case 6:
            _item6 = item is T6 item6 ? item6 : default!;
            break;
        case 7:
            _item7 = item is T7 item7 ? item7 : default!;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        _item0 = other.Get0<T0>();
        _item1 = other.Get1<T1>();
        _item2 = other.Get2<T2>();
        _item3 = other.Get3<T3>();
        _item4 = other.Get4<T4>();
        _item5 = other.Get5<T5>();
        _item6 = other.Get6<T6>();
        _item7 = other.Get7<T7>();
    }

    // Insert

    public override ArgumentList Insert<T>(int index, T item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7),
            1 => New(Item0, item, Item1, Item2, Item3, Item4, Item5, Item6, Item7),
            2 => New(Item0, Item1, item, Item2, Item3, Item4, Item5, Item6, Item7),
            3 => New(Item0, Item1, Item2, item, Item3, Item4, Item5, Item6, Item7),
            4 => New(Item0, Item1, Item2, Item3, item, Item4, Item5, Item6, Item7),
            5 => New(Item0, Item1, Item2, Item3, Item4, item, Item5, Item6, Item7),
            6 => New(Item0, Item1, Item2, Item3, Item4, Item5, item, Item6, Item7),
            7 => New(Item0, Item1, Item2, Item3, Item4, Item5, Item6, item, Item7),
            8 => New(Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public override ArgumentList InsertCancellationToken(int index, CancellationToken item)
        => index switch {
            0 => New(item, Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7),
            1 => New(Item0, item, Item1, Item2, Item3, Item4, Item5, Item6, Item7),
            2 => New(Item0, Item1, item, Item2, Item3, Item4, Item5, Item6, Item7),
            3 => New(Item0, Item1, Item2, item, Item3, Item4, Item5, Item6, Item7),
            4 => New(Item0, Item1, Item2, Item3, item, Item4, Item5, Item6, Item7),
            5 => New(Item0, Item1, Item2, Item3, Item4, item, Item5, Item6, Item7),
            6 => New(Item0, Item1, Item2, Item3, Item4, Item5, item, Item6, Item7),
            7 => New(Item0, Item1, Item2, Item3, Item4, Item5, Item6, item, Item7),
            8 => New(Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, item),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // Remove

    public override ArgumentList Remove(int index)
        => index switch {
            0 => New(Item1, Item2, Item3, Item4, Item5, Item6, Item7),
            1 => New(Item0, Item2, Item3, Item4, Item5, Item6, Item7),
            2 => New(Item0, Item1, Item3, Item4, Item5, Item6, Item7),
            3 => New(Item0, Item1, Item2, Item4, Item5, Item6, Item7),
            4 => New(Item0, Item1, Item2, Item3, Item5, Item6, Item7),
            5 => New(Item0, Item1, Item2, Item3, Item4, Item6, Item7),
            6 => New(Item0, Item1, Item2, Item3, Item4, Item5, Item7),
            7 => New(Item0, Item1, Item2, Item3, Item4, Item5, Item6),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 8)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != typeof(T4))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != typeof(T5))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != typeof(T6))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != typeof(T7))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(listType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, listType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item4")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item5")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item6")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, listType.GetProperty("Item7")!.GetGetMethod()!);

                // Call method
                il.Emit(method1.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method1);

                // Box return type
                if (method1.ReturnType == typeof(void))
                    il.Emit(OpCodes.Ldnull);
                else if (method1.ReturnType.IsValueType)
                    il.Emit(OpCodes.Box, method1.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object?, ArgumentList, object?>)m.CreateDelegate(typeof(Func<object, ArgumentList, object?>));
            }
            : static key => { // Expression trees
                var (listType, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 8)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != typeof(T0))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != typeof(T1))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != typeof(T2))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != typeof(T3))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != typeof(T4))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != typeof(T5))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != typeof(T6))
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != typeof(T7))
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(listType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, listType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.PropertyOrField(vList, "Item0")
                                , Expression.PropertyOrField(vList, "Item1")
                                , Expression.PropertyOrField(vList, "Item2")
                                , Expression.PropertyOrField(vList, "Item3")
                                , Expression.PropertyOrField(vList, "Item4")
                                , Expression.PropertyOrField(vList, "Item5")
                                , Expression.PropertyOrField(vList, "Item6")
                                , Expression.PropertyOrField(vList, "Item7")
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnObject(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnObject(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnObject(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnObject(typeof(T3), _item3, 3);
        if (typeof(T4).IsValueType)
            reader.OnStruct(_item4, 4);
        else
            reader.OnObject(typeof(T4), _item4, 4);
        if (typeof(T5).IsValueType)
            reader.OnStruct(_item5, 5);
        else
            reader.OnObject(typeof(T5), _item5, 5);
        if (typeof(T6).IsValueType)
            reader.OnStruct(_item6, 6);
        else
            reader.OnObject(typeof(T6), _item6, 6);
        if (typeof(T7).IsValueType)
            reader.OnStruct(_item7, 7);
        else
            reader.OnObject(typeof(T7), _item7, 7);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, offset + 0);
        else
            reader.OnObject(typeof(T0), _item0, offset + 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, offset + 1);
        else
            reader.OnObject(typeof(T1), _item1, offset + 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, offset + 2);
        else
            reader.OnObject(typeof(T2), _item2, offset + 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, offset + 3);
        else
            reader.OnObject(typeof(T3), _item3, offset + 3);
        if (typeof(T4).IsValueType)
            reader.OnStruct(_item4, offset + 4);
        else
            reader.OnObject(typeof(T4), _item4, offset + 4);
        if (typeof(T5).IsValueType)
            reader.OnStruct(_item5, offset + 5);
        else
            reader.OnObject(typeof(T5), _item5, offset + 5);
        if (typeof(T6).IsValueType)
            reader.OnStruct(_item6, offset + 6);
        else
            reader.OnObject(typeof(T6), _item6, offset + 6);
        if (typeof(T7).IsValueType)
            reader.OnStruct(_item7, offset + 7);
        else
            reader.OnObject(typeof(T7), _item7, offset + 7);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), 3)!;
        if (typeof(T4).IsValueType)
            _item4 = writer.OnStruct<T4>(4);
        else
            _item4 = (T4)writer.OnObject(typeof(T4), 4)!;
        if (typeof(T5).IsValueType)
            _item5 = writer.OnStruct<T5>(5);
        else
            _item5 = (T5)writer.OnObject(typeof(T5), 5)!;
        if (typeof(T6).IsValueType)
            _item6 = writer.OnStruct<T6>(6);
        else
            _item6 = (T6)writer.OnObject(typeof(T6), 6)!;
        if (typeof(T7).IsValueType)
            _item7 = writer.OnStruct<T7>(7);
        else
            _item7 = (T7)writer.OnObject(typeof(T7), 7)!;
    }


    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(offset + 0);
        else
            _item0 = (T0)writer.OnObject(typeof(T0), offset + 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(offset + 1);
        else
            _item1 = (T1)writer.OnObject(typeof(T1), offset + 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(offset + 2);
        else
            _item2 = (T2)writer.OnObject(typeof(T2), offset + 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(offset + 3);
        else
            _item3 = (T3)writer.OnObject(typeof(T3), offset + 3)!;
        if (typeof(T4).IsValueType)
            _item4 = writer.OnStruct<T4>(offset + 4);
        else
            _item4 = (T4)writer.OnObject(typeof(T4), offset + 4)!;
        if (typeof(T5).IsValueType)
            _item5 = writer.OnStruct<T5>(offset + 5);
        else
            _item5 = (T5)writer.OnObject(typeof(T5), offset + 5)!;
        if (typeof(T6).IsValueType)
            _item6 = writer.OnStruct<T6>(offset + 6);
        else
            _item6 = (T6)writer.OnObject(typeof(T6), offset + 6)!;
        if (typeof(T7).IsValueType)
            _item7 = writer.OnStruct<T7>(offset + 7);
        else
            _item7 = (T7)writer.OnObject(typeof(T7), offset + 7)!;
    }

    // Equality

    public bool Equals(ArgumentList<T0, T1, T2, T3, T4, T5, T6, T7>? other)
    {
        if (other == null)
            return false;

        if (!EqualityComparer<T7>.Default.Equals(Item7, other.Item7))
            return false;
        if (!EqualityComparer<T6>.Default.Equals(Item6, other.Item6))
            return false;
        if (!EqualityComparer<T5>.Default.Equals(Item5, other.Item5))
            return false;
        if (!EqualityComparer<T4>.Default.Equals(Item4, other.Item4))
            return false;
        if (!EqualityComparer<T3>.Default.Equals(Item3, other.Item3))
            return false;
        if (!EqualityComparer<T2>.Default.Equals(Item2, other.Item2))
            return false;
        if (!EqualityComparer<T1>.Default.Equals(Item1, other.Item1))
            return false;
        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentList<T0, T1, T2, T3, T4, T5, T6, T7> vOther)
            return false;

        if (skipIndex != 7 && !EqualityComparer<T7>.Default.Equals(Item7, vOther.Item7))
            return false;
        if (skipIndex != 6 && !EqualityComparer<T6>.Default.Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !EqualityComparer<T5>.Default.Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !EqualityComparer<T4>.Default.Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !EqualityComparer<T3>.Default.Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !EqualityComparer<T2>.Default.Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !EqualityComparer<T1>.Default.Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !EqualityComparer<T0>.Default.Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode = EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + EqualityComparer<T1>.Default.GetHashCode(Item1!);
            hashCode = 397*hashCode + EqualityComparer<T2>.Default.GetHashCode(Item2!);
            hashCode = 397*hashCode + EqualityComparer<T3>.Default.GetHashCode(Item3!);
            hashCode = 397*hashCode + EqualityComparer<T4>.Default.GetHashCode(Item4!);
            hashCode = 397*hashCode + EqualityComparer<T5>.Default.GetHashCode(Item5!);
            hashCode = 397*hashCode + EqualityComparer<T6>.Default.GetHashCode(Item6!);
            hashCode = 397*hashCode + EqualityComparer<T7>.Default.GetHashCode(Item7!);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode = skipIndex == 0 ? 0 : EqualityComparer<T0>.Default.GetHashCode(Item0!);
            hashCode = 397*hashCode + (skipIndex == 1 ? 0 : EqualityComparer<T1>.Default.GetHashCode(Item1!));
            hashCode = 397*hashCode + (skipIndex == 2 ? 0 : EqualityComparer<T2>.Default.GetHashCode(Item2!));
            hashCode = 397*hashCode + (skipIndex == 3 ? 0 : EqualityComparer<T3>.Default.GetHashCode(Item3!));
            hashCode = 397*hashCode + (skipIndex == 4 ? 0 : EqualityComparer<T4>.Default.GetHashCode(Item4!));
            hashCode = 397*hashCode + (skipIndex == 5 ? 0 : EqualityComparer<T5>.Default.GetHashCode(Item5!));
            hashCode = 397*hashCode + (skipIndex == 6 ? 0 : EqualityComparer<T6>.Default.GetHashCode(Item6!));
            hashCode = 397*hashCode + (skipIndex == 7 ? 0 : EqualityComparer<T7>.Default.GetHashCode(Item7!));
            return hashCode;
        }
    }
}

