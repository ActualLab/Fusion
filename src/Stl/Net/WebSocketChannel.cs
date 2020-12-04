using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Stl.Async;

namespace Stl.Net
{
    public class WebSocketChannel : Channel<string>, IAsyncDisposable
    {
        public const int DefaultReadBufferSize = 16_384;
        public const int DefaultWriteBufferSize = 16_384;

        protected int ReadBufferSize { get; }
        protected int WriteBufferSize { get; }
        protected Channel<string> ReadChannel { get; }
        protected Channel<string> WriteChannel { get; }
        protected volatile CancellationTokenSource StopCts;
        protected readonly CancellationToken StopToken;

        public WebSocket WebSocket { get; }
        public bool OwnsWebSocket { get; }
        public Task ReaderTask { get; }
        public Task WriterTask { get; }
        public Exception? ReaderError { get; protected set; }
        public Exception? WriterError { get; protected set; }

        public WebSocketChannel(WebSocket webSocket,
            int readBufferSize = DefaultReadBufferSize,
            int writeBufferSize = DefaultWriteBufferSize,
            Channel<string>? readChannel = null,
            Channel<string>? writeChannel = null,
            bool ownsWebSocket = true,
            BoundedChannelOptions? channelOptions = null
        )
        {
            ReadBufferSize = readBufferSize;
            WriteBufferSize = writeBufferSize;
            channelOptions ??= new BoundedChannelOptions(16) {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = true,
            };
            readChannel ??= Channel.CreateBounded<string>(channelOptions);
            writeChannel ??= Channel.CreateBounded<string>(channelOptions);
            WebSocket = webSocket;
            ReadChannel = readChannel;
            WriteChannel = writeChannel;
            Reader = readChannel.Reader;
            Writer = writeChannel.Writer;
            OwnsWebSocket = ownsWebSocket;

            StopCts = new CancellationTokenSource();
            var cancellationToken = StopCts.Token;
            ReaderTask = Task.Run(() => RunReaderAsync(cancellationToken));
            WriterTask = Task.Run(() => RunWriterAsync(cancellationToken));
        }

        public async ValueTask DisposeAsync()
        {
            var stopCts = Interlocked.Exchange(ref StopCts, null!);
            if (stopCts != null)
                return;

            try {
                StopCts.Cancel();
            }
            catch {
                // Dispose shouldn't throw exceptions
            }
            try {
                await WhenCompletedAsync(default).ConfigureAwait(false);
            }
            catch {
                // Dispose shouldn't throw exceptions
            }
            if (OwnsWebSocket)
                WebSocket.Dispose();
        }

        public Task WhenCompletedAsync(CancellationToken cancellationToken = default)
            => Task.WhenAll(ReaderTask, WriterTask).WithFakeCancellation(cancellationToken);

        protected virtual async Task TryCloseWebSocketAsync(CancellationToken cancellationToken)
        {
            var status = WebSocketCloseStatus.NormalClosure;
            var message = "Ok.";

            var error = ReaderError ?? WriterError;
            if (error != null) {
                status = WebSocketCloseStatus.InternalServerError;
                message = "Internal Server Error.";
            }

            await WebSocket.CloseAsync(status, message, cancellationToken).ConfigureAwait(false);
        }

        protected async Task RunReaderAsync(CancellationToken cancellationToken)
        {
            var error = (Exception?) null;
            try {
                await RunReaderAsyncUnsafe(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) {
                error = e;
                if (!(e is OperationCanceledException))
                    ReaderError = e;
                throw;
            }
            finally {
                ReadChannel.Writer.TryComplete(error);
            }
        }

        protected async Task RunWriterAsync(CancellationToken cancellationToken)
        {
            try {
                await RunWriterAsyncUnsafe(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) {
                if (!(e is OperationCanceledException))
                    WriterError = e;
                throw;
            }
            finally {
                if (OwnsWebSocket)
                    await TryCloseWebSocketAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        protected virtual async Task RunReaderAsyncUnsafe(CancellationToken cancellationToken)
        {
            using var bytesOwner = MemoryPool<byte>.Shared.Rent(ReadBufferSize);
            using var charsOwner = MemoryPool<char>.Shared.Rent(ReadBufferSize);

            var decoder = Encoding.UTF8.GetDecoder();
            var writer = ReadChannel.Writer;
            var decodedPart = (StringBuilder?) null;
            var mBytes = bytesOwner.Memory;
            var mChars = charsOwner.Memory;
            var mFreeBytes = mBytes;

            while (true) {
                var r = await WebSocket.ReceiveAsync(mFreeBytes, cancellationToken).ConfigureAwait(false);
                switch (r.MessageType) {
                case WebSocketMessageType.Binary:
                    // We skip binary messages
                    continue;
                case WebSocketMessageType.Close:
                    // Nothing else to do
                    return;
                case WebSocketMessageType.Text:
                    // Let's break from "switch" to process it
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
                }

                string? TryDecodeMessage()
                {
                    var freeChars = mChars.Span;
                    var readBytes = mFreeBytes.Span.Slice(0, r.Count);
                    decoder.Convert(readBytes, freeChars, r.EndOfMessage, out var usedByteCount, out var usedCharCount, out var completed);
                    Debug.Assert(completed);
                    var readChars = freeChars.Slice(0, usedCharCount);
                    var undecoded = readBytes.Slice(usedByteCount);

                    if (decodedPart != null) {
                        decodedPart.Append(readChars);
                        if (r.EndOfMessage) {
                            Debug.Assert(undecoded.Length == 0);
                            var message = decodedPart.ToString();
                            decodedPart = null;
                            return message;
                        }

                        undecoded.CopyTo(mBytes.Span);
                        mFreeBytes = mBytes.Slice(undecoded.Length);
                        return null;
                    }

                    if (r.EndOfMessage)
                        return new string(readChars);

                    decodedPart = new StringBuilder(readChars.Length);
                    decodedPart.Append(readChars);
                    undecoded.CopyTo(mBytes.Span);
                    mFreeBytes = mBytes.Slice(undecoded.Length);
                    return null;
                }

                var result = TryDecodeMessage();
                if (result != null)
                    await writer.WriteAsync(result, cancellationToken).ConfigureAwait(false);
            }
        }

        protected virtual async Task RunWriterAsyncUnsafe(CancellationToken cancellationToken)
        {
            using var bytesOwner = MemoryPool<byte>.Shared.Rent(WriteBufferSize);

            var encoder = Encoding.UTF8.GetEncoder();
            var reader = WriteChannel.Reader;
            var mBytes = bytesOwner.Memory;

            while (await reader.WaitToReadAsync(cancellationToken)) {
                if (!reader.TryRead(out var message))
                    continue;

                var processedCount = 0;

                bool CreateMessagePart(out Memory<byte> buffer)
                {
                    var remainingChars = message.AsSpan(processedCount);
                    var freeBytes = mBytes.Span;
                    encoder.Convert(remainingChars, freeBytes, true, out var usedCharCount, out var usedByteCount, out var completed);
                    processedCount += usedCharCount;
                    buffer = mBytes.Slice(0, usedByteCount);
                    return completed;
                }

                while (true) {
                    var isEndOfMessage = CreateMessagePart(out var buffer);
                    await WebSocket
                        .SendAsync(buffer, WebSocketMessageType.Text, isEndOfMessage, cancellationToken)
                        .ConfigureAwait(false);
                    if (isEndOfMessage)
                        break;
                }
            }
        }
    }
}
