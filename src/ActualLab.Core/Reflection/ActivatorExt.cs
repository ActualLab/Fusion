using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection.Emit;
using ActualLab.Internal;

namespace ActualLab.Reflection;

[UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume all used constructors are preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume all used constructors are preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "We assume all used constructors are preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume all used constructors are preserved")]
public static class ActivatorExt
{
    private static readonly ConcurrentDictionary<object, bool> HasDefaultCtorCache = new();
    private static readonly ConcurrentDictionary<object, object?> CtorDelegate0Cache = new();
    private static readonly ConcurrentDictionary<(object, object), object?> CtorDelegate1Cache = new();
    private static readonly ConcurrentDictionary<(object, object, object), object?> CtorDelegate2Cache = new();
    private static readonly ConcurrentDictionary<(object, object, object, object), object?> CtorDelegate3Cache = new();
    private static readonly ConcurrentDictionary<(object, object, object, object, object), object?> CtorDelegate4Cache = new();
    private static readonly ConcurrentDictionary<(object, object, object, object, object, object), object?> CtorDelegate5Cache = new();

    // An alternative to "new()" constraint
    public static T New<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        bool failIfNoDefaultConstructor = true)
    {
        var type = typeof(T);
        if (type.IsValueType)
            return default!;
        var hasDefaultCtor = HasDefaultCtorCache.GetOrAdd(type,
            key => ((Type)key).GetConstructor(Type.EmptyTypes) is not null);
        if (hasDefaultCtor)
            return (T)type.CreateInstance();
        if (failIfNoDefaultConstructor)
            throw Errors.NoDefaultConstructor(type);
        return default!;
    }

    public static object? GetConstructorDelegate(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] this Type type)
        => CtorDelegate0Cache.GetOrAdd(
            type,
            static key => {
                var tObject = (Type)key;
                var argTypes = Type.EmptyTypes;
                return CreateConstructorDelegate(tObject.GetConstructor(argTypes), argTypes);
            });

    public static object? GetConstructorDelegate(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        Type argument1)
        => CtorDelegate1Cache.GetOrAdd(
            (type, argument1),
            static key => {
                var tObject = (Type)key.Item1;
                var tArg1 = (Type)key.Item2;
                var argTypes = new[] { tArg1 };
                return CreateConstructorDelegate(tObject.GetConstructor(argTypes), argTypes);
            });

    public static object? GetConstructorDelegate(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        Type argument1, Type argument2)
        => CtorDelegate2Cache.GetOrAdd(
            (type, argument1, argument2),
            static key => {
                var tObject = (Type)key.Item1;
                var tArg1 = (Type)key.Item2;
                var tArg2 = (Type)key.Item3;
                var argTypes = new[] { tArg1, tArg2 };
                return CreateConstructorDelegate(tObject.GetConstructor(argTypes), argTypes);
            });

    public static object? GetConstructorDelegate(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        Type argument1, Type argument2, Type argument3)
        => CtorDelegate3Cache.GetOrAdd(
            (type, argument1, argument2, argument3),
            static key => {
                var tObject = (Type)key.Item1;
                var tArg1 = (Type)key.Item2;
                var tArg2 = (Type)key.Item3;
                var tArg3 = (Type)key.Item4;
                var argTypes = new[] { tArg1, tArg2, tArg3 };
                return CreateConstructorDelegate(tObject.GetConstructor(argTypes), argTypes);
            });

    public static object? GetConstructorDelegate(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        Type argument1, Type argument2, Type argument3, Type argument4)
        => CtorDelegate4Cache.GetOrAdd(
            (type, argument1, argument2, argument3, argument4),
            static key => {
                var tObject = (Type)key.Item1;
                var tArg1 = (Type)key.Item2;
                var tArg2 = (Type)key.Item3;
                var tArg3 = (Type)key.Item4;
                var tArg4 = (Type)key.Item5;
                var argTypes = new[] { tArg1, tArg2, tArg3, tArg4 };
                return CreateConstructorDelegate(tObject.GetConstructor(argTypes), argTypes);
            });

    public static object? GetConstructorDelegate(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        Type argument1, Type argument2, Type argument3, Type argument4, Type argument5)
        => CtorDelegate5Cache.GetOrAdd(
            (type, argument1, argument2, argument3, argument4, argument5),
            static key => {
                var tObject = (Type)key.Item1;
                var tArg1 = (Type)key.Item2;
                var tArg2 = (Type)key.Item3;
                var tArg3 = (Type)key.Item4;
                var tArg4 = (Type)key.Item5;
                var tArg5 = (Type)key.Item6;
                var argTypes = new[] { tArg1, tArg2, tArg3, tArg4, tArg5 };
                return CreateConstructorDelegate(tObject.GetConstructor(argTypes), argTypes);
            });

    public static object CreateInstance(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type)
    {
        var ctor = (Func<object>)type.GetConstructorDelegate()!;
        return ctor.Invoke();
    }

    public static object CreateInstance<T1>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        T1 argument1)
    {
        var ctor = (Func<T1, object>)type.GetConstructorDelegate(typeof(T1))!;
        return ctor.Invoke(argument1);
    }

    public static object CreateInstance<T1, T2>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        T1 argument1, T2 argument2)
    {
        var ctor = (Func<T1, T2, object>)type.GetConstructorDelegate(typeof(T1), typeof(T2))!;
        return ctor.Invoke(argument1, argument2);
    }

    public static object CreateInstance<T1, T2, T3>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        T1 argument1, T2 argument2, T3 argument3)
    {
        var ctor = (Func<T1, T2, T3, object>)type.GetConstructorDelegate(typeof(T1), typeof(T2), typeof(T3))!;
        return ctor.Invoke(argument1, argument2, argument3);
    }

    public static object CreateInstance<T1, T2, T3, T4>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        T1 argument1, T2 argument2, T3 argument3, T4 argument4)
    {
        var ctor = (Func<T1, T2, T3, T4, object>)type.GetConstructorDelegate(typeof(T1), typeof(T2), typeof(T3), typeof(T4))!;
        return ctor.Invoke(argument1, argument2, argument3, argument4);
    }

    public static object CreateInstance<T1, T2, T3, T4, T5>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type,
        T1 argument1, T2 argument2, T3 argument3, T4 argument4, T5 argument5)
    {
        var ctor = (Func<T1, T2, T3, T4, T5, object>)type.GetConstructorDelegate(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5))!;
        return ctor.Invoke(argument1, argument2, argument3, argument4, argument5);
    }

    public static Delegate? CreateConstructorDelegate(ConstructorInfo? ctor, params Type[] argumentTypes)
        =>  ctor is null
            ? null
            : RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
                ? CreateConstructorDelegateDM(ctor, argumentTypes)
                : CreateConstructorDelegateET(ctor, argumentTypes);

    // Private methods

    private static Delegate CreateConstructorDelegateDM(ConstructorInfo ctor, params Type[] argumentTypes)
    {
        var ctorParams = ctor.GetParameters();
        if (ctorParams.Length != argumentTypes.Length)
            throw new ArgumentOutOfRangeException(nameof(argumentTypes),
                "Count of arguments should match the count of constructor paramters.");

        var m = new DynamicMethod("_Ctor", ctor.DeclaringType, argumentTypes, true);
        var il = m.GetILGenerator();
        for (var i = 0; i < argumentTypes.Length; i++) {
            il.Emit(OpCodes.Ldarg, i);
            il.MaybeEmitCast(argumentTypes[i], ctorParams[i].ParameterType);
        }
        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Ret);
        var tDelegate = FuncExt.GetFuncType(argumentTypes, ctor.DeclaringType!);
        return m.CreateDelegate(tDelegate);
    }

    private static Delegate CreateConstructorDelegateET(ConstructorInfo ctor, params Type[] argumentTypes)
    {
        var ctorParams = ctor.GetParameters();
        if (ctorParams.Length != argumentTypes.Length)
            throw new ArgumentOutOfRangeException(nameof(argumentTypes),
                "Count of arguments should match the count of constructor paramters.");

        var parameters = new ParameterExpression[argumentTypes.Length];
        var callParameters = new Expression[argumentTypes.Length];
        for (var i = 0; i < argumentTypes.Length; i++) {
            parameters[i] = Expression.Parameter(argumentTypes[i]);
            callParameters[i] = ExpressionExt.MaybeConvert(parameters[i], ctorParams[i].ParameterType);
        }
        return Expression
            // ReSharper disable once CoVariantArrayConversion
            .Lambda(Expression.New(ctor, callParameters), parameters)
            .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
    }
}
