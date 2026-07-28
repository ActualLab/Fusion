using System.Globalization;
using System.Security.Cryptography;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Tests.Rpc;

[Trait("Category", "Rpc")]
public class RpcReconnectProofTest(ITestOutputHelper @out) : TestBase(@out)
{
    // PINNED CROSS-RUNTIME VECTOR - the exact same triples are asserted by the TypeScript client's
    // ts/packages/rpc/tests/rpc-reconnect-proof.test.ts. Any implementation of the reconnect proof
    // must reproduce them, or the two runtimes silently disagree and every reconnect from that
    // client is rejected. key = UTF8(secret), i.e. the secret is an opaque token and is NOT
    // base64url-decoded; message = UTF8(clientId + "\n" + counterText) with a single 0x0A.
    public const string VectorSecret = "xlHGbajpOkxzI-yS7ZqjKRzncF4sC25YezNcdQD9yOI";
    public const string VectorClientId = "x7FTKcK88zakKdYBij3p-w";
    public const string VectorCounterText = "1";
    public const string VectorProof = "-F2GMXBbj8WkL_gwF9KkzNwbIodkryKUXf_hE1__HjU";

    [Theory]
    [InlineData("1", "-F2GMXBbj8WkL_gwF9KkzNwbIodkryKUXf_hE1__HjU")]
    [InlineData("2", "kK7heeSM_I3uNW-kR0cpeBGG_kx-G-q3w0PMwWevJNM")]
    [InlineData("1234567890", "LPhpcJJw1fVbqvuYdKOExV2ONdvo--7d63SAXUnS7ac")]
    public void PinnedTestVector(string counterText, string proof)
    {
        RpcReconnectProof.Compute(VectorSecret, VectorClientId, counterText).Should().Be(proof);
        RpcReconnectProof.Verify(VectorSecret, VectorClientId, counterText, proof).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(43)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(200)]
    public void MatchesBclHmacSha256(int secretLength)
    {
        // RpcReconnectProof spells HMAC out over SHA256 because HMACSHA256 is a
        // PlatformNotSupportedException stub on net5.0/net6.0 browser-wasm. This is what keeps
        // that hand-written construction honest - including the key-longer-than-a-block branch.
        var secret = new string('s', secretLength);
        var counterText = "7";
        var message = EncodingExt.Utf8NoBom.GetBytes($"{VectorClientId}\n{counterText}");
        using var hmac = new HMACSHA256(EncodingExt.Utf8NoBom.GetBytes(secret));
        var expected = Base64UrlEncoder.Encode(hmac.ComputeHash(message));

        RpcReconnectProof.Compute(secret, VectorClientId, counterText).Should().Be(expected);
    }

    [Fact]
    public void NewSecretIsUnpaddedBase64UrlOf32Bytes()
    {
        var secrets = Enumerable.Range(0, 32).Select(_ => RpcReconnectProof.NewSecret()).ToArray();
        foreach (var secret in secrets) {
            secret.Length.Should().Be(43);
            secret.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
        }
        secrets.Distinct(StringComparer.Ordinal).Should().HaveCount(secrets.Length);
    }

    [Fact]
    public void ProofIsUnpaddedBase64Url()
        => VectorProof.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");

    [Theory]
    [InlineData("")]
    [InlineData("!!!")]
    [InlineData("short")]
    [InlineData("-F2GMXBbj8WkL_gwF9KkzNwbIodkryKUXf_hE1__Hj")] // One char too short
    [InlineData("-F2GMXBbj8WkL_gwF9KkzNwbIodkryKUXf_hE1__HjUA")] // One char too long
    [InlineData("aF2GMXBbj8WkL_gwF9KkzNwbIodkryKUXf_hE1__HjU")] // First char flipped
    [InlineData("-F2HMXBbj8WkL_gwF9KkzNwbIodkryKUXf_hE1__HjU")] // A middle char flipped
    public void VerifyRejectsMalformedOrTamperedProof(string proof)
        => RpcReconnectProof.Verify(VectorSecret, VectorClientId, VectorCounterText, proof)
            .Should().BeFalse();

    [Fact]
    public void VerifyRejectsMismatchedInputs()
    {
        RpcReconnectProof.Verify(RpcReconnectProof.NewSecret(), VectorClientId, VectorCounterText, VectorProof)
            .Should().BeFalse();
        RpcReconnectProof.Verify(VectorSecret, VectorClientId + "x", VectorCounterText, VectorProof)
            .Should().BeFalse();
        RpcReconnectProof.Verify(VectorSecret, VectorClientId, "43", VectorProof)
            .Should().BeFalse();
        // The counter text is hashed exactly as it arrives, so "042" is not "42"
        RpcReconnectProof.Verify(VectorSecret, VectorClientId, "042", VectorProof)
            .Should().BeFalse();
    }

    // TryVerify - the policy every server endpoint shares

    [Fact]
    public void UnknownPeerNeedsNoProof()
    {
        // D8: a first connect can't carry a proof, and neither can a client that reached
        // another replica - so garbage c/p on an unknown clientId must still be accepted.
        RpcReconnectProof.TryVerify(null, VectorClientId, "", "", requireProof: true)
            .Should().BeTrue();
        RpcReconnectProof.TryVerify(null, VectorClientId, "not-a-number", "!!!", requireProof: true)
            .Should().BeTrue();
    }

    [Fact]
    public async Task LegacyRequestIsGatedByRequireReconnectProof()
    {
        await using var services = NewServices();
        var peer = NewServerPeer(services);

        RpcReconnectProof.TryVerify(peer, peer.Ref.HostInfo, "", "", requireProof: false)
            .Should().BeTrue();
        RpcReconnectProof.TryVerify(peer, peer.Ref.HostInfo, "", "", requireProof: true)
            .Should().BeFalse();
        peer.LastCounter.Should().Be(0);
    }

    [Fact]
    public async Task ValidProofAdvancesTheCounterAndIsNotReplayable()
    {
        await using var services = NewServices();
        var peer = NewServerPeer(services);
        var clientId = peer.Ref.HostInfo;

        Verify(5).Should().BeTrue();
        peer.LastCounter.Should().Be(5);
        Verify(5).Should().BeFalse(); // Replay of a used counter
        Verify(4).Should().BeFalse(); // Older counter
        peer.LastCounter.Should().Be(5);
        Verify(6).Should().BeTrue();
        peer.LastCounter.Should().Be(6);
        return;

        bool Verify(long counter) {
            var counterText = counter.ToString(CultureInfo.InvariantCulture);
            var proof = RpcReconnectProof.Compute(peer.Secret, clientId, counterText);
            return RpcReconnectProof.TryVerify(peer, clientId, counterText, proof, requireProof: true);
        }
    }

    [Fact]
    public async Task ForgedProofIsRejectedWithoutBurningCounterSpace()
    {
        await using var services = NewServices();
        var peer = NewServerPeer(services);
        var clientId = peer.Ref.HostInfo;
        var forgedProof = RpcReconnectProof.Compute(RpcReconnectProof.NewSecret(), clientId, "9");

        RpcReconnectProof.TryVerify(peer, clientId, "9", forgedProof, requireProof: true)
            .Should().BeFalse();
        peer.LastCounter.Should().Be(0);

        var validProof = RpcReconnectProof.Compute(peer.Secret, clientId, "9");
        RpcReconnectProof.TryVerify(peer, clientId, "9", validProof, requireProof: true)
            .Should().BeTrue();
    }

    [Fact]
    public async Task HalfPresentProofPairIsMalformed()
    {
        await using var services = NewServices();
        var peer = NewServerPeer(services);
        var clientId = peer.Ref.HostInfo;
        var proof = RpcReconnectProof.Compute(peer.Secret, clientId, "1");

        RpcReconnectProof.TryVerify(peer, clientId, "1", "", requireProof: false)
            .Should().BeFalse();
        RpcReconnectProof.TryVerify(peer, clientId, "", proof, requireProof: false)
            .Should().BeFalse();
        peer.LastCounter.Should().Be(0);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1e3")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1,000")]
    [InlineData("9223372036854775808")] // long.MaxValue + 1
    public async Task NonCanonicalCounterIsRejected(string counterText)
    {
        await using var services = NewServices();
        var peer = NewServerPeer(services);
        var clientId = peer.Ref.HostInfo;
        var proof = RpcReconnectProof.Compute(peer.Secret, clientId, counterText);

        RpcReconnectProof.TryVerify(peer, clientId, counterText, proof, requireProof: false)
            .Should().BeFalse();
        peer.LastCounter.Should().Be(0);
    }

    // TryAdvanceCounter

    [Fact]
    public async Task TryAdvanceCounterAdmitsExactlyOneRacerForTheSameValue()
    {
        await using var services = NewServices();
        var peer = NewServerPeer(services);
        var winCount = 0;

        await Task.WhenAll(Enumerable.Range(0, 256).Select(_ => Task.Run(() => {
            if (peer.TryAdvanceCounter(1))
                Interlocked.Increment(ref winCount);
        })));

        winCount.Should().Be(1);
        peer.LastCounter.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentCountersConvergeOnTheHighestOne()
    {
        await using var services = NewServices();
        var peer = NewServerPeer(services);
        var accepted = new ConcurrentBag<long>();

        await Task.WhenAll(Enumerable.Range(1, 32).Select(counter => Task.Run(() => {
            if (peer.TryAdvanceCounter(counter))
                accepted.Add(counter);
        })));

        peer.LastCounter.Should().Be(32);
        accepted.Should().Contain(32); // Nothing can outrank it, so it's always admitted
        accepted.Max().Should().Be(32);
        accepted.Distinct().Should().HaveCount(accepted.Count);
    }

    // Connect URL

    [Fact]
    public async Task ConnectUrlCarriesNoProofUntilASecretArrives()
    {
        await using var services = NewServices(withWebSocketClientOptions: true);
        var peer = NewClientPeer(services);
        var options = services.GetRequiredService<RpcWebSocketClientOptions>();

        var url = options.ConnectionUriResolver.Invoke(peer)!.ToString();
        Out.WriteLine(url);

        url.Should().NotContain("&c=");
        url.Should().NotContain("&p=");
    }

    [Fact]
    public async Task ConnectUrlProofAdvancesOncePerAttempt()
    {
        await using var services = NewServices(withWebSocketClientOptions: true);
        var peer = NewClientPeer(services);
        var secret = RpcReconnectProof.NewSecret();
        peer.ApplyHandshake(secret);
        var options = services.GetRequiredService<RpcWebSocketClientOptions>();

        for (var counter = 1; counter <= 3; counter++) {
            var query = options.ConnectionUriResolver.Invoke(peer)!.Query;
            Out.WriteLine(query);
            var counterText = counter.ToString(CultureInfo.InvariantCulture);
            query.Should().Contain($"&c={counterText}&p=");

            var proof = query[(query.IndexOf("&p=", StringComparison.Ordinal) + 3)..];
            RpcReconnectProof.Verify(secret, peer.ClientId, counterText, proof).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ClientKeepsTheLastSecretItWasGiven()
    {
        await using var services = NewServices();
        var peer = NewClientPeer(services);
        var firstSecret = RpcReconnectProof.NewSecret();
        var secondSecret = RpcReconnectProof.NewSecret();

        peer.ApplyHandshake(firstSecret);
        peer.Secret.Should().Be(firstSecret);
        peer.ApplyHandshake(null); // A legacy server leaves the stored secret alone
        peer.Secret.Should().Be(firstSecret);
        peer.ApplyHandshake(secondSecret); // A different server instance replaces it
        peer.Secret.Should().Be(secondSecret);
    }

    // RpcHandshake

    [Fact]
    public void HandshakeToStringRedactsTheSecret()
    {
        const string secret = "s3cr3t-must-never-reach-the-log";
        var handshake = new RpcHandshake(
            Guid.NewGuid(), VersionSet.Empty, Guid.NewGuid(),
            RpcHandshake.CurrentProtocolVersion, 1, secret);
        var text = handshake.ToString();
        Out.WriteLine(text);

        text.Should().NotContain(secret);
        text.Should().Contain("Secret = <redacted>");
        (handshake with { Secret = null }).ToString().Should().Contain("Secret = null");
    }

    // Private methods

    private static ServiceProvider NewServices(bool withWebSocketClientOptions = false)
    {
        var services = new ServiceCollection();
        services.AddRpc();
        if (withWebSocketClientOptions)
            services.AddSingleton(_ => new RpcWebSocketClientOptions() {
                HostUrlResolver = _ => "wss://test-host",
            });
        return services.BuildServiceProvider();
    }

    private static RpcServerPeer NewServerPeer(IServiceProvider services)
        => new(services.RpcHub(), RpcRef.NewServer("test-client-id").Route);

    private static TestClientPeer NewClientPeer(IServiceProvider services)
        => new(services.RpcHub(), RpcRef.NewClient("wss://test-host").Route);

    // Nested types

    private sealed class TestClientPeer(RpcHub hub, RpcRoute route) : RpcClientPeer(hub, route)
    {
        public void ApplyHandshake(string? secret)
            => OnHandshake(new RpcHandshake(
                Guid.NewGuid(), VersionSet.Empty, Guid.NewGuid(),
                RpcHandshake.CurrentProtocolVersion, 1, secret));
    }
}
