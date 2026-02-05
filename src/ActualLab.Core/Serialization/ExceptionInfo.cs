using MessagePack;
using Errors = ActualLab.Serialization.Internal.Errors;

namespace ActualLab.Serialization;

/// <summary>
/// A serializable representation of an exception's type and message,
/// used for cross-process exception propagation.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public readonly partial struct ExceptionInfo : IEquatable<ExceptionInfo>
{
    private static readonly Type[] ExceptionCtorArgumentTypes1 = { typeof(string), typeof(Exception) };
    private static readonly Type[] ExceptionCtorArgumentTypes2 = { typeof(string) };

    public static readonly ExceptionInfo None = default;
    public static Func<TypeRef, Type>? UnknownExceptionTypeResolver { get; set; } = null;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public TypeRef TypeRef { get; }
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public string Message => field ?? "";
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsNone => TypeRef.AssemblyQualifiedName.IsNullOrEmpty();

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public ExceptionInfo(TypeRef typeRef, string? message)
    {
        TypeRef = typeRef;
        Message = message ?? "";
    }

    public ExceptionInfo(Exception? exception)
    {
        if (exception is null) {
            TypeRef = default;
            Message = "";
        } else {
            TypeRef = new TypeRef(exception.GetType()).WithoutAssemblyVersions();
            Message = exception.Message;
        }
    }

    public override string ToString()
        => IsNone
            ? $"{GetType().Name}()"
            : $"{GetType().Name}({TypeRef.ToString()}, {JsonFormatter.Format(Message)})";

    public Exception? ToException()
    {
        if (IsNone)
            return null;

        try {
            return TryCreateException(this) ?? Errors.RemoteException(this);
        }
        catch (Exception) {
            return Errors.RemoteException(this);
        }
    }

    // Conversion

    public static implicit operator ExceptionInfo(Exception exception)
        => new(exception);

    // Equality

    public bool Equals(ExceptionInfo other)
        => TypeRef.Equals(other.TypeRef)
            && string.Equals(Message, other.Message, StringComparison.Ordinal);
    public override bool Equals(object? obj)
        => obj is ExceptionInfo other && Equals(other);
    public override int GetHashCode()
        => HashCode.Combine(TypeRef, StringComparer.Ordinal.GetHashCode(Message));
    public static bool operator ==(ExceptionInfo left, ExceptionInfo right)
        => left.Equals(right);
    public static bool operator !=(ExceptionInfo left, ExceptionInfo right)
        => !left.Equals(right);

    // Private methods

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The method returns null in case the exception can't be constructed")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The method returns null in case the exception can't be constructed")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The method returns null in case the exception can't be constructed")]
    private static Exception? TryCreateException(ExceptionInfo exceptionInfo)
    {
        if (exceptionInfo.IsNone)
            return null;

        var (type, message) = (exceptionInfo.TypeRef.TryResolve(), exceptionInfo.Message);
        type ??= UnknownExceptionTypeResolver?.Invoke(exceptionInfo.TypeRef);
        if (type is null || !typeof(Exception).IsAssignableFrom(type))
            return null;

        var ctor = type.GetConstructor(ExceptionCtorArgumentTypes1);
        if (ctor is not null) {
            try {
                return (Exception)type.CreateInstance(message, (Exception?) null);
            }
            catch {
                // Intended
            }
        }

        ctor = type.GetConstructor(ExceptionCtorArgumentTypes2);
        if (ctor is null)
            return null;

        var parameter = ctor.GetParameters().SingleOrDefault();
        if (!string.Equals("message", parameter?.Name ?? "", StringComparison.Ordinal))
            return null;

        try {
            return (Exception)type.CreateInstance(message);
        }
        catch {
            return null;
        }
    }
}
