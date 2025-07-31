// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ArrangeConstructorOrDestructorBody
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection.Emit;
using ActualLab.Internal;
using MessagePack;

namespace ActualLab.Interception;

#pragma warning disable MA0012
#pragma warning disable CA2201, CS0219

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract partial record ArgumentList
{
    public const int MaxItemCount = 10;
    public const int MaxGenericItemCount = 4;

#if NET5_0_OR_GREATER
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS1))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG1<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS2))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG2<, >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS3))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG3<, , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS4))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG4<, , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS5))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG5<, , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS6))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG6<, , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS7))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG7<, , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS8))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG8<, , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS9))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG9<, , , >))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListS10))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ArgumentListG10<, , , >))]
#endif
    public static readonly ImmutableArray<Type> SimpleTypes = ImmutableArray.Create(new [] {
        typeof(ArgumentList0),
        typeof(ArgumentListS1),
        typeof(ArgumentListS2),
        typeof(ArgumentListS3),
        typeof(ArgumentListS4),
        typeof(ArgumentListS5),
        typeof(ArgumentListS6),
        typeof(ArgumentListS7),
        typeof(ArgumentListS8),
        typeof(ArgumentListS9),
        typeof(ArgumentListS10),
    });
    public static readonly ImmutableArray<Type> GenericTypes = ImmutableArray.Create(new [] {
        typeof(ArgumentList0),
        typeof(ArgumentListG1<>),
        typeof(ArgumentListG2<, >),
        typeof(ArgumentListG3<, , >),
        typeof(ArgumentListG4<, , , >),
        typeof(ArgumentListG5<, , , >),
        typeof(ArgumentListG6<, , , >),
        typeof(ArgumentListG7<, , , >),
        typeof(ArgumentListG8<, , , >),
        typeof(ArgumentListG9<, , , >),
        typeof(ArgumentListG10<, , , >),
    });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New() => Empty;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0>(T0 item0)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG1<T0>(item0)
            : new ArgumentListS1(ArgumentListType.Get(typeof(T0)), item0);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0, T1>(T0 item0, T1 item1)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG2<T0, T1>(item0, item1)
            : new ArgumentListS2(ArgumentListType.Get(typeof(T0), typeof(T1)), item0, item1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0, T1, T2>(T0 item0, T1 item1, T2 item2)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG3<T0, T1, T2>(item0, item1, item2)
            : new ArgumentListS3(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2)), item0, item1, item2);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0, T1, T2, T3>(T0 item0, T1 item1, T2 item2, T3 item3)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG4<T0, T1, T2, T3>(item0, item1, item2, item3)
            : new ArgumentListS4(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3)), item0, item1, item2, item3);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0, T1, T2, T3, T4>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG5<T0, T1, T2, T3>(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4)), item0, item1, item2, item3, item4)
            : new ArgumentListS5(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4)), item0, item1, item2, item3, item4);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0, T1, T2, T3, T4, T5>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG6<T0, T1, T2, T3>(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)), item0, item1, item2, item3, item4, item5)
            : new ArgumentListS6(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)), item0, item1, item2, item3, item4, item5);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0, T1, T2, T3, T4, T5, T6>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG7<T0, T1, T2, T3>(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)), item0, item1, item2, item3, item4, item5, item6)
            : new ArgumentListS7(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)), item0, item1, item2, item3, item4, item5, item6);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0, T1, T2, T3, T4, T5, T6, T7>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG8<T0, T1, T2, T3>(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7)), item0, item1, item2, item3, item4, item5, item6, item7)
            : new ArgumentListS8(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7)), item0, item1, item2, item3, item4, item5, item6, item7);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0, T1, T2, T3, T4, T5, T6, T7, T8>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG9<T0, T1, T2, T3>(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8)), item0, item1, item2, item3, item4, item5, item6, item7, item8)
            : new ArgumentListS9(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8)), item0, item1, item2, item3, item4, item5, item6, item7, item8);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(T0 item0, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8, T9 item9)
        => (UseGenerics && !DisableGenerics)
            ? new ArgumentListG10<T0, T1, T2, T3>(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9)), item0, item1, item2, item3, item4, item5, item6, item7, item8, item9)
            : new ArgumentListS10(ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9)), item0, item1, item2, item3, item4, item5, item6, item7, item8, item9);

    public virtual T Get0<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get0Untyped() => throw new IndexOutOfRangeException();
    public virtual T Get1<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get1Untyped() => throw new IndexOutOfRangeException();
    public virtual T Get2<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get2Untyped() => throw new IndexOutOfRangeException();
    public virtual T Get3<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get3Untyped() => throw new IndexOutOfRangeException();
    public virtual T Get4<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get4Untyped() => throw new IndexOutOfRangeException();
    public virtual T Get5<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get5Untyped() => throw new IndexOutOfRangeException();
    public virtual T Get6<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get6Untyped() => throw new IndexOutOfRangeException();
    public virtual T Get7<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get7Untyped() => throw new IndexOutOfRangeException();
    public virtual T Get8<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get8Untyped() => throw new IndexOutOfRangeException();
    public virtual T Get9<T>() => throw new IndexOutOfRangeException();
    public virtual object? Get9Untyped() => throw new IndexOutOfRangeException();
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList1 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 1;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[1];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG1<T0> : ArgumentList1
{
    private static ArgumentListType? _cachedType;
    // ReSharper disable once InconsistentNaming
    private static ArgumentListType _type => _cachedType ??= ArgumentListType.Get(typeof(T0));

    private T0 _item0;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }

    // Constructors

    public ArgumentListG1()
    {
        _item0 = default!;
    }

    public ArgumentListG1(T0 item0)
    {
        _item0 = item0;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListG1<T0>(Item0);

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
            if (itemType is not null && itemType != typeof(T0)) {
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
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;

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
            _ => default,
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
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG1<T0> vOther) {
            _item0 = vOther._item0;
        }
        else {
            _item0 = other.Get0<T0>();
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 1)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 1)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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

    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
    }

    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
    }

    // Equality

    public bool Equals(ArgumentListG1<T0>? other)
    {
        if (other is null)
            return false;

        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListG1<T0> vOther)
            return false;

        if (skipIndex != 0 && !EqualityComparer<T0>.Default.Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS1 : ArgumentList1
{
    private readonly ArgumentListType _type;

    private object? _item0;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }

    // Constructors

    public ArgumentListS1(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
    }

    public ArgumentListS1(ArgumentListType type, object? item0)
    {
        _type = type;
        _item0 = item0;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListS1(_type, Item0);

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
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;

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
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS1 vOther) {
            _item0 = vOther._item0;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 1)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 1)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
    }

    // Equality

    public bool Equals(ArgumentListS1? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS1 vOther)
            return false;

        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList2 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 2;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[2];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG2<T0, T1> : ArgumentList2
{
    private static ArgumentListType? _cachedType;
    // ReSharper disable once InconsistentNaming
    private static ArgumentListType _type => _cachedType ??= ArgumentListType.Get(typeof(T0), typeof(T1));

    private T0 _item0;
    private T1 _item1;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public T1 Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }

    // Constructors

    public ArgumentListG2()
    {
        _item0 = default!;
        _item1 = default!;
    }

    public ArgumentListG2(T0 item0, T1 item1)
    {
        _item0 = item0;
        _item1 = item1;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListG2<T0, T1>(Item0, Item1);

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
            if (itemType is not null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != typeof(T1)) {
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
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;

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
            _ => default,
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
            return;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG2<T0, T1> vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
        }
        else {
            _item0 = other.Get0<T0>();
            _item1 = other.Get1<T1>();
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 2)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 2)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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

    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnClass(typeof(T1), _item1, 1);
    }

    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnClass(typeof(T1), 1)!;
    }

    // Equality

    public bool Equals(ArgumentListG2<T0, T1>? other)
    {
        if (other is null)
            return false;

        if (!EqualityComparer<T1>.Default.Equals(Item1, other.Item1))
            return false;
        if (!EqualityComparer<T0>.Default.Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListG2<T0, T1> vOther)
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
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS2 : ArgumentList2
{
    private readonly ArgumentListType _type;

    private object? _item0;
    private object? _item1;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public object? Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }

    // Constructors

    public ArgumentListS2(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
        _item1 = defaultValues[1];
    }

    public ArgumentListS2(ArgumentListType type, object? item0, object? item1)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListS2(_type, Item0, Item1);

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
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[1];
        if (!expectedItemType.IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            1 => _type.ItemTypes[1],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;

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
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        case 1:
            _item1 = _type.CastItem(1, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS2 vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
            _item1 = _type.CastItem(1, other.GetUntyped(1));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 2)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                itemType = type.ItemTypes[1];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 2)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item1"), type.ItemTypes[1])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
        reader.OnAny(itemTypes[1], _item1, 1);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
        _item1 = writer.OnAny(itemTypes[1], 1, defaultValues[1]);
    }

    // Equality

    public bool Equals(ArgumentListS2? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item1, other.Item1))
            return false;
        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS2 vOther)
            return false;

        if (skipIndex != 1 && !Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList3 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 3;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[3];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG3<T0, T1, T2> : ArgumentList3
{
    private static ArgumentListType? _cachedType;
    // ReSharper disable once InconsistentNaming
    private static ArgumentListType _type => _cachedType ??= ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2));

    private T0 _item0;
    private T1 _item1;
    private T2 _item2;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public T1 Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public T2 Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }

    // Constructors

    public ArgumentListG3()
    {
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
    }

    public ArgumentListG3(T0 item0, T1 item1, T2 item2)
    {
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListG3<T0, T1, T2>(Item0, Item1, Item2);

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
            if (itemType is not null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != typeof(T2)) {
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
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;

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
            _ => default,
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
            return;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            return;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG3<T0, T1, T2> vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
        }
        else {
            _item0 = other.Get0<T0>();
            _item1 = other.Get1<T1>();
            _item2 = other.Get2<T2>();
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 3)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 3)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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

    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnClass(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnClass(typeof(T2), _item2, 2);
    }

    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnClass(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnClass(typeof(T2), 2)!;
    }

    // Equality

    public bool Equals(ArgumentListG3<T0, T1, T2>? other)
    {
        if (other is null)
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
        if (other is not ArgumentListG3<T0, T1, T2> vOther)
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
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS3 : ArgumentList3
{
    private readonly ArgumentListType _type;

    private object? _item0;
    private object? _item1;
    private object? _item2;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public object? Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public object? Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }

    // Constructors

    public ArgumentListS3(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
        _item1 = defaultValues[1];
        _item2 = defaultValues[2];
    }

    public ArgumentListS3(ArgumentListType type, object? item0, object? item1, object? item2)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListS3(_type, Item0, Item1, Item2);

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
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[1];
        if (!expectedItemType.IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[2];
        if (!expectedItemType.IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            1 => _type.ItemTypes[1],
            2 => _type.ItemTypes[2],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;

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
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        case 1:
            _item1 = _type.CastItem(1, item);
            return;
        case 2:
            _item2 = _type.CastItem(2, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS3 vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
            _item1 = _type.CastItem(1, other.GetUntyped(1));
            _item2 = _type.CastItem(2, other.GetUntyped(2));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 3)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                itemType = type.ItemTypes[1];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                itemType = type.ItemTypes[2];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 3)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item1"), type.ItemTypes[1])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item2"), type.ItemTypes[2])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
        reader.OnAny(itemTypes[1], _item1, 1);
        reader.OnAny(itemTypes[2], _item2, 2);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
        _item1 = writer.OnAny(itemTypes[1], 1, defaultValues[1]);
        _item2 = writer.OnAny(itemTypes[2], 2, defaultValues[2]);
    }

    // Equality

    public bool Equals(ArgumentListS3? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item2, other.Item2))
            return false;
        if (!Equals(Item1, other.Item1))
            return false;
        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS3 vOther)
            return false;

        if (skipIndex != 2 && !Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList4 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 4;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[4];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG4<T0, T1, T2, T3> : ArgumentList4
{
    private static ArgumentListType? _cachedType;
    // ReSharper disable once InconsistentNaming
    private static ArgumentListType _type => _cachedType ??= ArgumentListType.Get(typeof(T0), typeof(T1), typeof(T2), typeof(T3));

    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public T1 Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public T2 Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public T3 Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }

    // Constructors

    public ArgumentListG4()
    {
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
    }

    public ArgumentListG4(T0 item0, T1 item1, T2 item2, T3 item3)
    {
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListG4<T0, T1, T2, T3>(Item0, Item1, Item2, Item3);

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
            if (itemType is not null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != typeof(T3)) {
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
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;

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
            _ => default,
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
            return;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            return;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            return;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG4<T0, T1, T2, T3> vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
        }
        else {
            _item0 = other.Get0<T0>();
            _item1 = other.Get1<T1>();
            _item2 = other.Get2<T2>();
            _item3 = other.Get3<T3>();
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 4)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 4)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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

    public override void Read(ArgumentListReader reader)
    {
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnClass(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnClass(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnClass(typeof(T3), _item3, 3);
    }

    public override void Write(ArgumentListWriter writer)
    {
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnClass(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnClass(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnClass(typeof(T3), 3)!;
    }

    // Equality

    public bool Equals(ArgumentListG4<T0, T1, T2, T3>? other)
    {
        if (other is null)
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
        if (other is not ArgumentListG4<T0, T1, T2, T3> vOther)
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
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS4 : ArgumentList4
{
    private readonly ArgumentListType _type;

    private object? _item0;
    private object? _item1;
    private object? _item2;
    private object? _item3;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public object? Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public object? Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public object? Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }

    // Constructors

    public ArgumentListS4(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
        _item1 = defaultValues[1];
        _item2 = defaultValues[2];
        _item3 = defaultValues[3];
    }

    public ArgumentListS4(ArgumentListType type, object? item0, object? item1, object? item2, object? item3)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListS4(_type, Item0, Item1, Item2, Item3);

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
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[1];
        if (!expectedItemType.IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[2];
        if (!expectedItemType.IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[3];
        if (!expectedItemType.IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            1 => _type.ItemTypes[1],
            2 => _type.ItemTypes[2],
            3 => _type.ItemTypes[3],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;

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
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        case 1:
            _item1 = _type.CastItem(1, item);
            return;
        case 2:
            _item2 = _type.CastItem(2, item);
            return;
        case 3:
            _item3 = _type.CastItem(3, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS4 vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
            _item1 = _type.CastItem(1, other.GetUntyped(1));
            _item2 = _type.CastItem(2, other.GetUntyped(2));
            _item3 = _type.CastItem(3, other.GetUntyped(3));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 4)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                itemType = type.ItemTypes[1];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                itemType = type.ItemTypes[2];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                itemType = type.ItemTypes[3];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 4)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item1"), type.ItemTypes[1])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item2"), type.ItemTypes[2])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item3"), type.ItemTypes[3])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
        reader.OnAny(itemTypes[1], _item1, 1);
        reader.OnAny(itemTypes[2], _item2, 2);
        reader.OnAny(itemTypes[3], _item3, 3);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
        _item1 = writer.OnAny(itemTypes[1], 1, defaultValues[1]);
        _item2 = writer.OnAny(itemTypes[2], 2, defaultValues[2]);
        _item3 = writer.OnAny(itemTypes[3], 3, defaultValues[3]);
    }

    // Equality

    public bool Equals(ArgumentListS4? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item3, other.Item3))
            return false;
        if (!Equals(Item2, other.Item2))
            return false;
        if (!Equals(Item1, other.Item1))
            return false;
        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS4 vOther)
            return false;

        if (skipIndex != 3 && !Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList5 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 5;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[5];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG5<T0, T1, T2, T3> : ArgumentList5
{
    private readonly ArgumentListType _type;

    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private object? _item4;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public T1 Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public T2 Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public T3 Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }

    // Constructors

    public ArgumentListG5(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = defaultValues[4];
    }

    public ArgumentListG5(ArgumentListType type, T0 item0, T1 item1, T2 item2, T3 item3, object? item4)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListG5<T0, T1, T2, T3>(_type, Item0, Item1, Item2, Item3, Item4);

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
        Type? expectedItemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
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
            4 => _type.ItemTypes[4],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;

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
            _ => default,
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
            _item4 = _type.CastItem(4, item);
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
            _item4 = _type.CastItem(4, item);
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
            return;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            return;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            return;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG5<T0, T1, T2, T3> vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
        }
        else {
            _item0 = other.Get0<T0>();
            _item1 = other.Get1<T1>();
            _item2 = other.Get2<T2>();
            _item3 = other.Get3<T3>();
            _item4 = _type.CastItem(4, other.GetUntyped(4));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 5)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 5)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnClass(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnClass(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnClass(typeof(T3), _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnClass(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnClass(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnClass(typeof(T3), 3)!;
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
    }

    // Equality

    public bool Equals(ArgumentListG5<T0, T1, T2, T3>? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item4, other.Item4))
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
        if (other is not ArgumentListG5<T0, T1, T2, T3> vOther)
            return false;

        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
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
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS5 : ArgumentList5
{
    private readonly ArgumentListType _type;

    private object? _item0;
    private object? _item1;
    private object? _item2;
    private object? _item3;
    private object? _item4;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public object? Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public object? Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public object? Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }

    // Constructors

    public ArgumentListS5(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
        _item1 = defaultValues[1];
        _item2 = defaultValues[2];
        _item3 = defaultValues[3];
        _item4 = defaultValues[4];
    }

    public ArgumentListS5(ArgumentListType type, object? item0, object? item1, object? item2, object? item3, object? item4)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListS5(_type, Item0, Item1, Item2, Item3, Item4);

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
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[1];
        if (!expectedItemType.IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[2];
        if (!expectedItemType.IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[3];
        if (!expectedItemType.IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            1 => _type.ItemTypes[1],
            2 => _type.ItemTypes[2],
            3 => _type.ItemTypes[3],
            4 => _type.ItemTypes[4],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;

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
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        case 1:
            _item1 = _type.CastItem(1, item);
            return;
        case 2:
            _item2 = _type.CastItem(2, item);
            return;
        case 3:
            _item3 = _type.CastItem(3, item);
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS5 vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
            _item1 = _type.CastItem(1, other.GetUntyped(1));
            _item2 = _type.CastItem(2, other.GetUntyped(2));
            _item3 = _type.CastItem(3, other.GetUntyped(3));
            _item4 = _type.CastItem(4, other.GetUntyped(4));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 5)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                itemType = type.ItemTypes[1];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                itemType = type.ItemTypes[2];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                itemType = type.ItemTypes[3];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 5)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item1"), type.ItemTypes[1])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item2"), type.ItemTypes[2])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item3"), type.ItemTypes[3])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
        reader.OnAny(itemTypes[1], _item1, 1);
        reader.OnAny(itemTypes[2], _item2, 2);
        reader.OnAny(itemTypes[3], _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
        _item1 = writer.OnAny(itemTypes[1], 1, defaultValues[1]);
        _item2 = writer.OnAny(itemTypes[2], 2, defaultValues[2]);
        _item3 = writer.OnAny(itemTypes[3], 3, defaultValues[3]);
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
    }

    // Equality

    public bool Equals(ArgumentListS5? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item4, other.Item4))
            return false;
        if (!Equals(Item3, other.Item3))
            return false;
        if (!Equals(Item2, other.Item2))
            return false;
        if (!Equals(Item1, other.Item1))
            return false;
        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS5 vOther)
            return false;

        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList6 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 6;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[6];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG6<T0, T1, T2, T3> : ArgumentList6
{
    private readonly ArgumentListType _type;

    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private object? _item4;
    private object? _item5;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public T1 Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public T2 Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public T3 Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }

    // Constructors

    public ArgumentListG6(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
    }

    public ArgumentListG6(ArgumentListType type, T0 item0, T1 item1, T2 item2, T3 item3, object? item4, object? item5)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
        _item5 = item5;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListG6<T0, T1, T2, T3>(_type, Item0, Item1, Item2, Item3, Item4, Item5);

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
        Type? expectedItemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
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
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;

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
            _ => default,
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
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
            return;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            return;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            return;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG6<T0, T1, T2, T3> vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
        }
        else {
            _item0 = other.Get0<T0>();
            _item1 = other.Get1<T1>();
            _item2 = other.Get2<T2>();
            _item3 = other.Get3<T3>();
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 6)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 6)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnClass(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnClass(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnClass(typeof(T3), _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnClass(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnClass(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnClass(typeof(T3), 3)!;
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
    }

    // Equality

    public bool Equals(ArgumentListG6<T0, T1, T2, T3>? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
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
        if (other is not ArgumentListG6<T0, T1, T2, T3> vOther)
            return false;

        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
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
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS6 : ArgumentList6
{
    private readonly ArgumentListType _type;

    private object? _item0;
    private object? _item1;
    private object? _item2;
    private object? _item3;
    private object? _item4;
    private object? _item5;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public object? Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public object? Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public object? Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }

    // Constructors

    public ArgumentListS6(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
        _item1 = defaultValues[1];
        _item2 = defaultValues[2];
        _item3 = defaultValues[3];
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
    }

    public ArgumentListS6(ArgumentListType type, object? item0, object? item1, object? item2, object? item3, object? item4, object? item5)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
        _item5 = item5;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListS6(_type, Item0, Item1, Item2, Item3, Item4, Item5);

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
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[1];
        if (!expectedItemType.IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[2];
        if (!expectedItemType.IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[3];
        if (!expectedItemType.IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            1 => _type.ItemTypes[1],
            2 => _type.ItemTypes[2],
            3 => _type.ItemTypes[3],
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;

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
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        case 1:
            _item1 = _type.CastItem(1, item);
            return;
        case 2:
            _item2 = _type.CastItem(2, item);
            return;
        case 3:
            _item3 = _type.CastItem(3, item);
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS6 vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
            _item1 = _type.CastItem(1, other.GetUntyped(1));
            _item2 = _type.CastItem(2, other.GetUntyped(2));
            _item3 = _type.CastItem(3, other.GetUntyped(3));
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 6)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                itemType = type.ItemTypes[1];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                itemType = type.ItemTypes[2];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                itemType = type.ItemTypes[3];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 6)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item1"), type.ItemTypes[1])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item2"), type.ItemTypes[2])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item3"), type.ItemTypes[3])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
        reader.OnAny(itemTypes[1], _item1, 1);
        reader.OnAny(itemTypes[2], _item2, 2);
        reader.OnAny(itemTypes[3], _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
        _item1 = writer.OnAny(itemTypes[1], 1, defaultValues[1]);
        _item2 = writer.OnAny(itemTypes[2], 2, defaultValues[2]);
        _item3 = writer.OnAny(itemTypes[3], 3, defaultValues[3]);
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
    }

    // Equality

    public bool Equals(ArgumentListS6? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
            return false;
        if (!Equals(Item3, other.Item3))
            return false;
        if (!Equals(Item2, other.Item2))
            return false;
        if (!Equals(Item1, other.Item1))
            return false;
        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS6 vOther)
            return false;

        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList7 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 7;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[7];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG7<T0, T1, T2, T3> : ArgumentList7
{
    private readonly ArgumentListType _type;

    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private object? _item4;
    private object? _item5;
    private object? _item6;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public T1 Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public T2 Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public T3 Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }
    public object? Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
        init => _item6 = value;
    }

    // Constructors

    public ArgumentListG7(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
        _item6 = defaultValues[6];
    }

    public ArgumentListG7(ArgumentListType type, T0 item0, T1 item1, T2 item2, T3 item3, object? item4, object? item5, object? item6)
    {
        _type = type;
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
        => new ArgumentListG7<T0, T1, T2, T3>(_type, Item0, Item1, Item2, Item3, Item4, Item5, Item6);

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
        Type? expectedItemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[6];
        if (!expectedItemType.IsValueType) {
            itemType = _item6?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
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
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            6 => _type.ItemTypes[6],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;
    public override T Get6<T>() => Item6 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get6Untyped() => Item6;

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
            _ => default,
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
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
            return;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            return;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            return;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        case 6:
            _item6 = _type.CastItem(6, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG7<T0, T1, T2, T3> vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
            _item6 = vOther._item6;
        }
        else {
            _item0 = other.Get0<T0>();
            _item1 = other.Get1<T1>();
            _item2 = other.Get2<T2>();
            _item3 = other.Get3<T3>();
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
            _item6 = _type.CastItem(6, other.GetUntyped(6));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 7)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item6")!.GetGetMethod()!);
                itemType = type.ItemTypes[6];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 7)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item6"), type.ItemTypes[6])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnClass(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnClass(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnClass(typeof(T3), _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
        reader.OnAny(itemTypes[6], _item6, 6);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnClass(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnClass(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnClass(typeof(T3), 3)!;
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
        _item6 = writer.OnAny(itemTypes[6], 6, defaultValues[6]);
    }

    // Equality

    public bool Equals(ArgumentListG7<T0, T1, T2, T3>? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item6, other.Item6))
            return false;
        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
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
        if (other is not ArgumentListG7<T0, T1, T2, T3> vOther)
            return false;

        if (skipIndex != 6 && !Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
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
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (Item6 is { } item6 ? item6.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 6 ? 0 : (Item6 is { } item6 ? item6.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS7 : ArgumentList7
{
    private readonly ArgumentListType _type;

    private object? _item0;
    private object? _item1;
    private object? _item2;
    private object? _item3;
    private object? _item4;
    private object? _item5;
    private object? _item6;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public object? Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public object? Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public object? Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }
    public object? Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
        init => _item6 = value;
    }

    // Constructors

    public ArgumentListS7(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
        _item1 = defaultValues[1];
        _item2 = defaultValues[2];
        _item3 = defaultValues[3];
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
        _item6 = defaultValues[6];
    }

    public ArgumentListS7(ArgumentListType type, object? item0, object? item1, object? item2, object? item3, object? item4, object? item5, object? item6)
    {
        _type = type;
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
        => new ArgumentListS7(_type, Item0, Item1, Item2, Item3, Item4, Item5, Item6);

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
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[1];
        if (!expectedItemType.IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[2];
        if (!expectedItemType.IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[3];
        if (!expectedItemType.IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[6];
        if (!expectedItemType.IsValueType) {
            itemType = _item6?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[6] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            1 => _type.ItemTypes[1],
            2 => _type.ItemTypes[2],
            3 => _type.ItemTypes[3],
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            6 => _type.ItemTypes[6],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;
    public override T Get6<T>() => Item6 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get6Untyped() => Item6;

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
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        case 1:
            _item1 = _type.CastItem(1, item);
            return;
        case 2:
            _item2 = _type.CastItem(2, item);
            return;
        case 3:
            _item3 = _type.CastItem(3, item);
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        case 6:
            _item6 = _type.CastItem(6, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS7 vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
            _item6 = vOther._item6;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
            _item1 = _type.CastItem(1, other.GetUntyped(1));
            _item2 = _type.CastItem(2, other.GetUntyped(2));
            _item3 = _type.CastItem(3, other.GetUntyped(3));
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
            _item6 = _type.CastItem(6, other.GetUntyped(6));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 7)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                itemType = type.ItemTypes[1];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                itemType = type.ItemTypes[2];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                itemType = type.ItemTypes[3];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item6")!.GetGetMethod()!);
                itemType = type.ItemTypes[6];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 7)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item1"), type.ItemTypes[1])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item2"), type.ItemTypes[2])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item3"), type.ItemTypes[3])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item6"), type.ItemTypes[6])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
        reader.OnAny(itemTypes[1], _item1, 1);
        reader.OnAny(itemTypes[2], _item2, 2);
        reader.OnAny(itemTypes[3], _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
        reader.OnAny(itemTypes[6], _item6, 6);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
        _item1 = writer.OnAny(itemTypes[1], 1, defaultValues[1]);
        _item2 = writer.OnAny(itemTypes[2], 2, defaultValues[2]);
        _item3 = writer.OnAny(itemTypes[3], 3, defaultValues[3]);
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
        _item6 = writer.OnAny(itemTypes[6], 6, defaultValues[6]);
    }

    // Equality

    public bool Equals(ArgumentListS7? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item6, other.Item6))
            return false;
        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
            return false;
        if (!Equals(Item3, other.Item3))
            return false;
        if (!Equals(Item2, other.Item2))
            return false;
        if (!Equals(Item1, other.Item1))
            return false;
        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS7 vOther)
            return false;

        if (skipIndex != 6 && !Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (Item6 is { } item6 ? item6.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 6 ? 0 : (Item6 is { } item6 ? item6.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList8 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 8;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[8];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG8<T0, T1, T2, T3> : ArgumentList8
{
    private readonly ArgumentListType _type;

    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private object? _item4;
    private object? _item5;
    private object? _item6;
    private object? _item7;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public T1 Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public T2 Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public T3 Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }
    public object? Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
        init => _item6 = value;
    }
    public object? Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
        init => _item7 = value;
    }

    // Constructors

    public ArgumentListG8(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
        _item6 = defaultValues[6];
        _item7 = defaultValues[7];
    }

    public ArgumentListG8(ArgumentListType type, T0 item0, T1 item1, T2 item2, T3 item3, object? item4, object? item5, object? item6, object? item7)
    {
        _type = type;
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
        => new ArgumentListG8<T0, T1, T2, T3>(_type, Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7);

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
        Type? expectedItemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[6];
        if (!expectedItemType.IsValueType) {
            itemType = _item6?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[6] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[7];
        if (!expectedItemType.IsValueType) {
            itemType = _item7?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
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
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            6 => _type.ItemTypes[6],
            7 => _type.ItemTypes[7],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;
    public override T Get6<T>() => Item6 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get6Untyped() => Item6;
    public override T Get7<T>() => Item7 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get7Untyped() => Item7;

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
            _ => default,
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
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
            return;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            return;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            return;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        case 6:
            _item6 = _type.CastItem(6, item);
            return;
        case 7:
            _item7 = _type.CastItem(7, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG8<T0, T1, T2, T3> vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
            _item6 = vOther._item6;
            _item7 = vOther._item7;
        }
        else {
            _item0 = other.Get0<T0>();
            _item1 = other.Get1<T1>();
            _item2 = other.Get2<T2>();
            _item3 = other.Get3<T3>();
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
            _item6 = _type.CastItem(6, other.GetUntyped(6));
            _item7 = _type.CastItem(7, other.GetUntyped(7));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 8)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item6")!.GetGetMethod()!);
                itemType = type.ItemTypes[6];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item7")!.GetGetMethod()!);
                itemType = type.ItemTypes[7];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 8)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item6"), type.ItemTypes[6])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item7"), type.ItemTypes[7])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnClass(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnClass(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnClass(typeof(T3), _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
        reader.OnAny(itemTypes[6], _item6, 6);
        reader.OnAny(itemTypes[7], _item7, 7);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnClass(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnClass(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnClass(typeof(T3), 3)!;
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
        _item6 = writer.OnAny(itemTypes[6], 6, defaultValues[6]);
        _item7 = writer.OnAny(itemTypes[7], 7, defaultValues[7]);
    }

    // Equality

    public bool Equals(ArgumentListG8<T0, T1, T2, T3>? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item7, other.Item7))
            return false;
        if (!Equals(Item6, other.Item6))
            return false;
        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
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
        if (other is not ArgumentListG8<T0, T1, T2, T3> vOther)
            return false;

        if (skipIndex != 7 && !Equals(Item7, vOther.Item7))
            return false;
        if (skipIndex != 6 && !Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
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
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (Item6 is { } item6 ? item6.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (Item7 is { } item7 ? item7.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 6 ? 0 : (Item6 is { } item6 ? item6.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 7 ? 0 : (Item7 is { } item7 ? item7.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS8 : ArgumentList8
{
    private readonly ArgumentListType _type;

    private object? _item0;
    private object? _item1;
    private object? _item2;
    private object? _item3;
    private object? _item4;
    private object? _item5;
    private object? _item6;
    private object? _item7;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public object? Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public object? Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public object? Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }
    public object? Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
        init => _item6 = value;
    }
    public object? Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
        init => _item7 = value;
    }

    // Constructors

    public ArgumentListS8(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
        _item1 = defaultValues[1];
        _item2 = defaultValues[2];
        _item3 = defaultValues[3];
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
        _item6 = defaultValues[6];
        _item7 = defaultValues[7];
    }

    public ArgumentListS8(ArgumentListType type, object? item0, object? item1, object? item2, object? item3, object? item4, object? item5, object? item6, object? item7)
    {
        _type = type;
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
        => new ArgumentListS8(_type, Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7);

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
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[1];
        if (!expectedItemType.IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[2];
        if (!expectedItemType.IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[3];
        if (!expectedItemType.IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[6];
        if (!expectedItemType.IsValueType) {
            itemType = _item6?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[6] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[7];
        if (!expectedItemType.IsValueType) {
            itemType = _item7?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[7] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            1 => _type.ItemTypes[1],
            2 => _type.ItemTypes[2],
            3 => _type.ItemTypes[3],
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            6 => _type.ItemTypes[6],
            7 => _type.ItemTypes[7],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;
    public override T Get6<T>() => Item6 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get6Untyped() => Item6;
    public override T Get7<T>() => Item7 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get7Untyped() => Item7;

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
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        case 1:
            _item1 = _type.CastItem(1, item);
            return;
        case 2:
            _item2 = _type.CastItem(2, item);
            return;
        case 3:
            _item3 = _type.CastItem(3, item);
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        case 6:
            _item6 = _type.CastItem(6, item);
            return;
        case 7:
            _item7 = _type.CastItem(7, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS8 vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
            _item6 = vOther._item6;
            _item7 = vOther._item7;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
            _item1 = _type.CastItem(1, other.GetUntyped(1));
            _item2 = _type.CastItem(2, other.GetUntyped(2));
            _item3 = _type.CastItem(3, other.GetUntyped(3));
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
            _item6 = _type.CastItem(6, other.GetUntyped(6));
            _item7 = _type.CastItem(7, other.GetUntyped(7));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 8)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                itemType = type.ItemTypes[1];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                itemType = type.ItemTypes[2];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                itemType = type.ItemTypes[3];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item6")!.GetGetMethod()!);
                itemType = type.ItemTypes[6];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item7")!.GetGetMethod()!);
                itemType = type.ItemTypes[7];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 8)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item1"), type.ItemTypes[1])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item2"), type.ItemTypes[2])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item3"), type.ItemTypes[3])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item6"), type.ItemTypes[6])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item7"), type.ItemTypes[7])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
        reader.OnAny(itemTypes[1], _item1, 1);
        reader.OnAny(itemTypes[2], _item2, 2);
        reader.OnAny(itemTypes[3], _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
        reader.OnAny(itemTypes[6], _item6, 6);
        reader.OnAny(itemTypes[7], _item7, 7);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
        _item1 = writer.OnAny(itemTypes[1], 1, defaultValues[1]);
        _item2 = writer.OnAny(itemTypes[2], 2, defaultValues[2]);
        _item3 = writer.OnAny(itemTypes[3], 3, defaultValues[3]);
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
        _item6 = writer.OnAny(itemTypes[6], 6, defaultValues[6]);
        _item7 = writer.OnAny(itemTypes[7], 7, defaultValues[7]);
    }

    // Equality

    public bool Equals(ArgumentListS8? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item7, other.Item7))
            return false;
        if (!Equals(Item6, other.Item6))
            return false;
        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
            return false;
        if (!Equals(Item3, other.Item3))
            return false;
        if (!Equals(Item2, other.Item2))
            return false;
        if (!Equals(Item1, other.Item1))
            return false;
        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS8 vOther)
            return false;

        if (skipIndex != 7 && !Equals(Item7, vOther.Item7))
            return false;
        if (skipIndex != 6 && !Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (Item6 is { } item6 ? item6.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (Item7 is { } item7 ? item7.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 6 ? 0 : (Item6 is { } item6 ? item6.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 7 ? 0 : (Item7 is { } item7 ? item7.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList9 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 9;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[9];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG9<T0, T1, T2, T3> : ArgumentList9
{
    private readonly ArgumentListType _type;

    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private object? _item4;
    private object? _item5;
    private object? _item6;
    private object? _item7;
    private object? _item8;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public T1 Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public T2 Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public T3 Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }
    public object? Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
        init => _item6 = value;
    }
    public object? Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
        init => _item7 = value;
    }
    public object? Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
        init => _item8 = value;
    }

    // Constructors

    public ArgumentListG9(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
        _item6 = defaultValues[6];
        _item7 = defaultValues[7];
        _item8 = defaultValues[8];
    }

    public ArgumentListG9(ArgumentListType type, T0 item0, T1 item1, T2 item2, T3 item3, object? item4, object? item5, object? item6, object? item7, object? item8)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
        _item5 = item5;
        _item6 = item6;
        _item7 = item7;
        _item8 = item8;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListG9<T0, T1, T2, T3>(_type, Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8);

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
        sb.Append(", ");
        sb.Append(Item8 is CancellationToken ct8 ? ct8.Format() : Item8);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 },
            1 => new object?[] { Item0, Item2, Item3, Item4, Item5, Item6, Item7, Item8 },
            2 => new object?[] { Item0, Item1, Item3, Item4, Item5, Item6, Item7, Item8 },
            3 => new object?[] { Item0, Item1, Item2, Item4, Item5, Item6, Item7, Item8 },
            4 => new object?[] { Item0, Item1, Item2, Item3, Item5, Item6, Item7, Item8 },
            5 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item6, Item7, Item8 },
            6 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item7, Item8 },
            7 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item8 },
            8 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        Type? expectedItemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[6];
        if (!expectedItemType.IsValueType) {
            itemType = _item6?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[6] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[7];
        if (!expectedItemType.IsValueType) {
            itemType = _item7?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[7] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[8];
        if (!expectedItemType.IsValueType) {
            itemType = _item8?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[8] = itemType;
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
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            6 => _type.ItemTypes[6],
            7 => _type.ItemTypes[7],
            8 => _type.ItemTypes[8],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;
    public override T Get6<T>() => Item6 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get6Untyped() => Item6;
    public override T Get7<T>() => Item7 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get7Untyped() => Item7;
    public override T Get8<T>() => Item8 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get8Untyped() => Item8;

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
            8 => Item8 is T value ? value : default!,
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
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            8 => Item8,
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
            8 => Item8 is CancellationToken value ? value : default!,
            _ => default,
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        case 8:
            _item8 = _type.CastItem(8, item);
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        case 8:
            _item8 = _type.CastItem(8, item);
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
            return;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            return;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            return;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        case 6:
            _item6 = _type.CastItem(6, item);
            return;
        case 7:
            _item7 = _type.CastItem(7, item);
            return;
        case 8:
            _item8 = _type.CastItem(8, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG9<T0, T1, T2, T3> vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
            _item6 = vOther._item6;
            _item7 = vOther._item7;
            _item8 = vOther._item8;
        }
        else {
            _item0 = other.Get0<T0>();
            _item1 = other.Get1<T1>();
            _item2 = other.Get2<T2>();
            _item3 = other.Get3<T3>();
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
            _item6 = _type.CastItem(6, other.GetUntyped(6));
            _item7 = _type.CastItem(7, other.GetUntyped(7));
            _item8 = _type.CastItem(8, other.GetUntyped(8));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 9)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[8].ParameterType != type.ItemTypes[8])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item6")!.GetGetMethod()!);
                itemType = type.ItemTypes[6];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item7")!.GetGetMethod()!);
                itemType = type.ItemTypes[7];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item8")!.GetGetMethod()!);
                itemType = type.ItemTypes[8];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 9)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[8].ParameterType != type.ItemTypes[8])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item6"), type.ItemTypes[6])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item7"), type.ItemTypes[7])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item8"), type.ItemTypes[8])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnClass(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnClass(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnClass(typeof(T3), _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
        reader.OnAny(itemTypes[6], _item6, 6);
        reader.OnAny(itemTypes[7], _item7, 7);
        reader.OnAny(itemTypes[8], _item8, 8);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnClass(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnClass(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnClass(typeof(T3), 3)!;
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
        _item6 = writer.OnAny(itemTypes[6], 6, defaultValues[6]);
        _item7 = writer.OnAny(itemTypes[7], 7, defaultValues[7]);
        _item8 = writer.OnAny(itemTypes[8], 8, defaultValues[8]);
    }

    // Equality

    public bool Equals(ArgumentListG9<T0, T1, T2, T3>? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item8, other.Item8))
            return false;
        if (!Equals(Item7, other.Item7))
            return false;
        if (!Equals(Item6, other.Item6))
            return false;
        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
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
        if (other is not ArgumentListG9<T0, T1, T2, T3> vOther)
            return false;

        if (skipIndex != 8 && !Equals(Item8, vOther.Item8))
            return false;
        if (skipIndex != 7 && !Equals(Item7, vOther.Item7))
            return false;
        if (skipIndex != 6 && !Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
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
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (Item6 is { } item6 ? item6.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (Item7 is { } item7 ? item7.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 8) +
#else
            hashCode = 397*hashCode +
#endif
                (Item8 is { } item8 ? item8.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 6 ? 0 : (Item6 is { } item6 ? item6.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 7 ? 0 : (Item7 is { } item7 ? item7.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 8) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 8 ? 0 : (Item8 is { } item8 ? item8.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS9 : ArgumentList9
{
    private readonly ArgumentListType _type;

    private object? _item0;
    private object? _item1;
    private object? _item2;
    private object? _item3;
    private object? _item4;
    private object? _item5;
    private object? _item6;
    private object? _item7;
    private object? _item8;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public object? Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public object? Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public object? Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }
    public object? Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
        init => _item6 = value;
    }
    public object? Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
        init => _item7 = value;
    }
    public object? Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
        init => _item8 = value;
    }

    // Constructors

    public ArgumentListS9(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
        _item1 = defaultValues[1];
        _item2 = defaultValues[2];
        _item3 = defaultValues[3];
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
        _item6 = defaultValues[6];
        _item7 = defaultValues[7];
        _item8 = defaultValues[8];
    }

    public ArgumentListS9(ArgumentListType type, object? item0, object? item1, object? item2, object? item3, object? item4, object? item5, object? item6, object? item7, object? item8)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
        _item5 = item5;
        _item6 = item6;
        _item7 = item7;
        _item8 = item8;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListS9(_type, Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8);

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
        sb.Append(", ");
        sb.Append(Item8 is CancellationToken ct8 ? ct8.Format() : Item8);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 },
            1 => new object?[] { Item0, Item2, Item3, Item4, Item5, Item6, Item7, Item8 },
            2 => new object?[] { Item0, Item1, Item3, Item4, Item5, Item6, Item7, Item8 },
            3 => new object?[] { Item0, Item1, Item2, Item4, Item5, Item6, Item7, Item8 },
            4 => new object?[] { Item0, Item1, Item2, Item3, Item5, Item6, Item7, Item8 },
            5 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item6, Item7, Item8 },
            6 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item7, Item8 },
            7 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item8 },
            8 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[1];
        if (!expectedItemType.IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[2];
        if (!expectedItemType.IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[3];
        if (!expectedItemType.IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[6];
        if (!expectedItemType.IsValueType) {
            itemType = _item6?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[6] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[7];
        if (!expectedItemType.IsValueType) {
            itemType = _item7?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[7] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[8];
        if (!expectedItemType.IsValueType) {
            itemType = _item8?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[8] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            1 => _type.ItemTypes[1],
            2 => _type.ItemTypes[2],
            3 => _type.ItemTypes[3],
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            6 => _type.ItemTypes[6],
            7 => _type.ItemTypes[7],
            8 => _type.ItemTypes[8],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;
    public override T Get6<T>() => Item6 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get6Untyped() => Item6;
    public override T Get7<T>() => Item7 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get7Untyped() => Item7;
    public override T Get8<T>() => Item8 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get8Untyped() => Item8;

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
            8 => Item8 is T value ? value : default!,
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
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            8 => Item8,
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
            8 => Item8 is CancellationToken value ? value : default!,
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        case 8:
            _item8 = _type.CastItem(8, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        case 8:
            _item8 = _type.CastItem(8, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        case 1:
            _item1 = _type.CastItem(1, item);
            return;
        case 2:
            _item2 = _type.CastItem(2, item);
            return;
        case 3:
            _item3 = _type.CastItem(3, item);
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        case 6:
            _item6 = _type.CastItem(6, item);
            return;
        case 7:
            _item7 = _type.CastItem(7, item);
            return;
        case 8:
            _item8 = _type.CastItem(8, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS9 vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
            _item6 = vOther._item6;
            _item7 = vOther._item7;
            _item8 = vOther._item8;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
            _item1 = _type.CastItem(1, other.GetUntyped(1));
            _item2 = _type.CastItem(2, other.GetUntyped(2));
            _item3 = _type.CastItem(3, other.GetUntyped(3));
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
            _item6 = _type.CastItem(6, other.GetUntyped(6));
            _item7 = _type.CastItem(7, other.GetUntyped(7));
            _item8 = _type.CastItem(8, other.GetUntyped(8));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 9)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[8].ParameterType != type.ItemTypes[8])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                itemType = type.ItemTypes[1];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                itemType = type.ItemTypes[2];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                itemType = type.ItemTypes[3];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item6")!.GetGetMethod()!);
                itemType = type.ItemTypes[6];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item7")!.GetGetMethod()!);
                itemType = type.ItemTypes[7];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item8")!.GetGetMethod()!);
                itemType = type.ItemTypes[8];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 9)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[8].ParameterType != type.ItemTypes[8])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item1"), type.ItemTypes[1])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item2"), type.ItemTypes[2])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item3"), type.ItemTypes[3])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item6"), type.ItemTypes[6])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item7"), type.ItemTypes[7])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item8"), type.ItemTypes[8])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
        reader.OnAny(itemTypes[1], _item1, 1);
        reader.OnAny(itemTypes[2], _item2, 2);
        reader.OnAny(itemTypes[3], _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
        reader.OnAny(itemTypes[6], _item6, 6);
        reader.OnAny(itemTypes[7], _item7, 7);
        reader.OnAny(itemTypes[8], _item8, 8);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
        _item1 = writer.OnAny(itemTypes[1], 1, defaultValues[1]);
        _item2 = writer.OnAny(itemTypes[2], 2, defaultValues[2]);
        _item3 = writer.OnAny(itemTypes[3], 3, defaultValues[3]);
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
        _item6 = writer.OnAny(itemTypes[6], 6, defaultValues[6]);
        _item7 = writer.OnAny(itemTypes[7], 7, defaultValues[7]);
        _item8 = writer.OnAny(itemTypes[8], 8, defaultValues[8]);
    }

    // Equality

    public bool Equals(ArgumentListS9? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item8, other.Item8))
            return false;
        if (!Equals(Item7, other.Item7))
            return false;
        if (!Equals(Item6, other.Item6))
            return false;
        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
            return false;
        if (!Equals(Item3, other.Item3))
            return false;
        if (!Equals(Item2, other.Item2))
            return false;
        if (!Equals(Item1, other.Item1))
            return false;
        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS9 vOther)
            return false;

        if (skipIndex != 8 && !Equals(Item8, vOther.Item8))
            return false;
        if (skipIndex != 7 && !Equals(Item7, vOther.Item7))
            return false;
        if (skipIndex != 6 && !Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (Item6 is { } item6 ? item6.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (Item7 is { } item7 ? item7.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 8) +
#else
            hashCode = 397*hashCode +
#endif
                (Item8 is { } item8 ? item8.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 6 ? 0 : (Item6 is { } item6 ? item6.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 7 ? 0 : (Item7 is { } item7 ? item7.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 8) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 8 ? 0 : (Item8 is { } item8 ? item8.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public abstract record ArgumentList10 : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public override int Length => 10;

    protected static Type?[] CreateNonDefaultItemTypes()
        => new Type?[10];
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListG10<T0, T1, T2, T3> : ArgumentList10
{
    private readonly ArgumentListType _type;

    private T0 _item0;
    private T1 _item1;
    private T2 _item2;
    private T3 _item3;
    private object? _item4;
    private object? _item5;
    private object? _item6;
    private object? _item7;
    private object? _item8;
    private object? _item9;

    public override ArgumentListType Type => _type;

    public T0 Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public T1 Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public T2 Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public T3 Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }
    public object? Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
        init => _item6 = value;
    }
    public object? Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
        init => _item7 = value;
    }
    public object? Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
        init => _item8 = value;
    }
    public object? Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item9;
        init => _item9 = value;
    }

    // Constructors

    public ArgumentListG10(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = default!;
        _item1 = default!;
        _item2 = default!;
        _item3 = default!;
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
        _item6 = defaultValues[6];
        _item7 = defaultValues[7];
        _item8 = defaultValues[8];
        _item9 = defaultValues[9];
    }

    public ArgumentListG10(ArgumentListType type, T0 item0, T1 item1, T2 item2, T3 item3, object? item4, object? item5, object? item6, object? item7, object? item8, object? item9)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
        _item5 = item5;
        _item6 = item6;
        _item7 = item7;
        _item8 = item8;
        _item9 = item9;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListG10<T0, T1, T2, T3>(_type, Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9);

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
        sb.Append(", ");
        sb.Append(Item8 is CancellationToken ct8 ? ct8.Format() : Item8);
        sb.Append(", ");
        sb.Append(Item9 is CancellationToken ct9 ? ct9.Format() : Item9);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9 },
            1 => new object?[] { Item0, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9 },
            2 => new object?[] { Item0, Item1, Item3, Item4, Item5, Item6, Item7, Item8, Item9 },
            3 => new object?[] { Item0, Item1, Item2, Item4, Item5, Item6, Item7, Item8, Item9 },
            4 => new object?[] { Item0, Item1, Item2, Item3, Item5, Item6, Item7, Item8, Item9 },
            5 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item6, Item7, Item8, Item9 },
            6 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item7, Item8, Item9 },
            7 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item8, Item9 },
            8 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item9 },
            9 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        Type? expectedItemType;
        if (!typeof(T0).IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != typeof(T0)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        if (!typeof(T1).IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != typeof(T1)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        if (!typeof(T2).IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != typeof(T2)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        if (!typeof(T3).IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != typeof(T3)) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[6];
        if (!expectedItemType.IsValueType) {
            itemType = _item6?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[6] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[7];
        if (!expectedItemType.IsValueType) {
            itemType = _item7?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[7] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[8];
        if (!expectedItemType.IsValueType) {
            itemType = _item8?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[8] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[9];
        if (!expectedItemType.IsValueType) {
            itemType = _item9?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[9] = itemType;
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
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            6 => _type.ItemTypes[6],
            7 => _type.ItemTypes[7],
            8 => _type.ItemTypes[8],
            9 => _type.ItemTypes[9],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;
    public override T Get6<T>() => Item6 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get6Untyped() => Item6;
    public override T Get7<T>() => Item7 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get7Untyped() => Item7;
    public override T Get8<T>() => Item8 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get8Untyped() => Item8;
    public override T Get9<T>() => Item9 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get9Untyped() => Item9;

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
            8 => Item8 is T value ? value : default!,
            9 => Item9 is T value ? value : default!,
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
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            8 => Item8,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            9 => Item9,
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
            8 => Item8 is CancellationToken value ? value : default!,
            9 => Item9 is CancellationToken value ? value : default!,
            _ => default,
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        case 8:
            _item8 = _type.CastItem(8, item);
            break;
        case 9:
            _item9 = _type.CastItem(9, item);
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
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        case 8:
            _item8 = _type.CastItem(8, item);
            break;
        case 9:
            _item9 = _type.CastItem(9, item);
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
            return;
        case 1:
            _item1 = item is T1 item1 ? item1 : default!;
            return;
        case 2:
            _item2 = item is T2 item2 ? item2 : default!;
            return;
        case 3:
            _item3 = item is T3 item3 ? item3 : default!;
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        case 6:
            _item6 = _type.CastItem(6, item);
            return;
        case 7:
            _item7 = _type.CastItem(7, item);
            return;
        case 8:
            _item8 = _type.CastItem(8, item);
            return;
        case 9:
            _item9 = _type.CastItem(9, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListG10<T0, T1, T2, T3> vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
            _item6 = vOther._item6;
            _item7 = vOther._item7;
            _item8 = vOther._item8;
            _item9 = vOther._item9;
        }
        else {
            _item0 = other.Get0<T0>();
            _item1 = other.Get1<T1>();
            _item2 = other.Get2<T2>();
            _item3 = other.Get3<T3>();
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
            _item6 = _type.CastItem(6, other.GetUntyped(6));
            _item7 = _type.CastItem(7, other.GetUntyped(7));
            _item8 = _type.CastItem(8, other.GetUntyped(8));
            _item9 = _type.CastItem(9, other.GetUntyped(9));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 10)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[8].ParameterType != type.ItemTypes[8])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[9].ParameterType != type.ItemTypes[9])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item6")!.GetGetMethod()!);
                itemType = type.ItemTypes[6];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item7")!.GetGetMethod()!);
                itemType = type.ItemTypes[7];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item8")!.GetGetMethod()!);
                itemType = type.ItemTypes[8];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item9")!.GetGetMethod()!);
                itemType = type.ItemTypes[9];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 10)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[8].ParameterType != type.ItemTypes[8])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[9].ParameterType != type.ItemTypes[9])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
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
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item6"), type.ItemTypes[6])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item7"), type.ItemTypes[7])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item8"), type.ItemTypes[8])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item9"), type.ItemTypes[9])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        if (typeof(T0).IsValueType)
            reader.OnStruct(_item0, 0);
        else
            reader.OnClass(typeof(T0), _item0, 0);
        if (typeof(T1).IsValueType)
            reader.OnStruct(_item1, 1);
        else
            reader.OnClass(typeof(T1), _item1, 1);
        if (typeof(T2).IsValueType)
            reader.OnStruct(_item2, 2);
        else
            reader.OnClass(typeof(T2), _item2, 2);
        if (typeof(T3).IsValueType)
            reader.OnStruct(_item3, 3);
        else
            reader.OnClass(typeof(T3), _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
        reader.OnAny(itemTypes[6], _item6, 6);
        reader.OnAny(itemTypes[7], _item7, 7);
        reader.OnAny(itemTypes[8], _item8, 8);
        reader.OnAny(itemTypes[9], _item9, 9);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        if (typeof(T0).IsValueType)
            _item0 = writer.OnStruct<T0>(0);
        else
            _item0 = (T0)writer.OnClass(typeof(T0), 0)!;
        if (typeof(T1).IsValueType)
            _item1 = writer.OnStruct<T1>(1);
        else
            _item1 = (T1)writer.OnClass(typeof(T1), 1)!;
        if (typeof(T2).IsValueType)
            _item2 = writer.OnStruct<T2>(2);
        else
            _item2 = (T2)writer.OnClass(typeof(T2), 2)!;
        if (typeof(T3).IsValueType)
            _item3 = writer.OnStruct<T3>(3);
        else
            _item3 = (T3)writer.OnClass(typeof(T3), 3)!;
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
        _item6 = writer.OnAny(itemTypes[6], 6, defaultValues[6]);
        _item7 = writer.OnAny(itemTypes[7], 7, defaultValues[7]);
        _item8 = writer.OnAny(itemTypes[8], 8, defaultValues[8]);
        _item9 = writer.OnAny(itemTypes[9], 9, defaultValues[9]);
    }

    // Equality

    public bool Equals(ArgumentListG10<T0, T1, T2, T3>? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item9, other.Item9))
            return false;
        if (!Equals(Item8, other.Item8))
            return false;
        if (!Equals(Item7, other.Item7))
            return false;
        if (!Equals(Item6, other.Item6))
            return false;
        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
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
        if (other is not ArgumentListG10<T0, T1, T2, T3> vOther)
            return false;

        if (skipIndex != 9 && !Equals(Item9, vOther.Item9))
            return false;
        if (skipIndex != 8 && !Equals(Item8, vOther.Item8))
            return false;
        if (skipIndex != 7 && !Equals(Item7, vOther.Item7))
            return false;
        if (skipIndex != 6 && !Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
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
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (Item6 is { } item6 ? item6.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (Item7 is { } item7 ? item7.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 8) +
#else
            hashCode = 397*hashCode +
#endif
                (Item8 is { } item8 ? item8.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 21) +
#else
            hashCode = 397*hashCode +
#endif
                (Item9 is { } item9 ? item9.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 6 ? 0 : (Item6 is { } item6 ? item6.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 7 ? 0 : (Item7 is { } item7 ? item7.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 8) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 8 ? 0 : (Item8 is { } item8 ? item8.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 21) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 9 ? 0 : (Item9 is { } item9 ? item9.GetHashCode() : 0));
            return hashCode;
        }
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume ArgumentList code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ArgumentList code is preserved")]
public sealed record ArgumentListS10 : ArgumentList10
{
    private readonly ArgumentListType _type;

    private object? _item0;
    private object? _item1;
    private object? _item2;
    private object? _item3;
    private object? _item4;
    private object? _item5;
    private object? _item6;
    private object? _item7;
    private object? _item8;
    private object? _item9;

    public override ArgumentListType Type => _type;

    public object? Item0 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item0;
        init => _item0 = value;
    }
    public object? Item1 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item1;
        init => _item1 = value;
    }
    public object? Item2 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item2;
        init => _item2 = value;
    }
    public object? Item3 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item3;
        init => _item3 = value;
    }
    public object? Item4 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item4;
        init => _item4 = value;
    }
    public object? Item5 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item5;
        init => _item5 = value;
    }
    public object? Item6 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item6;
        init => _item6 = value;
    }
    public object? Item7 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item7;
        init => _item7 = value;
    }
    public object? Item8 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item8;
        init => _item8 = value;
    }
    public object? Item9 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item9;
        init => _item9 = value;
    }

    // Constructors

    public ArgumentListS10(ArgumentListType type)
    {
        _type = type;
        var defaultValues = type.DefaultValues;
        _item0 = defaultValues[0];
        _item1 = defaultValues[1];
        _item2 = defaultValues[2];
        _item3 = defaultValues[3];
        _item4 = defaultValues[4];
        _item5 = defaultValues[5];
        _item6 = defaultValues[6];
        _item7 = defaultValues[7];
        _item8 = defaultValues[8];
        _item9 = defaultValues[9];
    }

    public ArgumentListS10(ArgumentListType type, object? item0, object? item1, object? item2, object? item3, object? item4, object? item5, object? item6, object? item7, object? item8, object? item9)
    {
        _type = type;
        _item0 = item0;
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
        _item4 = item4;
        _item5 = item5;
        _item6 = item6;
        _item7 = item7;
        _item8 = item8;
        _item9 = item9;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListS10(_type, Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9);

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
        sb.Append(", ");
        sb.Append(Item8 is CancellationToken ct8 ? ct8.Format() : Item8);
        sb.Append(", ");
        sb.Append(Item9 is CancellationToken ct9 ? ct9.Format() : Item9);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
        => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9 };

    public override object?[] ToArray(int skipIndex)
        => skipIndex switch {
            0 => new object?[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9 },
            1 => new object?[] { Item0, Item2, Item3, Item4, Item5, Item6, Item7, Item8, Item9 },
            2 => new object?[] { Item0, Item1, Item3, Item4, Item5, Item6, Item7, Item8, Item9 },
            3 => new object?[] { Item0, Item1, Item2, Item4, Item5, Item6, Item7, Item8, Item9 },
            4 => new object?[] { Item0, Item1, Item2, Item3, Item5, Item6, Item7, Item8, Item9 },
            5 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item6, Item7, Item8, Item9 },
            6 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item7, Item8, Item9 },
            7 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item8, Item9 },
            8 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item9 },
            9 => new object?[] { Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 },
            _ => throw new ArgumentOutOfRangeException(nameof(skipIndex))
        };

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var itemTypes = (Type?[]?)null;
        Type? itemType;
        Type? expectedItemType;
        expectedItemType = _type.ItemTypes[0];
        if (!expectedItemType.IsValueType) {
            itemType = _item0?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[0] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[1];
        if (!expectedItemType.IsValueType) {
            itemType = _item1?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[1] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[2];
        if (!expectedItemType.IsValueType) {
            itemType = _item2?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[2] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[3];
        if (!expectedItemType.IsValueType) {
            itemType = _item3?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[3] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[4];
        if (!expectedItemType.IsValueType) {
            itemType = _item4?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[4] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[5];
        if (!expectedItemType.IsValueType) {
            itemType = _item5?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[5] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[6];
        if (!expectedItemType.IsValueType) {
            itemType = _item6?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[6] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[7];
        if (!expectedItemType.IsValueType) {
            itemType = _item7?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[7] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[8];
        if (!expectedItemType.IsValueType) {
            itemType = _item8?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[8] = itemType;
            }
        }
        expectedItemType = _type.ItemTypes[9];
        if (!expectedItemType.IsValueType) {
            itemType = _item9?.GetType();
            if (itemType is not null && itemType != expectedItemType) {
                itemTypes ??= CreateNonDefaultItemTypes();
                itemTypes[9] = itemType;
            }
        }
        return itemTypes;
    }

    // GetType

    public override Type? GetType(int index)
        => index switch {
            0 => _type.ItemTypes[0],
            1 => _type.ItemTypes[1],
            2 => _type.ItemTypes[2],
            3 => _type.ItemTypes[3],
            4 => _type.ItemTypes[4],
            5 => _type.ItemTypes[5],
            6 => _type.ItemTypes[6],
            7 => _type.ItemTypes[7],
            8 => _type.ItemTypes[8],
            9 => _type.ItemTypes[9],
            _ => null,
        };

    // Get

    public override T Get0<T>() => Item0 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get0Untyped() => Item0;
    public override T Get1<T>() => Item1 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get1Untyped() => Item1;
    public override T Get2<T>() => Item2 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get2Untyped() => Item2;
    public override T Get3<T>() => Item3 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get3Untyped() => Item3;
    public override T Get4<T>() => Item4 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get4Untyped() => Item4;
    public override T Get5<T>() => Item5 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get5Untyped() => Item5;
    public override T Get6<T>() => Item6 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get6Untyped() => Item6;
    public override T Get7<T>() => Item7 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get7Untyped() => Item7;
    public override T Get8<T>() => Item8 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get8Untyped() => Item8;
    public override T Get9<T>() => Item9 is T value ? value : default!;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    public override object? Get9Untyped() => Item9;

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
            8 => Item8 is T value ? value : default!,
            9 => Item9 is T value ? value : default!,
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
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            8 => Item8,
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            9 => Item9,
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
            8 => Item8 is CancellationToken value ? value : default!,
            9 => Item9 is CancellationToken value ? value : default!,
            _ => default,
        };

    // Set

    public override void Set<T>(int index, T item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        case 8:
            _item8 = _type.CastItem(8, item);
            break;
        case 9:
            _item9 = _type.CastItem(9, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetUntyped(int index, object? item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            break;
        case 1:
            _item1 = _type.CastItem(1, item);
            break;
        case 2:
            _item2 = _type.CastItem(2, item);
            break;
        case 3:
            _item3 = _type.CastItem(3, item);
            break;
        case 4:
            _item4 = _type.CastItem(4, item);
            break;
        case 5:
            _item5 = _type.CastItem(5, item);
            break;
        case 6:
            _item6 = _type.CastItem(6, item);
            break;
        case 7:
            _item7 = _type.CastItem(7, item);
            break;
        case 8:
            _item8 = _type.CastItem(8, item);
            break;
        case 9:
            _item9 = _type.CastItem(9, item);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        switch (index) {
        case 0:
            _item0 = _type.CastItem(0, item);
            return;
        case 1:
            _item1 = _type.CastItem(1, item);
            return;
        case 2:
            _item2 = _type.CastItem(2, item);
            return;
        case 3:
            _item3 = _type.CastItem(3, item);
            return;
        case 4:
            _item4 = _type.CastItem(4, item);
            return;
        case 5:
            _item5 = _type.CastItem(5, item);
            return;
        case 6:
            _item6 = _type.CastItem(6, item);
            return;
        case 7:
            _item7 = _type.CastItem(7, item);
            return;
        case 8:
            _item8 = _type.CastItem(8, item);
            return;
        case 9:
            _item9 = _type.CastItem(9, item);
            return;
        }
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListS10 vOther) {
            _item0 = vOther._item0;
            _item1 = vOther._item1;
            _item2 = vOther._item2;
            _item3 = vOther._item3;
            _item4 = vOther._item4;
            _item5 = vOther._item5;
            _item6 = vOther._item6;
            _item7 = vOther._item7;
            _item8 = vOther._item8;
            _item9 = vOther._item9;
        }
        else {
            _item0 = _type.CastItem(0, other.GetUntyped(0));
            _item1 = _type.CastItem(1, other.GetUntyped(1));
            _item2 = _type.CastItem(2, other.GetUntyped(2));
            _item3 = _type.CastItem(3, other.GetUntyped(3));
            _item4 = _type.CastItem(4, other.GetUntyped(4));
            _item5 = _type.CastItem(5, other.GetUntyped(5));
            _item6 = _type.CastItem(6, other.GetUntyped(6));
            _item7 = _type.CastItem(7, other.GetUntyped(7));
            _item8 = _type.CastItem(8, other.GetUntyped(8));
            _item9 = _type.CastItem(9, other.GetUntyped(9));
        }
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (_type, method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 10)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[8].ParameterType != type.ItemTypes[8])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[9].ParameterType != type.ItemTypes[9])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var itemType = (Type?)null;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    new [] { typeof(object), typeof(ArgumentList) },
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.DeclareLocal(type.ListType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, type.ListType);
                il.Emit(OpCodes.Stloc_0);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
                }

                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item0")!.GetGetMethod()!);
                itemType = type.ItemTypes[0];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item1")!.GetGetMethod()!);
                itemType = type.ItemTypes[1];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item2")!.GetGetMethod()!);
                itemType = type.ItemTypes[2];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item3")!.GetGetMethod()!);
                itemType = type.ItemTypes[3];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item4")!.GetGetMethod()!);
                itemType = type.ItemTypes[4];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item5")!.GetGetMethod()!);
                itemType = type.ItemTypes[5];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item6")!.GetGetMethod()!);
                itemType = type.ItemTypes[6];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item7")!.GetGetMethod()!);
                itemType = type.ItemTypes[7];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item8")!.GetGetMethod()!);
                itemType = type.ItemTypes[8];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Call, type.ListType.GetProperty("Item9")!.GetGetMethod()!);
                itemType = type.ItemTypes[9];
                il.Emit(itemType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, itemType);

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
                var (type, method1) = key;
                var parameters = method1.GetParameters();
                if (parameters.Length != 10)
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[0].ParameterType != type.ItemTypes[0])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[1].ParameterType != type.ItemTypes[1])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[2].ParameterType != type.ItemTypes[2])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[3].ParameterType != type.ItemTypes[3])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[4].ParameterType != type.ItemTypes[4])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[5].ParameterType != type.ItemTypes[5])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[6].ParameterType != type.ItemTypes[6])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[7].ParameterType != type.ItemTypes[7])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[8].ParameterType != type.ItemTypes[8])
                    throw new ArgumentOutOfRangeException(nameof(method));
                if (parameters[9].ParameterType != type.ItemTypes[9])
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(type.ListType, "l");
                var eBody = Expression.Block(
                    new [] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, type.ListType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(
                                method1.IsStatic
                                    ? null
                                    : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item0"), type.ItemTypes[0])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item1"), type.ItemTypes[1])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item2"), type.ItemTypes[2])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item3"), type.ItemTypes[3])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item4"), type.ItemTypes[4])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item5"), type.ItemTypes[5])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item6"), type.ItemTypes[6])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item7"), type.ItemTypes[7])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item8"), type.ItemTypes[8])
                                , Expression.Convert(Expression.PropertyOrField(vList, "Item9"), type.ItemTypes[9])
                                ))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    public override void Read(ArgumentListReader reader)
    {
        var itemTypes = _type.ItemTypes;
        reader.OnAny(itemTypes[0], _item0, 0);
        reader.OnAny(itemTypes[1], _item1, 1);
        reader.OnAny(itemTypes[2], _item2, 2);
        reader.OnAny(itemTypes[3], _item3, 3);
        reader.OnAny(itemTypes[4], _item4, 4);
        reader.OnAny(itemTypes[5], _item5, 5);
        reader.OnAny(itemTypes[6], _item6, 6);
        reader.OnAny(itemTypes[7], _item7, 7);
        reader.OnAny(itemTypes[8], _item8, 8);
        reader.OnAny(itemTypes[9], _item9, 9);
    }

    public override void Write(ArgumentListWriter writer)
    {
        var itemTypes = _type.ItemTypes;
        var defaultValues = _type.DefaultValues;
        _item0 = writer.OnAny(itemTypes[0], 0, defaultValues[0]);
        _item1 = writer.OnAny(itemTypes[1], 1, defaultValues[1]);
        _item2 = writer.OnAny(itemTypes[2], 2, defaultValues[2]);
        _item3 = writer.OnAny(itemTypes[3], 3, defaultValues[3]);
        _item4 = writer.OnAny(itemTypes[4], 4, defaultValues[4]);
        _item5 = writer.OnAny(itemTypes[5], 5, defaultValues[5]);
        _item6 = writer.OnAny(itemTypes[6], 6, defaultValues[6]);
        _item7 = writer.OnAny(itemTypes[7], 7, defaultValues[7]);
        _item8 = writer.OnAny(itemTypes[8], 8, defaultValues[8]);
        _item9 = writer.OnAny(itemTypes[9], 9, defaultValues[9]);
    }

    // Equality

    public bool Equals(ArgumentListS10? other)
    {
        if (other is null)
            return false;

        if (!Equals(Item9, other.Item9))
            return false;
        if (!Equals(Item8, other.Item8))
            return false;
        if (!Equals(Item7, other.Item7))
            return false;
        if (!Equals(Item6, other.Item6))
            return false;
        if (!Equals(Item5, other.Item5))
            return false;
        if (!Equals(Item4, other.Item4))
            return false;
        if (!Equals(Item3, other.Item3))
            return false;
        if (!Equals(Item2, other.Item2))
            return false;
        if (!Equals(Item1, other.Item1))
            return false;
        if (!Equals(Item0, other.Item0))
            return false;
        return true;
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListS10 vOther)
            return false;

        if (skipIndex != 9 && !Equals(Item9, vOther.Item9))
            return false;
        if (skipIndex != 8 && !Equals(Item8, vOther.Item8))
            return false;
        if (skipIndex != 7 && !Equals(Item7, vOther.Item7))
            return false;
        if (skipIndex != 6 && !Equals(Item6, vOther.Item6))
            return false;
        if (skipIndex != 5 && !Equals(Item5, vOther.Item5))
            return false;
        if (skipIndex != 4 && !Equals(Item4, vOther.Item4))
            return false;
        if (skipIndex != 3 && !Equals(Item3, vOther.Item3))
            return false;
        if (skipIndex != 2 && !Equals(Item2, vOther.Item2))
            return false;
        if (skipIndex != 1 && !Equals(Item1, vOther.Item1))
            return false;
        if (skipIndex != 0 && !Equals(Item0, vOther.Item0))
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        unchecked {
            var hashCode =
                (Item0 is { } item0 ? item0.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (Item1 is { } item1 ? item1.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (Item2 is { } item2 ? item2.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (Item3 is { } item3 ? item3.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (Item4 is { } item4 ? item4.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (Item5 is { } item5 ? item5.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (Item6 is { } item6 ? item6.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (Item7 is { } item7 ? item7.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 8) +
#else
            hashCode = 397*hashCode +
#endif
                (Item8 is { } item8 ? item8.GetHashCode() : 0);
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 21) +
#else
            hashCode = 397*hashCode +
#endif
                (Item9 is { } item9 ? item9.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            var hashCode =
                (skipIndex == 0 ? 0 : (Item0 is { } item0 ? item0.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 13) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 1 ? 0 : (Item1 is { } item1 ? item1.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 26) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 2 ? 0 : (Item2 is { } item2 ? item2.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 7) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 3 ? 0 : (Item3 is { } item3 ? item3.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 20) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 4 ? 0 : (Item4 is { } item4 ? item4.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 1) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 5 ? 0 : (Item5 is { } item5 ? item5.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 14) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 6 ? 0 : (Item6 is { } item6 ? item6.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 27) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 7 ? 0 : (Item7 is { } item7 ? item7.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 8) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 8 ? 0 : (Item8 is { } item8 ? item8.GetHashCode() : 0));
#if NETCOREAPP3_1_OR_GREATER
            hashCode = (int)BitOperations.RotateLeft((uint)hashCode, 21) +
#else
            hashCode = 397*hashCode +
#endif
                (skipIndex == 9 ? 0 : (Item9 is { } item9 ? item9.GetHashCode() : 0));
            return hashCode;
        }
    }
}
