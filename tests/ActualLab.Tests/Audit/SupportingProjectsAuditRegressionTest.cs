using System.Globalization;
using System.Net;
using System.Text;
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
        Exception? error = null;
        try {
            await TestExt.When(
                () => false.Should().BeTrue(),
                [TimeSpan.Zero],
                CancellationToken.None);
        }
        catch (Exception e) {
            error = e;
        }

        error.Should().NotBeNull();
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
    public void QuerySerializerShouldIgnoreIndexedProperties()
    {
        var serializer = new ExposedQuerySerializer();

        var result = serializer.SerializeComplex("model", new IndexedModel());

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
#endif

    private sealed class ExposedQuerySerializer : RestEaseRequestQueryParamSerializer
    {
        private static readonly RequestQueryParamSerializerInfo SerializerInfo =
            new(null!, null, CultureInfo.InvariantCulture);

        public string? SerializeSimple(object value)
            => SerializeSimpleType(value, SerializerInfo);

        public Dictionary<string, string> SerializeComplex(string name, object value)
            => SerializeComplexType(name, value, SerializerInfo);
    }

    private sealed class IndexedModel
    {
        public string Name => "value";
        public string this[int index] => index.ToString(CultureInfo.InvariantCulture);
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
