Source  
<a href="https://img.youtube.com/vi/vwm1l8eevak/maxresdefault.jpg"><img src="https://img.youtube.com/vi/eMO7AmI6ui4/maxresdefault.jpg"></a>

Transcript

0:00
so a little bit more on uh like who am
0:07
I um you can find me on medium I uh
0:13
wrote uh a number of posts related to Performance these are some of the uh
0:19
kind of uh more uh interesting ones uh
0:25
then um I'm also the creator of uh actual
0:32
chat uh you can try the app and uh um my
0:37
like previous talk on Fusion was uh covering a decent amount of code from
0:43
actual chat so um I definitely recommend you to play with it to see uh the
0:51
Technologies we are going to talk about in this talk yeah that's a screenshot okay and
0:59
but like ultimately it's uh about fusion and its RPC protocol which you can use
1:06
independently uh from Fusion so you don't need Fusion to use actual La
1:12
RPC uh so just a kind of reminder right what Fusion is it's a distributed uh
1:21
realtime State Management abstraction that's like extremely powerful by itself
1:26
but like this it's distributed part is powered by uh the protocol that uh well
1:36
uh is built in particular to address some of the problems uh other or like
1:44
more let's say standard protocols uh don't address or like can't address well
1:51
and here I'm going to talk mostly like like right now at least uh I'm I'm going
1:57
to explain why so uh yeah
2:04
that's so a bit of a history uh a long time ago like one year ago
2:11
actually a little bit more than one year ago uh Fusion was relying on
2:18
HTTP and U HTTP works like this right you have a client uh which um well sorry
2:28
when it has to get some date from the server it sends the call it's like one
2:34
message right and gets the response back uh so all of his stuff HTTP is a text
2:40
based protocol on top of TCP IP and um
2:45
uh that's near it so uh for Fusion to so to make it work for
2:53
Fusion uh well I built kind of extension to this prodoc
3:00
on top of this protocol uh basically when Fusion uh
3:05
sends the call it sends a special header um and uh when a web server
3:15
replies it responds with another header if it sees this one and basically the
3:22
value it gets in this header uh allows uh or like uh it's
3:30
basically the ID of um publication uh on
3:36
the server side and if you watch messages uh on the Web soet Channel basically you can think of it as a site
3:42
channel for inv validations right and so sorry Fusion watches for these messages
3:48
so basically it kind of establishes this channel in background and watches for
3:54
the messages there and like uh the computed values it creat
4:00
on the client it also invalidates them when it sees the invalidation message here right um so uh well I mean all of
4:12
this uh seems pretty weird if you don't know like much about Fusion but
4:18
nevertheless like if you know about Fusion then this uh is like
4:24
uh how to say well basically all of this is very logical right
4:30
it needs one other message to indicate that certain value is kind of expired or
4:35
stale or invalidated and that's how it gets this message now uh yeah and uh I guess uh
4:46
why well um what else this protocol allows is well I mean if your client is smart
4:53
and it knows that like when you send certain requests like for example first
4:59
time you send the request for Session One get user and gets the data right and
5:04
invalidation message for it didn't came through yet or like it just wasn't invalidated then when you send the same
5:11
or like when the client gets a request to execute the same call for the second time and it knows for sure that it's the
5:18
same value right the one you got because invalidation message didn't came and it
5:24
returns it so that's like uh like what kind of fusion does in general and uh
5:32
it's uh like one of its key features it eliminates like tons of calls it's it's about networking calls it's about remote
5:39
computation or like local computations does it in the same way like like nearly like is described here
5:45
right um so uh and yeah that's uh how like Call of f
5:54
actually looks in terms of what it adds to the protocol so
6:00
basically um uh response has some extra headers
6:07
request has some extra headers but what I want you to pay attention to is the
6:12
amount of data transferred here well or like I think there was a bunch of
6:19
problems actually with the specific implementation I think first is that like uh well so we were using all this
6:27
in actual chat and the amount of traffic you get uh if you use HTTP it's
6:34
like it's way higher than we want to be want to have so uh ideally you want to
6:42
use more compact protocol right then uh the second piece is like
6:49
this stuff with uh site websocket Channel and like primary sort of
6:55
Communication channel it was extremely
7:00
difficult to kind of make it work right in sense that like imagine a mobile app
7:06
right and you are in the car and disconnect happens like each and every
7:12
time so for HTTP uh like you send a message and it's
7:18
like if you know how uh HTTP works and like you get a time out uh you may get
7:24
that time out way after uh like you send the call right so basically at some
7:29
point it detects that like okay you are disconnected but it's not immediate
7:35
right then like it's different with your websocket Channel with your regular
7:40
messages so basically it's Ms and uh um I think that was like ultimately the
7:47
main reason to build uh like our own protocol for um um well communication uh
7:56
which is extendable enough to support this uh kind of um 1 plus two transmission model that
8:05
Fusion needs so that that's one of slides from
8:13
my old presentation about Fusion basically it Compares like kind of
8:21
like um not quite Compares but mentions that uh I long time ago I wanted to uh
8:31
Implement more efficient protocol for Fusion specifically uh but like so the kind of
8:41
um the special ability that Fusion has is eliminates Network calls right and
8:49
like no other protocol has it so that's why I think uh Fusion was kind of more
8:57
interesting choice than for example grpc even before all of that but like so all
9:04
of this changed nearly year ago and uh yeah I'm that's just kind of
9:12
uh uh to you probably already understand how it works uh with Fusion uh so it
9:20
yeah sends request gets response and maybe maybe at some point later gets an
9:27
invalidation or doesn't um and it also supports uh like cing uh
9:34
like with you can think of this scheme as EAC scheme but like it implements it
9:41
for like any calls you make so um yeah
9:46
back to kind of now and um we already so
9:51
we have this uh protocol action lab RPC which is incredibly fast
10:01
and here is like uh well here are some
10:06
highlights uh so first of all in terms of calls well per single core uh your server can
10:16
process around 300 uh 50 uh th000 calls per second per
10:27
core uh it's an extremely high number if you look uh for example at like
10:35
well-known grpc benchmarks uh you will notice that uh you see the same number
10:42
but for maybe four course uh for streaming yeah it supports
10:49
streaming uh it's about 5 million items per second and that's completely crazy
10:56
number because like normally you need well I'll show you soon like how big
11:05
these numbers are actually compared to other uh protocols so how large these
11:12
numbers um and besides that it's also extremely convenient if you use it on
11:19
net and by the way it doesn't work anywhere else so it's if you are building everything on net and your
11:25
client is on net or like your cluster of servers is all on net and that's uh like
11:31
an extremely uh basically good Feit but otherwise like you well I no one
11:38
prevents you from using other protocols it's just like this thing works only on
11:43
that net so um it's extremely convenient
11:51
basically all you need to use it is a shared interface between client and server uh you don't need to put any
12:00
attributes or like whatever uh and uh
12:05
um all you need is to uh like properly register the client or the servers it's
12:12
about Service registration it supports streams uh I mentioned it already uh
12:18
moreover like it supports streams better than anyone else it can I mean you can
12:24
send streams uh with items containing streams and uh uh like can send them as
12:31
arguments to calls as part of arguments to certain calls as part of results and
12:37
so so on so basically it's like a fully native citizen there or a fully native
12:43
data type uh there well it also supports um oneway
12:51
calls these are the calls you sent and don't expect the don't expect to get the
12:56
result back and it's not going to be sent so it's like no response calls or like
13:02
uh they're called RPC no weight calls in RPC
13:08
it's and it's interesting that like in reality these calls are used by the
13:14
protocol to deliver back the results and so basically all the system level stuff that happens it's kind of powered by
13:21
these calls uh there is a system service that runs on like one kind of I mean
13:28
available as a server on the client and on the server site and basically uh
13:33
regular calls happen because these two things they exchange with system messages which are oneway
13:40
calls okay and uh one interesting thing is that the protocol is fully
13:46
bidirectional which means that the um the server can call the client one once
13:53
it connects so it's not like just the client can call the server you can do the opposite as well
14:00
um and uh it also supports extremely flexible routing in sense that when you
14:07
send the call the destination I mean the server that will process the call it's
14:14
determined uh not like in advance but right when you send the call so
14:20
basically um a single like client can route calls to like hundreds of servers
14:28
and uh all of this happens transparently finally so uh
14:36
unsurprisingly it uses the fastest serializers available on net um memory P
14:43
message pack and uh yeah custom serializers are also supported Json of
14:51
course as well uh and it's aot friendly
14:57
uh which means uh well I mean you can use it on
15:05
uh you can use it uh in Maui you can use it well literally anywhere where you
15:12
probably don't want to run uh full blown. net um there is a bunch of interesting
15:19
features for example transparent reconnection uh it's like uh I mean typically right
15:26
if the server is unavailable then like call will fail so with RPC it's never
15:32
like this uh the call Will Fail only when you cancel it explicitly by
15:37
canceling the cancellation token so at least that's what happens by default you can overwrite this for certain calls and
15:42
stuff like this but by default it doesn't cancel any calls and like it basically will be trying to send it and
15:48
process it until the moment you cancel it uh so once connection so if you are
15:54
not connected right now then the call is going to kind of it's going to
16:00
be running let's say so the whoever sent it is not going to get the result until
16:07
the connection is established and like the call is executed and the result
16:12
comes through back okay and um overall this is an
16:19
extremely good I mean this this alone is like a solution to so many problems um
16:26
um you basically don't need to care of uh this reconnection thing uh and or
16:33
like you you don't need to worry about broken connections and stuff like this so This the processing of all of this
16:40
stuff is kind of baked into the uh client and the protocol uh and it runs on top of web
16:48
sockets okay so uh yeah how efficient
16:53
this thing is and uh this is the I think what you need to pay attention
16:59
here is um this part the yellow kind of bucket
17:07
of numbers you see that probably it's like 2 kilobytes or so right for a
17:13
pretty tiny number of calls and if we look at uh similar kind of scenario here
17:23
it also sends I think I don't know like five or six calls and uh so first two
17:29
are uh handshakes so it's like it happens just once but the rest I think
17:35
it's um how much well my guess is like uh 400 bytes right five time five times
17:43
less that's nearly the same set of data is transmitted and so it's basically way
17:49
more efficient than HTTP of course and uh interestingly that like yeah if we
17:56
turn on other Fusion features like local caching then it's going to be even well in terms of data more but in terms of
18:03
number of Transmissions like all the calls are sent single packet and you get a single response pack so it's it's
18:11
extremely efficient okay now we are going to benchmarks and uh let's uh remember a
18:20
single number 120,000 call per
18:26
second um any this instance or like at least um okay like default uh Docker
18:37
container for redus has radius Benchmark tool which uh can Benchmark redus so
18:43
that's the number of calls uh radius instance can process per second on my
18:49
machine it's unconstrained right and like basically um it sends them in parallel
18:56
and stuff like this and so that's the number of calls that's kind
19:03
of uh presumed to be normal for high performance service right okay now I'm
19:12
well uh now I'm going to uh run RPC
19:19
Benchmark and that's a benchmark that like sorry you can run it uh on your own
19:27
R uh I I'll run it's doed version oh let's run it for calls
19:38
right uh so it's going to um well oh okay cool it's already built in my
19:46
case um so what's interesting here um
19:52
the server in this Benchmark is constrained to four course and you can
19:59
compare uh some results from this Benchmark with uh grpc Benchmark uh I
20:07
think there is one um I so I'll add the link in the end but uh one of tests here
20:16
is directly comparable uh with grpc Benchmark um and uh but what also worth
20:25
mentioning is that uh well it actually benchmarks JPC
20:30
on.net and so this thing is capable of benchmarking nearly every at least kind
20:37
of every well-known RPC protocol onnet so right now it runs kind of a
20:46
little bit uh slower than it's supposed to run uh because well of this video
20:53
recording and like processing uh so uh
20:59
um uh it's not exactly the result uh that you uh or that's kind of the best
21:07
on my machine but nevertheless um so instead of like
21:14
waiting here I guess I'll run a video with uh all of these benchmarks
21:25
recorded uh yeah uh I think the speed up is like 30X or 20x
21:34
here and that's basically it shows uh what you are supposed to see if you run
21:41
these benchmarks locally so sorry some are in Docker some
21:48
are just uh running locally uh so the docker version is
21:54
constrained to four course remember this okay so that's uh like what you are
22:00
supposed to see but um with uh decent speed up yeah so
22:08
um I'm not going to wait till the very end here I I'll just show later that these numbers are nearly the same in my
22:16
slides um okay so let's get back to um
22:23
well two slides right so remember right already
22:29
is 120,000 calls per second so what
22:34
actual RPC does with for core server it's around
22:41
well 1 million to 15,000 calls per second I think the most
22:49
recent version even does a little bit more uh and uh by the way uh speaking of
22:56
the call you can compare with other benchmarks so say hello scenario is the
23:02
one you can compare with grpc Benchmark on like every I mean there is an
23:09
implementation of basically this thing for every platform and you can directly compare uh the results here but overall
23:16
actual op RPC is like two to maybe 4X faster than
23:22
grpc uh on calls and uh probably like again well it's uh I don't know 1.5 to
23:30
2x faster than signal r as for streams
23:36
uh uh the difference is even bigger um I think it's 3x faster than
23:45
grpc uh on streams with short like with small items and uh well I mean why small
23:54
items and why it's important is well we measure
23:59
the extra expenses uh that framework adds right or like Library ODS uh so the
24:06
smaller is the item the more kind of complex is the scenario it basically highlights the cost of uh kind of
24:13
processing the stream item by the library so that's why so stream one is
24:19
like I think extremely short items probably I mean the array they sent is
24:25
one by array but the item itself so basically there is some Envelope as well but it's small and stream 100 is uh 100
24:33
byte items uh so but even on 100 byte items
24:38
the difference is pretty huge with grpc and again that's uh on Docker so it's
24:45
like a server constraint to to for course that's how much it can
24:52
process now if we uh run the same tests uh just uh
24:59
without Docker and unconstrained and uh on um my machine which is uh Ryon well
25:08
this Ryon uh so for calls and remember the
25:14
client and server uh both are running on these machines so it's like nearly um I
25:20
mean you can get twice of that if you run just server so it's uh I think uh
25:27
6.5 million calls currently at Peak for uh
25:33
the kind of small calls and um uh as for
25:39
the larger ones uh well it's still basically um about 4 million calls and
25:46
yeah this result here it's an exception basically there is a lck contention in
25:51
concurrent que uh with memory pack but uh there's no such problem with message
25:57
PS it's basically like I think they hit some weird scenario where for the
26:03
specific payload serialization is much slower somehow but yeah so it's not
26:10
related to uh RPC protocol it's related to memory pack and like ultimately
26:17
concurrent Q on that net and uh here is the chart for
26:24
streaming so uh as you can see the
26:30
server here sends uh I think um about 70 million calls per
26:40
second I mean server plus client on the same machine so why it's
26:47
fast uh well I think the key piece is automatic batching and it's also based
26:56
on uh the fact it uses a Channel of RPC
27:01
messages basically a lowlevel obstruction that kind of um sends and
27:08
receives call is channel of RPC messages and channels are extremely efficient and
27:15
Powerful think on net if you never used them then you should learn about them but like basically they allow you to
27:23
create really nice pipelines so uh automatic batching is uh
27:31
basically a piece of code that awaits new messages on a channel and
27:40
uh while it can get these messages synchronously it packs it it packs them
27:46
into a single uh networking bucket and um uh more like when it hits
27:54
a certain size threshold but uh like this this piece is even customizable
28:00
actually um and uh why this thing is important is like well the less weights
28:07
you have the more efficient your pipeline is so like basically the more you can pack
28:15
into a single pocket the better it's not better just in terms of networking but
28:21
like also in terms of uh the extras you kind of spend on the receiving gun
28:29
right um there is also one nice unification everything that like the
28:36
protocol can actually deal with is a call just uh well some calls I uh oneway
28:44
calls they don't require a response and that's how RPC sends a response to
28:51
regular calls like basically you send it sends a call uh to One Direction and
28:56
gets back a message called like system. okay I mean that's the name of the method or system. error uh which
29:04
delivers the result of this call so uh the unification of course helps to
29:12
build a simpler pipeline uh and instead of like dealing with I don't know like tons of different
29:19
messages um it has just one uh so that's why it's kind of cool
29:26
piece um and U one other important thing is that
29:34
like yeah it uses the fastest serializers available
29:40
onnet all thanks to uh yesui Kawai if you don't know who is this guy well you
29:47
definitely should U read about uh well
29:53
both these two serializers and the person but it's it it's interesting that
29:59
like two fastest binary serializers on net are implemented by same
30:05
person um so uh yeah besides that there
30:12
is a fancy argument list type which is basically a type that efficiently wraps
30:20
uh method call arguments without boxing uh or like with just one allocation so
30:26
it's like uh for example if you look at how signal are or like other RPC libraries process something similar they
30:34
allocate every I mean they box every argument and the argument list is exactly what uh kind of allows
30:42
to uh skip on boxing um there are special Parts eliminating
30:49
some uh memory copies uh in case uh the amount of data to copy is relatively
30:55
large and uh even some like really kind
31:01
of lowlevel optimizations on the protocol or like sorry on serialization
31:08
format um level you you can optionally turn them on and for example it can send
31:15
uh method name hashes instead of method names and it kind of seems stupid because you can get hash collisions
31:22
right but like in reality the chance of collision is well just like 100 of a
31:28
percent so it's like one per 10,000 um if you have thousand methods
31:34
so it's very low and it makes sense to use it if you actually need the peak for
31:41
output right and finally finally the last piece is uh
31:47
well the proxies that actual La RPC uses
31:53
interestingly the uh proxy generator and the proxy code that is used by actual
32:01
lab RPC is the same these are the same proxies as Fusion uses which means that
32:07
these proxies are generic generic in sense that like they can intercept any
32:13
calls it's basically like they are generated once and you can reconfigure
32:19
them to do different kind of processing dependently on what kind of Interceptor
32:24
you plug in it's kind of similar to Castle dynamic proxy but the difference
32:30
is that well first of all it's two it's 2x faster because the number of
32:35
allocations is way way lower here and I I'll show you that and uh
32:43
it's like literally a single allocation for intercepted call and the second
32:48
piece is that these proxies are generated in compile time not in the run
32:54
run time and because of that it's aot friendly
33:00
uh so and yeah there are some other optimizations like for example this
33:05
argument list type has special parts for cancellation token access um and like
33:11
special because otherwise there would be a virtual generic call or boxing so um
33:19
yeah uh so there is a decent number of such uh optimizations that kind of um
33:27
eliminate the most uh kind of frequent um wastes let's
33:35
say okay uh let me show you the proxy quote right and uh that's a benchmark
33:43
you can find it in Fusion repository probably wouldn't so basically it's like one of tests in Fusion repository but
33:50
you can see that like what's interesting here is that for example for this call right first of all uh well
33:59
um actual La do interception Interceptor or like proxies they uh process uh these
34:09
SKS 2x faster than CLE Dynamic proxy so 2x plus I would say but another
34:18
interesting piece is that like they allocate just 32 bytes per call well
34:24
this this is literally like an object header 16 bytes on 64bit platform plus like 16 more bytes
34:32
that's probably for the arguments here right and that's literally it and um as for
34:39
Castle Dynamic proxy as I said like basically it boxes every argument that's why there is way more of like way more
34:48
sorry way more data is allocated on the Heap I think in this case it should be
34:54
at least four allocations per call okay and uh yeah so you can I guess walk
35:02
through uh like you can do this I mean
35:07
you can put a break point uh into I think every call chain involving Fusion
35:14
or RPC and you will see something like this here at some point you will see the
35:20
interceptors on the call stack and the proxy on the call stack so and I
35:28
I'm let me quickly show you a proxy code here like why it basically explains why
35:35
it's so efficient so this first part uh happens just once because of these uh
35:41
two question marks equals and like what happens here is it cat the delegate that
35:48
uh may call the base method or like the actual proxy Target it has to be here
35:55
because if it calls the base method like literally has to be here right you can't kind of call base from base method from
36:03
the random spot but like so this piece happens just on the first call uh as for
36:10
the rest um so I zoom out a little bit but as for the rest like what happens is
36:17
it Con constructs invocation which is a struct here there is a single allocation
36:23
that's kind of that actually happens and um yeah passes intercepted delegator so
36:31
that you can call like your Interceptor can call Base method basically the method that is intercepted right and uh
36:40
uses this weird thing to actually invoke an Interceptor the reason why it uses
36:47
this weird thing is that like so in reality in
36:52
reality uh it calls this method with
36:59
um well the sorry so it calls C intercepted one here right and if you
37:06
look at what cach intercepted one it's an Interceptor do intercept method but
37:12
uh with uh parameterized with this generic parameter so basically like it
37:19
Cates the uh you may think of this is as it it Cates the delegate that points to
37:26
uh um an instance of generic method with specific parameters like the return type
37:33
here um so and yeah that that also allows you
37:39
allows this thing to kind of get rid of some allocations uh so that's nearly how it
37:47
works oh and yeah the other interesting part is that it generates kind of module
37:54
initializer for every uh proxy which does in reality it does
38:02
nothing because like this method it actually doesn't call this thing but
38:08
nevertheless the compiler uh thinks that all these methods are invoked and basically this is something that allows
38:15
you to um well first of all it uh kind of marks
38:21
everything that's supposed to be used by aot but also it allows you to kind of extend this thing uh because uh uh
38:30
well some types that are used here are interfaces and if you implement them
38:37
like it's going to think that like okay every implementation with uh like uh
38:44
every implementation of uh the interface is supposed to be used with like certain
38:50
method parameters and methods so that's why uh that's what uh kind of makes this
38:57
thing aot friendly as
39:03
well well and yeah finally I think like it caches nearly everything that's
39:09
possible to cach uh so that when it processes uh the call both on sending
39:16
site or on receiving site it doesn't do
39:21
any work that actually shouldn't be done
39:27
okay now it's a demo time and U that's
39:33
uh going to be the very first demo here or maybe I'll start from uh well more
39:41
visually attractive one it's one of fusion demos but I think
39:46
the interesting part here is this one so it's um basically it uses RPC streaming
39:53
so Fusion is not used here or at least like it like this part shows just the RPC piece
40:02
and yeah it shows that like it basically can send uh well a stream of images with
40:10
uh well nearly the origin FPS um and yeah it's web
40:19
assembly okay uh nevertheless Let's uh let's uh [Music]
40:26
run uh the sample that
40:33
uh actually shows the RPC in
40:45
action I'll close well but we can leave it uh so if you saw my previous video
40:54
you already saw this sample but I didn't show this part right uh so this part
41:00
shows just the RPC uh uh calls and uh
41:07
you can see that if you refresh the page and like basically it's stream of rows
41:14
with stream of items you can switch it to server by the way so there is no any
41:19
RPC it just shows that okay these streams also work locally I think the only thing that doesn't work is like
41:25
this pink p with uh oneway calls uh
41:31
so okay so let's uh let me show you a couple things here
41:40
first uh what happens over the network right in this
41:47
case so you can see that it sends uh actually like pretty big messages
41:54
sometimes uh which Buck a a bunch of calls and uh uh so streaming is
42:01
extremely efficient it's uh yeah it tries to p as many stream items into a
42:07
single batch and like multiple uh sorry items from multiple streams into a
42:12
single uh Network transmission so uh in terms of traffic it's uh extremely
42:19
efficient the like uh I think the only why I can scroll so that what's
42:25
happening right now is is like this ping pong stuff and these are uni dire sorry
42:33
oneway calls and basically like 32 bytes and 42 bytes response I think most of
42:40
this stuff is like at least large part of this stuff is the message itself okay so let's look at the code I
42:48
guess right and see
42:54
so yeah so that's let's start from the
42:59
client um so I think everything that happens here
43:06
happens in initializing oh by the way let me show you one other thing so
43:13
we so it's interesting right how it's going to behave if
43:18
we uh shut down the server right now and we'll go to the same page right so I
43:25
stop the server
43:30
um so you know that this stuff works even if you are disconnected kind of
43:36
works renders stuff because I mean I can uh yeah uh
43:42
still uh kind of scroll through these Pages even if you are disconnected because there is an offline cach but
43:51
like if we go here it doesn't show anything right and like reconnecting so
43:57
will it work if I'm going to start the server
44:06
right yeah I'm curious It's supposed to work so you see that like basically as
44:11
soon as it connected it like started to show the data and if you uh again stop
44:18
it then like okay it sends tries to send pinks but doesn't get pong back so
44:27
that's what happens uh let me kind of Click uh a
44:33
bunch of times here to make this
44:39
counter uh count for at least like 30 seconds or
44:45
so okay so now it's going to take a while for it to reconnect so again I'm
44:52
running the server right now and let's
44:57
get to some other page you see that the counter is like the same but now if
45:05
yeah so it's not connected that's why basically it can like run the I mean the
45:13
all these calls in initializer sync they are waiting and if I click reconnect and
45:20
like things too spin okay so let let's look at the code here right
45:33
uh uh so um yeah we need this page to be
45:39
disposable that's like uh what you have to do if you of course
45:45
deal with streams and want to kind of shut them down gracefully uh so what we do here we send
45:54
greet call and we send get table call and also start Ping
46:00
Pong task let's start from this thing right so greet is uh simple call
46:07
returning string yes string and if we go to the
46:13
implementation then like that's the result it produces on the server side so
46:18
nothing fancy um once uh greeting task is completed it
46:27
says greeting right um uh it's interesting interesting well
46:35
I mean we could put something like uh state has changed here right but it
46:41
waits for everything before like well uh rendering basically this thing but so
46:48
the point is then like it also sends get table call and uh ping pong task starts
46:54
ping pong task so let's look at get table a it returns table of in what is
47:01
table table is actually a table of T
47:06
here so uh it's an instance of generic type and um it has a title and RPC
47:15
stream of row of T right and row of T has an index and RPC stream of
47:22
items uh so as you might guess this thing can travel over the
47:31
um well RPC protocol and uh you can use it as part of other
47:38
objects uh and as part of arguments like
47:43
here and uh well deeply inside some other objects and like yeah you can send
47:49
items which stream items which contain other streams so uh
48:01
let's look on like so here we get table right and then
48:10
create a model and also start read table method so what read table does it
48:16
literally iterates through its rows and uh like adds the
48:22
row then for every row it calls r r
48:27
and if you look at the redraw it's uh it's kind of even more interesting right it
48:36
basically iterates through items and uh puts them into the model but like in the
48:43
end it calls like when it basically gets uh the next item which calls update sum
48:51
and uh update some uh gets the roll
48:57
and uh creates an RPC stream out of its items and sends uh the call to some
49:04
method which gets this Stream So basically like the point is like this sample shows all kinds of streaming you
49:12
can get and finally the ping pong part it's um uh the unusual thing here is that
49:19
like the calls that it sends are oneway calls so that's why we
49:28
uh so this check it's just like it can't work of course if uh there is no RPC but
49:35
like we start send Pink's task here and uh then uh on the so there is a
49:45
like um so client side service is um basically the service that registers
49:51
calls to punks and it exposes these like results in punks
49:57
channel uh so but I think that's the only interesting part here it's uh if we
50:02
go to send pinks then like we get Pier uh so for oneway call you uh
50:11
can um you have to pre-out most of them and that's why we get the peer and uh
50:21
uh oh no no no sorry uh you don't have to you uh like yeah you don't have to
50:29
but like if you for example respond to such calls on server site and you need to know the P that kind of send you the
50:37
uh ping to respond but here we just wait for like when it this spear gets
50:44
connected because like these calls they are kind of fire and forget and if you
50:49
are not connected then it's just going to be kind of skipped so we
50:57
uh call this method in the end simple service the P pass a message and uh if
51:02
you look at the signature the important part here is that there is RPC no weight
51:08
um so basically it's it's a method that returns task of RPC no when you see such
51:15
method it indicates for RPC that it's a call that doesn't expect like you don't
51:21
have to send a response to such a call okay and uh that's that's why you alsoa
51:28
typically don't need to have a cancellation Tok in there just I mean like these calls they are sent like
51:36
almost synchronously and so they enter that kind of send Channel like
51:43
synchronously so uh let's look at the server site
51:51
right so it's pretty easy we so get rows
51:56
at I think it should uh return the sync enumerable of rows right and all we need
52:01
to do is to kind of re this rra this stuff into RPC stream to um make it work
52:09
so basically RPC stream is a wrapper over a sync inumerable the reason why
52:15
you can't pass just a sync inumerable like simply a sync inumerable is that for most of serializers there is no way
52:22
to like override the serialization for this type but but there is an easy way
52:28
to override serialization for your custom type that's why there is a rer RPC stream well plus it actually
52:35
provides you with a few useful properties so for example this two uh period and
52:44
acknowledgement period and acknowledgement Advance it basically tells you have like you can customize it
52:50
on per stream basis okay so for streaming I think it's
52:55
more or less clear right it's like you uh well you literally like deal almost
53:03
with the syn enumerables but the streams so one thing you need to remember is that like on the client side the stream
53:09
can be enumerated just once you can't enumerate it multiple times um okay so and for get rows so you
53:17
basically you you see that like every time it needs to send a stream it constructs it from a syn numerable or
53:24
regular en numerable okay and uh I think the only tricky part here is pink method
53:32
and it's like uh it has to respond with PK so the way it responds with PK to the
53:37
same P it uh gets the inbound RPC cont text and gets the peer here then it
53:45
creates outbound RPC context and activates it so inside this using block
53:50
basically the outbound RPC context is kind of pre-created and uh um so since it passes
53:58
the pier there um by default so if you
54:03
don't do that then RPC will use uh its
54:08
uh router to find the pier uh and that's what happens normally but in this case
54:16
we want to send a response to the same P that like got this call right I mean from
54:22
which we got U we uh got here so that's why we kind of pre- Route the
54:30
call we are going to send and other than so other than that it's that's that's it
54:38
so okay um um yeah I'm not sure like I guess it's
54:44
more or less clear I think what I want to show is like you can use RPC without
54:50
Fusion all you need to do is nearly this
54:58
so first you add RPC then you do this and so we don't of
55:08
course like we can't expose Fusion clients but like we can expose RPC clients so and in
55:16
this case so we literally like that's literally all you need to do to uh
55:22
configure RPC on the client side you add RPC it is it's the same by the way on
55:27
the server side as well and then you uh well
55:33
uh tell that you're going to use web soet client for this RPC protocol and
55:39
like you add the client for this service and basically that what uh that's what
55:46
ensures that you will be able to use it it's proxy in like you will be resolved
55:52
basically this service okay that's uh that's mostly it I
55:59
think for the RPC did I change
56:06
something oh okay okay [Music]
56:14
um let me think about what else is like
56:19
worth mentioning here oh uh the last part and I guess uh is
56:26
going to be the most weird one and also the fun one so there is a demo called
56:35
mesh C
56:40
uh and uh it shows that like uh well let
56:47
me find
56:53
onec helpers route callede so and
57:01
usages so um you can so RPC call router is a
57:09
delegate and you can register your custom one so default one routes every
57:15
call to the default Pier default client Pier but you can register your own call
57:22
router and uh uh if you look at the code here here basically you will see that
57:28
this method gets an RPC method def the method that's that was called on the
57:33
client and all of its arguments right and uh it needs to return uh PF in the
57:43
end this thing that's that's I think think of it as a URL but more
57:50
flexible uh so basically it's like a custom URL with custom scheme something
57:55
like this so you see that this thing basically gets the type of the first argument
58:01
checks if it's host TR it's like if the first argument of a call is an exact
58:07
reference to specific cost then it's going to return a special ref that is a
58:16
post ref uh I mean basically it will it will
58:22
construct a reference to appear that is kind of associated with specific cost
58:30
then there is a more complex thing that uh like if it sees a Shar ref then it
58:38
returns basically a ref to a sharp and the difference is that like I mean if
58:43
you implement Dynamic sharding when poost join and like Lea shards right and
58:48
A Single Shard May resolve to one host first like at some point in time right
58:54
then to another host and so so on so uh basically it's a dynamic thing right and
59:00
that's why there is not PF that's more complex you by the way can see that they
59:05
are implemented right here so as part of this demo they uh you basically can find
59:12
all the source code that you need so and anyway and let's say if it sees that the
59:18
first argument is integer then like in reality it gets this integer construct a
59:23
Shard reference to a shard that can be associated with this integer and so so
59:31
basically like it's fully Dynamic routing and
59:39
um let me find mesh RPC here
59:46
right uh so when you run this sample that's it's and that's not it actually
59:53
so there is more here but so I'll pause it at some point and show you what's
1:00:00
going on so basically it simulates a situation when hosts are starting dying
1:00:07
and uh join basically some map of sharts and so plus here indicates that like
1:00:14
this host with this name like virtual host but it's like real server here uh I
1:00:21
mean real as.net server uh with uh uh
1:00:26
actual La RPC uh so it took Shard number one three
1:00:32
and like I don't know what this number is and the scheme changes right and so
1:00:38
what it does is like if you scroll here you will see that like it sends uh calls
1:00:45
to like basically so sorry so the service on each of these hosts it
1:00:52
basically tries to communicate with other hosts and and uh they maintain uh
1:00:58
like this map of sharts independently and like basically communicate
1:01:04
independently and so the whole point of this demo is that like there should be
1:01:09
no red lines showing that some calls like failed in a hard way without
1:01:15
reprocessing and stuff like this and yeah that that's what happens here I mean I can stop like and by the way it
1:01:22
calls Fusion Services nonfusion services and like basically basically it shows that the protocol is extremely tolerant
1:01:29
to different kind of failers so that's that's nearly it you see that like right
1:01:35
now kind of throws these calls very rapidly
1:01:42
okay so that's what actual RPC is um so
1:01:48
this part with uh routing and so so on it's kind of complicated and you really
1:01:54
need it only if you want to have a mesh of services on the server side and um
1:02:00
like I think the problem it solves like ultimately is that like you really don't need a low balancer with it and this
1:02:08
kind of also allows you to implement a more efficient communication schema
1:02:14
about on your server side uh plus uh yeah it's extremely how to say well
1:02:21
basically the error reprocessing like connection U problem and so so on they
1:02:26
are kind of out of scope because of the uh protocol and the
1:02:33
library okay uh back to slides
1:02:39
right that's not a slide so yeah I already
1:02:45
shown you this demo and yeah it's just a reminder that
1:02:53
if you use it together with usion you get like uh the numbers that are uh
1:03:02
completely out of reach uh even with RPC itself so it's a very different story if
1:03:10
you use it with Fusion so uh and just a reminder right um we
1:03:20
started uh from I think like not started but there was a slide showing how many
1:03:26
calls RIS can process if you think about these calls these are extremely
1:03:31
lightweight in terms of processing right so it's basically like kind of Dictionary
1:03:37
lookups so that I think I would say that the networking part in this case should
1:03:44
be kind of more expensive than the rest nevertheless so just 100
1:03:51
20,000 right calls per second and so here you
1:03:57
see numbers like well I mean these are kind of heavy calls so just 1.5 million
1:04:06
calls per second but yeah more lightweight calls similar to probably
1:04:12
the test on radius are well 6 million calls per second on this
1:04:17
machine okay it's like 60x like 50x compared to R this
1:04:29
so yeah that's where you can find actual up RPC samples and thanks a lot for
1:04:38
watching please feel free to reach me out ask any questions and yeah if you
1:04:45
would like to contribute something then that would be amazing okay thank you
