using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stl.Channels;
using Stl.DependencyInjection;
using Stl.Fusion.Bridge;
using Stl.Fusion.Bridge.Messages;
using Stl.Fusion.Internal;
using Stl.Net;
using Stl.Serialization;
using Stl.Text;

namespace Stl.Fusion.Client
{
    public class WebSocketChannelProvider : IChannelProvider, IHasServiceProvider
    {
        public class Options : IOptions
        {
            public Uri BaseUri { get; set; } = new Uri("http://localhost:5000/");
            public string RequestPath { get; set; } = "/fusion/ws";
            public string PublisherIdQueryParameterName { get; set; } = "publisherId";
            public string ClientIdQueryParameterName { get; set; } = "clientId";
            public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);
            public LogLevel? MessageLogLevel { get; set; } = null;
            public int? MessageMaxLength { get; set; } = 2048;
            public Func<IServiceProvider, ChannelSerializerPair<Message, string>> ChannelSerializerPairFactory { get; set; } =
                DefaultChannelSerializerPairFactory;
            public Func<IServiceProvider, ClientWebSocket> ClientWebSocketFactory { get; set; } =
                DefaultClientWebSocketFactory;
            public Func<WebSocketChannelProvider, Symbol, Uri> ConnectionUrlResolver { get; set; } =
                DefaultConnectionUrlResolver;

            public static ChannelSerializerPair<Message, string> DefaultChannelSerializerPairFactory(IServiceProvider services)
                => new ChannelSerializerPair<Message, string>(
                    new SafeJsonNetSerializer(t => typeof(ReplicatorMessage).IsAssignableFrom(t)).ToTyped<Message>(),
                    new JsonNetSerializer().ToTyped<Message>());

            public static ClientWebSocket DefaultClientWebSocketFactory(IServiceProvider services)
                => services?.GetService<ClientWebSocket>() ?? new ClientWebSocket();

            public static Uri DefaultConnectionUrlResolver(WebSocketChannelProvider channelProvider, Symbol publisherId)
            {
                var url = channelProvider.BaseUri.ToString();
                if (url.StartsWith("http://"))
                    url = "ws://" + url.Substring(7);
                else if (url.StartsWith("https://"))
                    url = "wss://" + url.Substring(8);
                if (url.EndsWith("/"))
                    url = url.Substring(0, url.Length - 1);
                url += channelProvider.RequestPath;
                var uriBuilder = new UriBuilder(url);
                var queryTail =
                    $"{channelProvider.PublisherIdQueryParameterName}={publisherId.Value}" +
                    $"&{channelProvider.ClientIdQueryParameterName}={channelProvider.ClientId.Value}";
                if (!string.IsNullOrEmpty(uriBuilder.Query))
                    uriBuilder.Query += "&" + queryTail;
                else
                    uriBuilder.Query = queryTail;
                return uriBuilder.Uri;
            }
        }

        private readonly ILogger _log;

        public Uri BaseUri { get; }
        public string RequestPath { get; }
        public string PublisherIdQueryParameterName { get; }
        public string ClientIdQueryParameterName { get; }
        public TimeSpan ConnectTimeout { get; }
        public IServiceProvider ServiceProvider { get; }

        protected Func<IServiceProvider, ChannelSerializerPair<Message, string>> ChannelSerializerPairFactory { get; }
        protected Func<IServiceProvider, ClientWebSocket> ClientWebSocketFactory { get; }
        public Func<WebSocketChannelProvider, Symbol, Uri> ConnectionUrlResolver { get; }
        protected LogLevel? MessageLogLevel { get; }
        protected int? MessageMaxLength { get; }
        protected Lazy<IReplicator>? ReplicatorLazy { get; }
        protected Symbol ClientId => ReplicatorLazy?.Value.Id ?? Symbol.Empty;

        public WebSocketChannelProvider(
            Options? options,
            IServiceProvider serviceProvider,
            ILogger<WebSocketChannelProvider>? log = null)
        {
            options = options.OrDefault(serviceProvider);
            _log = log ??= NullLogger<WebSocketChannelProvider>.Instance;

            ServiceProvider = serviceProvider;
            BaseUri = options.BaseUri;
            RequestPath = options.RequestPath;
            PublisherIdQueryParameterName = options.PublisherIdQueryParameterName;
            ClientIdQueryParameterName = options.ClientIdQueryParameterName;
            MessageLogLevel = options.MessageLogLevel;
            MessageMaxLength = options.MessageMaxLength;
            ConnectTimeout = options.ConnectTimeout;
            ReplicatorLazy = new Lazy<IReplicator>(serviceProvider.GetRequiredService<IReplicator>);
            ChannelSerializerPairFactory = options.ChannelSerializerPairFactory;
            ClientWebSocketFactory = options.ClientWebSocketFactory;
            ConnectionUrlResolver = options.ConnectionUrlResolver;
        }

        public async Task<Channel<Message>> CreateChannelAsync(
            Symbol publisherId, CancellationToken cancellationToken)
        {
            var clientId = ClientId.Value;
            try {
                var connectionUri = ConnectionUrlResolver.Invoke(this, publisherId);
                _log.LogInformation($"{clientId}: Connecting to {connectionUri}...");
                var ws = ClientWebSocketFactory.Invoke(ServiceProvider);
                using var cts = new CancellationTokenSource(ConnectTimeout);
                using var lts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                await ws.ConnectAsync(connectionUri, lts.Token).ConfigureAwait(false);
                _log.LogInformation($"{clientId}: Connected.");

                var wsChannel = new WebSocketChannel(ws);
                Channel<string> stringChannel = wsChannel;
                if (MessageLogLevel.HasValue)
                    stringChannel = stringChannel.WithLogger(
                        clientId, _log,
                        MessageLogLevel.GetValueOrDefault(),
                        MessageMaxLength);
                var serializers = ChannelSerializerPairFactory.Invoke(ServiceProvider);
                var resultChannel = stringChannel.WithSerializers(serializers);
                wsChannel.WhenCompletedAsync(default).ContinueWith(async _ => {
                    await Task.Delay(1000, default).ConfigureAwait(false);
                    await wsChannel.DisposeAsync().ConfigureAwait(false);
                }, CancellationToken.None).Ignore();
                return resultChannel;
            }
            catch (OperationCanceledException) {
                if (cancellationToken.IsCancellationRequested)
                    throw;
                throw Errors.WebSocketConnectTimeout();
            }
            catch (Exception e) {
                _log.LogError(e, $"{clientId}: Error.");
                throw;
            }
        }
    }
}
