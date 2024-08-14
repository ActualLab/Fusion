using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection.Emit;
using ActualLab.Internal;

namespace ActualLab.Interception;

public abstract record ArgumentListPair : ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public abstract ArgumentListNative UntypedHead { get; }
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public abstract ArgumentList UntypedTail { get; }

    public void Deconstruct(out ArgumentListNative untypedHead, out ArgumentList untypedTail)
    {
        untypedHead = UntypedHead;
        untypedTail = UntypedTail;
    }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentListPair<THead, TTail> : ArgumentListPair
    where THead : ArgumentListNative, new() // And it's always full, i.e. contains NativeTypeCount items
    where TTail : ArgumentList, new()
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember]
    public override int Length => Head.Length + Tail.Length;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public THead Head { get; init; }
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public TTail Tail { get; init; }

    // Computed properties
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember]
    public override ArgumentListNative UntypedHead => Head;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember]
    public override ArgumentList UntypedTail => Tail;

    // Constructors

    public ArgumentListPair()
    {
        Head = new();
        Tail = new();
    }

    public ArgumentListPair((ArgumentListNative Head, ArgumentList Tail) pair)
    {
        Head = (THead)pair.Head;
        Tail = (TTail)pair.Tail;
    }

    [method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public ArgumentListPair(THead Head, TTail Tail)
    {
        this.Head = Head;
        this.Tail = Tail;
    }

    // Duplicate

    public override ArgumentList Duplicate()
        => new ArgumentListPair<THead, TTail>((THead)Head.Duplicate(), (TTail)Tail.Duplicate());

    // ToString & ToArray

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append('(');
        sb.Append(Head.ToString()[1..^1]);
        if (sb.Length > 1)
            sb.Append(", ");
        sb.Append(Tail.ToString()[1..^1]);
        sb.Append(')');
        return sb.ToStringAndRelease();
    }

    public override object?[] ToArray()
    {
        var result = new object?[Length];
        for (var i = 0; i < result.Length; i++)
            result[i] = GetUntyped(i);
        return result;
    }

    public override object?[] ToArray(int skipIndex)
    {
        var result = new object?[Length - 1];
        if (skipIndex < 0 || skipIndex >= result.Length)
            throw new ArgumentOutOfRangeException(nameof(skipIndex));

        for (int i = 0, j = 0; i < result.Length; i++, j++) {
            if (j == skipIndex)
                j++;
            result[i] = GetUntyped(j);
        }
        return result;
    }

    // GetNonDefaultItemTypes

    public override Type?[]? GetNonDefaultItemTypes()
    {
        var r0 = Head.GetNonDefaultItemTypes();
        var r1 = Tail.GetNonDefaultItemTypes();
        if (r0 == null && r1 == null)
            return null;

        var result = new Type?[Length];
        r0?.CopyTo(result, 0);
        r1?.CopyTo(result, Head.Length);
        return result;
    }

    // GetType

    public override Type? GetType(int index)
        => index < Head.Length
            ? Head.GetType(index)
            : Tail.GetType(index - Head.Length);

    // Get

    public override T Get<T>(int index)
        => index < Head.Length
            ? Head.Get<T>(index)
            : Tail.Get<T>(index - Head.Length);

    public override object? GetUntyped(int index)
        => index < Head.Length
            ? Head.GetUntyped(index)
            : Tail.GetUntyped(index - Head.Length);

    public override CancellationToken GetCancellationToken(int index)
        => index < Head.Length
            ? Head.GetCancellationToken(index)
            : Tail.GetCancellationToken(index - Head.Length);

    // Set

    public override void Set<T>(int index, T item)
    {
        if (index < Head.Length)
            Head.Set(index, item);
        else
            Tail.Set(index - Head.Length, item);
    }

    public override void SetUntyped(int index, object? item)
    {
        if (index < Head.Length)
            Head.SetUntyped(index, item);
        else
            Tail.SetUntyped(index - Head.Length, item);
    }

    public override void SetCancellationToken(int index, CancellationToken item)
    {
        if (index < Head.Length)
            Head.SetCancellationToken(index, item);
        else
            Tail.SetCancellationToken(index - Head.Length, item);
    }

    // SetFrom

    public override void SetFrom(ArgumentList other)
    {
        if (other is ArgumentListPair<THead, TTail> vOther) {
            Head.SetFrom(vOther.Head);
            Tail.SetFrom(vOther.Tail);
        }
        else {
            var length = Length;
            for (var i = 0; i < length; i++)
                SetUntyped(i, other.GetUntyped(i));
        }
    }

    // Insert

    public override ArgumentList Insert<T>(int index, T item)
    {
        var tList = GetLongerListType(GetType(), index, typeof(T));
        ArgumentList tail;
        if (index >= Head.Length) {
            tail = Tail.Insert(index - Head.Length, item);
            return NewPair(tList, Head, tail);
        }

        var headLength = Head.Length;
        var head = Head.Insert(index, item);
        var tOverflowItem = head.GetType(headLength)!;
        var overflowItem = head.GetUntyped(headLength)!;
        head = head.Remove(headLength);
        tail = Tail.InsertUntyped(0, tOverflowItem, overflowItem);
        return NewPair(tList, (ArgumentListNative)head, tail);
    }

    public override ArgumentList InsertCancellationToken(int index, CancellationToken item)
    {
        var tList = GetLongerListType(GetType(), index, typeof(CancellationToken));
        ArgumentList tail;
        if (index >= Head.Length) {
            tail = Tail.Insert(index - Head.Length, item);
            return NewPair(tList, Head, tail);
        }

        var headLength = Head.Length;
        var head = Head.Insert(index, item);
        var tOverflowItem = head.GetType(headLength)!;
        var overflowItem = head.GetUntyped(headLength)!;
        head = head.Remove(headLength);
        tail = Tail.InsertUntyped(0, tOverflowItem, overflowItem);
        return NewPair(tList, (ArgumentListNative)head, tail);
    }

    // Remove

    public override ArgumentList Remove(int index)
    {
        var tList = GetShorterListType(GetType(), index);
        ArgumentList tail;
        if (index >= Head.Length) {
            tail = Tail.Remove(index - Head.Length);
            return tail.Length == 0
                ? Head
                : NewPair(tList, Head, tail);
        }

        var tOverflowItem = Tail.GetType(0)!;
        var overflowItem = Tail.GetUntyped(0)!;
        var headLength = Head.Length;
        var head = Head.Remove(index).InsertUntyped(headLength - 1, tOverflowItem, overflowItem);
        tail = Tail.Remove(0);
        return tail.Length == 0
            ? head
            : NewPair(tList, (ArgumentListNative)head, tail);
    }

    // GetInvoker

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
                ? static key => { // Dynamic methods
                    var (listType, method1) = key;
                    var parameters = method1.GetParameters();
                    var itemTypes = GetItemTypes(listType);
                    if (parameters.Length != itemTypes.Length)
                        throw new ArgumentOutOfRangeException(nameof(method));
                    for (var i = 0; i < parameters.Length; i++)
                        if (parameters[i].ParameterType != itemTypes[i])
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

                    var mGet = listType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public)!;
                    for (var i = 0; i < itemTypes.Length; i++) {
                        il.Emit(OpCodes.Ldloc_0);
                        il.Emit(OpCodes.Ldc_I4, i);
                        il.Emit(OpCodes.Call, mGet.MakeGenericMethod(itemTypes[i]));
                    }

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
                    var itemTypes = GetItemTypes(listType);
                    if (parameters.Length != itemTypes.Length)
                        throw new ArgumentOutOfRangeException(nameof(method));
                    for (var i = 0; i < parameters.Length; i++)
                        if (parameters[i].ParameterType != itemTypes[i])
                            throw new ArgumentOutOfRangeException(nameof(method));

                    var pSource = Expression.Parameter(typeof(object), "source");
                    var pList = Expression.Parameter(typeof(ArgumentList), "list");
                    var vList = Expression.Variable(listType, "l");
                    var mGet = listType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public)!;
                    var eBody = Expression.Block(
                        new [] { vList },
                        [
                            Expression.Assign(vList, Expression.Convert(pList, listType)),
                            ExpressionExt.ConvertToObject(
                                Expression.Call(
                                    method1.IsStatic
                                        ? null
                                        : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                    method1,
                                    Enumerable.Range(0, itemTypes.Length)
                                        .Select(i => (Expression)Expression.Call(
                                            vList,
                                            mGet.MakeGenericMethod(itemTypes[i]),
                                            Expression.Constant(i)))
                                        .ToArray()
                                ))
                        ]);
                    return (Func<object?, ArgumentList, object?>)Expression
                        .Lambda(eBody, pSource, pList)
                        .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
                }
        );

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    {
        Head.Read(reader);
        Tail.Read(reader, Head.Length);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    {
        Head.Read(reader, offset);
        Tail.Read(reader, offset + Head.Length);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    {
        Head.Write(writer);
        Tail.Write(writer, Head.Length);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    {
        Head.Write(writer, offset);
        Tail.Write(writer, offset + Head.Length);
    }

    // Equality

    public bool Equals(ArgumentListPair<THead, TTail>? other)
    {
        if (other == null)
            return false;

        return Head.Equals(other.Head) && Tail.Equals(other.Tail);
    }

    public override bool Equals(ArgumentList? other, int skipIndex)
    {
        if (other is not ArgumentListPair<THead, TTail> vOther)
            return false;

        return skipIndex < Head.Length
            ? Head.Equals(vOther.Head, skipIndex) && Tail.Equals(vOther.Tail)
            : Head.Equals(vOther.Head) && Tail.Equals(vOther.Tail, skipIndex);
    }

    public override int GetHashCode()
        => (397 * Head.GetHashCode()) + Tail.GetHashCode();

    public override int GetHashCode(int skipIndex)
    {
        unchecked {
            return skipIndex < Head.Length
                ? (397 * Head.GetHashCode(skipIndex)) + Tail.GetHashCode()
                : (397 * Head.GetHashCode()) + Tail.GetHashCode(skipIndex);
        }
    }
}
