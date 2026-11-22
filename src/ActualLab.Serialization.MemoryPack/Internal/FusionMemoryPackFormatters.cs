using ActualLab.Fusion;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.Extensions;
using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// MemoryPack formatter for <see cref="Session"/>.
/// </summary>
public sealed class SessionMemoryPackFormatter : VersionTolerantMemoryPackFormatter<Session>
{
    private static MemoryPackFormatter<string> StringFormatter
        => field ??= StringAsSymbolMemoryPackFormatterAttribute.Formatter;

    protected override int MemberCount => 1;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, Session value)
    {
        writer.WriteValueWithFormatter(StringFormatter, value.Id);
        offsets[0] = writer.WrittenCount;
    }

    protected override Session ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var id = deltas[0] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<string>, string>(
                StringFormatter)
            : null;
        return new Session(id!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="UserIdentity"/>.
/// </summary>
public sealed class UserIdentityMemoryPackFormatter : VersionTolerantMemoryPackFormatter<UserIdentity>
{
    private static MemoryPackFormatter<string> StringFormatter
        => field ??= StringAsSymbolMemoryPackFormatterAttribute.Formatter;

    protected override int MemberCount => 1;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, UserIdentity value)
    {
        writer.WriteValueWithFormatter(StringFormatter, value.Id);
        offsets[0] = writer.WrittenCount;
    }

    protected override UserIdentity ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var id = deltas[0] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<string>, string>(
                StringFormatter)
            : null;
        return new UserIdentity(id!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="User"/>.
/// </summary>
public sealed class UserMemoryPackFormatter : VersionTolerantMemoryPackFormatter<User>
{
    private static MemoryPackFormatter<string> StringFormatter
        => field ??= StringAsSymbolMemoryPackFormatterAttribute.Formatter;

    protected override int MemberCount => 5;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, User value)
    {
        writer.WriteValueWithFormatter(StringFormatter, value.Id);
        offsets[0] = writer.WrittenCount;
        writer.WriteString(value.Name);
        offsets[1] = writer.WrittenCount;
        writer.WriteUnmanaged(value.Version);
        offsets[2] = writer.WrittenCount;
        writer.WriteValue(value.Claims);
        offsets[3] = writer.WrittenCount;
        writer.WriteValue(value.JsonCompatibleIdentities);
        offsets[4] = writer.WrittenCount;
    }

    protected override User ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var id = deltas[0] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<string>, string>(
                StringFormatter)
            : null;
        var name = deltas[1] != 0 ? reader.ReadString() : null;
        var version = deltas[2] != 0 ? reader.ReadUnmanaged<long>() : 0L;
        var claims = deltas[3] != 0 ? reader.ReadValue<ApiMap<string, string>>() : null;
        var jsonCompatibleIdentities = deltas[4] != 0 ? reader.ReadValue<ApiMap<string, string>>() : null;
        return new User(id!, name!, version, claims!, jsonCompatibleIdentities!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="SessionAuthInfo"/>.
/// </summary>
public sealed class SessionAuthInfoMemoryPackFormatter : VersionTolerantMemoryPackFormatter<SessionAuthInfo>
{
    private static MemoryPackFormatter<string> StringFormatter
        => field ??= StringAsSymbolMemoryPackFormatterAttribute.Formatter;

    protected override int MemberCount => 4;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        SessionAuthInfo value)
    {
        writer.WriteString(value.SessionHash);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.AuthenticatedIdentity);
        offsets[1] = writer.WrittenCount;
        writer.WriteValueWithFormatter(StringFormatter, value.UserId);
        offsets[2] = writer.WrittenCount;
        writer.WriteUnmanaged(value.IsSignOutForced);
        offsets[3] = writer.WrittenCount;
    }

    protected override SessionAuthInfo ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var sessionHash = deltas[0] != 0 ? reader.ReadString() : null;
        var authenticatedIdentity = deltas[1] != 0 ? reader.ReadValue<UserIdentity>() : default;
        var userId = deltas[2] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<string>, string>(
                StringFormatter)
            : null;
        var isSignOutForced = deltas[3] != 0 && reader.ReadUnmanaged<bool>();
        return new SessionAuthInfo() {
            SessionHash = sessionHash!,
            AuthenticatedIdentity = authenticatedIdentity,
            UserId = userId!,
            IsSignOutForced = isSignOutForced,
        };
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="SessionInfo"/>. Its own members start at order 10,
/// so members 4-9 don't exist and are written as empty ones.
/// </summary>
public sealed class SessionInfoMemoryPackFormatter : VersionTolerantMemoryPackFormatter<SessionInfo>
{
    private static MemoryPackFormatter<string> StringFormatter
        => field ??= StringAsSymbolMemoryPackFormatterAttribute.Formatter;

    protected override int MemberCount => 16;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, SessionInfo value)
    {
        writer.WriteString(value.SessionHash);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.AuthenticatedIdentity);
        offsets[1] = writer.WrittenCount;
        writer.WriteValueWithFormatter(StringFormatter, value.UserId);
        offsets[2] = writer.WrittenCount;
        writer.WriteUnmanaged(value.IsSignOutForced);
        offsets[3] = writer.WrittenCount;
        offsets[4..10].Fill(writer.WrittenCount);
        writer.WriteUnmanaged(value.Version);
        offsets[10] = writer.WrittenCount;
        writer.WriteUnmanaged(value.CreatedAt);
        offsets[11] = writer.WrittenCount;
        writer.WriteUnmanaged(value.LastSeenAt);
        offsets[12] = writer.WrittenCount;
        writer.WriteString(value.IPAddress);
        offsets[13] = writer.WrittenCount;
        writer.WriteString(value.UserAgent);
        offsets[14] = writer.WrittenCount;
        writer.WriteValue(value.Options);
        offsets[15] = writer.WrittenCount;
    }

    protected override SessionInfo ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var sessionHash = deltas[0] != 0 ? reader.ReadString() : null;
        var authenticatedIdentity = deltas[1] != 0 ? reader.ReadValue<UserIdentity>() : default;
        var userId = deltas[2] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<string>, string>(
                StringFormatter)
            : null;
        var isSignOutForced = deltas[3] != 0 && reader.ReadUnmanaged<bool>();
        for (var i = 4; i < 10; i++)
            reader.Advance(deltas[i]);
        var version = deltas[10] != 0 ? reader.ReadUnmanaged<long>() : 0L;
        var createdAt = deltas[11] != 0 ? reader.ReadUnmanaged<Moment>() : default;
        var lastSeenAt = deltas[12] != 0 ? reader.ReadUnmanaged<Moment>() : default;
        var ipAddress = deltas[13] != 0 ? reader.ReadString() : null;
        var userAgent = deltas[14] != 0 ? reader.ReadString() : null;
        var options = deltas[15] != 0 ? reader.ReadValue<PropertyBag>() : default;
        return new SessionInfo() {
            SessionHash = sessionHash!,
            AuthenticatedIdentity = authenticatedIdentity,
            UserId = userId!,
            IsSignOutForced = isSignOutForced,
            Version = version,
            CreatedAt = createdAt,
            LastSeenAt = lastSeenAt,
            IPAddress = ipAddress!,
            UserAgent = userAgent!,
            Options = options,
        };
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="Auth_EditUser"/>.
/// </summary>
public sealed class AuthEditUserMemoryPackFormatter : VersionTolerantMemoryPackFormatter<Auth_EditUser>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, Auth_EditUser value)
    {
        writer.WriteValue(value.Session);
        offsets[0] = writer.WrittenCount;
        writer.WriteString(value.Name);
        offsets[1] = writer.WrittenCount;
    }

    protected override Auth_EditUser ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var session = deltas[0] != 0 ? reader.ReadValue<Session>() : null;
        var name = deltas[1] != 0 ? reader.ReadString() : null;
        return new Auth_EditUser(session!, name);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="Auth_SignOut"/>.
/// </summary>
public sealed class AuthSignOutMemoryPackFormatter : VersionTolerantMemoryPackFormatter<Auth_SignOut>
{
    protected override int MemberCount => 4;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, Auth_SignOut value)
    {
        writer.WriteValue(value.Session);
        offsets[0] = writer.WrittenCount;
        writer.WriteString(value.KickUserSessionHash);
        offsets[1] = writer.WrittenCount;
        writer.WriteUnmanaged(value.KickAllUserSessions);
        offsets[2] = writer.WrittenCount;
        writer.WriteUnmanaged(value.Force);
        offsets[3] = writer.WrittenCount;
    }

    protected override Auth_SignOut ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var session = deltas[0] != 0 ? reader.ReadValue<Session>() : null;
        var kickUserSessionHash = deltas[1] != 0 ? reader.ReadString() : null;
        var kickAllUserSessions = deltas[2] != 0 && reader.ReadUnmanaged<bool>();
        var force = deltas[3] != 0 && reader.ReadUnmanaged<bool>();
        return new Auth_SignOut(session!, kickUserSessionHash, kickAllUserSessions, force);
    }
}

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

/// <summary>
/// MemoryPack formatter for <see cref="PageRef{TKey}"/>. <see cref="PageRef{TKey}.After"/> is
/// written by <see cref="OptionMemoryPackFormatter{T}"/> even for an unmanaged <typeparamref name="TKey"/>.
/// </summary>
public sealed class PageRefMemoryPackFormatter<TKey> : VersionTolerantMemoryPackFormatter<PageRef<TKey>>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, PageRef<TKey> value)
    {
        writer.WriteUnmanaged(value.Count);
        offsets[0] = writer.WrittenCount;
        writer.WriteValueWithFormatter(OptionMemoryPackFormatter<TKey>.Default, value.After);
        offsets[1] = writer.WrittenCount;
    }

    protected override PageRef<TKey> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var count = deltas[0] != 0 ? reader.ReadUnmanaged<int>() : 0;
        var after = deltas[1] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<Option<TKey>>, Option<TKey>>(
                OptionMemoryPackFormatter<TKey>.Default)
            : default;
        return new PageRef<TKey>(count, after);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="SandboxedKeyValueStore_Set"/>.
/// </summary>
public sealed class SandboxedKeyValueStoreSetMemoryPackFormatter
    : VersionTolerantMemoryPackFormatter<SandboxedKeyValueStore_Set>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        SandboxedKeyValueStore_Set value)
    {
        writer.WriteValue(value.Session);
        offsets[0] = writer.WrittenCount;
        writer.WriteArray(value.Items);
        offsets[1] = writer.WrittenCount;
    }

    protected override SandboxedKeyValueStore_Set ReadMembers(
        ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var session = deltas[0] != 0 ? reader.ReadValue<Session>() : null;
        var items = deltas[1] != 0 ? reader.ReadArray<(string Key, string Value, Moment? ExpiresAt)>() : null;
        return new SandboxedKeyValueStore_Set(session!, items!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="SandboxedKeyValueStore_Remove"/>.
/// </summary>
public sealed class SandboxedKeyValueStoreRemoveMemoryPackFormatter
    : VersionTolerantMemoryPackFormatter<SandboxedKeyValueStore_Remove>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        SandboxedKeyValueStore_Remove value)
    {
        writer.WriteValue(value.Session);
        offsets[0] = writer.WrittenCount;
        writer.WriteArray(value.Keys);
        offsets[1] = writer.WrittenCount;
    }

    protected override SandboxedKeyValueStore_Remove ReadMembers(
        ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var session = deltas[0] != 0 ? reader.ReadValue<Session>() : null;
        var keys = deltas[1] != 0 ? reader.ReadArray<string>() : null;
        return new SandboxedKeyValueStore_Remove(session!, keys!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="KeyValueStore_Set"/>.
/// </summary>
public sealed class KeyValueStoreSetMemoryPackFormatter : VersionTolerantMemoryPackFormatter<KeyValueStore_Set>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        KeyValueStore_Set value)
    {
        writer.WriteString(value.Shard);
        offsets[0] = writer.WrittenCount;
        writer.WriteArray(value.Items);
        offsets[1] = writer.WrittenCount;
    }

    protected override KeyValueStore_Set ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var shard = deltas[0] != 0 ? reader.ReadString() : null;
        var items = deltas[1] != 0 ? reader.ReadArray<(string Key, string Value, Moment? ExpiresAt)>() : null;
        return new KeyValueStore_Set(shard!, items!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="KeyValueStore_Remove"/>.
/// </summary>
public sealed class KeyValueStoreRemoveMemoryPackFormatter : VersionTolerantMemoryPackFormatter<KeyValueStore_Remove>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        KeyValueStore_Remove value)
    {
        writer.WriteString(value.Shard);
        offsets[0] = writer.WrittenCount;
        writer.WriteArray(value.Keys);
        offsets[1] = writer.WrittenCount;
    }

    protected override KeyValueStore_Remove ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var shard = deltas[0] != 0 ? reader.ReadString() : null;
        var keys = deltas[1] != 0 ? reader.ReadArray<string>() : null;
        return new KeyValueStore_Remove(shard!, keys!);
    }
}
