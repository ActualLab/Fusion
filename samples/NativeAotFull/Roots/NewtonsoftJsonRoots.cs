using ActualLab.Trimming;

// Newtonsoft.Json is reflection-based, so this covers only what ActualLab's converters close
// via MakeGenericType; ActualLab.Generators doesn't cover it.
// Like CodeKeeper's own: the roots never execute, they only make the AOT compiler keep the code
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The roots only keep the code")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The roots only keep the code")]
public static class NewtonsoftJsonRoots
{
    public static void Keep()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        // ApiArrayNewtonsoftJsonConverter's converters are private
        CodeKeeper.Keep("ActualLab.Serialization.Internal.ApiArrayNewtonsoftJsonConverter+Converter`1"
            + "[[System.Int32, System.Runtime]], ActualLab.Serialization.NewtonsoftJson");
    }
}
