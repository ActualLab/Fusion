namespace ActualLab.Reflection;

#pragma warning disable IL3050

public static class FuncExt
{
    public static Type GetActionType(Type[] argumentTypes)
        => argumentTypes.Length switch {
            00 => typeof(Action),
            01 => typeof(Action<>).MakeGenericType(argumentTypes[0]),
            02 => typeof(Action<,>).MakeGenericType(argumentTypes[0], argumentTypes[1]),
            03 => typeof(Action<,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2]),
            04 => typeof(Action<,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3]),
            05 => typeof(Action<,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4]),
            06 => typeof(Action<,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5]),
            07 => typeof(Action<,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6]),
            08 => typeof(Action<,,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6], argumentTypes[7]),
            09 => typeof(Action<,,,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6], argumentTypes[7], argumentTypes[8]),
            10 => typeof(Action<,,,,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6], argumentTypes[7], argumentTypes[8], argumentTypes[9]),
            11 => typeof(Action<,,,,,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6], argumentTypes[7], argumentTypes[8], argumentTypes[9], argumentTypes[10]),
            _ => throw new ArgumentOutOfRangeException(nameof(argumentTypes)),
        };

    public static Type GetFuncType(Type[] argumentTypes, Type returnType)
        => argumentTypes.Length switch {
            00 => typeof(Func<>).MakeGenericType(returnType),
            01 => typeof(Func<,>).MakeGenericType(argumentTypes[0], returnType),
            02 => typeof(Func<,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], returnType),
            03 => typeof(Func<,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], returnType),
            04 => typeof(Func<,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], returnType),
            05 => typeof(Func<,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], returnType),
            06 => typeof(Func<,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], returnType),
            07 => typeof(Func<,,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6], returnType),
            08 => typeof(Func<,,,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6], argumentTypes[7], returnType),
            09 => typeof(Func<,,,,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6], argumentTypes[7], argumentTypes[8], returnType),
            10 => typeof(Func<,,,,,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6], argumentTypes[7], argumentTypes[8], argumentTypes[9], returnType),
            11 => typeof(Func<,,,,,,,,,,,>).MakeGenericType(argumentTypes[0], argumentTypes[1], argumentTypes[2], argumentTypes[3], argumentTypes[4], argumentTypes[5], argumentTypes[6], argumentTypes[7], argumentTypes[8], argumentTypes[9], argumentTypes[10], returnType),
            _ => throw new ArgumentOutOfRangeException(nameof(argumentTypes)),
        };
}
