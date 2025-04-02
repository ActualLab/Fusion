using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace ActualLab;

/// <summary>
/// Misc. helpers and extensions related to <see cref="Result{T}"/> and <see cref="IResult{T}"/> types.
/// </summary>
public static class ResultExt
{
    // IsValue

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValue<T>(this Result<T> result, [MaybeNullWhen(false)] out T value)
    {
        (value, var error) = result;
        return error == null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValue<T>(this IResult<T> result, [MaybeNullWhen(false)] out T value)
    {
        (value, var error) = result;
        return error == null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValue<T>(this Result<T> result, [MaybeNullWhen(false)] out T value, [NotNullWhen(false)] out Exception? error)
    {
        (value, error) = result;
        return error == null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValue<T>(this IResult<T> result, [MaybeNullWhen(false)] out T value, [NotNullWhen(false)] out Exception? error)
    {
        (value, error) = result;
        return error == null;
    }

    // IsValueUntyped

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValueUntyped(this Result result, out object? value)
    {
        (value, var error) = result;
        return error == null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValueUntyped(this IResult result, out object? value)
    {
        (value, var error) = result;
        return error == null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValueUntyped(this Result result, out object? value, [NotNullWhen(false)] out Exception? error)
    {
        (value, error) = result;
        return error == null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValueUntyped(this IResult result, out object? value, [NotNullWhen(false)] out Exception? error)
    {
        (value, error) = result;
        return error == null;
    }

    // ToTypedResult

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToTypedResult<T>(this Result result)
    {
        var (untypedValue, error) = result;
        return error == null
            ? new Result<T>((T)untypedValue!, error)
            : new Result<T>(default!, error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToTypedResult<T>(this Result<T> result)
        => result;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToTypedResult<T>(this IResult result)
    {
        var (untypedValue, error) = result;
        return error == null
            ? new Result<T>((T)untypedValue!, error)
            : new Result<T>(default!, error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToTypedResult<T>(this IResult<T> result)
        => new(result.ValueOrDefault!, result.Error);

    // ToUntypedResult

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result ToUntypedResult(this Result result)
        => result;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result ToUntypedResult(this IResult result)
        => new(result.GetUntypedValueOrErrorBox());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result ToUntypedResult<T>(this Result<T> result)
        => new(result.GetUntypedValueOrErrorBox());

    // ToTask

    public static Task<T> ToTask<T>(this Result result)
    {
        var (value, error) = result;
        return error == null
            ? Task.FromResult((T)value!)
            : Task.FromException<T>(error);
    }

    public static Task<T> ToTask<T>(this Result<T> result)
    {
        var (value, error) = result;
        return error == null
            ? Task.FromResult(value)
            : Task.FromException<T>(error);
    }

    public static Task<T> ToTask<T>(this IResult result)
    {
        var (value, error) = result;
        return error == null
            ? Task.FromResult((T)value!)
            : Task.FromException<T>(error);
    }

    public static Task<T> ToTask<T>(this IResult<T> result)
    {
        var (value, error) = result;
        return error == null
            ? Task.FromResult(value)
            : Task.FromException<T>(error);
    }

    public static Task ToTask(this Result result, Type resultType)
    {
        var (value, error) = result;
        return error == null
            ? TaskExt.FromResult(value, resultType)
            : TaskExt.FromException(error, resultType);
    }

    // ToValueTask

    public static ValueTask<T> ToValueTask<T>(this Result result)
    {
        var (value, error) = result;
        return error == null
            ? ValueTaskExt.FromResult((T)value!)
            : ValueTaskExt.FromException<T>(error);
    }

    public static ValueTask<T> ToValueTask<T>(this Result<T> result)
    {
        var (value, error) = result;
        return error == null
            ? ValueTaskExt.FromResult(value)
            : ValueTaskExt.FromException<T>(error);
    }

    public static ValueTask<T> ToValueTask<T>(this IResult result)
    {
        var (value, error) = result;
        return error == null
            ? ValueTaskExt.FromResult((T)value!)
            : ValueTaskExt.FromException<T>(error);
    }

    public static ValueTask<T> ToValueTask<T>(this IResult<T> result)
    {
        var (value, error) = result;
        return error == null
            ? ValueTaskExt.FromResult(value)
            : ValueTaskExt.FromException<T>(error);
    }

    // ThrowIfError

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfError(this Result result)
    {
        if (result.Error is { } error)
            ExceptionDispatchInfo.Capture(error).Throw();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfError<T>(this Result<T> result)
    {
        if (result.Error is { } error)
            ExceptionDispatchInfo.Capture(error).Throw();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfError(this IResult result)
    {
        if (result.Error is { } error)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
