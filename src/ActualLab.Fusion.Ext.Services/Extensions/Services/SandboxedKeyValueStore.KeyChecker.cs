using ActualLab.Fusion.Extensions.Internal;

namespace ActualLab.Fusion.Extensions.Services;

public partial class SandboxedKeyValueStore<TContext>
{
    /// <summary>
    /// Validates that key prefixes conform to the sandboxed store's
    /// session and user constraints, and enforces expiration limits.
    /// </summary>
    public record KeyChecker
    {
        public MomentClock Clock { get; init; } = null!;
        public string Prefix { get; init; } = "";
        public string? SecondaryPrefix { get; init; }
        public TimeSpan? ExpirationTime { get; init; }
        public TimeSpan? SecondaryExpirationTime { get; init; }

        public virtual void CheckKeyPrefix(string keyPrefix)
        {
            if (MatchesPrefix(keyPrefix, Prefix))
                return;
            if (SecondaryPrefix is not null && MatchesPrefix(keyPrefix, SecondaryPrefix))
                return;

            throw Errors.KeyViolatesSandboxedKeyValueStoreConstraints();
        }

        public virtual void CheckKey(string key)
            => CheckKeyPrefix(key);

        public virtual void CheckKey(string key, ref Moment? expiresAt)
        {
            if (MatchesPrefix(key, Prefix)) {
                if (!ExpirationTime.HasValue)
                    return;
                var maxExpiresAt = Clock.Now + ExpirationTime.GetValueOrDefault();
                expiresAt = expiresAt.HasValue
                    ? Moment.Min(maxExpiresAt, expiresAt.GetValueOrDefault())
                    : maxExpiresAt;
                return;
            }
            if (SecondaryPrefix is not null && MatchesPrefix(key, SecondaryPrefix)) {
                if (!SecondaryExpirationTime.HasValue)
                    return;
                var maxExpiresAt = Clock.Now + SecondaryExpirationTime.GetValueOrDefault();
                expiresAt = expiresAt.HasValue
                    ? Moment.Min(maxExpiresAt, expiresAt.GetValueOrDefault())
                    : maxExpiresAt;
                return;
            }
            throw Errors.KeyViolatesSandboxedKeyValueStoreConstraints();
        }

        private static bool MatchesPrefix(string key, string prefix)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            if (prefix.Length == 0
                || key.Length == prefix.Length
                || prefix[prefix.Length - 1] == KeyValueStoreExt.Delimiter)
                return true;

            return key[prefix.Length] == KeyValueStoreExt.Delimiter;
        }
    }
}
