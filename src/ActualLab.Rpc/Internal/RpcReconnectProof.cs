using System.Globalization;
using System.Security.Cryptography;

namespace ActualLab.Rpc.Internal;

/// <summary>
/// Proof-of-possession for RPC reconnects: the server mints a per-<see cref="RpcServerPeer"/>
/// <c>Secret</c> and ships it inside its <see cref="Infrastructure.RpcHandshake"/>; the client
/// proves it holds that secret on every subsequent connect by signing a monotonic counter.
/// </summary>
/// <remarks>
/// The construction is pinned by a cross-runtime test vector (see <c>RpcReconnectProofTest</c>)
/// and mirrored by the TypeScript client, so it must not change:
/// <code>
/// key     = UTF8(secret)
/// message = UTF8(clientId + "\n" + counterText)
/// proof   = Base64Url_NoPad(HMAC_SHA256(key, message))
/// </code>
/// </remarks>
public static class RpcReconnectProof
{
    private const int BlockByteCount = 64; // SHA-256 block size, i.e. the HMAC key block

    public const int SecretByteCount = 32;
    public const int ProofByteCount = 32;
    public const char SeparatorChar = '\n';

    public static string NewSecret()
    {
        // The static RandomNumberGenerator.GetBytes(int) only exists from net6.0 on, so this takes
        // the instance API that every TFM has. It runs once per server peer, so it costs nothing.
        var secret = new byte[SecretByteCount];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(secret);
        return Base64UrlEncoder.Encode(secret);
    }

    public static string Compute(string secret, string clientId, string counterText)
        => Base64UrlEncoder.Encode(ComputeHash(secret, clientId, counterText));

    public static bool Verify(string secret, string clientId, string counterText, string proof)
    {
        var actual = TryDecodeProof(proof);
        if (actual is null)
            return false;

        var expected = ComputeHash(secret, clientId, counterText);
        return FixedTimeEquals(expected, actual);
    }

    // The gate policy shared by every RPC server endpoint - keep it here rather than in the
    // endpoints, so ASP.NET Core WebSocket, ASP.NET Core HTTP/2 and OWIN can't drift apart.
    // A null peer means the clientId is unknown, i.e. there is nothing to hijack yet.
    public static bool TryVerify(
        RpcServerPeer? peer,
        string clientId,
        string counterText,
        string proof,
        bool requireProof)
    {
        if (peer is null)
            return true;

        var hasProof = counterText.Length != 0 || proof.Length != 0;
        if (!hasProof) {
            // LastSeenReconnectCounter is advanced only by a verified proof, so a non-zero one means this peer's
            // client has already proven possession at least once - it can't credibly stop now.
            // Refusing the downgrade is what makes the gate protect anything while RequireProof is
            // false: without it, omitting c and p is enough to be treated as a legacy client.
            // A genuinely old client never advances the counter, so it stays on the legacy path.
            return !requireProof && peer.LastSeenReconnectCounter == 0;
        }
        if (counterText.Length == 0 || proof.Length == 0)
            return false; // Exactly one of the two - malformed
        if (!long.TryParse(counterText, NumberStyles.None, CultureInfo.InvariantCulture, out var counter)
            || counter <= 0)
            return false;
        if (!Verify(peer.ReconnectSecret, clientId, counterText, proof))
            return false;

        // Advanced only after the proof verifies, so a forged request can't burn a legitimate
        // client's counter space. A false here means the counter was already used, i.e. a replay.
        return peer.TryAdvanceReconnectCounter(counter);
    }

    // Private methods

    private static byte[] ComputeHash(string secret, string clientId, string counterText)
    {
        var key = EncodingExt.Utf8NoBom.GetBytes(secret);
        var message = EncodingExt.Utf8NoBom.GetBytes($"{clientId}{SeparatorChar}{counterText}");
        return ComputeHmacSha256(key, message);
    }

    // RFC 2104 HMAC over SHA-256, spelled out rather than delegated to System.Security.Cryptography
    // .HMACSHA256. That type is a pure PlatformNotSupportedException stub on net5.0 and net6.0
    // browser-wasm - verified by decompiling the runtime packs, see tmp/review-r2/WASM-crypto-coverage.md:
    // those runtimes ship a managed hash provider but no managed MAC provider (that arrives in net7.0).
    // Using it there would break every Blazor WASM 5/6 client's reconnect with an exception thrown
    // from the connect URL resolver. SHA256 itself is verified managed on every TFM Fusion ships.
    // One implementation on all TFMs, rather than a net5/net6-only fallback, so the code every host
    // runs is also the code the test suite covers - RpcReconnectProofTest checks it against the BCL
    // HMACSHA256 across key lengths that exercise both the padding and the key-too-long branch.
    // CA1850: the static SHA256.HashData isn't the code path WASM-crypto-coverage.md verified as
    // managed on net5.0/net6.0 browser-wasm - SHA256.Create() + ComputeHash is. Staying on the
    // verified path is the entire point of this method.
#pragma warning disable CA1850
    private static byte[] ComputeHmacSha256(byte[] key, byte[] message)
    {
        using var sha256 = SHA256.Create();
        if (key.Length > BlockByteCount)
            key = sha256.ComputeHash(key);

        var innerInput = new byte[BlockByteCount + message.Length];
        var outerInput = new byte[BlockByteCount + ProofByteCount];
        for (var i = 0; i < BlockByteCount; i++) {
            var keyByte = i < key.Length ? key[i] : (byte)0;
            innerInput[i] = (byte)(keyByte ^ 0x36);
            outerInput[i] = (byte)(keyByte ^ 0x5C);
        }
        message.CopyTo(innerInput, BlockByteCount);
        // ComputeHash re-initializes the algorithm, so the same instance is reusable
        sha256.ComputeHash(innerInput).CopyTo(outerInput, BlockByteCount);
        return sha256.ComputeHash(outerInput);
    }
#pragma warning restore CA1850

    private static byte[]? TryDecodeProof(string proof)
    {
        if (proof.Length == 0)
            return null;

        try {
            var bytes = Base64UrlEncoder.Decode(proof);
            return bytes.Length == ProofByteCount ? bytes.ToArray() : null;
        }
        catch (ArgumentOutOfRangeException) {
            return null;
        }
    }

#if NETSTANDARD2_0
    // CryptographicOperations.FixedTimeEquals doesn't exist on netstandard2.0 (i.e. on net472/net48,
    // which is what ActualLab.Rpc.Server.NetFx binds to), so this is the required fallback.
    // NoInlining | NoOptimization keeps the JIT from short-circuiting the loop.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
            return false;

        var accumulator = 0;
        for (var i = 0; i < left.Length; i++)
            accumulator |= left[i] ^ right[i];
        return accumulator == 0;
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        => CryptographicOperations.FixedTimeEquals(left, right);
#endif
}
