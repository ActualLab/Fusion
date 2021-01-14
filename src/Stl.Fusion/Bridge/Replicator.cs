using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Concurrency;
using Stl.DependencyInjection;
using Stl.Fusion.Bridge.Internal;
using Stl.OS;
using Stl.Generators;
using Stl.Text;

namespace Stl.Fusion.Bridge
{
    public interface IReplicator
    {
        Symbol Id { get; }

        IReplica? TryGet(PublicationRef publicationRef);
        IReplica<T> GetOrAdd<T>(PublicationStateInfo<T> publicationStateInfo, bool requestUpdate = false);

        IState<bool> GetPublisherConnectionState(Symbol publisherId);
    }

    public interface IReplicatorImpl : IReplicator, IHasServices
    {
        IChannelProvider ChannelProvider { get; }
        TimeSpan ReconnectDelay { get; }

        void Subscribe(IReplica replica);
        void OnReplicaDisposed(IReplica replica);
    }

    public class Replicator : AsyncDisposableBase, IReplicatorImpl
    {
        public class Options
        {
            public static Symbol NewId() => "R-" + RandomStringGenerator.Default.Next();

            public Symbol Id { get; set; } = NewId();
            public TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
        }

        protected ConcurrentDictionary<Symbol, ReplicatorChannelProcessor> ChannelProcessors { get; }
        protected Func<Symbol, ReplicatorChannelProcessor> CreateChannelProcessorHandler { get; }
        public Symbol Id { get; }
        public IServiceProvider Services { get; }
        public IChannelProvider ChannelProvider { get; }
        public TimeSpan ReconnectDelay { get; }

        public Replicator(Options? options, IServiceProvider services, IChannelProvider channelProvider)
        {
            options ??= new();
            Id = options.Id;
            ReconnectDelay = options.ReconnectDelay;
            Services = services;
            ChannelProvider = channelProvider;
            ChannelProcessors = new ConcurrentDictionary<Symbol, ReplicatorChannelProcessor>();
            CreateChannelProcessorHandler = CreateChannelProcessor;
        }

        public IReplica? TryGet(PublicationRef publicationRef)
            => ReplicaRegistry.Instance.TryGet(publicationRef);

        public IReplica<T> GetOrAdd<T>(PublicationStateInfo<T> publicationStateInfo, bool requestUpdate = false)
        {
            var (replica, isNew) = ReplicaRegistry.Instance.GetOrRegister(publicationStateInfo.PublicationRef,
                () => new Replica<T>(this, publicationStateInfo, requestUpdate));
            if (isNew)
                Subscribe(replica);
            return (IReplica<T>) replica;
        }

        public IState<bool> GetPublisherConnectionState(Symbol publisherId)
            => ChannelProcessors
                .GetOrAddChecked(publisherId, CreateChannelProcessorHandler)
                .IsConnected;

        protected virtual ReplicatorChannelProcessor GetChannelProcessor(Symbol publisherId)
            => ChannelProcessors
                .GetOrAddChecked(publisherId, CreateChannelProcessorHandler);

        protected virtual ReplicatorChannelProcessor CreateChannelProcessor(Symbol publisherId)
        {
            var channelProcessor = new ReplicatorChannelProcessor(this, publisherId);
            channelProcessor.RunAsync().ContinueWith(_ => {
                // Since ChannelProcessor is AsyncProcessorBase desc.,
                // its disposal will shut down RunAsync as well,
                // so "subscribing" to RunAsync completion is the
                // same as subscribing to its disposal.
                ChannelProcessors.TryRemove(publisherId, channelProcessor);
            });
            return channelProcessor;
        }

        void IReplicatorImpl.Subscribe(IReplica replica)
            => Subscribe(replica);
        protected virtual void Subscribe(IReplica replica)
        {
            if (replica.Replicator != this)
                throw new ArgumentOutOfRangeException(nameof(replica));
            GetChannelProcessor(replica.PublicationRef.PublisherId).Subscribe(replica);
        }

        void IReplicatorImpl.OnReplicaDisposed(IReplica replica)
            => OnReplicaDisposed(replica);
        protected virtual void OnReplicaDisposed(IReplica replica)
        {
            if (replica.Replicator != this)
                throw new ArgumentOutOfRangeException(nameof(replica));
            GetChannelProcessor(replica.PublicationRef.PublisherId).Unsubscribe(replica);
        }

        protected override async ValueTask DisposeInternalAsync(bool disposing)
        {
            var channelProcessors = ChannelProcessors;
            while (!channelProcessors.IsEmpty) {
                var tasks = channelProcessors
                    .Take(HardwareInfo.GetProcessorCountFactor(4, 4))
                    .ToList()
                    .Select(p => {
                        var (_, channelProcessor) = (p.Key, p.Value);
                        return channelProcessor.DisposeAsync().AsTask();
                    });
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            await base.DisposeInternalAsync(disposing).ConfigureAwait(false);
        }
    }
}
