using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ActualLab.Reflection;

/// <summary>
/// Provides typed invocation-list helpers and closure inspection for delegates.
/// </summary>
public static class DelegateExt
{
    public static TDelegate[] GetInvocationList<TDelegate>(TDelegate? @delegate)
        where TDelegate : Delegate
    {
        if (@delegate is null)
            return [];

        var invocationList = @delegate.GetInvocationList();
        var result = new TDelegate[invocationList.Length];
        for (var i = 0; i < result.Length; i++)
            result[i] = (TDelegate)invocationList[i];
        return result;
    }

    // True for a static method, a static or non-capturing lambda, or a static local function.
    // False for an instance method group and for a lambda capturing `this` or any local.
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "If closure fields are trimmed, the delegate is reported as capture-free: a false negative")]
    public static bool IsCaptureFree(this Delegate @delegate)
    {
        var target = @delegate.Target;
        if (target is null)
            return true;

        // A non-capturing lambda's target is the compiler-generated singleton (`<>c`), which has no instance fields
        var targetType = target.GetType();
        return targetType.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            && targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0;
    }
}
