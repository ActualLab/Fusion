using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stl.Serialization;

namespace Stl.Channels
{
    public static partial class ChannelEx
    {
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
            this Channel<(T Item, ExceptionDispatchInfo? Error)> channel,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var reader = channel.Reader;
            while (await reader.WaitToReadAsync(cancellationToken)) {
                if (!reader.TryRead(out var pair))
                    continue;
                var (item, error) = pair;
                if (error == null)
                    yield return item;
                else
                    error.Throw();
            }
        }

        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
            this Channel<T> channel,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var reader = channel.Reader;
            while (await reader.WaitToReadAsync(cancellationToken)) {
                if (!reader.TryRead(out var item))
                    continue;
                yield return item;
            }
        }

        public static async Task CopyAsync<T>(
            this ChannelReader<T> reader, ChannelWriter<T> writer,
            ChannelCompletionMode channelCompletionMode = ChannelCompletionMode.CompleteAndPropagateError,
            CancellationToken cancellationToken = default)
        {
            try {
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) {
                    if (reader.TryRead(out var value))
                        await writer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
                }
                if ((channelCompletionMode & ChannelCompletionMode.Complete) != 0)
                    writer.TryComplete();
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception e) {
                if (channelCompletionMode == ChannelCompletionMode.CompleteAndPropagateError)
                    writer.TryComplete(e);
                else
                    throw;
            }
        }

        public static Task ConnectAsync<T>(
            this Channel<T> channel1, Channel<T> channel2,
            ChannelCompletionMode channelCompletionMode = ChannelCompletionMode.CompleteAndPropagateError,
            CancellationToken cancellationToken = default)
            => Task.WhenAll(
                Task.Run(() => channel1.Reader.CopyAsync(
                    channel2, channelCompletionMode, cancellationToken), CancellationToken.None),
                Task.Run(() => channel2.Reader.CopyAsync(
                    channel1, channelCompletionMode, cancellationToken), CancellationToken.None)
            );

        public static Task ConnectAsync<T1, T2>(
            this Channel<T1> channel1, Channel<T2> channel2,
            Func<T1, T2> adapter12, Func<T2, T1> adapter21,
            ChannelCompletionMode channelCompletionMode = ChannelCompletionMode.CompleteAndPropagateError,
            CancellationToken cancellationToken = default)
            => Task.WhenAll(
                Task.Run(() => channel1.Reader.TransformAsync(
                    channel2, adapter12, channelCompletionMode, cancellationToken), CancellationToken.None),
                Task.Run(() => channel2.Reader.TransformAsync(
                    channel1, adapter21, channelCompletionMode, cancellationToken), CancellationToken.None)
            );

        public static async Task ConsumeAsync<T>(
            this ChannelReader<T> reader,
            CancellationToken cancellationToken = default)
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                reader.TryRead(out var v);
        }

        public static async Task ConsumeSilentAsync<T>(
            this ChannelReader<T> reader,
            CancellationToken cancellationToken = default)
        {
            try {
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    reader.TryRead(out var v);
            }
            catch {
                // Silent means silent :)
            }
        }

        public static Channel<T> WithSerializers<T, TSerialized>(
            this Channel<TSerialized> downstreamChannel,
            ChannelSerializerPair<T, TSerialized> serializers,
            BoundedChannelOptions? channelOptions = null,
            CancellationToken cancellationToken = default)
            => downstreamChannel.WithSerializers(
                serializers.Serializer, serializers.Deserializer,
                channelOptions, cancellationToken);

        public static Channel<T> WithSerializers<T, TSerialized>(
            this Channel<TSerialized> downstreamChannel,
            ITypedSerializer<T, TSerialized> serializer,
            ITypedSerializer<T, TSerialized> deserializer,
            BoundedChannelOptions? channelOptions = null,
            CancellationToken cancellationToken = default)
        {
            channelOptions ??= new BoundedChannelOptions(16) {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = true,
            };
            var pair = ChannelPair.CreateTwisted(
                Channel.CreateBounded<T>(channelOptions),
                Channel.CreateBounded<T>(channelOptions));

            downstreamChannel.ConnectAsync(pair.Channel1,
                deserializer.Deserialize, serializer.Serialize,
                ChannelCompletionMode.CompleteAndPropagateError,
                cancellationToken);
            return pair.Channel2;
        }

        public static Channel<T> WithLogger<T>(
            this Channel<T> channel,
            string channelName,
            ILogger logger, LogLevel logLevel, int? maxLength = null,
            BoundedChannelOptions? channelOptions = null,
            CancellationToken cancellationToken = default)
        {
            var mustLog = logLevel != LogLevel.None && logger.IsEnabled(logLevel);
            if (!mustLog)
                return channel;

            channelOptions ??= new BoundedChannelOptions(16) {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = true,
            };
            var pair = ChannelPair.CreateTwisted(
                Channel.CreateBounded<T>(channelOptions),
                Channel.CreateBounded<T>(channelOptions));

            T LogMessage(T message, bool isIncoming)
            {
                var text = message?.ToString() ?? "<null>";
                if (maxLength.HasValue && text.Length > maxLength.GetValueOrDefault())
                    text = text.Substring(0, maxLength.GetValueOrDefault()) + "...";
                logger.Log(logLevel, $"{channelName} {(isIncoming ? "<-" : "->")} {text}");
                return message;
            }

            channel.ConnectAsync(pair.Channel1,
                m => LogMessage(m, true),
                m => LogMessage(m, false),
                ChannelCompletionMode.CompleteAndPropagateError,
                cancellationToken);
            return pair.Channel2;
        }

        public static CustomChannelWithId<TId, T> WithId<TId, T>(
            this Channel<T> channel, TId id)
            => new CustomChannelWithId<TId, T>(id, channel.Reader, channel.Writer);
    }
}
