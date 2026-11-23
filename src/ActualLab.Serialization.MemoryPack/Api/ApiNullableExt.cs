namespace ActualLab.Api;

/// <summary>
/// Extension methods for converting nullable values to <see cref="ApiNullable{T}"/>
/// and <see cref="ApiNullable8{T}"/>.
/// </summary>
public static class ApiNullableExt
{
    public static ApiNullable<T> ToApiNullable<T>(this T? value)
        where T : struct
        => new(value);

    public static ApiNullable8<T> ToApiNullable8<T>(this T? value)
        where T : struct
        => new(value);
}
