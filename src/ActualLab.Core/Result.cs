using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using ActualLab.Conversion;
using ActualLab.Internal;
using ActualLab.OS;
using MessagePack;

namespace ActualLab;

/// <summary>
/// Describes untyped result of a computation.
/// </summary>
public interface IResult
{
    /// <summary>
    /// Retrieves the result's value. Throws an <see cref="Error"/> when <see cref="HasError"/>.
    /// </summary>
    public object? Value { get; }
    /// <summary>
    /// Retrieves result's error (if any).
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Indicates whether the result is successful (its <see cref="Error"/> is <code>null</code>).
    /// Same as <code>!HasError</code>.
    /// </summary>
    public bool HasValue { get; }
    /// <summary>
    /// Indicates whether the result is error (its <see cref="Error"/> is not <code>null</code>).
    /// Same as <code>!HasValue</code>.
    /// </summary>
    public bool HasError { get; }

    /// <summary>
    /// Deconstructs the result.
    /// </summary>
    /// <param name="untypedValue">Gets <see cref="Value"/> value.</param>
    /// <param name="error">Gets <see cref="Error"/> value.</param>
    public void Deconstruct(out object? untypedValue, out Exception? error);

    /// <summary>
    /// Gets the value or <see cref="ErrorBox"/> instance.
    /// </summary>
    /// <returns>Untyped value or <see cref="ErrorBox"/> instance.</returns>
    public object? GetUntypedValueOrErrorBox();
}

/// <summary>
/// Describes untyped result of a computation that can be changed.
/// </summary>
public interface IMutableResult : IResult
{
    /// <summary>
    /// <see cref="Object"/>-typed version of <see cref="IMutableResult{T}.Value"/>.
    /// </summary>
    public new object? Value { get; set; }
    /// <summary>
    /// Retrieves or sets mutable result's error.
    /// </summary>
    public new Exception? Error { get; set; }

    /// <summary>
    /// Sets mutable result's value and error from the provided <paramref name="result"/>.
    /// </summary>
    /// <param name="result">The result to set value and error from.</param>
    public void Set(Result result);
}

/// <summary>
/// Describes strongly typed result of a computation.
/// </summary>
/// <typeparam name="T">The type of <see cref="Value"/>.</typeparam>
// ReSharper disable once PossibleInterfaceMemberAmbiguity
public interface IResult<T> : IResult, IConvertibleTo<T>
{
    /// <summary>
    /// Retrieves result's value. Returns <code>default</code> when <see cref="IResult.HasError"/>.
    /// </summary>
    public T? ValueOrDefault { get; }
    /// <summary>
    /// Retrieves result's value. Throws an <see cref="Error"/> when <see cref="IResult.HasError"/>.
    /// </summary>
    public new T Value { get; }

    /// <summary>
    /// Deconstructs the result.
    /// </summary>
    /// <param name="value">Gets <see cref="ValueOrDefault"/> value.</param>
    /// <param name="error">Gets <see cref="Error"/> value.</param>
    public void Deconstruct(out T value, out Exception? error);
}

/// <summary>
/// Describes strongly typed result of a computation that can be changed.
/// </summary>
/// <typeparam name="T">The type of <see cref="Value"/>.</typeparam>
public interface IMutableResult<T> : IResult<T>, IMutableResult
{
    /// <summary>
    /// Retrieves or sets mutable result's value. Throws an <see cref="Error"/> when <see cref="IResult.HasError"/>.
    /// </summary>
    public new T Value { get; set; }

    /// <summary>
    /// Sets mutable result's value and error from the provided <paramref name="result"/>.
    /// </summary>
    /// <param name="result">The result to set value and error from.</param>
    public void Set(Result<T> result);

    /// <summary>
    /// Atomically sets mutable result's value and error by invoking the provided <paramref name="updater"/>.
    /// </summary>
    /// <param name="updater">The update function.</param>
    /// <param name="throwOnError"><c>true</c> if exception in <paramref name="updater"/> must be rethrown
    /// without settings the <see cref="Value"/>; otherwise, <c>false</c>.</param>
    public void Set(Func<Result<T>, Result<T>> updater, bool throwOnError = false);

    /// <summary>
    /// Atomically sets mutable result's value and error by invoking the provided <paramref name="updater"/>.
    /// </summary>
    /// <param name="state">State argument to pass to the updater.</param>
    /// <param name="updater">The update function.</param>
    /// <param name="throwOnError"><c>true</c> if exception in <paramref name="updater"/> must be rethrown
    /// without settings the <see cref="Value"/>; otherwise, <c>false</c>.</param>
    public void Set<TState>(TState state, Func<TState, Result<T>, Result<T>> updater, bool throwOnError = false);
}

/// <summary>
/// Untyped result of a computation and some helper methods related to <see cref="Result{T}"/> type.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("({" + nameof(DebugValue) + "}, Error = {" + nameof(Error) + "})")]
public readonly struct Result : IResult, IEquatable<Result>, IEquatable<IResult>
{
    private static readonly ConcurrentDictionary<Type, Func<Exception, IResult>> ErrorCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly MethodInfo ErrorInternalMethod
        = typeof(Result).GetMethod(nameof(ErrorInternal), BindingFlags.Static | BindingFlags.NonPublic)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> New<T>(T value, Exception? error = null) => new(value, error);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> NewError<T>(Exception error) => new(default!, error);
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume ErrorInternal method is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume ErrorInternal method is preserved")]
    public static IResult NewError(Type resultType, Exception error)
        => ErrorCache.GetOrAdd(
            resultType,
            static tResult => (Func<Exception, IResult>)ErrorInternalMethod
                .MakeGenericMethod(tResult)
                .CreateDelegate(typeof(Func<Exception, IResult>))
        ).Invoke(error);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result NewUntyped(object? untypedValueOrErrorBox) => new(untypedValueOrErrorBox);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result NewUntyped(object? untypedValue, Exception? error) => new(untypedValue, error);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result NewUntypedError(Exception error) => new(new ErrorBox(error));

    private readonly object? _valueOrErrorBox;

    public object? DebugValue
        => _valueOrErrorBox is ErrorBox ? null : _valueOrErrorBox;

    /// <inheritdoc />
    public object? Value {
        get {
            if (_valueOrErrorBox is ErrorBox e)
                ExceptionDispatchInfo.Capture(e.Error).Throw();
            return _valueOrErrorBox;
        }
    }

    /// <inheritdoc />
    public Exception? Error {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _valueOrErrorBox is ErrorBox e ? e.Error : null;
    }

    /// <inheritdoc />
    public bool HasValue {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _valueOrErrorBox is not ErrorBox;
    }

    /// <inheritdoc />
    public bool HasError {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _valueOrErrorBox is ErrorBox;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="untypedValueOrErrorBox">Untyped value or <see cref="ErrorBox"/>.</param>
    public Result(object? untypedValueOrErrorBox)
        => _valueOrErrorBox = untypedValueOrErrorBox;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="untypedValue">Untyped value.</param>
    /// <param name="error">Error, if it's an error.</param>
    public Result(object? untypedValue, Exception? error)
        => _valueOrErrorBox = error is null ? untypedValue : new ErrorBox(error);

    /// <inheritdoc />
    public void Deconstruct(out object? untypedValue, out Exception? error)
    {
        if (_valueOrErrorBox is ErrorBox e) {
            untypedValue = null;
            error = e.Error;
        }
        else {
            untypedValue = _valueOrErrorBox;
            error = null;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var errorBox = _valueOrErrorBox as ErrorBox;
        return $"{nameof(Result)}({(errorBox is not null ? $"Error: {errorBox.Error}" : _valueOrErrorBox?.ToString())})";
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetUntypedValueOrErrorBox()
        => _valueOrErrorBox;

    // Equality

    /// <inheritdoc />
    public bool Equals(Result other)
        => Equals(_valueOrErrorBox, other._valueOrErrorBox);
    public bool Equals(IResult? other)
        => !ReferenceEquals(other, null) && Equals(_valueOrErrorBox, other.GetUntypedValueOrErrorBox());
    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is IResult o && Equals(o);

    /// <inheritdoc />
    public override int GetHashCode() => _valueOrErrorBox?.GetHashCode() ?? 0;
    public static bool operator ==(Result left, Result right) => left.Equals(right);
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    // Private methods

    private static IResult ErrorInternal<T>(Exception error)
        // ReSharper disable once HeapView.BoxingAllocation
        => new Result<T>(default!, error);
}

/// <summary>
/// A struct describing strongly typed result of a computation.
/// </summary>
/// <typeparam name="T">The type of <see cref="Value"/>.</typeparam>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("({" + nameof(ValueOrDefault) + "}, Error = {" + nameof(Error) + "})")]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial struct Result<T> : IResult<T>, IEquatable<Result<T>>
{
    /// <inheritdoc />
    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public T? ValueOrDefault { get; }

    /// <inheritdoc />
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public Exception? Error { get; }

    [DataMember(Order = 1), MemoryPackOrder(1), Key(1)]
    public ExceptionInfo? ExceptionInfo => Error?.ToExceptionInfo();

    /// <inheritdoc />
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool HasValue {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Error is null;
    }

    /// <inheritdoc />
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool HasError {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Error is not null;
    }

    /// <inheritdoc />
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public T Value {
        get {
            if (Error is not null)
                ExceptionDispatchInfo.Capture(Error).Throw();
            return ValueOrDefault!;
        }
    }

    /// <inheritdoc />
    // ReSharper disable once HeapView.BoxingAllocation
    object? IResult.Value => Error is null ? Value : null;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="valueOrDefault"><see cref="ValueOrDefault"/> value.</param>
    /// <param name="error"><see cref="Error"/> value.</param>
    public Result(T valueOrDefault, Exception? error = null)
    {
        if (error is not null)
            valueOrDefault = default!;
        ValueOrDefault = valueOrDefault;
        Error = error;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public Result(T valueOrDefault, ExceptionInfo? exceptionInfo)
    {
        if (exceptionInfo is { IsNone: false } vExceptionInfo) {
            ValueOrDefault = default;
            Error = vExceptionInfo.ToException();
        }
        else {
            ValueOrDefault = valueOrDefault;
            Error = null;
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
    {
        value = ValueOrDefault!;
        error = Error;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IResult.Deconstruct(out object? untypedValue, out Exception? error)
    {
        untypedValue = ValueOrDefault;
        error = Error;
    }

    /// <inheritdoc />
    public override string ToString()
        => $"{GetType().GetName()}({(HasError ? $"Error: {Error}" : Value?.ToString())})";

    /// <inheritdoc />
    T IConvertibleTo<T>.Convert() => Value;

    /// <inheritdoc />
    public object? GetUntypedValueOrErrorBox()
        => Error is { } error ? new ErrorBox(error) : ValueOrDefault;

    // Equality

    /// <inheritdoc />
    public bool Equals(Result<T> other)
        => Error == other.Error && EqualityComparer<T>.Default.Equals(ValueOrDefault!, other.ValueOrDefault!);
    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is Result<T> o && Equals(o);
    /// <inheritdoc />
    public override int GetHashCode() => (ValueOrDefault?.GetHashCode() ?? 0) ^ (Error?.GetHashCode() ?? 0);
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    // Operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator T(Result<T> source) => source.Value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T>(T source) => new(source, null);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T>((T Value, Exception? Error) source) => new(source.Value, source.Error);
}
