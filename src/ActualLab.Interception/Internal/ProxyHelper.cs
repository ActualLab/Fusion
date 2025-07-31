using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Interception.Internal;

public static class ProxyHelper
{
    private static readonly BindingFlags GetMethodInfoBindingFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "False positive")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodInfo GetMethodInfo(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        string name, Type[] argumentTypes)
    {
        var result = GetMethodInfoImpl(type, name, argumentTypes);
        if (result is not null)
            return result;

        if (type.IsInterface) {
            foreach (var tInterface in type.GetAllBaseTypes(false, true)) {
                result = GetMethodInfoImpl(tInterface, name, argumentTypes);
                if (result is not null)
                    return result;
            }
        }

        throw new MissingMethodException(type.Name, name);
    }

    private static MethodInfo? GetMethodInfoImpl(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.NonPublicMethods)] Type type,
        string name, Type[] argumentTypes)
    {
#if NETSTANDARD || NETCOREAPP
        return type.GetMethod(name, GetMethodInfoBindingFlags, null, argumentTypes, null);
#else
        return type.GetMethod(name, GetMethodInfoBindingFlags, types);
#endif
    }
}
