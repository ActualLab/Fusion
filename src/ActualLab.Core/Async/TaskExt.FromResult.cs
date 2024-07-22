namespace ActualLab.Async;

public static partial class TaskExt
{
    public static Task FromResult(Action action)
    {
        try {
            action.Invoke();
            return Task.CompletedTask;
        }
        catch (Exception e) {
            return Task.FromException(e);
        }
    }

    public static Task FromResult<TArg1>(Action<TArg1> action, TArg1 arg1)
    {
        try {
            action.Invoke(arg1);
            return Task.CompletedTask;
        }
        catch (Exception e) {
            return Task.FromException(e);
        }
    }

    public static Task FromResult<TArg1, TArg2>(Action<TArg1, TArg2> action, TArg1 arg1, TArg2 arg2)
    {
        try {
            action.Invoke(arg1, arg2);
            return Task.CompletedTask;
        }
        catch (Exception e) {
            return Task.FromException(e);
        }
    }

    public static Task FromResult<TArg1, TArg2, TArg3>(Action<TArg1, TArg2, TArg3> action, TArg1 arg1, TArg2 arg2, TArg3 arg3)
    {
        try {
            action.Invoke(arg1, arg2, arg3);
            return Task.CompletedTask;
        }
        catch (Exception e) {
            return Task.FromException(e);
        }
    }

    public static Task<T> FromResult<T>(Func<T> func)
    {
        try {
            return Task.FromResult(func.Invoke());
        }
        catch (Exception e) {
            return Task.FromException<T>(e);
        }
    }

    public static Task<T> FromResult<TArg1, T>(Func<TArg1, T> func, TArg1 arg1)
    {
        try {
            return Task.FromResult(func.Invoke(arg1));
        }
        catch (Exception e) {
            return Task.FromException<T>(e);
        }
    }

    public static Task<T> FromResult<TArg1, TArg2, T>(Func<TArg1, TArg2, T> func, TArg1 arg1, TArg2 arg2)
    {
        try {
            return Task.FromResult(func.Invoke(arg1, arg2));
        }
        catch (Exception e) {
            return Task.FromException<T>(e);
        }
    }

    public static Task<T> FromResult<TArg1, TArg2, TArg3, T>(Func<TArg1, TArg2, TArg3, T> func, TArg1 arg1, TArg2 arg2, TArg3 arg3)
    {
        try {
            return Task.FromResult(func.Invoke(arg1, arg2, arg3));
        }
        catch (Exception e) {
            return Task.FromException<T>(e);
        }
    }
}
