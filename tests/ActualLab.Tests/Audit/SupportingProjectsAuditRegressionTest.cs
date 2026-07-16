using System.Globalization;
using System.Net;
using System.Text;
using ActualLab.Api;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.RestEase.Internal;
using RestEase;

namespace ActualLab.Tests.Audit;

public class SupportingProjectsAuditRegressionTest
{
    [Fact]
    public void TestIdFormatterShouldIncludeSelectedParts()
    {
        var formatter = new TestIdFormatter("alpha") {
            MachineId = "machine",
            MaxLength = 128,
        };

        formatter.Format(withMachineId: false, withRunId: false)
            .Should().Be("alpha");
    }

    [Fact]
    public async Task FinitePollingSequenceShouldNotHideLastAssertion()
    {
        var attempt = 0;
        Exception? error = null;
        try {
            await TestExt.When(
                () => false.Should().BeTrue($"on attempt {++attempt}"),
                [TimeSpan.Zero, TimeSpan.Zero],
                CancellationToken.None);
        }
        catch (Exception e) {
            error = e;
        }

        error.Should().NotBeNull();
        error!.Message.Should().Contain("on attempt 2");
    }

    [Fact]
    public async Task FiniteAsyncPollingSequenceShouldNotHideLastAssertion()
    {
        var attempt = 0;
        Exception? error = null;
        try {
            await TestExt.When(
                async () => {
                    await Task.Yield();
                    false.Should().BeTrue($"on attempt {++attempt}");
                },
                [TimeSpan.Zero, TimeSpan.Zero],
                CancellationToken.None);
        }
        catch (Exception e) {
            error = e;
        }

        error.Should().NotBeNull();
        error!.Message.Should().Contain("on attempt 2");
    }

    [Fact]
    public void QuerySerializerShouldUseInvariantCultureForSimpleValues()
    {
        var serializer = new ExposedQuerySerializer();
        var oldCulture = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            serializer.SerializeSimple(1.5m).Should().Be("1.5");
        }
        finally {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Fact]
    public void QuerySerializerShouldHonorFormatAndFormatProvider()
    {
        var serializer = new ExposedQuerySerializer();
        var formatProvider = new NumberFormatInfo { NumberDecimalSeparator = "," };

        serializer.SerializeSimple(1.5m, "F2", formatProvider).Should().Be("1,50");
    }

    [Fact]
    public void QuerySerializerShouldIgnoreIndexedProperties()
    {
        var serializer = new ExposedQuerySerializer();

        var result = serializer.SerializeComplex("model", new IndexedModel());

        Assert.Single(result);
        Assert.Equal("value", result["model.Name"]);
    }

    [Fact]
    public void QuerySerializerShouldIgnoreUnreadableProperties()
    {
        var serializer = new ExposedQuerySerializer();

        var result = serializer.SerializeComplex("model", new WriteOnlyModel());

        Assert.Single(result);
        Assert.Equal("value", result["model.Name"]);
    }

    [Fact]
    public async Task ErrorHandlerShouldDisposeRejectedResponse()
    {
        var content = new TrackingContent("failure");
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = content,
        };
        using var services = new ServiceCollection().BuildServiceProvider();
        using var handler = new RestEaseHttpMessageHandler(services) {
            InnerHandler = new ResponseHandler(response),
        };
        using var invoker = new HttpMessageInvoker(handler);

        try {
            await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"), default);
        }
        catch (RemoteException) { }

        content.IsDisposed.Should().BeTrue();
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void OptionConverterShouldConsumeItsWholeDeclaredArray()
    {
        var serializer = NerdbankMessagePackByteSerializer.Default.ToTyped<Option<int>>();
        var data = new byte[] { 0x92, 0x01, 0x02 };

        _ = serializer.Read(data, out var readLength);

        readLength.Should().Be(data.Length);
    }

    [Fact]
    public void ApiOptionConverterShouldConsumeItsWholeDeclaredArray()
    {
        var serializer = NerdbankMessagePackByteSerializer.Default.ToTyped<ApiOption<int>>();
        var data = new byte[] { 0x92, 0x01, 0x02 };

        _ = serializer.Read(data, out var readLength);

        readLength.Should().Be(data.Length);
    }

    [Fact]
    public void RpcStreamConverterShouldConsumeTheWholeObjectId()
    {
        var serializer = NerdbankMessagePackByteSerializer.Default.ToTyped<RpcStream<int>?>();
        var hostId = Guid.NewGuid();
        var data = new List<byte> { 0x81, 0xAC };
        data.AddRange(Encoding.UTF8.GetBytes("SerializedId"));
        data.AddRange([0x93, 0xD9, 0x24]);
        data.AddRange(Encoding.UTF8.GetBytes(hostId.ToString()));
        data.AddRange([0x01, 0x02]);

        var oldContext = RpcInboundContext.Current;
        RpcInboundContext.Current = (RpcInboundContext)RuntimeHelpers.GetUninitializedObject(typeof(RpcInboundContext));
        try {
            var result = serializer.Read(data.ToArray(), out var readLength);

            result!.Id.Should().Be(new RpcObjectId(hostId, 1));
            readLength.Should().Be(data.Count);
        }
        finally {
            RpcInboundContext.Current = oldContext;
        }
    }
#endif

    private sealed class ExposedQuerySerializer : RestEaseRequestQueryParamSerializer
    {
        private static readonly RequestQueryParamSerializerInfo SerializerInfo =
            new(null!, null, CultureInfo.InvariantCulture);

        public string? SerializeSimple(object value)
            => SerializeSimpleType(value, SerializerInfo);

        public string? SerializeSimple(object value, string? format, IFormatProvider? formatProvider)
            => SerializeSimpleType(value, new RequestQueryParamSerializerInfo(null!, format, formatProvider));

        public Dictionary<string, string> SerializeComplex(string name, object value)
            => SerializeComplexType(name, value, SerializerInfo);
    }

    private sealed class IndexedModel
    {
        public string Name => "value";
        public string this[int index] => index.ToString(CultureInfo.InvariantCulture);
    }

    private sealed class WriteOnlyModel
    {
        public string Name => "value";
        public string WriteOnly { set { } }
    }

    private sealed class TrackingContent(string value) : HttpContent
    {
        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return stream.WriteAsync(bytes).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = Encoding.UTF8.GetByteCount(value);
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
