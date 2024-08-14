using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection.Emit;
using ActualLab.Internal;

namespace ActualLab.Interception;

public abstract partial record ArgumentList
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public abstract int Length { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArgumentList New()
        => Empty;
    public static ArgumentListPair NewPair(Type listType, ArgumentListNative head, ArgumentList tail)
        => (ArgumentListPair)listType.CreateInstance((head, tail));

    public abstract ArgumentList Duplicate();

    public virtual object?[] ToArray() => [];
    public virtual object?[] ToArray(int skipIndex) => [];

    public virtual Type?[]? GetNonDefaultItemTypes()
        => null;

    public virtual Type? GetType(int index)
        => null;
    public virtual T Get<T>(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));
    public virtual object? GetUntyped(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    // Virtual non-generic method for frequent operation
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual CancellationToken GetCancellationToken(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public virtual void Set<T>(int index, T value)
         => throw new ArgumentOutOfRangeException(nameof(index));
    public virtual void SetUntyped(int index, object? value)
         => throw new ArgumentOutOfRangeException(nameof(index));

    // Virtual non-generic method for frequent operation
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void SetCancellationToken(int index, CancellationToken item)
         => throw new ArgumentOutOfRangeException(nameof(index));

    public virtual void SetFrom(ArgumentList other)
    { }

    public virtual ArgumentList Insert<T>(int index, T item)
        => index == 0
            ? New(item)
            : throw new ArgumentOutOfRangeException(nameof(index));

    // Virtual non-generic method for frequent operation
    public virtual ArgumentList InsertCancellationToken(int index, CancellationToken item)
        => index == 0
            ? New(item)
            : throw new ArgumentOutOfRangeException(nameof(index));

    public ArgumentList InsertUntyped(int index, Type itemType, object value)
    {
        var func = InsertUntypedCache.GetOrAdd(
            itemType,
            static key => (Func<ArgumentList, int, object?, ArgumentList>)InsertUntypedImplMethod
                .MakeGenericMethod(key)
                .CreateDelegate(typeof(Func<ArgumentList, int, object?, ArgumentList>))
            );
        return func.Invoke(this, index, value);
    }

    public virtual ArgumentList Remove(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public abstract Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void Read(ArgumentListReader reader);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void Read(ArgumentListReader reader, int offset);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void Write(ArgumentListWriter writer);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void Write(ArgumentListWriter writer, int offset);

    // Equality

    public abstract bool Equals(ArgumentList? other, int skipIndex);
    public abstract int GetHashCode(int skipIndex);
}

public abstract record ArgumentListNative : ArgumentList;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ArgumentList0 : ArgumentListNative
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember]
    public override int Length => 0;

    public override string ToString() => "()";

    public override ArgumentList Duplicate()
        => new ArgumentList0();

    public override Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => InvokerCache.GetOrAdd(
            (GetType(), method),
            RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? static key => { // Dynamic methods
                var (listType, method1) = key;
                if (method1.GetParameters().Length != 0)
                    throw new ArgumentOutOfRangeException(nameof(method));

                var declaringType = method1.DeclaringType!;
                var m = new DynamicMethod("_Invoke",
                    typeof(object),
                    [typeof(object), typeof(ArgumentList)],
                    true);
                var il = m.GetILGenerator();

                // Cast ArgumentList to its actual type
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, listType);
                il.Emit(OpCodes.Pop);

                // Unbox target
                if (!method1.IsStatic) {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(declaringType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, declaringType);
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
            : static key => { // Expressions
                var (listType, method1) = key;
                if (method1.GetParameters().Length != 0)
                    throw new ArgumentOutOfRangeException(nameof(method));

                var pSource = Expression.Parameter(typeof(object), "source");
                var pList = Expression.Parameter(typeof(ArgumentList), "list");
                var vList = Expression.Variable(listType, "l");
                var eBody = Expression.Block(
                    new[] { vList },
                    [
                        Expression.Assign(vList, Expression.Convert(pList, listType)),
                        ExpressionExt.ConvertToObject(
                            Expression.Call(method1.IsStatic
                                ? null
                                : ExpressionExt.MaybeConvert(pSource, method1.DeclaringType!),
                                method1))
                    ]);
                return (Func<object?, ArgumentList, object?>)Expression
                    .Lambda(eBody, pSource, pList)
                    .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
            }
        );

    // Read & Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader)
    { }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Read(ArgumentListReader reader, int offset)
    { }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer)
    { }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(ArgumentListWriter writer, int offset)
    { }

    // Equality

    public bool Equals(ArgumentList0? other)
        => !ReferenceEquals(other, null);
    public override bool Equals(ArgumentList? other, int skipIndex)
        => other?.GetType() == typeof(ArgumentList0);

    public override int GetHashCode() => 1;
    public override int GetHashCode(int skipIndex) => 1;
}
