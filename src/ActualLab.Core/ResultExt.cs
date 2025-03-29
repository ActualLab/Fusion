using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace ActualLab;

/// <summary>
/// Misc. helpers and extensions related to <see cref="Result{T}"/> and <see cref="IResult{T}"/> types.
/// </summary>
public static class ResultExt
{
    // IsValueUntyped, IsValue

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValueUntyped(this Result result, out object? value)
    {
        (value, var error) = result;
        return error == null;
    }

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

    // AsUntyped

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result AsUntyped(this Result result)
        => result;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result AsUntyped(this IResult result)
        => new(result.GetUntypedValueOrErrorBox());

    // AsTyped

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> AsTyped<T>(this Result result)
    {
        var (untypedValue, error) = result;
        return new Result<T>((T)untypedValue!, error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> AsTyped<T>(this Result<T> result)
        => result;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> AsTyped<T>(this IResult result)
    {
        var (untypedValue, error) = result;
        return new Result<T>((T)untypedValue!, error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> AsTyped<T>(this IResult<T> result)
    {
        return new Result<T>(result.ValueOrDefault!, result.Error);
    }

    // AsTask

    public static Task<T> AsTask<T>(this Result result)
    {
        var (value, error) = result;
        return error == null
            ? Task.FromResult((T)value!)
            : Task.FromException<T>(error);
    }

    public static Task<T> AsTask<T>(this Result<T> result)
    {
        var (value, error) = result;
        return error == null
            ? Task.FromResult(value)
            : Task.FromException<T>(error);
    }

    public static Task<T> AsTask<T>(this IResult result)
    {
        var (value, error) = result;
        return error == null
            ? Task.FromResult((T)value!)
            : Task.FromException<T>(error);
    }

    public static Task<T> AsTask<T>(this IResult<T> result)
    {
        var (value, error) = result;
        return error == null
            ? Task.FromResult(value)
            : Task.FromException<T>(error);
    }

    public static Task AsTask(this Result result, Type resultType)
    {
        var (value, error) = result;
        return error == null
            ? TaskExt.FromResult(value, resultType)
            : TaskExt.FromException(error, resultType);
    }

    // AsValueTask

    public static ValueTask<T> AsValueTask<T>(this Result result)
    {
        var (value, error) = result;
        return error == null
            ? ValueTaskExt.FromResult((T)value!)
            : ValueTaskExt.FromException<T>(error);
    }

    public static ValueTask<T> AsValueTask<T>(this Result<T> result)
    {
        var (value, error) = result;
        return error == null
            ? ValueTaskExt.FromResult(value)
            : ValueTaskExt.FromException<T>(error);
    }

    public static ValueTask<T> AsValueTask<T>(this IResult result)
    {
        var (value, error) = result;
        return error == null
            ? ValueTaskExt.FromResult((T)value!)
            : ValueTaskExt.FromException<T>(error);
    }

    public static ValueTask<T> AsValueTask<T>(this IResult<T> result)
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
