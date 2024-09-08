using ActualLab.Trimming;

namespace ActualLab.Interception.Trimming;

public class ArgumentListCodeKeeper : CodeKeeper
{
    public virtual void KeepArgumentListArgument<TArg>()
    {
        if (AlwaysTrue)
            return;

        Keep<TArg>();
        var tArg = typeof(TArg);
        KeepArgumentListArgument<ArgumentList0, TArg>(
            new ArgumentList0());
        KeepArgumentListArgument<ArgumentListS1, TArg>(
            new ArgumentListS1(ArgumentListType.Get(false, tArg)));
        KeepArgumentListArgument<ArgumentListS2, TArg>(
            new ArgumentListS2(ArgumentListType.Get(false, tArg, tArg)));
        KeepArgumentListArgument<ArgumentListS3, TArg>(
            new ArgumentListS3(ArgumentListType.Get(false, tArg, tArg, tArg)));
        KeepArgumentListArgument<ArgumentListS4, TArg>(
            new ArgumentListS4(ArgumentListType.Get(false, tArg, tArg, tArg, tArg)));
        KeepArgumentListArgument<ArgumentListS5, TArg>(
            new ArgumentListS5(ArgumentListType.Get(false, tArg, tArg, tArg, tArg, tArg)));
        KeepArgumentListArgument<ArgumentListS6, TArg>(
            new ArgumentListS6(ArgumentListType.Get(false, tArg, tArg, tArg, tArg, tArg, tArg)));
        KeepArgumentListArgument<ArgumentListS7, TArg>(
            new ArgumentListS7(ArgumentListType.Get(false, tArg, tArg, tArg, tArg, tArg, tArg, tArg)));
        KeepArgumentListArgument<ArgumentListS8, TArg>(
            new ArgumentListS8(ArgumentListType.Get(false, tArg, tArg, tArg, tArg, tArg, tArg, tArg, tArg)));
        KeepArgumentListArgument<ArgumentListS9, TArg>(
            new ArgumentListS9(ArgumentListType.Get(false, tArg, tArg, tArg, tArg, tArg, tArg, tArg, tArg, tArg)));
        KeepArgumentListArgument<ArgumentListS10, TArg>(
            new ArgumentListS10(ArgumentListType.Get(false, tArg, tArg, tArg, tArg, tArg, tArg, tArg, tArg, tArg, tArg)));
    }

    public virtual void KeepArgumentListArgument<TList, TArg>(TList list)
        where TList : ArgumentList
    {
        if (AlwaysTrue)
            return;

        Keep<TList>();
        CallSilently(() => list.Get<TArg>(0));
        CallSilently(() => list.Set<TArg>(0, default!));
    }
}
