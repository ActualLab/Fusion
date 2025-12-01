using System.Linq.Expressions;
using System.Reflection.Emit;
using ActualLab.Internal;
using ActualLab.OS;

namespace ActualLab.Reflection;

[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume all used fields, getters, and setters are preserved")]
public static class MemberInfoExt
{
    private static readonly ConcurrentDictionary<(object, object), object> GetterCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(object, object), object> SetterCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(object, object), object> UntypedGetterCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(object, object), object> UntypedSetterCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static Type ReturnType(this MemberInfo memberInfo)
        => memberInfo switch {
            PropertyInfo propertyInfo => propertyInfo.PropertyType,
            FieldInfo fieldInfo => fieldInfo.FieldType,
            MethodInfo methodInfo => methodInfo.ReturnType,
            ConstructorInfo constructorInfo => constructorInfo.DeclaringType!,
            _ => throw Errors.UnexpectedMemberType(memberInfo.ToString()!)
        };

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static Func<TType, TValue> GetGetter<TType, TValue>(
        this MemberInfo propertyOrField, bool isValueUntyped = false)
        => (Func<TType, TValue>)GetGetter(propertyOrField, typeof(TType), isValueUntyped);

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static Func<object?, TValue> GetGetter<TValue>(
        this MemberInfo propertyOrField, bool isValueUntyped = false)
        => (Func<object?, TValue>)GetGetter(propertyOrField, typeof(object), isValueUntyped);

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static Func<object?, object?> GetGetter(this MemberInfo propertyOrField)
        => (Func<object?, object?>)GetGetter(propertyOrField, typeof(object), true);

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static Action<TType, TValue> GetSetter<TType, TValue>(
        this MemberInfo propertyOrField, bool isValueUntyped = false)
        => (Action<TType, TValue>)GetSetter(propertyOrField, typeof(TType), isValueUntyped);

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static Action<object?, TValue> GetSetter<TValue>(
        this MemberInfo propertyOrField, bool isValueUntyped = false)
        => (Action<object?, TValue>)GetSetter(propertyOrField, typeof(object), isValueUntyped);

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static Action<object?, object?> GetSetter(this MemberInfo propertyOrField)
        => (Action<object?, object?>)GetSetter(propertyOrField, typeof(object), true);

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static object GetGetter(this MemberInfo propertyOrField, Type sourceType, bool isValueUntyped = false)
    {
        var key = (propertyOrField, sourceType);
        return isValueUntyped
            ? UntypedGetterCache.GetOrAdd(key, static key1 => {
                var propertyOrField = (MemberInfo)key1.Item1;
                var sourceType = (Type)key1.Item2;
                var valueType = typeof(object);
                return CreateGetter(sourceType, propertyOrField, valueType);
            })
            : GetterCache.GetOrAdd(key, static key1 => {
                var propertyOrField = (MemberInfo)key1.Item1;
                var sourceType = (Type)key1.Item2;
                var valueType = propertyOrField.ReturnType();
                return CreateGetter(sourceType, propertyOrField, valueType);
            });
    }

    public static object GetSetter(this MemberInfo propertyOrField, Type sourceType, bool isValueUntyped = false)
    {
        var key = (propertyOrField, sourceType);
        return isValueUntyped
            ? UntypedSetterCache.GetOrAdd(key, static key1 => {
                var propertyOrField = (MemberInfo)key1.Item1;
                var sourceType = (Type)key1.Item2;
                var valueType = typeof(object);
                return CreateSetter(sourceType, propertyOrField, valueType);
            })
            : SetterCache.GetOrAdd(key, static key1 => {
                var propertyOrField = (MemberInfo)key1.Item1;
                var sourceType = (Type)key1.Item2;
                var valueType = propertyOrField.ReturnType();
                return CreateSetter(sourceType, propertyOrField, valueType);
            });
    }

    // Private methods

    private static Delegate CreateSetter(Type sourceType, MemberInfo propertyOrField, Type valueType)
        => RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? CreateSetterDM(sourceType, propertyOrField, valueType)
            : CreateSetterET(sourceType, propertyOrField, valueType);

    private static Delegate CreateGetter(Type sourceType, MemberInfo propertyOrField, Type valueType)
        => RuntimeCodegen.Mode == RuntimeCodegenMode.DynamicMethods
            ? CreateGetterDM(sourceType, propertyOrField, valueType)
            : CreateGetterET(sourceType, propertyOrField, valueType);

    // Dynamic methods-based codegen

    private static Delegate CreateGetterDM(
        Type sourceType,
        MemberInfo propertyOrField,
        Type valueType)
    {
        var funcType = typeof(Func<,>).MakeGenericType(sourceType, valueType);
        var declaringType = propertyOrField.DeclaringType!;
        DynamicMethod m;
        ILGenerator il;
        if (propertyOrField is PropertyInfo pi) {
            var isStatic = pi.GetMethod!.IsStatic;
            if (!isStatic && declaringType.IsAssignableFrom(sourceType) && valueType == pi.PropertyType)
                return pi.GetMethod!.CreateDelegate(funcType);

            m = new DynamicMethod("_Getter", valueType, [sourceType], true);
            il = m.GetILGenerator();
            if (isStatic)
                il.Emit(OpCodes.Call, pi.GetMethod!);
            else {
                il.Emit(OpCodes.Ldarg_0);
                il.MaybeEmitCast(sourceType, declaringType);
                il.Emit(OpCodes.Callvirt, pi.GetMethod!);
            }
        }
        else if (propertyOrField is FieldInfo fi) {
            m = new DynamicMethod("_Getter", valueType, [sourceType], true);
            il = m.GetILGenerator();
            if (fi.IsStatic)
                il.Emit(OpCodes.Ldsfld, fi);
            else {
                il.Emit(OpCodes.Ldarg_0);
                il.MaybeEmitCast(sourceType, declaringType);
                il.Emit(OpCodes.Ldfld, fi);
            }
        }
        else
            throw new ArgumentOutOfRangeException(nameof(propertyOrField));

        il.MaybeEmitCast(propertyOrField.ReturnType(), valueType);
        il.Emit(OpCodes.Ret);
        return m.CreateDelegate(funcType);
    }

    private static Delegate CreateSetterDM(
        Type sourceType,
        MemberInfo propertyOrField,
        Type valueType)
    {
        var funcType = typeof(Action<,>).MakeGenericType(sourceType, valueType);
        var declaringType = propertyOrField.DeclaringType!;
        DynamicMethod m;
        ILGenerator il;
        if (propertyOrField is PropertyInfo pi) {
            var isStatic = pi.SetMethod!.IsStatic;
            if (!isStatic && declaringType.IsAssignableFrom(sourceType) && valueType == pi.PropertyType)
                return pi.SetMethod!.CreateDelegate(funcType);

            m = new DynamicMethod("_Setter", null, [sourceType, valueType], true);
            il = m.GetILGenerator();
            if (isStatic) {
                il.Emit(OpCodes.Ldarg_1);
                il.MaybeEmitCast(valueType, propertyOrField.ReturnType());
                il.Emit(OpCodes.Call, pi.SetMethod!);
            }
            else {
                il.Emit(OpCodes.Ldarg_0);
                il.MaybeEmitCast(sourceType, declaringType);
                il.Emit(OpCodes.Ldarg_1);
                il.MaybeEmitCast(valueType, propertyOrField.ReturnType());
                il.Emit(OpCodes.Callvirt, pi.SetMethod!);
            }
        }
        else if (propertyOrField is FieldInfo fi) {
            m = new DynamicMethod("_Setter", null, [sourceType, valueType], true);
            il = m.GetILGenerator();
            if (fi.IsStatic) {
                il.Emit(OpCodes.Ldarg_1);
                il.MaybeEmitCast(valueType, propertyOrField.ReturnType());
                il.Emit(OpCodes.Stsfld, fi);
            }
            else {
                il.Emit(OpCodes.Ldarg_0);
                il.MaybeEmitCast(sourceType, declaringType);
                il.Emit(OpCodes.Ldarg_1);
                il.MaybeEmitCast(valueType, propertyOrField.ReturnType());
                il.Emit(OpCodes.Stfld, fi);
            }
        }
        else
            throw new ArgumentOutOfRangeException(nameof(propertyOrField));

        il.Emit(OpCodes.Ret);
        return m.CreateDelegate(funcType);
    }

    // Expression trees-based codegen

    private static Delegate CreateGetterET(
        Type sourceType,
        MemberInfo propertyOrField,
        Type valueType)
    {
        var funcType = typeof(Func<,>).MakeGenericType(sourceType, valueType);
        var declaringType = propertyOrField.DeclaringType!;
        var pSource = Expression.Parameter(sourceType);
        if (propertyOrField is PropertyInfo pi) {
            var isStatic = pi.GetMethod!.IsStatic;
            if (!isStatic && declaringType.IsAssignableFrom(sourceType) && valueType == pi.PropertyType)
                return pi.GetMethod!.CreateDelegate(funcType);
        }

        return Expression
            .Lambda(funcType,
                ExpressionExt.MaybeConvert(
                    ExpressionExt.PropertyOrField(pSource, propertyOrField),
                    valueType),
                [pSource])
            .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
    }

    private static Delegate CreateSetterET(
        Type sourceType,
        MemberInfo propertyOrField,
        Type valueType)
    {
        var funcType = typeof(Action<,>).MakeGenericType(sourceType, valueType);
        var declaringType = propertyOrField.DeclaringType!;
        var pSource = Expression.Parameter(sourceType);
        var pValue = Expression.Parameter(valueType);
        if (propertyOrField is PropertyInfo pi) {
            var isStatic = pi.SetMethod!.IsStatic;
            if (!isStatic && declaringType.IsAssignableFrom(sourceType) && valueType == pi.PropertyType)
                return pi.SetMethod!.CreateDelegate(funcType);
        }

        return Expression
            .Lambda(funcType,
                Expression.Assign(
                    ExpressionExt.PropertyOrField(pSource, propertyOrField),
                    ExpressionExt.MaybeConvert(pValue, propertyOrField.ReturnType())),
                [pSource, pValue])
            .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
    }
}
