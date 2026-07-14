## ActualLab.Rpc - the fastest RPC protocol on .NET

<a href="https://www.youtube.com/watch?v=vwm1l8eevak">
    <img src="https://img.youtube.com/vi/vwm1l8eevak/maxresdefault.jpg" 
        alt="Watch the video"
        style="border: 1px solid #000; border-radius: 8px; max-height: 15em; height: auto;">
</a>

## Table of Contents

- Introduction and Background ([0:00](https://www.youtube.com/watch?v=vwm1l8eevak&t=0s))
- What Is Fusion? ([1:02](https://www.youtube.com/watch?v=vwm1l8eevak&t=62s))
- Fusion's Original HTTP Transport ([1:58](https://www.youtube.com/watch?v=vwm1l8eevak&t=118s))
- Problems with HTTP ([6:16](https://www.youtube.com/watch?v=vwm1l8eevak&t=376s))
- Building Our Own Protocol ([7:46](https://www.youtube.com/watch?v=vwm1l8eevak&t=466s))
- ActualLab.Rpc Features ([9:36](https://www.youtube.com/watch?v=vwm1l8eevak&t=576s))
- Performance Highlights ([10:10](https://www.youtube.com/watch?v=vwm1l8eevak&t=610s))
- Benchmarks ([18:15](https://www.youtube.com/watch?v=vwm1l8eevak&t=1095s))
- Comparison with Other Protocols ([22:14](https://www.youtube.com/watch?v=vwm1l8eevak&t=1334s))
- Performance Internals ([26:33](https://www.youtube.com/watch?v=vwm1l8eevak&t=1593s))
- Demo ([39:28](https://www.youtube.com/watch?v=vwm1l8eevak&t=2368s))
- Code Walkthrough ([42:29](https://www.youtube.com/watch?v=vwm1l8eevak&t=2549s))
- Call-Routing Mesh Demo ([56:34](https://www.youtube.com/watch?v=vwm1l8eevak&t=3394s))
- Conclusion ([1:02:50](https://www.youtube.com/watch?v=vwm1l8eevak&t=3770s))

## Transcript

[00:00](https://www.youtube.com/watch?v=vwm1l8eevak&t=0s)
First, a little bit about who I am. You can find me on Medium, where I have written a number of posts about performance. Those are some of the more interesting ones.

[00:32](https://www.youtube.com/watch?v=vwm1l8eevak&t=32s)
I'm also the creator of Actual Chat. You can try the app, and my previous talk on Fusion covered a decent amount of code from it. I recommend playing with the app to see the technologies we're going to discuss in this talk.

[01:02](https://www.youtube.com/watch?v=vwm1l8eevak&t=62s)
Ultimately, this talk is about Fusion and its RPC protocol. You can use ActualLab.Rpc independently, so you don't need Fusion to use it. As a reminder, Fusion is a distributed real-time state-management abstraction. Its distributed side is powered by a protocol built specifically to address problems that more standard protocols don't address well. I'll explain why.

[01:32](https://www.youtube.com/watch?v=vwm1l8eevak&t=92s)
The protocol exists because several requirements of distributed Fusion aren't handled well by conventional RPC transports. That context is the focus of the first part of the talk.

[01:58](https://www.youtube.com/watch?v=vwm1l8eevak&t=118s)
A little more than a year ago, Fusion relied on HTTP. HTTP works in the familiar way: when a client needs data from a server, it sends a request and receives a response. It is a text-based protocol on top of TCP/IP.

[02:30](https://www.youtube.com/watch?v=vwm1l8eevak&t=150s)
That request-response exchange is enough for a conventional data fetch, but Fusion also has to connect the returned value with future invalidations.

[02:44](https://www.youtube.com/watch?v=vwm1l8eevak&t=164s)
To make HTTP work for Fusion, I built an extension on top of it. When Fusion sent a call, it included a special header. If the web server saw that header, it returned another one containing the ID of a server-side publication.

[03:09](https://www.youtube.com/watch?v=vwm1l8eevak&t=189s)
The publication ID linked that HTTP response to a value the server could later report as invalidated. That later notification traveled separately from the original response.

[03:37](https://www.youtube.com/watch?v=vwm1l8eevak&t=217s)
Fusion also watched messages on a WebSocket channel, which you can think of as a side channel for invalidations. It established that channel in the background and invalidated client-side computed values whenever the corresponding invalidation message arrived.

[04:06](https://www.youtube.com/watch?v=vwm1l8eevak&t=246s)
This may seem strange if you don't know Fusion, but it is logical once you do: Fusion needs one additional message indicating that a certain value has expired, become stale, or been invalidated.

[04:41](https://www.youtube.com/watch?v=vwm1l8eevak&t=281s)
This arrangement also enables caching. Suppose the client calls `GetUser` for a session and receives a value. If no invalidation for that call has arrived, then the next identical call can safely return the existing value.

[05:12](https://www.youtube.com/watch?v=vwm1l8eevak&t=312s)
That is one of Fusion's key features: it eliminates a huge number of calls and computations. It does this for network calls, remote computations, and local computations in nearly the same way.

[05:45](https://www.youtube.com/watch?v=vwm1l8eevak&t=345s)
This slide shows what the old design added to HTTP: extra request and response headers. What I want you to notice is the amount of data transferred.

[06:16](https://www.youtube.com/watch?v=vwm1l8eevak&t=376s)
There were several problems with this implementation. We used it in Actual Chat, and HTTP generated far more traffic than we wanted. Ideally, we needed a more compact protocol.

[06:47](https://www.youtube.com/watch?v=vwm1l8eevak&t=407s)
The other problem was coordinating the primary HTTP channel with the side WebSocket channel. Imagine a mobile app in a moving car, where disconnections happen constantly. An HTTP request may time out long after it was sent, while the WebSocket discovers the same disconnect on a different schedule. The two channels were a mess to coordinate.

[07:19](https://www.youtube.com/watch?v=vwm1l8eevak&t=439s)
There was no single, immediate view of whether communication was alive. Each channel could detect failure at a different moment, leaving application logic to reconcile them.

[07:46](https://www.youtube.com/watch?v=vwm1l8eevak&t=466s)
That was ultimately the main reason to build our own communication protocol: it had to be extensible enough to support the request, response, and later invalidation transmission model that Fusion needs.

[08:16](https://www.youtube.com/watch?v=vwm1l8eevak&t=496s)
This is a slide from my older Fusion presentation. Even then, I wanted a more efficient protocol designed specifically for Fusion. Fusion's special ability is eliminating network calls, and other RPC protocols don't provide that ability.

[08:44](https://www.youtube.com/watch?v=vwm1l8eevak&t=524s)
That was why Fusion was already an interesting choice compared with, for example, gRPC. The transport changed almost a year ago, but the Fusion model remained the same.

[09:11](https://www.youtube.com/watch?v=vwm1l8eevak&t=551s)
Fusion sends a request, receives a response, and may receive an invalidation later. It also supports caching. You can think of it as an ETag-like scheme applied to arbitrary method calls.

[09:36](https://www.youtube.com/watch?v=vwm1l8eevak&t=576s)
Today we have ActualLab.Rpc, an incredibly fast protocol. Here are its highlights.

[10:10](https://www.youtube.com/watch?v=vwm1l8eevak&t=610s)
For ordinary calls, a server can process around 350,000 calls per second per core. That is an extremely high number. Well-known gRPC benchmarks may show a similar figure while using roughly four cores.

[10:45](https://www.youtube.com/watch?v=vwm1l8eevak&t=645s)
For streaming, ActualLab.Rpc reaches about five million items per second. I'll soon show how large those numbers are compared with other protocols.

[11:19](https://www.youtube.com/watch?v=vwm1l8eevak&t=679s)
It is also very convenient when everything runs on .NET. It is specifically a .NET protocol: if your client and server, or your server cluster, are all on .NET, it is a very good fit. Otherwise, nothing prevents you from choosing another protocol.

[11:51](https://www.youtube.com/watch?v=vwm1l8eevak&t=711s)
All you need is an interface shared by the client and server. You don't need to annotate every method; you mainly register the client and server services. Streaming is a native part of the design.

[12:24](https://www.youtube.com/watch?v=vwm1l8eevak&t=744s)
Streams can appear in call arguments, results, or inside other objects. You can even send streams whose items contain streams. In other words, an RPC stream behaves like a native data type in the protocol.

[12:55](https://www.youtube.com/watch?v=vwm1l8eevak&t=775s)
The protocol also supports one-way calls: calls that don't expect a result to be returned. In ActualLab.Rpc these are `RpcNoWait` calls. The protocol itself uses them to deliver results and other system-level messages.

[13:26](https://www.youtube.com/watch?v=vwm1l8eevak&t=806s)
A system service is available on both the client and server. Regular calls work through system messages exchanged by these services, and those messages are one-way calls. The protocol is also fully bidirectional: after a client connects, the server can call services on that client.

[13:55](https://www.youtube.com/watch?v=vwm1l8eevak&t=835s)
ActualLab.Rpc supports flexible routing. The destination that processes a call is selected when the call is sent rather than fixed in advance. A single client can transparently route calls to hundreds of servers.

[14:30](https://www.youtube.com/watch?v=vwm1l8eevak&t=870s)
It uses the fastest serializers available on .NET, including MemoryPack and MessagePack. Custom serializers and JSON are also supported. The protocol is AOT-friendly.

[15:02](https://www.youtube.com/watch?v=vwm1l8eevak&t=902s)
That means you can use it in MAUI and other environments where you don't want a full runtime. Another important feature is transparent reconnection. In a typical RPC system, a call fails when the server is unavailable. By default, ActualLab.Rpc keeps the call pending until it can reconnect, execute it, and return the result.

[15:34](https://www.youtube.com/watch?v=vwm1l8eevak&t=934s)
The call fails only when you explicitly cancel its cancellation token. You can override that behavior for particular calls, but the default is to keep trying. If there is no current connection, the caller simply keeps awaiting the result until a connection is established and the remote execution completes.

[16:06](https://www.youtube.com/watch?v=vwm1l8eevak&t=966s)
This alone solves many problems. Application code no longer needs to manage reconnection or deal directly with broken connections; that processing is built into the client and protocol. The transport runs over WebSockets.

[16:39](https://www.youtube.com/watch?v=vwm1l8eevak&t=999s)
Now let's look at its efficiency. In the yellow section of this HTTP trace, a fairly small number of calls transfers roughly two kilobytes.

[17:07](https://www.youtube.com/watch?v=vwm1l8eevak&t=1027s)
The comparable ActualLab.Rpc scenario sends five or six calls. The first two messages are one-time handshakes; the remaining traffic is roughly 400 bytes—about five times less for nearly the same data.

[17:41](https://www.youtube.com/watch?v=vwm1l8eevak&t=1061s)
It is much more efficient than HTTP. When we enable Fusion features such as local caching, the number of transmissions falls further: many calls can be packed into one packet, followed by one response.

[18:15](https://www.youtube.com/watch?v=vwm1l8eevak&t=1095s)
Let's move to benchmarks and remember one number: 120,000 calls per second. A default Redis Docker image includes `redis-benchmark`, and that is roughly how many calls an unconstrained Redis instance processes per second on my machine. This is a familiar baseline for a high-performance service.

[18:47](https://www.youtube.com/watch?v=vwm1l8eevak&t=1127s)
The benchmark sends requests in parallel without constraining Redis, so this isn't an artificially limited result. Keep that number in mind as we look at the RPC tests.

[19:18](https://www.youtube.com/watch?v=vwm1l8eevak&t=1158s)
Now I'll run the RPC benchmark. You can run it yourself. The server in this benchmark is constrained to four cores, and one scenario can be compared directly with the standard gRPC benchmark.

[19:44](https://www.youtube.com/watch?v=vwm1l8eevak&t=1184s)
The project is already built, so the run starts immediately. The four-core constraint is important when comparing these results with the other implementations.

[20:17](https://www.youtube.com/watch?v=vwm1l8eevak&t=1217s)
The benchmark suite also measures gRPC on .NET and can exercise nearly every well-known .NET RPC protocol. It is running a little slower here because I'm recording and processing video at the same time, so these aren't the best results my machine can produce.

[20:46](https://www.youtube.com/watch?v=vwm1l8eevak&t=1246s)
The on-screen run is therefore illustrative rather than a peak measurement. I'll switch to a pre-recorded run instead of waiting through the entire suite.

[21:15](https://www.youtube.com/watch?v=vwm1l8eevak&t=1275s)
Rather than wait for every run, I'll show a recording of the full benchmark. Some implementations run in Docker and some run locally. Remember that the Docker server is limited to four cores.

[21:39](https://www.youtube.com/watch?v=vwm1l8eevak&t=1299s)
The recorded run shows the expected result without the slowdown caused by this screen recording. The slide summarizes nearly the same figures.

[22:14](https://www.youtube.com/watch?v=vwm1l8eevak&t=1334s)
Back to the slides. Redis managed about 120,000 calls per second. With four server cores, ActualLab.Rpc reaches roughly 1.25 million calls per second, and newer versions do a little more.

[22:38](https://www.youtube.com/watch?v=vwm1l8eevak&t=1358s)
The “Say Hello” scenario is directly comparable with the gRPC benchmark implementations available for different platforms. Overall, in these tests ActualLab.Rpc is about two to four times faster than gRPC for calls.

[23:09](https://www.youtube.com/watch?v=vwm1l8eevak&t=1389s)
It is also roughly one-and-a-half to two times faster than SignalR for calls. The difference is even larger for streams.

[23:32](https://www.youtube.com/watch?v=vwm1l8eevak&t=1412s)
For streams of small items, it is about three times faster than gRPC. Small items matter because they expose the framework's per-item overhead: the smaller the payload, the larger the protocol and library costs are relative to the useful data.

[24:04](https://www.youtube.com/watch?v=vwm1l8eevak&t=1444s)
The `Stream 1` case uses one-byte payloads inside a small envelope, while `Stream 100` uses 100-byte items. Even for 100-byte items, the gap from gRPC remains large. Again, these figures come from a Docker server constrained to four cores.

[24:31](https://www.youtube.com/watch?v=vwm1l8eevak&t=1471s)
That four-core limit makes the comparison reproducible. The next chart removes Docker and lets the benchmark use the whole machine.

[25:01](https://www.youtube.com/watch?v=vwm1l8eevak&t=1501s)
If I run the same tests outside Docker, unconstrained on this Ryzen machine, both client and server share the same hardware. A server-only result could therefore be higher. Small calls peak around 6.5 million calls per second, while larger calls remain around four million.

[25:31](https://www.youtube.com/watch?v=vwm1l8eevak&t=1531s)
One MemoryPack result contains an exception caused by contention in `ConcurrentQueue`; MessagePack doesn't have the same problem. That anomalous result is related to MemoryPack and `ConcurrentQueue`, not to the RPC protocol.

[26:04](https://www.youtube.com/watch?v=vwm1l8eevak&t=1564s)
The streaming chart is next. Unlike ordinary calls, it measures how quickly a continuous pipeline can move items from server to client.

[26:33](https://www.youtube.com/watch?v=vwm1l8eevak&t=1593s)
For streaming, the server and client together process about 70 million items per second on the same machine. Why is it so fast? A key reason is automatic batching. At the lowest level, the transport sends and receives a channel of RPC messages.

[27:07](https://www.youtube.com/watch?v=vwm1l8eevak&t=1627s)
.NET channels are efficient and make it possible to build clean pipelines. The batching code awaits new messages on a channel, then takes every message immediately available and packs them into one network buffer until it reaches a configurable size threshold.

[27:32](https://www.youtube.com/watch?v=vwm1l8eevak&t=1652s)
As long as messages can be read synchronously, the batch grows without another asynchronous suspension. Once it reaches its threshold, the transport sends the buffer.

[27:59](https://www.youtube.com/watch?v=vwm1l8eevak&t=1679s)
The fewer awaits a pipeline performs, the more efficient it is. Packing more into each packet helps not just network utilization but also processing overhead at the receiving end.

[28:33](https://www.youtube.com/watch?v=vwm1l8eevak&t=1713s)
Another useful simplification is that everything the protocol handles is represented as a call. Some are one-way calls that don't require a response. Even a normal result comes back as a system call—such as `System.Ok` or `System.Error`—that carries the result.

[29:05](https://www.youtube.com/watch?v=vwm1l8eevak&t=1745s)
This unification creates a simpler pipeline. Instead of processing many unrelated message shapes, the protocol handles one fundamental abstraction.

[29:35](https://www.youtube.com/watch?v=vwm1l8eevak&t=1775s)
It also uses the fastest serializers available on .NET, thanks to Yoshifumi Kawai. Both MemoryPack and MessagePack-CSharp—the two fastest binary serializers in this comparison—were implemented by him.

[30:01](https://www.youtube.com/watch?v=vwm1l8eevak&t=1801s)
ActualLab.Rpc also has an `ArgumentList` type that efficiently wraps method-call arguments without boxing each one and with just one allocation. By contrast, libraries such as SignalR commonly box the arguments.

[30:34](https://www.youtube.com/watch?v=vwm1l8eevak&t=1834s)
There are special paths that eliminate memory copies when the amount of data is large, plus lower-level, optional serialization-format optimizations.

[31:08](https://www.youtube.com/watch?v=vwm1l8eevak&t=1868s)
For example, the protocol can send method-name hashes instead of full names. Hash collisions are possible, but with about a thousand methods the probability is very low. That option can make sense when you need peak throughput.

[31:38](https://www.youtube.com/watch?v=vwm1l8eevak&t=1898s)
Finally, ActualLab.Rpc and Fusion use the same proxy generator and proxy machinery. These proxies can intercept arbitrary calls. They are generated once and configured for different behavior by plugging in different interceptors.

[32:08](https://www.youtube.com/watch?v=vwm1l8eevak&t=1928s)
This resembles Castle DynamicProxy, but ActualLab's proxies are more than twice as fast in this benchmark because they allocate much less. An intercepted call uses a single allocation.

[32:36](https://www.youtube.com/watch?v=vwm1l8eevak&t=1956s)
The proxies are generated at compile time rather than runtime, which also makes them AOT-friendly. `ArgumentList` has specialized paths for operations such as accessing a cancellation token, avoiding a virtual generic call or boxing.

[33:08](https://www.youtube.com/watch?v=vwm1l8eevak&t=1988s)
There are many such optimizations, each removing a frequent source of waste. Let me show the proxy code and benchmark, which you can find in the Fusion repository.

[33:39](https://www.youtube.com/watch?v=vwm1l8eevak&t=2019s)
ActualLab.Interception processes these calls more than twice as fast as Castle DynamicProxy. It allocates only 32 bytes per call: essentially an object header plus enough storage for the arguments in this example.

[34:10](https://www.youtube.com/watch?v=vwm1l8eevak&t=2050s)
That single small allocation is the complete per-call heap cost shown here. It is a major reason the generated proxy can stay on the hot path without dominating RPC overhead.

[34:42](https://www.youtube.com/watch?v=vwm1l8eevak&t=2082s)
Castle DynamicProxy boxes each argument, producing several allocations and much more heap traffic. If you put a breakpoint in a call chain involving Fusion or RPC, you will eventually see the interceptor and generated proxy on the stack.

[35:18](https://www.youtube.com/watch?v=vwm1l8eevak&t=2118s)
The generated proxy code explains the efficiency. The first section runs only once: it caches a delegate capable of calling the base method or actual proxy target. That delegate must be created in the proxy method because only there can the generated code make a base call.

[35:52](https://www.youtube.com/watch?v=vwm1l8eevak&t=2152s)
On each later call, the proxy constructs a struct invocation, makes one allocation for the argument list, and passes the cached intercepted delegate so the interceptor can invoke the underlying method.

[36:24](https://www.youtube.com/watch?v=vwm1l8eevak&t=2184s)
The invocation carries the information the interceptor needs, including the delegate for reaching the intercepted implementation when the interceptor chooses to proceed.

[36:47](https://www.youtube.com/watch?v=vwm1l8eevak&t=2207s)
The generated code also caches a delegate to the appropriate closed generic `Intercept` method for the call's return type. That avoids more allocations on the hot path.

[37:17](https://www.youtube.com/watch?v=vwm1l8eevak&t=2237s)
You can think of it as caching the delegate for a particular generic method instantiation, including the call's return type, rather than discovering it repeatedly.

[37:50](https://www.youtube.com/watch?v=vwm1l8eevak&t=2270s)
Each proxy also gets generated code tied into module initialization. Although those calls don't execute normally, they tell the compiler which generic methods, interface implementations, and related code paths must remain available for AOT compilation.

[38:24](https://www.youtube.com/watch?v=vwm1l8eevak&t=2304s)
That generated reachability information also makes the machinery extensible: implementations of the relevant interfaces can retain the exact generic code paths they require.

[38:52](https://www.youtube.com/watch?v=vwm1l8eevak&t=2332s)
Finally, the implementation caches nearly everything it can, so sending and receiving calls doesn't repeat work that could have been done once.

[39:28](https://www.youtube.com/watch?v=vwm1l8eevak&t=2368s)
Now it's demo time. I'll begin with the more visually attractive demo. This is one of the Fusion samples, but the interesting part here uses only RPC streaming.

[40:02](https://www.youtube.com/watch?v=vwm1l8eevak&t=2402s)
It sends a stream of images at nearly the source frame rate, and the client is running on WebAssembly.

[40:20](https://www.youtube.com/watch?v=vwm1l8eevak&t=2420s)
Now let's run the sample that shows more of ActualLab.Rpc in action. If you saw my previous video, you saw this sample, but not this RPC-focused page.

[40:51](https://www.youtube.com/watch?v=vwm1l8eevak&t=2451s)
The page displays a stream of rows whose items are themselves streams. Switching it to server mode shows that the same abstractions work locally without RPC. The pink ping-pong text is the only part that requires remote one-way calls.

[41:23](https://www.youtube.com/watch?v=vwm1l8eevak&t=2483s)
Let's inspect the network traffic. Some messages are large because each one packs many calls. Streaming tries to batch as many items as possible, including items from multiple streams, into a single network transmission.

[41:59](https://www.youtube.com/watch?v=vwm1l8eevak&t=2519s)
The remaining traffic is the ping-pong exchange. These are one-way calls, with messages of roughly 32 and 42 bytes; much of that size is the application message itself.

[42:29](https://www.youtube.com/watch?v=vwm1l8eevak&t=2549s)
Let's look at the client code. Most of the work starts in `InitializeAsync`. Before that, I'll demonstrate what happens when the server disappears.

[43:00](https://www.youtube.com/watch?v=vwm1l8eevak&t=2580s)
I stop the server and return to the page. Other parts of the app still work while disconnected because they have an offline cache, but the RPC stream page has no data and reports that it is reconnecting.

[43:30](https://www.youtube.com/watch?v=vwm1l8eevak&t=2610s)
The cached pages remain scrollable, but this page waits because its live streams have no server connection. Now I'll restart the server and see whether those pending calls recover.

[44:07](https://www.youtube.com/watch?v=vwm1l8eevak&t=2647s)
As soon as the server starts again and the client connects, the data appears. If I stop it once more, the client tries to send pings but receives no pongs.

[44:40](https://www.youtube.com/watch?v=vwm1l8eevak&t=2680s)
Calls started while disconnected remain pending. The code in `InitializeAsync` is waiting. When I force a reconnect, everything begins moving again.

[45:07](https://www.youtube.com/watch?v=vwm1l8eevak&t=2707s)
Now let's inspect the code. The page is disposable because it owns streams and needs to shut them down cleanly.

[45:41](https://www.youtube.com/watch?v=vwm1l8eevak&t=2741s)
It sends a `Greet` call, sends a `GetTable` call, and starts the ping-pong task. `Greet` is an ordinary call returning a string. The server implementation simply constructs that string, and the page displays it after the greeting task completes.

[46:14](https://www.youtube.com/watch?v=vwm1l8eevak&t=2774s)
There is nothing transport-specific in the implementation. The client awaits a normal task and uses its string result like any other service response.

[46:48](https://www.youtube.com/watch?v=vwm1l8eevak&t=2808s)
`GetTable` returns `Table<T>`. A table has a title and an `RpcStream<Row<T>>`; each row has an index and an `RpcStream<T>` of items.

[47:24](https://www.youtube.com/watch?v=vwm1l8eevak&t=2844s)
That entire object can travel through the protocol. Streams may be nested inside other objects, used in arguments, or carried inside stream items.

[47:54](https://www.youtube.com/watch?v=vwm1l8eevak&t=2874s)
After receiving the table, the client creates a model and starts `ReadTable`. That method iterates through the stream of rows and starts `ReadRow` for each one.

[48:28](https://www.youtube.com/watch?v=vwm1l8eevak&t=2908s)
`ReadRow` iterates through the row's item stream and puts each value into the model. On some updates it also constructs another `RpcStream` from the row's items and passes that stream to a remote `Sum` method. The sample therefore demonstrates several forms of nested and argument streaming.

[49:02](https://www.youtube.com/watch?v=vwm1l8eevak&t=2942s)
The ping-pong section demonstrates one-way calls. A client-side service receives `Pong` calls and exposes their values through a channel.

[49:31](https://www.youtube.com/watch?v=vwm1l8eevak&t=2971s)
This part runs only in RPC mode. `SendPings` produces outbound calls, while the client-side service handles the reverse calls carrying pongs.

[50:02](https://www.youtube.com/watch?v=vwm1l8eevak&t=3002s)
In `SendPings`, the code gets the peer and waits until it is connected. A one-way call is fire-and-forget, so if no connection exists when it is sent, it can be skipped. Waiting for a connected peer avoids that here.

[50:34](https://www.youtube.com/watch?v=vwm1l8eevak&t=3034s)
The client calls `Ping` with a message. Its signature returns `Task<RpcNoWait>`, which tells ActualLab.Rpc that the call doesn't require a response. Such calls usually don't need a cancellation token because they enter the outbound channel almost synchronously.

[51:06](https://www.youtube.com/watch?v=vwm1l8eevak&t=3066s)
`RpcNoWait` is therefore visible in the service contract itself. The caller can identify the operation as one-way without a separate transport-specific API.

[51:37](https://www.youtube.com/watch?v=vwm1l8eevak&t=3097s)
On the server, `GetRows` returns an `IAsyncEnumerable<Row<T>>`, and the implementation wraps it in `RpcStream<T>` for transport.

[52:06](https://www.youtube.com/watch?v=vwm1l8eevak&t=3126s)
`RpcStream<T>` wraps `IAsyncEnumerable<T>`. Most serializers don't let a library override serialization for the interface itself, but they can specialize a custom wrapper type. The wrapper also carries useful settings, including acknowledgement period and acknowledgement advance.

[52:39](https://www.youtube.com/watch?v=vwm1l8eevak&t=3159s)
Programming against it otherwise feels much like using an async enumerable. One thing to remember is that the received client-side stream can be enumerated only once. Whenever the service sends a stream, it constructs an `RpcStream<T>` from an async or synchronous enumerable.

[53:09](https://www.youtube.com/watch?v=vwm1l8eevak&t=3189s)
The only tricky server method is `Ping`, because it must send `Pong` back to the same peer. It obtains the inbound RPC context and gets that peer.

[53:36](https://www.youtube.com/watch?v=vwm1l8eevak&t=3216s)
It then creates and activates an outbound RPC context with the peer. Normally, RPC would ask its router to select a destination. Here we pre-route the outbound call because it must return to the peer that sent the ping.

[53:59](https://www.youtube.com/watch?v=vwm1l8eevak&t=3239s)
Inside that context scope, the ordinary service call uses the selected peer automatically. Outside it, the standard router remains responsible for destination selection.

[54:29](https://www.youtube.com/watch?v=vwm1l8eevak&t=3269s)
That is essentially the whole streaming example. More importantly, it shows that you can use ActualLab.Rpc without Fusion.

[55:02](https://www.youtube.com/watch?v=vwm1l8eevak&t=3302s)
On the client, you add RPC, select the WebSocket client transport, and add a client for the shared service interface. The server setup begins with the same `AddRpc` registration. This is enough for dependency injection to resolve the generated client proxy.

[55:36](https://www.youtube.com/watch?v=vwm1l8eevak&t=3336s)
The service proxy then looks like an ordinary implementation of that interface to the rest of the application. Most of the transport configuration is concentrated in registration.

[56:05](https://www.youtube.com/watch?v=vwm1l8eevak&t=3365s)
There is one last, stranger, and more entertaining demo: flexible call routing.

[56:34](https://www.youtube.com/watch?v=vwm1l8eevak&t=3394s)
An RPC call router is a delegate, and you can register your own. The default router sends every call to the default client peer. A custom router receives the `RpcMethodDef` for the method being called and its argument list, then returns an `RpcPeerRef`.

[57:14](https://www.youtube.com/watch?v=vwm1l8eevak&t=3434s)
Because the router sees both the method definition and arguments, it can make a destination decision from the actual call rather than from a fixed endpoint chosen earlier.

[57:44](https://www.youtube.com/watch?v=vwm1l8eevak&t=3464s)
Think of `RpcPeerRef` as something like a URL, but more flexible. This router examines the first argument. If it is a `HostRef`, the router returns the peer reference associated with that specific host.

[58:16](https://www.youtube.com/watch?v=vwm1l8eevak&t=3496s)
If it sees a `ShardRef`, it returns a reference to that shard. With dynamic sharding, hosts can join or leave and ownership can move, so the same shard reference may resolve to different hosts over time.

[58:50](https://www.youtube.com/watch?v=vwm1l8eevak&t=3530s)
The demo includes the implementations of these peer-reference types. If the first argument is an integer, the router maps that integer to a shard and constructs the corresponding shard reference. Routing is fully dynamic.

[59:23](https://www.youtube.com/watch?v=vwm1l8eevak&t=3563s)
The same router can therefore address a concrete host, a moving shard, or a shard derived from an ordinary key. Now let's run the mesh sample.

[59:49](https://www.youtube.com/watch?v=vwm1l8eevak&t=3589s)
The mesh sample simulates hosts starting, stopping, and joining a shard map. Each virtual host is backed by a real ASP.NET server using ActualLab.Rpc. The plus signs show which shards a host has taken, and the allocation changes over time.

[1:00:24](https://www.youtube.com/watch?v=vwm1l8eevak&t=3624s)
As hosts come and go, the shard assignments are redistributed. The display makes those topology changes visible while calls continue in the background.

[1:00:52](https://www.youtube.com/watch?v=vwm1l8eevak&t=3652s)
Services on those hosts call one another and maintain their shard maps independently. The goal of the demo is to show no red lines—no calls that fail permanently without rerouting or reprocessing—even while hosts disappear and return.

[1:01:27](https://www.youtube.com/watch?v=vwm1l8eevak&t=3687s)
It calls both Fusion and non-Fusion services and demonstrates that the protocol tolerates many kinds of failure. Calls continue to flow rapidly while the topology changes.

[1:01:55](https://www.youtube.com/watch?v=vwm1l8eevak&t=3715s)
This routing machinery is complicated, and you mainly need it for a server-side service mesh. It can remove the need for a load balancer and enable more efficient communication between servers.

[1:02:24](https://www.youtube.com/watch?v=vwm1l8eevak&t=3744s)
Connection failures, retries, and rerouting are largely handled by the protocol and library rather than application code.

[1:02:50](https://www.youtube.com/watch?v=vwm1l8eevak&t=3770s)
As a final reminder, combining ActualLab.Rpc with Fusion produces results far beyond RPC alone. Redis handled about 120,000 very lightweight calls per second on this machine—essentially dictionary lookups where networking likely costs more than the operation itself.

[1:03:22](https://www.youtube.com/watch?v=vwm1l8eevak&t=3802s)
Fusion's cache-resolving calls avoid most of that networking and repeated execution. That changes the scale of the result rather than merely improving the RPC transport by another small factor.

[1:03:56](https://www.youtube.com/watch?v=vwm1l8eevak&t=3836s)
Here, heavier calls reach about 1.5 million per second, while lightweight calls similar to the Redis benchmark reach around six million per second on the same machine—roughly 50 to 60 times the Redis number.

[1:04:16](https://www.youtube.com/watch?v=vwm1l8eevak&t=3856s)
That's where you can find the ActualLab.Rpc samples. Thanks a lot for watching. Please feel free to reach out with questions, and contributions would be amazing. Thank you.
