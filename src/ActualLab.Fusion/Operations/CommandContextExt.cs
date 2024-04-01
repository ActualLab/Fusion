using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Fusion.Operations;

public static class CommandContextExt
{
    public static Operation Operation(this CommandContext context)
        => context.Items.Get<Operation>().Require();

    public static Operation Operation<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TOperationScope>(this CommandContext context)
        where TOperationScope : class, IOperationScope
        => context.Items.Get<TOperationScope>().Require().Operation;

    public static void SetOperation(this CommandContext context, Operation? operation)
        => context.Items.Set(operation);
}
