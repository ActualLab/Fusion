#if NET5_0_OR_GREATER
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

// Runs the full RpcWebSocketTest suite, but with the client connecting over the
// full-duplex HTTP/2 transport (RpcHttpClient / RpcPipeTransport) instead of WebSockets.
[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcHttpBasicTest : RpcWebSocketTest
{
    public RpcHttpBasicTest(ITestOutputHelper @out) : base(@out)
        => UseHttp = true;
}
#endif
