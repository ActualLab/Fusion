using ActualLab.Async.Internal;

namespace ActualLab.Async;

/// <summary>
/// A pooled helper for attaching completion callbacks to tasks.
/// Each instance caches its delegate, and instances are pooled to minimize allocations.
/// </summary>
public abstract class TaskCompletionHandler
{
    protected readonly Action OnCompleted;
    protected Task Task = null!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected TaskCompletionHandler()
        => OnCompleted = OnCompletedImpl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskCompletionHandler Get(Task task, object? state, Action<Task, object?> handler)
        => Handler1.GetImpl(task, state, handler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskCompletionHandler Get(Task task, object? state1, object? state2, Action<Task, object?, object?> handler)
        => Handler2.GetImpl(task, state1, state2, handler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaskCompletionHandler Get(Task task, object? state1, object? state2, object? state3, Action<Task, object?, object?, object?> handler)
        => Handler3.GetImpl(task, state1, state2, state3, handler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Attach(bool flowExecutionContext = false)
    {
        if (flowExecutionContext)
            Task.GetAwaiter().OnCompleted(OnCompleted);
        else {
            if (!TaskImpl.AddContinuation(Task, OnCompleted, false))
                OnCompleted();
        }
    }

    protected abstract void OnCompletedImpl();

    // Nested types

    private sealed class Handler1 : TaskCompletionHandler
    {
        [ThreadStatic] private static Pool _pool;

        private object? _state;
        private Action<Task, object?>? _handler;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Handler1 GetImpl(Task task, object? state, Action<Task, object?> handler)
        {
            var instance = Unsafe.As<Handler1>(_pool.Rent()) ?? new Handler1();
            instance.Task = task;
            instance._state = state;
            instance._handler = handler;
            return instance;
        }

        protected override void OnCompletedImpl()
        {
            var handler = _handler!;
            var task = Task;
            var state = _state;

            _handler = null;
            Task = null!;
            _state = null;
            _pool.Return(this);

            handler.Invoke(task, state);
        }
    }

    private sealed class Handler2 : TaskCompletionHandler
    {
        [ThreadStatic] private static Pool _pool;

        private object? _state1;
        private object? _state2;
        private Action<Task, object?, object?>? _handler;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Handler2 GetImpl(Task task, object? state1, object? state2, Action<Task, object?, object?> handler)
        {
            var instance = Unsafe.As<Handler2>(_pool.Rent()) ?? new Handler2();
            instance.Task = task;
            instance._state1 = state1;
            instance._state2 = state2;
            instance._handler = handler;
            return instance;
        }

        protected override void OnCompletedImpl()
        {
            var handler = _handler!;
            var task = Task;
            var state1 = _state1;
            var state2 = _state2;

            _handler = null;
            Task = null!;
            _state1 = null;
            _state2 = null;
            _pool.Return(this);

            handler.Invoke(task, state1, state2);
        }
    }

    private sealed class Handler3 : TaskCompletionHandler
    {
        [ThreadStatic] private static Pool _pool;

        private object? _state1;
        private object? _state2;
        private object? _state3;
        private Action<Task, object?, object?, object?>? _handler;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Handler3 GetImpl(Task task, object? state1, object? state2, object? state3, Action<Task, object?, object?, object?> handler)
        {
            var instance = Unsafe.As<Handler3>(_pool.Rent()) ?? new Handler3();
            instance.Task = task;
            instance._state1 = state1;
            instance._state2 = state2;
            instance._state3 = state3;
            instance._handler = handler;
            return instance;
        }

        protected override void OnCompletedImpl()
        {
            var handler = _handler!;
            var task = Task;
            var state1 = _state1;
            var state2 = _state2;
            var state3 = _state3;

            _handler = null;
            Task = null!;
            _state1 = null;
            _state2 = null;
            _state3 = null;
            _pool.Return(this);

            handler.Invoke(task, state1, state2, state3);
        }
    }

    // Pool<T>

    protected struct Pool
    {
        private const int Capacity = 64;

        private TaskCompletionHandler[]? _items;
        private int _position;

        public TaskCompletionHandler? Rent()
        {
            if (_position <= 0)
                return null;

            var items = _items ??= new TaskCompletionHandler[Capacity];
#if NET6_0_OR_GREATER
            ref var itemRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(items), --_position);
#else
            ref var itemRef = ref items[--_position];
#endif
            var item = itemRef;
            itemRef = null!;
            return item;

        }

        public void Return(TaskCompletionHandler item)
        {
            if (_position >= Capacity)
                return;

            var items = _items ??= new TaskCompletionHandler[Capacity];
#if NET6_0_OR_GREATER
            ref var itemRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(items), _position++);
#else
            ref var itemRef = ref items[_position++];
#endif
            itemRef = item;
        }
    }
}
