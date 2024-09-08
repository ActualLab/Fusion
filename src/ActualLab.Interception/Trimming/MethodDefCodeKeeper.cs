using ActualLab.Trimming;

namespace ActualLab.Interception.Trimming;

public class MethodDefCodeKeeper : CodeKeeper
{
    public virtual void KeepCodeForResult<TResult, TUnwrapped>()
    {
        if (AlwaysTrue)
            return;

        var methodDef = Keep<MethodDef>();
        methodDef.KeepCodeForResult<TResult, TUnwrapped>();
    }
}
