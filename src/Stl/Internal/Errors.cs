using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Stl.Async;
using Stl.DependencyInjection;

namespace Stl.Internal
{
    public static class Errors
    {
        public static Exception MustImplement<TExpected>(Type type, string? argumentName = null)
            => MustImplement(type, typeof(TExpected), argumentName);
        public static Exception MustImplement(Type type, Type expectedType, string? argumentName = null)
        {
            var message = $"'{type}' must implement '{expectedType}'.";
            return string.IsNullOrEmpty(argumentName)
                ? (Exception) new InvalidOperationException(message)
                : new ArgumentOutOfRangeException(argumentName, message);
        }

        public static Exception MustBeUnfrozen() =>
            new InvalidOperationException("The object must be unfrozen.");
        public static Exception MustBeUnfrozen(string paramName) =>
            new ArgumentException("The object must be unfrozen.", paramName);
        public static Exception MustBeFrozen() =>
            new InvalidOperationException("The object must be frozen.");
        public static Exception MustBeFrozen(string paramName) =>
            new ArgumentException("The object must be frozen.", paramName);

        public static Exception InvokerIsAlreadyRunning() =>
            new InvalidOperationException("Can't perform this action while invocation is already in progress.");

        public static Exception ExpressionDoesNotSpecifyAMember(string expression) =>
            new ArgumentException("Expression '{expression}' does not specify a member.");
        public static Exception UnexpectedMemberType(string memberType) =>
            new InvalidOperationException($"Unexpected member type: {memberType}");

        public static Exception InvalidListFormat() =>
            new FormatException("Invalid list format.");

        public static Exception CircularDependency<T>(T item) =>
            new InvalidOperationException($"Circular dependency on {item} found.");

        public static Exception OptionIsNone() =>
            new InvalidOperationException("Option is None.");

        public static Exception TaskIsNotCompleted() =>
            new InvalidOperationException("Task is expected to be completed at this point, but it's not.");

        public static Exception PathIsRelative(string? paramName) =>
            new ArgumentException("Path is relative.", paramName);

        public static Exception UnsupportedTypeForJsonSerialization(Type type)
            => new JsonSerializationException($"Unsupported type: '{type}'.");

        public static Exception AlreadyDisposed() =>
            new ObjectDisposedException("unknown", "The object is already disposed.");
        public static Exception AlreadyDisposedOrDisposing(DisposalState disposalState = DisposalState.Disposed)
        {
            switch (disposalState) {
            case DisposalState.Disposing:
                return new ObjectDisposedException("unknown", "The object is disposing.");
            case DisposalState.Disposed:
                return new ObjectDisposedException("unknown", "The object is already disposed.");
            default:
                return new InvalidOperationException($"Invalid disposal state: {disposalState}.");
            }
        }

        public static Exception KeyAlreadyExists() =>
            new ArgumentException("Specified key already exists.");
        public static Exception AlreadyInvoked(string methodName) =>
            new InvalidOperationException($"'{methodName}' can be invoked just once.");
        public static Exception AlreadyInitialized(string? propertyName = null)
        {
            var message = "Already initialized.";
            return propertyName == null
                ? (Exception) new InvalidOperationException(message)
                : new ArgumentException(message, propertyName);
        }
        public static Exception AlreadyLocked() =>
            new InvalidOperationException($"The lock is already acquired by one of callers of the current method.");
        public static Exception AlreadyUsed() =>
            new InvalidOperationException("The object was already used somewhere else.");
        public static Exception AlreadyCompleted() =>
            new InvalidOperationException("The event source is already completed.");
        public static Exception ThisValueCanBeSetJustOnce() =>
            new InvalidOperationException($"This value can be set just once.");
        public static Exception NoDefaultConstructor(Type type) =>
            new InvalidOperationException($"Type '{type}' doesn't have a default constructor.");

        public static Exception InternalError(string message) =>
            new SystemException(message);

        public static Exception GenericMatchForConcreteType(Type type, Type matchType) =>
            new InvalidOperationException($"Generic type '{matchType}' can't be a match for concrete type '{type}'.");
        public static Exception ConcreteMatchForGenericType(Type type, Type matchType) =>
            new InvalidOperationException($"Concrete type '{matchType}' can't be a match for generic type '{type}'.");

        public static Exception NoServiceAttribute(Type implementationType) =>
            new InvalidOperationException(
                $"No matching [{nameof(ServiceAttributeBase)}] descendant is found " +
                $"on '{implementationType}'.");

        public static Exception HostedServiceHasToBeSingleton(Type implementationType) =>
            new InvalidOperationException(
                $"'{implementationType}' has to use {nameof(ServiceAttribute.Lifetime)} == " +
                $"{nameof(ServiceLifetime)}.{nameof(ServiceLifetime.Singleton)} " +
                $"to be registered as {nameof(IHostedService)}.");
    }
}
