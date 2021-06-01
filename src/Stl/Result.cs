using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stl.Async;
using Stl.Internal;

namespace Stl
{
    /// <summary>
    /// Describes untyped result of a computation.
    /// </summary>
    public interface IResult
    {
        /// <summary>
        /// Indicates whether the result is successful (its <see cref="Error"/> is <code>null</code>).
        /// Same as <code>!HasError</code>.
        /// </summary>
        bool HasValue { get; }
        /// <summary>
        /// Retrieves result's value. Throws an <see cref="Error"/> when <see cref="HasError"/>.
        /// </summary>
        object? Value { get; }

        /// <summary>
        /// Indicates whether the result is error (its <see cref="Error"/> is not <code>null</code>).
        /// Same as <code>!HasValue</code>.
        /// </summary>
        bool HasError { get; }
        /// <summary>
        /// Retrieves result's error (if any).
        /// </summary>
        Exception? Error { get; }

        /// <summary>
        /// Casts result to another result type.
        /// </summary>
        /// <typeparam name="TOther">Another result type.</typeparam>
        /// <returns>A result of the specified type.</returns>
        Result<TOther> Cast<TOther>();
    }

    /// <summary>
    /// Describes untyped result of a computation that can be changed.
    /// </summary>
    public interface IMutableResult : IResult
    {
        /// <summary>
        /// <see cref="Object"/>-typed version of <see cref="IMutableResult{T}.Value"/>.
        /// </summary>
        object? UntypedValue { get; set; }
        /// <summary>
        /// Retrieves or sets mutable result's error.
        /// </summary>
        new Exception? Error { get; set; }
        /// <summary>
        /// Sets mutable result's value and error from the provided <paramref name="result"/>.
        /// </summary>
        /// <param name="result">The result to set value and error from.</param>
        void Set(IResult result);
    }

    /// <summary>
    /// Describes strongly typed result of a computation.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="Value"/>.</typeparam>
    // ReSharper disable once PossibleInterfaceMemberAmbiguity
    public interface IResult<T> : IResult, IConvertibleTo<T>, IConvertibleTo<Result<T>>
    {
        /// <summary>
        /// Retrieves result's value. Returns <code>default</code> when <see cref="IResult.HasError"/>.
        /// </summary>
        [MaybeNull] T ValueOrDefault { get; }
        /// <summary>
        /// Retrieves result's value. Throws an <see cref="Error"/> when <see cref="IResult.HasError"/>.
        /// </summary>
        new T Value { get; }

        /// <summary>
        /// Deconstructs the result.
        /// </summary>
        /// <param name="value">Gets <see cref="ValueOrDefault"/> value.</param>
        /// <param name="error">Gets <see cref="Error"/> value.</param>
        void Deconstruct(out T value, out Exception? error);
        bool IsValue([MaybeNullWhen(false)] out T value);
        bool IsValue([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out Exception error);

        /// <summary>
        /// Casts a custom-typed result to <see cref="Result{T}"/> (struct).
        /// </summary>
        /// <returns><see cref="Result{T}"/> struct.</returns>
        Result<T> AsResult();
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
        new T Value { get; set; }

        /// <summary>
        /// Sets mutable result's value and error from the provided <paramref name="result"/>.
        /// </summary>
        /// <param name="result">The result to set value and error from.</param>
        void Set(Result<T> result);
    }

    /// <summary>
    /// A struct describing strongly typed result of a computation.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="Value"/>.</typeparam>
    [DebuggerDisplay("({" + nameof(ValueOrDefault) + "}, Error = {" + nameof(Error) + "})")]
    public readonly struct Result<T> : IResult<T>, IEquatable<Result<T>>
    {
        /// <inheritdoc />
        [MaybeNull]
        public T ValueOrDefault { get; }
        /// <inheritdoc />
        public Exception? Error { get; }

        /// <inheritdoc />
        [JsonIgnore] public bool HasValue {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Error == null;
        }

        /// <inheritdoc />
        [JsonIgnore] public bool HasError {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Error != null;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public T Value {
            get {
                if (Error != null)
                    ExceptionDispatchInfo.Capture(Error).Throw();
                return ValueOrDefault!;
            }
        }

        /// <inheritdoc />
        // ReSharper disable once HeapView.BoxingAllocation
        object? IResult.Value => Value;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="valueOrDefault"><see cref="ValueOrDefault"/> value.</param>
        /// <param name="error"><see cref="Error"/> value.</param>
        [JsonConstructor]
        public Result(T valueOrDefault, Exception? error)
        {
            if (error != null) valueOrDefault = default!;
            ValueOrDefault = valueOrDefault;
            Error = error;
        }

        public override string? ToString()
            => $"{GetType().Name}({(HasError ? $"Error: {Error}" : Value?.ToString())})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T value, out Exception? error)
        {
            value = ValueOrDefault!;
            error = Error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValue([MaybeNullWhen(false)] out T value)
        {
            value = HasError ? default! : ValueOrDefault!;
            return !HasError;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValue([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out Exception error)
        {
            error = Error!;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            var hasValue = error == null;
            value = hasValue ? ValueOrDefault : default!;
            return hasValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T> AsResult() => this;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TOther> Cast<TOther>() =>
            new((TOther) (object) ValueOrDefault!, Error);
        T IConvertibleTo<T>.Convert() => Value;
        Result<T> IConvertibleTo<Result<T>>.Convert() => AsResult();

        // Equality

        public bool Equals(Result<T> other) =>
            Error == other.Error && EqualityComparer<T>.Default.Equals(ValueOrDefault!, other.ValueOrDefault!);
        public override bool Equals(object? obj) =>
            obj is Result<T> o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(ValueOrDefault!, Error);
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

    /// <summary>
    /// Helper methods related to <see cref="Result{T}"/> type.
    /// </summary>
    public static class Result
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<T> New<T>(T value, Exception? error = null) => new(value, error);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<T> Value<T>(T value) => new(value, null);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Result<T> Error<T>(Exception? error) => new(default!, error);

        public static Result<T> FromTask<T>(Task<T> task)
        {
            if (!task.IsCompleted)
                throw Errors.TaskIsNotCompleted();
            if (task.IsCompletedSuccessfully())
                return Value(task.Result);
            return Error<T>(task.Exception
                ?? Errors.InternalError("Task hasn't completed successfully but has no Exception."));
        }

        public static Result<T> FromFunc<T, TState>(TState state, Func<TState, T> func)
        {
            try {
                return Value(func.Invoke(state));
            }
            catch (Exception e) {
                return Error<T>(e);
            }
        }

        public static Result<T> FromFunc<T>(Func<T> func)
        {
            try {
                return Value(func.Invoke());
            }
            catch (Exception e) {
                return Error<T>(e);
            }
        }
    }
}
