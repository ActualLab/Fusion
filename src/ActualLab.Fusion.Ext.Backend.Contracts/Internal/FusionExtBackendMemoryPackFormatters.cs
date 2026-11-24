#if !NETSTANDARD2_0
using ActualLab.Fusion.Authentication;
using ActualLab.Text;
using MemoryPack;
using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// MemoryPack formatter for <see cref="AuthBackend_SetSessionOptions"/>.
/// </summary>
public sealed class AuthBackendSetSessionOptionsMemoryPackFormatter
    : VersionTolerantMemoryPackFormatter<AuthBackend_SetSessionOptions>
{
    protected override int MemberCount => 3;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        AuthBackend_SetSessionOptions value)
    {
        writer.WriteValue(value.Session);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.Options);
        offsets[1] = writer.WrittenCount;
        writer.DangerousWriteUnmanaged(value.ExpectedVersion);
        offsets[2] = writer.WrittenCount;
    }

    protected override AuthBackend_SetSessionOptions ReadMembers(
        ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var session = deltas[0] != 0 ? reader.ReadValue<Session>() : null;
        var options = deltas[1] != 0 ? reader.ReadValue<PropertyBag>() : default;
        long? expectedVersion = null;
        if (deltas[2] != 0)
            reader.DangerousReadUnmanaged(out expectedVersion);
        return new AuthBackend_SetSessionOptions(session!, options, expectedVersion);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="AuthBackend_SetupSession"/>.
/// </summary>
public sealed class AuthBackendSetupSessionMemoryPackFormatter
    : VersionTolerantMemoryPackFormatter<AuthBackend_SetupSession>
{
    protected override int MemberCount => 4;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        AuthBackend_SetupSession value)
    {
        writer.WriteValue(value.Session);
        offsets[0] = writer.WrittenCount;
        writer.WriteString(value.IPAddress);
        offsets[1] = writer.WrittenCount;
        writer.WriteString(value.UserAgent);
        offsets[2] = writer.WrittenCount;
        writer.WriteValue(value.Options);
        offsets[3] = writer.WrittenCount;
    }

    protected override AuthBackend_SetupSession ReadMembers(
        ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var session = deltas[0] != 0 ? reader.ReadValue<Session>() : null;
        var ipAddress = deltas[1] != 0 ? reader.ReadString() : null;
        var userAgent = deltas[2] != 0 ? reader.ReadString() : null;
        var options = deltas[3] != 0 ? reader.ReadValue<PropertyBag>() : default;
        return new AuthBackend_SetupSession(session!, ipAddress!, userAgent!, options);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="AuthBackend_SignIn"/>.
/// </summary>
public sealed class AuthBackendSignInMemoryPackFormatter : VersionTolerantMemoryPackFormatter<AuthBackend_SignIn>
{
    protected override int MemberCount => 3;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        AuthBackend_SignIn value)
    {
        writer.WriteValue(value.Session);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.User);
        offsets[1] = writer.WrittenCount;
        writer.WriteValue(value.AuthenticatedIdentity);
        offsets[2] = writer.WrittenCount;
    }

    protected override AuthBackend_SignIn ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var session = deltas[0] != 0 ? reader.ReadValue<Session>() : null;
        var user = deltas[1] != 0 ? reader.ReadValue<User>() : null;
        var authenticatedIdentity = deltas[2] != 0 ? reader.ReadValue<UserIdentity>() : default;
        return new AuthBackend_SignIn(session!, user!, authenticatedIdentity);
    }
}
#endif
