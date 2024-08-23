namespace ActualLab.Api;

public static class ApiNullableExt
{
    public static ApiNullable<T> ToApiNullable<T>(this T? value)
        where T : struct
        => new(value);

    public static ApiNullable8<T> ToApiNullable8<T>(this T? value)
        where T : struct
        => new(value);
}
