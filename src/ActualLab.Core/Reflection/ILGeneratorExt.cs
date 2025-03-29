using System.Reflection.Emit;
using ActualLab.Internal;

namespace ActualLab.Reflection;

// ReSharper disable once InconsistentNaming
public static class ILGeneratorExt
{
    public static void MaybeEmitCast(this ILGenerator il, Type fromType, Type toType)
    {
        if (fromType == toType)
            return;

        if (toType.IsAssignableFrom(fromType)) {
            // Upcast (... -> base)
            if (!fromType.IsValueType)
                return; // No cast is needed in this case

            // struct -> Object
            il.Emit(OpCodes.Box, fromType);
            return;
        }

        if (!fromType.IsAssignableFrom(toType)) // Cast between two types which aren't related
            throw Errors.MustBeAssignableTo(fromType, toType, nameof(toType));

        // Downcast (base -> ...)
        il.Emit(OpCodes.Castclass, toType);
        if (toType.IsValueType)
            il.Emit(OpCodes.Unbox_Any, toType);
    }
}
