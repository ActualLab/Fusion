Source  
<a href="https://www.youtube.com/watch?v=eMO7AmI6ui4"><img src="https://img.youtube.com/vi/eMO7AmI6ui4/maxresdefault.jpg"></a>

## Table of Contents

- Introduction and Background (0:00)
- Redis Baseline (0:39)
- Fusion Speed (1:08)
- What is Fusion (1:47)
- Actual Chat Demo (8:08)
- Compute State Component (9:17)
- Why Real-time is Hard (11:30)
- Fusion Features (15:47)
- Todo App Demo (18:48)
- Code Explanation (43:59)
- How Fusion Works (1:07:31)
- Performance and Benchmarks (1:21:29)
- Conclusion (1:49:23)

Transcript

0:00
so I recorded this video yesterday and after uh composing the final version I

0:07
realized that two hours is way too long for any kind of intro

0:14
so uh I decided to record a prequel to this

0:21
intro uh an intro for intro and uh that's uh what we are going

0:29
to start uh with so what you see here is uh how radius

0:39
performs on my machine um it's 120,000 calls per second uh and let's uh

0:47
think of this number as a baseline right uh you can run this Benchmark in any

0:52
radius container the utility is called R Benchmark so 120,000 callers calls per

1:02
second and that's the number you are expected to get with Fusion on the same

1:08
machine it's a kind of similar one

1:14

130 but million calls per second and that's if you use fusions

1:21
client um so it's 1,500 times speed

1:27
up and if you use uh Fusion just on the server side so without its client then

1:33
the speed up is going to be about 20x and by the way notice that this number I

1:39
mean almost 90,000 calls per second it's pretty similar to what you saw for Ed is

1:45
so in this case the underlying storage is POS gra and uh yeah obviously when

1:50
there is not a lot of data then they are supposed to produce nearly the same kind of results uh

1:57
okay uh what you see here is even crazier so um the data on right or like

2:06
the traffic over webset connection on the right shows that um Fusion client is

2:13
uh managed to resolve six calls uh using a single uh round trip to

2:20
the server uh so first few uh first two

2:25
messages are a part of handshake and basically one is outg goinging and one

2:31
is incoming and six calls are resolved uh so why six uh well uh we need some

2:39
data to resolve a user from station and show this piece right then one call is

2:45
for summary and three calls out for this part the first one gets the list of IDs and then like two more calls gets the

2:53
data and uh I think what's weird is that uh there is a clearer data depend

2:59
dependency right so for example you need to get the IDS first and only after that you can get the items by these IDs or

3:07
like to get this whole piece of data you probably need to authenticate first so how it's possible that this thing

3:13
manages to kind of puckle of these calls into a single packet and get back the

3:19
data right uh and yeah right now I'm going to prove

3:25
you this so that's the real qu of UI component that renders the list of items

3:31
and basically all it does is it gets the list of IDs and well puts them into your

3:37
model and like when the model is rendered uh so the next component that's

3:42
the component that renders the item it gets the data for the item by uh well

3:48
basically it's ID so as I said there is a clear data dependency nevertheless

3:54
what you saw is like it's real uh a single uh round Tre to the server

4:01
um so you may think that this is kind of fictional scenario right and it's a small sample but that's the real app uh

4:08
actual chat the app that we built and it's built on top of fusion so it sends

4:15
around uh 1300 calls uh when it starts or reconnects at least for my account

4:23
right uh I have a decent number of chats and stuff like this and um it needs

4:30
approximately 15 messages to kind of get the data for them typically uh and it's just 10 kilobytes

4:37
of data on reconnection and uh yeah it supports offline mode as well which doesn't require any extra code so

4:43
basically if you start without internet connection it's still going to render nearly the same

4:49
stuff uh and that's the code from actual chat you you can instantly spot that the

4:56
code looks weird right I mean okay like stuff like this you maybe can

5:02
run it locally but like it doesn't seem like you can do this with uh on the

5:08
client side assuming that most of these calls are going to the server side and like basically trigger some kind of RPC

5:16
uh nevertheless that's the real code and moreover this code like actually uh so

5:22
it triggers one of frequent animations uh I'll talk about this a little bit

5:28
later so that's the repository uh that's uh where Fusion lives and uh uh the

5:38
original uh so originally it was called as a Steel fusion uh I started the

5:45
project while I was a CTO at service Tian and I think I left during one year

5:51
after that um I contributed to the original repository for the next I think

5:57
couple years but then we decided to basically start maintaining our own fork

6:02
and that's the address of the fork uh so

6:08
uh yeah the reason why it happened is like I was basically the like almost uh

6:14
exclusively the only contributor there so uh it makes sense actually

6:21
um now I'm also known for some posts on medium uh fancy comparison of Go versus

6:29
C I mean in reality it's very in-depth comparison so if you are interested and

6:35
definitely check this out and like most of my posts are uh kind of circling

6:40
around uh performance uh and do map um so as I said I'm also known as a

6:49
creator of actual chat um it's a fancy chat up that

6:55
uh allows you to talk uh in like uh

7:01
really talk and we provide both instant transcription and deliver your voice so

7:06
basically every chat is kind of like a walkie-talkie where people uh can talk

7:12
but don't have to listen all the time uh and uh yeah they can join at any moment

7:18
uh continue conversation by typing or just uh well telling what they want to

7:24
say okay and uh uh yeah uh I think I

7:29
think we bet on one simple thing that like many people actually don't like to type I mean think about your

7:36
grandparents or like young children or uh situations where you it's just not

7:42
convenient to type right and U uh we basically mix these two experiences into

7:49
a single medium typing and talking um there are a few different

7:56
versions of this app you can download any and uh in any other sense it's more

8:02
or less uh like normal chat so um it

8:08
should be a good Feit for like either family chatting or like a small team where conversations are

8:14
frequent okay and now we are going to see the first demo like the first

8:25
example so um that's how actual chat works you

8:33
press this big recording button and start talking and everyone else who sees the same chat they see the transcription

8:40
and can listen to what other person says but like in reality uh yeah if you want

8:47
to respond you typically just respond and uh uh that's it so I'm going to talk

8:53
about this little button you see it's spinning right now um probably yeah I'll have to to

8:59
replay this part so this little button uh it spins

9:05
when someone else talks in uh the chat

9:10
you uh in in the chat you are in right so

9:17
it's a fusion component and all of them inherit from computer State component uh

9:24
the model uh is declared right here the model I mean the data that uh basically

9:31
this thing uh produces to render uh itself and uh that's the key method so

9:38
compute state is a method that exists in every computer State component so that's

9:45
the DAT compute computes and we are going to focus on is animated property

9:52
here or flag uh as you see like the way it produces this uh flag is first it get

9:59
gets uh chat audio state from chat audio UI well basically it's clear that it's

10:08
uh some uh kind of state that describes what what's going on with audio right

10:14
now uh is something Plank and so so on so as you see first it checks that like

10:23
uh you aren't listening right now which means that like this button is depressed

10:28
uh sorry yeah uh not pressed uh and then we get uh information about the current

10:35
chat uh get last text entry there see if it's streaming if it's has an audio

10:41
entry right and if that's the case then we get uh your own author in this chat

10:50
and uh check if the message is basically not originating from you so if like

10:56
someone else is talking then we start any so that's the code like and I think

11:04
what's unusual here right is that I mean okay like if you have all the data

11:09
locally then this code may work but in reality it works on the client nevertheless the code is kind

11:16
of it's it's weird it it looks like everything is like literally every bit

11:23
of data uh exists on the client right so why real time is

11:30
hard uh it's hard not because uh like just

11:36
reporting changes is something that's complex it's hard because you usually

11:42
have tons of different components right uh which report the state changes and in

11:49
the end you need to combine a single state for example you show the one you

11:54
show in the UI um and you need to make sure that the the single state is eventually

12:01
consistent the changes there are displayed in the right temporal order and so so on and that's the part uh

12:09
which is hard to get right uh

12:14
overall you need some uh framework and some set of core

12:20
principles uh that uh allow you to build all of that uh otherwise it's going to

12:27
be extremely complicated and let me show you why so

12:33
um let's say we want to like uh approach real time uh like we normally do so

12:43
let's say we have an API which returns a single item right and we want to render

12:50
it and then we want to observe for its changes and render each of these changes

12:57
right so that's the quote we probably would write in this case uh the only thing that's missing here I guess is I

13:04
forgot to write state has changes uh somewhere here

13:09
right well uh reality is very different uh like there are many problems in this

13:17
code and uh yeah right now I'll show you some so first of all we need to

13:26
unsubscribe from like receiving changes at some point so we can do it by

13:32
implementing I disposable here right and like implementing some sort of cancellation token we pass to observe

13:40
changes which is going to abort in the end and unsubscribe but like another problem is

13:47
that we need to call this method first uh and only after that call this one

13:52
because otherwise we may miss some changes uh and uh

14:00
so another problem is that this code doesn't do anything in case we

14:06
disconnect basically doesn't handle the situation with disconnects and

14:11
reconnects right and we also didn't touch the code on the server side because observe item has to be like

14:19
nearly as complex and as the code here actually maybe even more because you may

14:24
have a mesh of servers on the server side and state might be

14:30
um composed as well so um uh that's nearly the scenario we

14:38
have to deal with right and what I shown you here is like I didn't even touch the

14:45
piece where we combine a state from like multiple sources uh so even this part I mean when

14:53
we deal with a single Source it's kind of complicated already right and uh

15:00
these complications or like complexity that we get is what uh what we want to

15:07
avoid by all means because uh like

15:12
complexity I mean it's good to some extent but like uh it's well known that

15:20
that's sort of uh sorry the source of uh well the worst problems you may F face

15:27
in especially in the kind of second half of a lifetime of any product uh and it

15:34
skills not just products but the societies so you may find some

15:40
interesting analysis on this on the web okay okay so uh what is

15:47
fusion fusion is um distributed State Management obstruction which kind of

15:54
views the whole state of Europe as something it manages

16:00
uh that includes the state that's Exposed on the clients on the servers on

16:06
backends basically it connects all these pieces together and make sure that like

16:12
they get all the updates in real time uh

16:17
interestingly it's um it also solves many problems which don't look like even

16:26
related to uh real time for example caching uh well and uh

16:34
that's because like you have a perfect caching right you need to evict a stuff

16:41
from cash at the right moment I mean ideally as quickly as possible and uh to have this part you

16:49
need something that tells you that like this thing changed so that's how it's related to real time then um it's also

16:58
related to uh mobile apps and like reducing the network traffic basically

17:04
being very savy on network traffic uh because again uh if you know that

17:12
certain thing still stays the same you don't have to request it again from uh

17:18
the server uh it's also related to uh monoliths and microservices basically it

17:24
allows you to build a monolith that is extremely easy to transition to

17:30
microservice architecture and yeah I'll later I'll explain why and um like as

17:36
you saw already right uh another interesting piece is that like the code you write with Fusion looks kind of

17:44
almost normal uh it looks almost exactly like the code you would write but

17:50
without Fusion so that's a cool part because it's quite readable quite clean

17:55
and uh easy to understand well and finally it's uh really kind of Blazer friendly

18:04
there is a very thin integration with blazer but basically it's a natural fit for Blazer ups and uh yeah one of again

18:13
kind of fancy consequences is that you can build um server side Blazer UI and

18:21
web assembly UI for hybrid UI and it's going to be the same code like you don't

18:26
really need to make any changes begin to work uh that's how it works in actell

18:32
chat and in all Fusion samples so you can switch them from server side Blazer to web assembly basically Standalone

18:39
client or client running on the server and it's it's the same thing okay um so now I'm going to show

18:48
you one of U kind of uh bigger samples uh from Fusion it's

18:56
called Todo up and uh I'm going to um basically my goal is to

19:04
illustrate how uh all of that uh

19:09
basically impacts on um certain behaviors of the app so let's

19:19
start let's start the app right

19:29
so I already have a couple windows open you see that it just reconnected and I

19:36
am authenticated here well let's item

19:42
three and do the check box so you see that basically all these properties they

19:47
update and this one is by the way working Blazer server this one is web

19:53
assembly let's switch this one to web assembly as well

19:58
so you see they work in sync right now I'm going to sign out just to show how

20:05
it works and or sign in so it basically does this in both

20:11
windows okay um sign

20:17
out um so um there are two versions of

20:22
uh this uh to-do page and uh um the

20:28
version two is the one we are going to uh study and

20:36
um yeah let's have one item here for now

20:43
so um yeah I'm I'm going to stop the up

20:50
right

20:57
now so um let's open these pages

21:03
right to do page one and is to do page two

21:09
right um so what's the difference the difference here is uh how like computed

21:18
State uh is implemented um you see that uh on page

21:24
one it basically calls uh some to-do service and gets all the items and the item here

21:33
is to- do item if we click on it we will see that basically it contains

21:39
everything right then uh if we go to to do page

21:44
two uh you will find out that this thing

21:49
fetches IDs of the items and its model it uh uh exposes these IDs so uh the

21:59
code that renders the page uses to- do item view which gets the ID if you click

22:06
on to do item view then you will see that its compute State method uh

22:13
actually gets the item by this ID and renders it and uh as for the first page

22:22
it's a little bit different there it use a different component to render the item

22:28
call too item row you and this one doesn't fetch anything it basically gets

22:34
the item and renders it so uh I think uh what's reasonable to

22:42
ask right now is like um what happens uh under the hood right what kind of uh

22:49
requests it sends to the server and U like how basically it gets all the data

22:57
so we're going to run it again right now and um I'm also going to leave just

23:04
one of these windows and open

23:09
Chrome inspector okay

23:15
okay so first let's refresh the page oh

23:21
my God let not to do uh let's refresh the page right

23:30
uh and how do we clear everything

23:42
here okay now uh

23:50
let's let's retry okay so it sends um

23:59
so it doesn't uh use HTTP client to send all these requests it uses custom

24:06
protocol and that's why I basically enabled some logging and showing you

24:12
what kind of um calls it makes uh so uh that's the first call uh

24:19
the first call is handshake right then it uh calls get summary that's this

24:25
component right then Authentication part like pulls I think that's to render this

24:32
component uh pulls some data you see that uh get summary now got a

24:39
response uh then uh I guess uh authentication get user get a response

24:46
and um I think in the end well uh let's

24:51
focus on list this yeah so it uh I think

24:56
all these calls if you look at the like timing of all these calls it basically like sends all these calls almost

25:04
simultaneously right and then gets the first kind of chunk of data let's add

25:10
one more item two and I want to add also item three and item

25:17
four so let's uh let's uh do uh refresh

25:23
once more so you see that uh like

25:30
as soon as uh list IDs returns uh the

25:36
result it sends like all these get methods like I mean go get requests for

25:42
the item data in parallel and uh uh like gets the data right if you go to tut2

25:49
page two and press refresh you will I think

25:55
um wait uh

26:03
sorry so you will basically see the same uh output so it's like list ID returns

26:10
then it sends a bunch of calls to get the items right and as for everything

26:16
else so basically they Works in a very similar way

26:21
right okay so um

26:28
now let's uh also look at the networking tab right and um what I want to do here

26:36
again let's uh clean blog so I want to show you how basically many exchanges

26:44
happens here so these first two are handshakes then that's the request that

26:51
combines a bunch of calls and uh like get summary data user data I think and

26:56
other stuff and this is the request that uh I think gets all the

27:02
items so what's interesting is that like from the networking standpoint uh it

27:08
looks like uh like accept handshake just two exchanges I mean just two like round

27:14
trips happened right okay um and yeah by the way I'm so

27:21
I'm going to uh now I'm going to show you

27:26
uh the

27:33
um the most option sorry optimal version of this

27:39
exchange uh so uh what I did uh before

27:45
running this sample is I disabled one of services here and um basically the

27:52
service uh is called uh local storage remote

27:58
computed cach you can uh click on it it's implemented right here it's a very simple service

28:05
that like basically extend extends remote computed cache so you can have a

28:11
custom version of it better one than this one but uh so let's see what's

28:18
going to happen uh once I run the app with this service

28:39
so again cleaning V refresh we need updated

28:45
client and you see the first refresh it's nearly the same exchange right so it's

28:50
basically um let me actually do one interesting

28:57
thing so I'll change the protocol as well right now and uh we'll use the protocol

29:06
that at least uh shows uh the names of the methods it uh calls right in so

29:13
basically the one that was used here it's like extremely efficient version and it uses um anyway that's I guess

29:23
I'll explain it later but for now just let's switch it to uh um let's switch

29:29
the client to a less efficient protocol in terms of like the amount of data send

29:36
a little bit less efficient so I refresh and click here there is

29:42
literally so this is a handshake request response right but this one I mean this

29:50
message it combines literally everything uh I mean we see that there is a get

29:56
summary get user and even uh to do API get uh

30:05
methods like basically get list IDs uh so literally everything

30:13
uh is combined into a single uh Network

30:18
packet and moreover that's how the response looks like so the question is

30:24
like what's going on right uh how it's possible that this thing um literally

30:32
just makes just a single network round FP and gets all the

30:37
data and uh yeah let me show you uh what happens

30:44
under the foot so if we go into the application local storage so I shown you

30:50
that like we now use cash and uh what

30:55
actually happens is that Fusion CL basically emulates

31:02
the connectivity with the server and like when it gets a request to call one

31:09
of remote methods if it has a value in its local cash then it immediately

31:15
responds and uh at the same time it validates it against the I mean

31:22
basically sends a request to the server which uh checks if the value that client

31:29
has is still the same and sends back the data only if it differs otherwise it

31:34
responds that hey your data is still correct um so

31:41
um now so so um let me clear it uh and uh just again

31:51
to show how the exchange starts so that that's

31:58
what happens when you have an empty cache right basically it sends the first Chun of calls the once it gets all in

32:07
like in a single Network bucket uh uh when you refresh the page gets a

32:14
response and then sends next Chun of calls okay

32:23
um so uh let's check a few uh things here first let's see if it's going to

32:30
work if I disable web sockets uh like disable web sockets

32:37
completely so what okay should be use

32:42
web soet right

32:49
mhm okay so we will disable this

32:54
part and I'm yeah let's restart the

33:08
server so you see that it's unable to reconnect right right now basically if I

33:14
click reconnect nothing happens it can't reconnect uh and if we go to console

33:22
then uh well it's going to dump your messages here um but like

33:31
it kind of pretends like it works uh I just clicked here right and it can

33:37
basically run any actions but nevertheless right now it's kind of let

33:45
me show you I can even switch these Pages give it you authentication like basically it behaves

33:50
like it works let's refresh the page so I just clicked refresh it still

33:58
can't reconnect it basically um yeah it dumps all these zor messages

34:06
because it can't reconnect uh but nevertheless I still can uh kind of

34:13
scroll through the app and so that's what uh this remote computed cat does

34:20
basically emulates the presence of a server uh the server that responds

34:25
instantly with the last version of well whatever data you requested that it

34:31
knows on the other hand since like and you will learn about this later in this

34:37
video but since everything is designed that basically every piece of data May uh kind of become stale and report that

34:45
it has to be updated and like uh this emulation is possible because uh well

34:51
literally if server or like if the client sees that the new value on the server is different

34:58
uh like for example after reconnection right then um it will

35:07
uh it will run a process that will invalidate it here and like recompute

35:13
and fetch the new data so basically um all of this uh kind of works in tandem

35:23
with uh this mechanism that allows uh Fusion to distribute the information

35:31
about changes okay let's check one more thing uh here

35:36
so right now uh so right now uh we're going to leave this

35:43
client operational and we going to change the back end on the server side

35:49
and restart the server uh what like supposed to happen right uh

35:58
is that like okay there is a different server different band so it has to

36:03
update the data because like it can cannot expose this data anymore um let

36:11
me do this so first let's reenable web

36:18
circuits and run it I just want you to like see that the client will reconnect

36:27
in this case is and like basically will continue working as it's supposed

36:33
to just connect it yeah so the data is there you can make

36:39
changes now now I'm again stopping the server you see that okay

36:45
disconnected and um so let's replace

36:52
uh uh so you see that like the service that's uh um I2 do API is the service

37:02
that uh is used on the client side and

37:08
uh there is a server for this service right so that's how the server is

37:13
registered Fusion at server and that's the service and that's the

37:19
implementation so we are going to swap it right now with this thing and uh so

37:26
this thing goes to the dat database and like it's even more complex than that and I'm going to like use right now a

37:33
completely different implementation and uh so let's start the

37:39
server and see what's going to happen here right just connected so you see

37:44
that it like refreshed everything right now basically if you look here uh it

37:52
sent all these calls that are necessary to refresh the data right

37:58
and uh so which one is spending uh this one right we are

38:05
looking for it basically tries to connect I guess too many

38:14
times I'm not okay finished finished

38:22
finished yeah I'm not sure like uh how to find the right web

38:29
circuit connection now but I guess it's fine so the point was that like it

38:34
changed the state okay let's add a bunch of items

38:41
here well no wait uh so we need yeah we can't use in memory service

38:49
for this it's going

38:54
to and so um my point is it works but if I shut

39:01
down the server right then um it's going to uh well um the state will be uh will

39:15
disappear so um let me quickly get back to the old

39:23
version H it's it's by the way it's wait wait wait wait uh so it's a fancy thing

39:30
it just reconnected and uh shown the Old State yeah I wanted to show like what's

39:36
going to happen here so uh now the interesting question

39:42
is so it sends uh a bunch of calls right when it starts right and uh I think the

39:50
question is like if this state is catched what part of logic is

39:55
responsible for sying that cach State and right now I'm going to show you that

40:01
like uh it's uh okay so I just refreshed oh by the

40:09
way that's that's also interesting to see it right so that's what what happens

40:14
in presence of a cache so you see that like it literally throws every call like

40:21
in parallel before it even gets the response it's like get summary and and

40:27
it looks here that it sends the Gap all first it just kind of goes through first right in reality it requested this thing

40:34
it responded immediately then it sends uh like it requested all these things and like uh basically that's how it gets

40:43
this single packet uh to send on the server because like all these calls they

40:48
are literally produced synchronously and uh they are ready to

40:54
send basically at almost the same time uh so let's let's uh just modify the UI

41:03
code and remove this uh thing and I think the expected effect is that we should not see these calls as well right

41:17
um so we need let's do this on Tod do page two uh which features the IDS

41:25
right um

41:41
I think it's fine uh let's start

41:48
it so right now I have to refresh the client and

41:55
uh I click control r so you see that uh there are no calls to

42:04
both list IDs and get uh like uh uh I

42:10
mean to do api. getet if we go to

42:15
another version of this page and we didn't modify it then like that's what

42:21
going to happen it sends all these calls and if you will go to uh networking tab

42:31
you may see that I think all these calls were sent so this is list this get get

42:40
get they were sent at once and uh yeah uh in terms of

42:48
responses uh so they came in two different pockets and that's basically

42:54
depends on server right if server can produce the response syn noly synchronously like because data is

43:00
available and it does it otherwise it may send send a bunch of buckets

43:06
okay so a lot of fun stuff but I think the main conclusion here is that like

43:12
it's extremely efficient in terms of getting the data via Network and uh like

43:19
even packaging it into uh basically the least possible number of Transmissions

43:26
not exactly least possible but quite close to that okay now we are going to see how it

43:35
works so basically what is the code that's responsible for all these real time updates in this app and um like

43:44
look a little bit deeper so far we saw mainly the UI code right and uh we are

43:51
going to look into the server site code okay um

43:59
so I'm I'm going to um basically undo some of my changes

44:06
here and what else what else let me see

44:11
yeah I think that's the only change that I I made oh yeah so like let's keep this

44:19
protocol for now so let's start from

44:27
uh to- do page one the one that basically fetch everything with this

44:36
call so what is todu todu is to

44:42
do uh let's see how this thing is registered

44:48
right um You can easily see that it's

44:53
registered as a compute service which is scoped scoped means it lives in like

45:01
Blazer scop so basically it's a client side service like we are talking about the Blazer um apps

45:09
right uh so uh and uh you see that it was uh

45:16
registered as compute service and uh it also kind of extends this weird

45:23
interface I compute Service uh so it's a tagging interface well

45:32
for yeah the comment is wrong here but it's tagging interface for comput

45:38
service yeah uh so basically I think it's it's a copy from RPC service requ

45:44
iing proxy probably from one of these interfaces but the point is like uh this

45:50
is a tagging interface you don't have to implement any methods it just like tells

45:56
uh Pro generator that this thing needs a proxy and I'll talk about this later

46:02
then so we were calling this method here right and uh uh what it does it calls to

46:10
do API list IDs then it for each of these IDs it calls this method so

46:16
basically gets the item collect is Task one0 basically it's like a we can

46:22
rewrite it as T all and

46:28
um something like this wait it's it's uh

46:36
well it here something like this it's the same code

46:42
uh I'll return it back collect is just a little bit more flexible thing

46:49
um so what you see here is that like this

46:56
thing literally calls uh to do API and gets all the data from it kind of

47:01
rearranges it right pugs it into uh basically uh a single array or list

47:09
right and um returns it now uh so what is to- do API um Tod

47:16
do API is um an interface let's see how this thing

47:23
is registered and uh um well you can find

47:30
there is a number of registrations but we need the one that's uh registered on

47:36
the client that's the thing basically it's a fusion client for this uh

47:44
service if we click on it then we see that it has well add door update and

47:50
remove methods uh like Let's ignore this command Handler thing but again we see

47:56
this comput method compute method compute method on get list this and get

48:02
summary so that's the method that uh that's

48:08
basically Fusion method the method that's kind of backed by Fusion uh

48:15
logic um and

48:22
um let's find the server right for this client uh um so I'm going

48:28
to uh do the same thing and that's how it is registered on the server site at

48:36
server Tod do API let's go to the to-do

48:44
API um so list IDs here uh

48:51
um it's pretty simple it calls get folder uh and pass the station here

48:58
right and then calls back end uh passing

49:03
the folder so you may think of this as uh so uh to do API is basically a

49:11
front-end service a service that is responsible for kind of rewrapping what

49:18
backends can provide but uh running extra security checks and basically uh

49:25
kind of filtering out what you can't access right and what it does is like uh

49:30
basically if you look at get folder method there you will see that uh it

49:37
resolves the user uh from s and so ignore the tenant part but I think

49:44
basically if the so this thing is going to be empty uh in this case because we

49:50
don't run it in multi- tency mode but yeah sample can be executed in multi

49:55
tency mode as well so this thing is going to be empty uh this whole piece

50:02
right and ultimately it like basically returns a prefix to some key value store

50:09
right which is either this or that if you are dependently on whether you are

50:14
authenticated or not right and then for this prefix or folder it uh so oh sorry

50:23
list ID SCS uh well method providing

50:28
this folder so you you can think of it as like basically this part uh did some

50:33
Security checks right and kind of rout at the user to the right location on the

50:38
backend storage and then like it calls the back end and gets the data and uh um

50:47
let's look at this thing right so theend is also compute Service uh and uh and by the way uh let

50:55
me show you one other I think get user is also compute service

51:01
so um you see that like nearly every call that they make is uh a compute

51:09
service method or a method that calls some other compute service methods like like this one it doesn't have this

51:14
compute service thing oh let me clarify one thing uh so there is no compute

51:20
service attribute here but it's inherited from the interface kind of inherited because it's here okay so the

51:29
point is uh the point is uh so let's go to the list on the back end and go to

51:37
now I'll just use I'm going to use this very way simpler navigation and just

51:42
give to the implementation oh and yeah one of my old break points is here so if

51:49
you look here you will find out that like this code is again like almost

51:55
normal in terms of like what it does it basically gets the DB context right

52:02
using some uh well um additional service

52:08
but uh ultimately it gets the DB context and then just Runing query and gets all

52:14
the keys right uh maybe by like with some kind of

52:20
postprocessing uh so um what's unusual is I guess this part

52:28
right but that's nearly it so let's

52:36
um so the question is right what happens when we add the new

52:42
too item so that like somehow somehow this method gets called

52:48
again right and the answer would

52:54
be uh well yeah let's let's leave these break

53:00
points actually let's leave these break points um the answer would be this

53:10
piece uh so any method that makes some change it has a block like this uh and

53:20
um this block is responsible for uh running socalled invest validations

53:27
basically whatever method is called here with whatever arguments

53:32
passed um the result of this method if it was cached before then it's going to

53:40
be you you may think of it as if it's going to be evicted from the cach so in

53:46
reality it's not exactly like this and it's it's a little bit different and but

53:51
you may think of like what happens here as I just described basically if you

53:57
call uh for example get summary method with some folder here and default is for

54:03
cancellation token I just want to show you so you can remove it but like it's

54:08
going to highlight it right so okay uh I mean the writer is going to kind of

54:14
complain uh so let me show you one thing I'm going to comment out this piece right now and

54:22
uh now when I will be removing an item summary is not going to

54:29
update so since we changed just the server right now let me see right so

54:38
that's the only change that we've made then uh I don't have to update the client sorry that's not the that's not

54:47
what I want to show okay so summary you

54:53
see that like right now it's updating the delay there is intentional so it's not like there is a protocol delay or

55:00
something it's basically one of settings I'm not going to show like uh well maybe

55:07
I'll show how easy it is to like control all these delays but the point is so

55:13
right now you see that summary gets updated now let's try to remove

55:18
one and you will see that like it wouldn't change and moreover

55:26
uh it will never change like uh basically unless uh I mean I can call

55:32
like state has changed inv validate recompute I can open another version of

55:38
this page and it's still incorrect because uh because no one told

55:46
Fusion that summary has to be invalidated uh so that's that's how it

55:53
works but if we will add the a new it or like edit this one for

56:00
example oh no so on edits it's not going to be updated but when we add an

56:09
item it it's again in sync right now so um uh let me get back to

56:19
the code that we we're playing

56:24
quiz so get some I think the reasonable question to ask is like why there is such a weird syntax

56:32
and what actually happens because it's like it feels

56:38
like uh this code it kind of cannot run

56:44
once right I mean it feels like this method runs the number of times and

56:51
that's that's what actually happens uh so when this method is executed for the

56:57
first time or like when basically the original call is made it goes through

57:04
basically this part and uh this condition is false

57:11
but uh once it completes and once the transaction uh is getting committed and

57:18
that's why by the way it uses a special call here to get uh Deb text uh such DB

57:26
context or like basically it tells that like okay this method makes changes and

57:32
it needs a special DB context that is going to be uh I mean the transaction is

57:38
going to be created there and a bunch of other things that kind of make this Machinery work but ultimately in the end

57:45
when the transaction uh is committed it's going to run this piece and it's

57:50
going to run it not only on like this single machine but on every machine in

57:56
the uh cluster that uh like talks to the same

58:01
DB uh so basically uh it's uh like another aspect

58:09
that kind of helps uh this distributed Mode work uh I mean on the server side

58:15
it doesn't even have to kind of sync something over the network protocol or

58:22
like basically there is a different uh approach that allows different servers

58:28
to uh invalidate uh a part of their State uh

58:33
when you make changes on one of these machines and it's extremely reliable uh

58:40
like uh uh I think well I mean the simple explanation would be that it uses

58:47
outbox pattern but in reality it's like a little bit more complex and

58:53
advanced um okay

58:58
um and yeah one thing I want to show you

59:03
is API so if you like don't involve the

59:09
DB and use more kind of low level approach then uh that's the code that

59:17
actually uh allows you to invalidate something uh you uh run this thing in

59:25
using log and whatever you call inside is uh getting

59:30
invalidated so uh in this case basically the block like this is executed by one

59:39
of pipeline handlers for this command like any command actually basically

59:45
detects that okay there is an operation that was committed so I have to run this

59:50
command G but in the invalidation mode that's nearly how it works uh um let me

59:57
show you the last thing uh right now so I'm going to run uh Aspire host for uh

1:00:06
this sample and

1:00:12
uh it is going to be even more

1:00:18
interesting I don't want to open it here okay let me open it in Chrome as

1:00:30
well oh wait I need this

1:00:36
thing okay so right now this thing starts started like six

1:00:45
hosts uh three API hosts on five, five 6

1:00:51
and seven and for each of these host hosts it started back end hosts so

1:00:57
backend hosts they run a backend API host they run to do API and API talks to

1:01:05
a backend on corresponding host so um

1:01:11
now let's do

1:01:19
well we're going to close this thing and yeah

1:01:27
and we're going to go to another post here so remember they still talk to the

1:01:36
same I mean they still use the same DB and what I'm going to show you now is

1:01:41
how these distributed inv validation on back end for so you see that like this

1:01:47
is a different front end host and this is like also a different front end host

1:01:52
they use different backend hosts but in the end they use the same DB so what's

1:01:58
going to happen if I click here

1:02:03
oh

1:02:09
um six what happened with this thing

1:02:27
okay now let me show you the last uh piece about this sample so I'm going to

1:02:34
start Aspire host and

1:02:39
uh yeah um not here so I already opened it here

1:02:50
will it yeah it works so um let me

1:02:56
let me get full screen for now so uh Aspire host started six hosts

1:03:07
uh basically three API hosts and three backend hosts and each API host like

1:03:14
this one for example running on 50005 Port is tooking to this back end

1:03:20
and like basically to do API Services working here and uh uh it uses backend

1:03:29
as a fusion client so I to do backend which uh actually uh is hosted

1:03:37
here and this service uses this backend and so so on

1:03:43
so now uh yeah let's refresh both

1:03:50
things so you may notice that ports are different here right basically they use

1:03:56
different front ends so the different apis um API hosts and these API hosts

1:04:02
they use different backhand hosts and the only common thing they use is

1:04:09
database uh so are they going to

1:04:15
sync well as you see they do uh and that's how this distributed invalidation

1:04:22
on backand Works uh like yeah they stay in sync as they are

1:04:31
supposed to be um so let me you can Swit switch this to server like really

1:04:38
doesn't matter what you do uh so let's

1:04:46
um let's do one other thing

1:04:52
sohm you can find traces here for any host but I'm going to focus on metric

1:05:00
and we are going to uh observe metrix from

1:05:10
the let's observe Matrix from uh this back

1:05:15
end and let's go to call duration call count right so uh

1:05:24
list IDs uh basically it gets called when we

1:05:30
change something right I mean in terms of number of items in the

1:05:36
list Let's uh okay I can't click here yeah so I just removed we get another

1:05:43
call so the interesting question is what's going to happen if I

1:05:51
um click refresh here that's the first thing will it call back backend uh once

1:05:57
more cuz like I mean in in the normal situation right uh like this thing will

1:06:04
try to get the data and will hit the back end again so I'm refreshing

1:06:11
it refreshed refreshed it doesn't hit the

1:06:17
back end right uh the reason is that like the value the value that it gets

1:06:24
it's catched on the front end let's kill the back

1:06:31
endm so we go to resources po one backend can we kill it

1:06:39
[Music] somehow let's try

1:06:49
to well

1:06:55
looks like yeah I can't kill it here

1:07:02
unfortunately um yeah I don't know how to like U but

1:07:09
my point was that like if I kill the front end then uh of course uh it would

1:07:16
get the heat on the back end so I can show this

1:07:21
unfortunately okay so uh that's uh the demo part and let's move

1:07:31
on so we went through the most complex part and uh now uh I'm going to explain

1:07:41
how all of this stuff works so think about how you build uh binaries

1:07:51
uh now most of uh build tools like make uh Ms build uh net build whatever they

1:08:00
are incremental Builders which means that they uh build the final binary by

1:08:07
building some intermediates from basically the very low level inputs like

1:08:13
well files or like whatever they reference and uh so imagine that you

1:08:21
change one of these files right for example services. CS

1:08:26
then like whatever depends on this file kind of gets you you may think of like

1:08:33
what incremental Builder does is that it kind of marks everything that depends on this file as stale or like

1:08:41
invalidated and uh uh so for example if you want to build this thing up. ex

1:08:49
there right and it will rebuild server. dll services. DL and so basic basically

1:08:55
whatever was uh like on this path right and uh as for the rest it will simply

1:09:03
reuse that right for example uidl is going to be reused and that's exactly

1:09:09
what Fusion does uh like uh it might be unclear how it's relevant

1:09:17
to what you saw but Fusion does exactly the same thing but for your functions so

1:09:24
you like may look at your build processes function calls like where the

1:09:29
argument like that's the function right you call and that's the argument and uh if

1:09:36
Fusion gets a chance to transform these functions to such incremental Builders

1:09:42
then like you would get exactly the same thing as incremental build there is one of samples by the way in I think it's

1:09:52
uh probably hello world sample or something like this it does exactly this in like but like simulating exactly this

1:10:01
but on top of fusion yeah or like uh so I think that's

1:10:10
uh the same graph but with just different methods and different

1:10:15
arguments so that's how Fusion works and I

1:10:21
think the only thing you need to

1:10:26
kind of add to so you already can like build or

1:10:32
describe such graphs in your programs right by just calling functions the only

1:10:38
missing piece is this like uh inv validation concept and I already shown

1:10:44
you how it works in case with Fusion you basically use inv validation block and

1:10:50
call functions which has to be marked as stale with like the arguments you pass

1:10:56
there so that's nearly how it works now uh

1:11:02
yeah and obviously like if you think about um I mean any modern app then like

1:11:11
in reality you can think of it as you simply rebuild the UI when like by

1:11:19
calling functions which call other functions and so so on and uh so if you

1:11:24
have this feature right that basically marks uh certain call as stale and like

1:11:32
you can rebuild the UI so uh as a reaction to some

1:11:37
change okay so uh how Fusion works well it wraps

1:11:47
your functions into basically higher order function uh decorator uh uh that

1:11:56
adds this incremental build and caching behavior and uh a very

1:12:03
very or like extremely simplified version of this decorator would look

1:12:09
like this it produces a key from function and function you call I mean

1:12:17
and it's input input includes This and like all the other arguments right then it looks into some cach trying to get

1:12:26
basically a box that kind of describes previously computed value let's call

1:12:32
this boxes computed right and uh well if it gets one it uses it uh otherwise

1:12:41
locks uh well this key and tries to get it once more so it's basically a double

1:12:47
check locking right and returns it if it finds it but if it can't find such a box

1:12:54
then it basically it produces it and um uh so here like it basically creates the

1:13:01
Box exposes it and then calls the function that is supposed to

1:13:08
uh compute the value for this box and uh so

1:13:13
interestingly uh if during the computation it gets calls to similar

1:13:19
functions right then they will resolve with this use or this use or this use

1:13:24
and since we exposed the current box and basically this thing allows them to

1:13:30
register as dependencies in the current box and that's how it builds this graph

1:13:36
of uh basically cached computed values uh each time it computes something it

1:13:43
kind of updates or like rilds the small part of this graph so in reality it's way more

1:13:50
complex than this way more complex because like first uh well

1:13:56
uh you can't it's pseud code right you can write it on C and second is like

1:14:01
it's a synchronous it's truly threat safe and like many many other things but ultimately it's basically does nearly

1:14:09
this job okay and yeah if you think of like uh how so what happens with these

1:14:18
boxes over their lifetime or like what happens with computations right and so

1:14:25
Al you start the computation and get is result or error and like we can kind of

1:14:31
draw a separate part for cancellation on that net so it's like in reality it's

1:14:37
Herer but like special kind of here right and so with Fusion it kind of

1:14:42
extends this thing uh since this thing produces a box that sits behind the scenes and no one kind of sees it except

1:14:50
Fusion right then like B basically it Returns the value from this box but the

1:14:56
Box itself stays and uh at some point later it may become

1:15:04
invalidated uh so this is a very very ancient kind of Animation that I create

1:15:10
on like how invalidation works so that's why this part it's like it's totally

1:15:18
irrelevant uh right now it works a little bit differently but uh I think

1:15:24
it's kind good in terms of illustrating what happens under the scene when some

1:15:31
invalidation happens and then recomputation happens so we invalidate like let's say like one of low level

1:15:38
values for example like on back end right and it invalidates everything until the front end that depends on it

1:15:46
and then front end like decides okay I need to recompute this and then basically it rebuilds a set of nodes by

1:15:52
and some of these noes May reuse other computed values implicitly so you

1:15:58
like you don't have to do anything to for this to happen but like nevertheless

1:16:03
that's nearly what happens under the

1:16:08
food okay uh yeah so

1:16:14
uh we will skip this part because I already shown how it works uh and uh I

1:16:22
think uh we already kind of know what approximately happens when we

1:16:28
invalidate or don't invalidate things now

1:16:35
uh one other interesting part is like this Fusion client right so you saw that

1:16:43
um web assembly client talks to the server and servers may talk to other

1:16:49
servers like for example the backend um uh could

1:16:55
talk with the uh sorry the front end API service could talk with backend service

1:17:01
running on another poost so

1:17:07
um and uh the reason why uh Fusion uses

1:17:12
uh well basically a special client is that this protocol and that's

1:17:20
the protocol you kind of normally use for like any other RPC service it

1:17:26
doesn't work in Fusion case and you probably already know why it's not

1:17:32
enough for Fusion so uh you see that like we throw one message to send the

1:17:39
call and throw like the server responds with another message and delivers the

1:17:44
result in case with Fusion there can be a third message uh called

1:17:51
invalidate why I'm saying can be well because uh in

1:17:56
reality uh I think for a majority of data that leaves on the

1:18:04
client uh it doesn't get it uh even like

1:18:10
I mean every session so um just think about this like most of user data most

1:18:16
of like uh whatever you show it actually doesn't change so this part almost never

1:18:23
happens in reality uh but nevertheless the protocol has to support this piece right and one other

1:18:31
important thing is that like if you think about this uh it doesn't change

1:18:36
anything that this piece exist in sense that like okay you uh so instead of like

1:18:46
one one round trip you get like one plus maybe two uh Transmissions but in terms

1:18:54
of like it doesn't change the big picture basically and this message is extremely short it basically tells that

1:19:01
the call number three is now invalidated well and uh I think it worth

1:19:08
mentioning that Fusion protocol also supports this thing it's like it basically it's designed in assumption

1:19:15
that the client may have an offline cash for any result it gets so since this

1:19:23
part is is kind of is here right then

1:19:28
well basically when this message goes uh to the client it can evict the value from cash instantly right and uh

1:19:36
basically if you don't have this piece then your client side cash is kind of useless because it's going to return uh

1:19:44
well mostly it's going to return stale values I guess right and if you have this part on the other hand then like it

1:19:52
becomes well basically extremely useful and you still need some extensions to

1:19:57
the protocol right for example this kind of response much response that doesn't

1:20:02
send the data back right um to make it

1:20:08
work okay so

1:20:15
um and now I think uh that's

1:20:21
um like why this part explains why it's so efficient so if you think about the

1:20:27
call that uh your client kind of wants to make

1:20:33
right for example we want to get an item with certain ID right and if we think

1:20:40
about the chances of how this call is going to be resolved then like I think

1:20:46
the highest chance is that it's already computed on the client so basically the value is available right then like okay

1:20:55
uh let's assume it's not computed then maybe it can be computed from other

1:21:01
values on the client which are are already computed and okay if it's not

1:21:07
this spot then maybe okay we resort to RPC but it can be computed on the server

1:21:14
already and basically the same process repeats on server and like it may not hit the can server and so so on so

1:21:21
that's why uh like so if you think about this it's exactly what incremental build does and that's why um Fusion is so

1:21:29
efficient in terms of traffic in terms of like literally everything in terms of saving CPU Cycles Network traffic

1:21:37
literally everything

1:21:42
so it's kind of interesting to think of like how efficient uh

1:21:50
it actually is right um and like how it translates into the

1:21:59
number of calls per second for example or uh like so what's the speed up what's

1:22:05
the actual speed up right and uh so there is a test uh you can run it I'll

1:22:11
probably run it right now actually let me so what I want to run I want to run

1:22:19
let's run it in Docker uh yeah this this thing is fine

1:22:25
so I'm going to run it right now um yeah it's going to build but let me kind

1:22:32
of um quickly show you the

1:22:40
results so you will see near these results maybe

1:22:45
a little bit uh less impressive because I'm going to run it uh with Docker right

1:22:52
now and I'm also recording video I'm also um like I mean there's so basically

1:22:59
a bunch of things are running on my machine right now but the point is so if

1:23:07
you have a local Fusion service and it if it gets a chance to kind of cash a

1:23:13
large percent of uh method calls in this case uh so this test uh like basically

1:23:21
runs uh like uh a few hundred re ERS and the single writer that modifies

1:23:27
continuously modifies one random uh item so I think the chance that it kind of

1:23:34
gets uh basically if if it's about the local

1:23:39
service then most of these calls are going to be resolved from like cash right and uh so when it hits this happy

1:23:48
path scenario then it runs well 100 so

1:23:53
it's basically something like I think if you run it on a single core it's going to be around 15 million calls per second

1:24:02
per core in like uh my machine is more powerful but it's like I think 32 cores

1:24:11
in reality so of course it can't scale like uh with

1:24:16
literally uh no uh penalty but uh so it's uh 16

1:24:25

165 million calls per second now the interesting part is that

1:24:30
if you are going to use Fusion client for the same service and service is

1:24:36
going to be located remotely then there will be almost no change the reason is that it just doesn't send the call right

1:24:43
when it knows um that a given value is still consistent so like literally it's

1:24:51
almost identical number of calls per second but like when you use a

1:24:58
networking client Fusion client and that's why so remember the beginning of

1:25:04
uh this talk right I was showing you uh service from actual chat like sorry

1:25:09
small UI component from actual chat that renders the uh listening

1:25:15
button and the quot looks like uses just local stuff and somehow it works so

1:25:21
that's the reason why it works uh the reason is quite simple networking services like built on

1:25:28
top of fusion I mean the services CL networking clients right built on top of fusion they nearly as efficient as like

1:25:36
local services and I mean moreover they also cash calls so that's why uh like there

1:25:44
is no difference between using like a service that reides on your machine or

1:25:49
resides remotely and in most of cases there is like no

1:25:55
difference uh and that's why the whole UI in actual chat is designed like this

1:26:02
it's basically like just throws calls to different Services gets the data and

1:26:08
feels like like it doesn't have to do any extra to kind of efficiently process

1:26:13
it or uh react to something it just like sends the calls and gets the data

1:26:22
rewraps it and shows in right okay

1:26:29
Soh as for like some other results here uh so that's uh like uh so Fusion

1:26:37
Service uh versus the same service but without Fusion uh decorator or proxy

1:26:44
it's I think 1,000 times slower which is expected because this thing is go is

1:26:50
going to database each time you call it here then like a few other interesting

1:26:56
results is that like so this is a fusion networking client right smart client

1:27:02
that knows when a value is still consistent and doesn't send the call so

1:27:07
what if we replace it with like less smart client but based on the same

1:27:14
protocol that Fusion uses so this like I'm going to uh record a video dedicated to this

1:27:22
protocol basically uh uh that shows how efficient this protocol is but even here

1:27:28
you can see that like uh if you basically strip off this feature with

1:27:33
caching uh on the client then it's going to send something

1:27:39
like uh 50 sorry uh 1.5 million calls

1:27:45
per second and uh that's five times faster than HTTP client would do uh by

1:27:54
wearing the same service um well and this is the result you would get in like normal case in

1:28:03
sense that there is no Fusion no Fusion clim nothing so that's what you are

1:28:10
expected to have in in case you don't add any extras and by the way nearly the

1:28:15
same you would get nearly the same output if you would use r cach on top of

1:28:21
that I mean together with this thing I'll show you why uh quite soon or maybe

1:28:29
like maybe it's going to be in the next video

1:28:36
okay so uh let's see uh what's the output right for

1:28:44
our um Benchmark that we run in Docker so

1:28:49
it's a kind of quite close to what you just so so it's like slightly lower

1:28:57
numbers but still interestingly that https I mean the HTTP client like just

1:29:03
inside the docker is it's marginally faster

1:29:09
okay so let's let's stop this thing

1:29:16
um oh let me show you one more thing related to uh our samples right

1:29:24
um so is it running yeah it's running uh

1:29:30
let's run console client for this sample because it's it's kind of

1:29:36
interesting to see how it's going to work will it work so that's the

1:29:46
sample oh my God I I want to run it

1:29:55
let's stop console client uh I want to run it in uh

1:30:01
dedicated now I mean yeah external

1:30:10
conso mhm okay yeah

1:30:16
let's can we do something like this uh

1:30:22
yeah uh so since we are not authenticated I don't have to uh use any

1:30:28
session ID it's still going to use the same set of items I mean Global one remember so I'm just uh okay I have to

1:30:38
enter something I guess um basically there is some validation

1:30:45
Logic for this station ID think uh doesn't allow you to use short IDs so

1:30:55
um yeah these are our items right let's change

1:31:02
something yeah it changed let's delete

1:31:07
[Music] delete so it works uh so uh let me show you the client uh so as you might guess

1:31:16
it's very simple up right and um

1:31:26
uh yeah this [Music] one

1:31:31
okay um

1:31:39
so what do we have here so station ID let uh like Let's

1:31:46
ignore this stuff what we do here is we build service provider by adding Fusion

1:31:53
adding soet client add think authentication client to do API uh so this thing is used in like uh

1:32:02
RPC demo part we don't need it here actually and we can comment it out but

1:32:09
the point is like uh so how it gets these updates in real time right and

1:32:14
then it like at some point it calls observe to do and uh you see that like

1:32:22
what you can do is for example example you can create a new computed which is uh wrapping this

1:32:31
computation you instantly update this because like this thing I mean this part

1:32:36
can be executed synchronously and to compute something you need like uh to

1:32:42
run a synchronous computation this thing this thing all of this is a synchronous

1:32:47
right so we want B basically if uh we remove this part and the first value

1:32:53
it's going to be output it's going to be the default basically for like this

1:32:59
thing um and default uh for this for the type of

1:33:06
like what's this so basically now in this case right okay and uh then you can observe

1:33:17
changes like this there are lots of so these the overloads for changes methods

1:33:23
and some other uh you see that for example there is an update delayer you can pass uh let me show

1:33:31
you delayer get well so this is going to be one

1:33:38
second delay and let's also yeah oh did I click stop consol client

1:33:47
let's run it again so uh the first update is quick of

1:33:55
course because we run it explicitly but like let

1:34:00
me move it to so I click here one second

1:34:05
later you see the change here and like that's the power of this abstraction

1:34:11
basically it uh allows uh you to see things in inconsistent State and know

1:34:18
about this okay uh I guess we are done with this this

1:34:24
part um like what's the impact of all of this in real

1:34:30
apps and uh you can find this chart in Fusion repository I mean on fusion page

1:34:39
uh but basically it shows how uh our actual server responds to one of the

1:34:46
most frequent calls chat get tile it's basically returns like five messages uh

1:34:53
starting from from like certain uh boundary uh so you see that most of

1:35:00
these calls are resolved in 30 microsc it's not milliseconds it's

1:35:06
microsc and uh like we get timings like three milliseconds only when uh it

1:35:14
actually hits the database um moreover I think what kind

1:35:20
of what's important here is that like most of these calls are actually

1:35:27
eliminated on the clients so these are the calls that kind of made it to the

1:35:33
server uh and way more calls of course were eliminated on The Client because

1:35:39
the value was still the same uh okay yeah that's a famous quote on cash

1:35:47
in validation and unsurprisingly naming things uh so the interesting piece here

1:35:54
is I guess that like Fusion offers a fancy solution to this

1:36:01
problem now like how many calls are actually tracked in Fusion based apps so

1:36:11
actual chart is a good example here and as you can see like uh in for example in

1:36:18
my case it tracks I think uh more than 1,000 calls so basically these are the

1:36:25
values it kind of watches on the server side

1:36:30
observes um and it costs nothing like uh it

1:36:37
moreover uh now I'm going to also show yeah so that's like how the actual uh

1:36:45
cache looks like it uses index DB uh in case with actual chat not local storage

1:36:51
because like local storage is like there are much more constraints right but uh

1:36:57
you can see that uh so that's for def instance and that's why uh the number of

1:37:02
Kiss is much lower here but it's still 600 uh so basically 600 results of

1:37:09
calls are cached right and uh this is how actual Network

1:37:15
traffic looks like uh so you see that that's the very beginning of the communication and the protocol used here

1:37:21
is also a little bit kind of out dated I I think I made this screenshot maybe a

1:37:28
month ago or so and I made a bunch of improvements to the protocol so right now it's much more efficient but the

1:37:34
point is like you see that like after the handshake it sends again one big

1:37:41
call with like well sorry one big packet which kind of packs a bunch of calls

1:37:48
right and then like it starts getting uh so the first response is like almost 5

1:37:54
kilobytes it also uh includes the data for like a number of calls you can see I

1:38:01
think what's highlighted here is that like uh you even can see by the pattern

1:38:07
that probably it's uh the same method that uh sorry the same kind of response

1:38:16
and yeah that's match response telling that basically hey your value uh that's

1:38:22
catched is still correct so okay we are getting to the

1:38:29
very final part right now and

1:38:34
uh there is a bunch of kind of comparison slides uh for what Fusion

1:38:42
versus some more kind of well-known abstractions so for uh State Management

1:38:50
abstractions like fluxer MX and Redux and there are many of them uh I think

1:38:59
the key difference between fusion and them is that Fusion is distributed of

1:39:04
course then it's also threat safe and as synchronous and that's important because

1:39:10
uh this is what makes Fusion kind of a fit for server side piece so you don't

1:39:16
have to use it on the client you can use it just on the server side and uh this will speed up your basically API

1:39:26
um one other interesting thing is that like I think no one does the same stuff

1:39:31
with methods as what Fusion does basically this kind of decoration part

1:39:37
and impling cach implicit caching um yeah most of these

1:39:44
obstructions they are very explicit in terms of like expressing that you want

1:39:49
certain computation to be dependent on like like so there are no like methods

1:39:55
with you can just call right and um again it's quite important thing

1:40:02
because that's what makes your code clean and readable uh well as for

1:40:07
everything else I think uh uh yeah uh well I I'll probably skip it

1:40:14
for now but you can read right there are of course like many other

1:40:20
differences okay now so what people typically use to address

1:40:27
these real time scenarios right and I think one of most well-known kind of

1:40:33
combinations is signal R Plus radius um so uh why you may want to use

1:40:41
Fusion instead well because uh it's simple versus easy and I think I'll get

1:40:49
back to this part later and like uh we will touch this topic but the point

1:40:55
is like Fusion offers a

1:41:01
framework um something that like allows you to build uh

1:41:09
well everything in a consistent fashion like every piece of realtime logic is

1:41:16
going to look the same with fusion and as for signal and radius sorry signal R

1:41:22
and radius uh yeah you will be basically doing the same but like each time you are going to

1:41:28
solve the same problem again and again and in the end like of course this approach is way more error prone because

1:41:36
all depends on a developer who works on a given part of the app and if they know

1:41:42
how like uh to do it right then it's going to maybe work well but what if

1:41:49
they don't right and uh so yeah and finally it's kind of it's a lot

1:41:58
about reliability right so if the framework takes care of like uh making

1:42:06
certain things work the same way it's actually way better than like when a

1:42:14
developer a different developer uh each time takes care of the

1:42:20
same problem CU like okay it may be solved in 80% of cases but like the

1:42:26
remaining 20% are not going to work well and moreover these issues are extremely

1:42:32
hard to debat or like even identify uh so I think the picture here

1:42:38
is kind of shows that like yeah you can kind of drive on manual transmission but

1:42:44
like who does it nowadays um so and I think the

1:42:51
interesting scenario or like one interesting Cas is like graphql uh and

1:42:57
similar protocols because they sort of uh at least the perception is that they

1:43:04
can help you to solve the same problem and uh well the harsh truth is that not

1:43:14
quite if you think about what Fusion does is like in terms of efficiency and

1:43:20
so so on of course like graphql allows you to I'll basically reshape the results and filter out the stuff you

1:43:26
don't need but like each time you refresh the page each time you start a

1:43:31
client or like even like go to a certain part of UI maybe sometimes right you're

1:43:37
going to get this data from server as a single huge box and that's what's shown

1:43:43
on the right side right as for the fusion scenario you saw that like in

1:43:50
reality first of all it kind of doesn't need this thing with well basically

1:43:57
boxing everything into a single box it it does it like completely differently

1:44:04
it allows you to literally like uh run the computation in such a way that the

1:44:11
client uh well just uh sends request to

1:44:17
each individual single small item and nevertheless all these requests give at

1:44:23
once um and like literally in a single Network pocket and moreover the response

1:44:29
will be much shorter because more likely than not they are cashed on the client

1:44:34
so that's I think in case with actual chat for example the traffic you get on

1:44:40
reconnect is typically just 10 kilobytes it's like when it reconnects or restart you restart the app you get 10 kilobyte

1:44:47
of traffic uh for the sake of clarity I think in May I mean this year in May the

1:44:54
traffic was about 1 Megabyte and that's uh before the moment we added this

1:45:00
protocol that kind of uh I mean we added this extension for local caches and

1:45:07
stuff like this uh because and I think that's what you are kind of expected to

1:45:12
have with signal R sorry with graphql for example so yeah you you basically um

1:45:18
you may uh optimize things and so so on but like if if there is nothing like

1:45:25
this and the like there is no cash or if there is a cash that it's not like

1:45:32
tweaked for tiny items you fetch individually there is so like you

1:45:39
wouldn't get the same result and uh um yeah that's the

1:45:46
scenario okay so there are a few more uh

1:45:51
code examples from ual chat um and uh

1:45:56
I'm not going to like um I guess um cover them in all the details I I'll

1:46:03
just show that you can use all of this stuff so for example here uh this

1:46:12
um stuff with observing a result of some computation you see that online 81 for

1:46:19
example we call computer. new then create a computed which basically in the

1:46:25
end it returns a single Boolean right but like it basically awaits when you either uh leave the uh homepage or your

1:46:36
account changes and uh yeah you can like create a computer that computes this

1:46:41
value and await for it and uh basically create some logic that based on this so

1:46:47
you can observe literally the sequence of uh so you

1:46:53
let me rephrase this you can await that uh user uh on like of your up takes a

1:47:03
sequence of steps and hits certain State and uh the like you can do this uh

1:47:11
because every piece of your UI uh is like Fusion based so for example like

1:47:18
service that Returns the current user account right or the browser history service history is browser history

1:47:25
service that that that knows the like where you are in the app I mean in which

1:47:31
URL and like what steps you took um so basically if all of this is based on

1:47:36
Fusion then you can just wait for certain state to

1:47:42
happen and finally testing right so in tests you can use this construction

1:47:48
which calls a bunch of uh compute methods to again a wait when certain

1:47:54
assertion kind of passes through in this case it's like chat IDs should not

1:47:59
contain chat ID right so basically like the list of chat IDs that is produced

1:48:05
here it's like uh there is no more certain chat in your contacts here and

1:48:11
yeah this when is going to wait for up to 10 seconds and like recompute this thing every time something changes and

1:48:19
uh once uh well basically this thing doesn't an exception it exits more like

1:48:25
otherwise it will fail if it cannot satisfy it in certain time out so you

1:48:31
can use this stuff in tests oh and that's a very ancient

1:48:37
example I think it's like one of other like one other trick you can do in all

1:48:43
these compute methods it's like you basically get the current computed and automatically invalidated after a

1:48:50
certain period of time uh to make this thing recompute if

1:48:57
someone uh watches over it right if no one watches then uh well okay you

1:49:02
invalidated it but like who cares but if someone observes the value of this thing

1:49:08
or uses it in some other computer right then like uh uh they will see the change

1:49:15
every second okay so uh I guess

1:49:23
um the purpose of this talk was to kind of um give

1:49:30
you um an impression of what Fusion is about and what kind of problems it

1:49:37
solves and uh yeah there are probably more questions than answers but like

1:49:45
uh I think thinking about questions or like asking questions is I guess the first step to get his answers and that's

1:49:52
normal right uh

1:49:57
so why all of this is so important uh in my opinion because you can rarely get a

1:50:07
performance Simplicity and like low cost

1:50:12
I mean if we are talking about implementing some real time stuff then Fusion is like extremely lowc cost

1:50:19
solution I'll show you later like how how tiny is the amount of code

1:50:24
responsible to for real time in actual chart for example and um so um basically you get

1:50:34
like 100x performance uh nearly the same Simplicity as you used to have without

1:50:40
real time and like like the cost of real time is nearly zero for you

1:50:46
okay and yeah you saw these numbers already but that's the speed up you are

1:50:51
guante you kind of get right uh and that's a visualization of this speed up

1:50:59
I mean literally a single server May uh handle a lot that otherwise would be uh

1:51:07
handled by hundred of servers and that's it's crazy

1:51:15
right now like uh what are some other reasons or like what might be some other

1:51:21
reasons for you to try all of that well there was a famous I mean there is a

1:51:28
famous St overflow survey they run it every year and uh one of questions in

1:51:33
the last one was like what causes the most frustration for

1:51:40
developers um it's easy to predict the number one item right and it's the

1:51:47
amount of technical do in like literally every system that lives for like

1:51:55
years that's number one kind of downside

1:52:01
or like now number one thing that bothers everyone well at least like if

1:52:07
we're talking about developers right uh so

1:52:15
uh what else developers care about they want to improve the quality of code they

1:52:22
want want to learn you Tech and I'll skip a bunch of items and they want to contribute to open

1:52:28
source and uh like think what what basically kind of checkboxes

1:52:35
fusion uh crosses in this case and uh yeah speaking of simple

1:52:43
versus easy um I mentioned this earlier there is a

1:52:49
famous top which uh has exactly this name uh or like almost exactly this name

1:52:57
simple Made Easy uh the author of this talk is the

1:53:02
creator of closure and uh uh yeah it's all about

1:53:07
like why it's important to um reduce the complexity of your

1:53:15
system so the the chart shown here is uh

1:53:24
um shows the long-term impact of all these changes basically uh uh his

1:53:32
description of easy versus simple is nearly this easy is what's

1:53:39
easy to use easy to learn and uh like basically requires zero efforts to kind

1:53:46
of start to uh or like

1:53:52
B basically easy is something that doesn't require you to invest some time

1:53:59
to learn adopt and use and on contrary

1:54:04
simple is something that may require a decent investment in the beginning but

1:54:11
in the end it allows you to produce a code that everyone understands the

1:54:17
maintainable code that kind of code that's easy to read so uh

1:54:24
obviously yeah you can basically I think and it's also a lot about the level of

1:54:30
obstruction right so simple can be of a higher level of abstraction and that's

1:54:36
why it requires you to kind of invest into uh studying it or like using it but

1:54:45
like easy can be like easy so no investment uh but the problem

1:54:52
is that like basically if you use these like lowlevel abstraction things right

1:54:58
then you will quickly get into a state where you're like the system you build becomes kind of unmaintainable and hard

1:55:05
to evolve okay and uh I think this illustration is

1:55:12
uh a good example of like what level of abstraction means or like what I guess

1:55:18
simple versus easy means uh so if you don't know about Ayn weight then the Cod

1:55:25
on the left looks perfectly fine for you like you may even think like okay uh

1:55:33
probably there is no better way to do that right and uh on the other hand if

1:55:39
you know about a SN weight then this code looks kind of dumb

1:55:44
right so speaking of fusion that's what uh that's nearly what it does with

1:55:52
your code it's like yeah it's well probably even better because like it

1:55:57
like in reality your new code looks almost like what you would right

1:56:03
otherwise without any real time stuff catching them blah blah blah okay

1:56:10
uh so what's the cost of like using fusion and uh the cost of well basically

1:56:18
building real time up on top of f

1:56:24
uh so if you look uh at the bottom part of the screen

1:56:29
you will see that I was searching for if invalidation do is

1:56:35
right uh and basically I counted the number of invalidation blocks in actual

1:56:43
chat so the total number is 90 it's like 90

1:56:49
blocks uh and I think the total number of lines of quot is something like I

1:56:55
don't remember exactly but probably like 200,000 something like this um so it's a

1:57:02
a tiny tiny percent of cod like literally tiny percent of cod that makes

1:57:10
like probably like almost the whole real time so so at

1:57:16
least except maybe some rare Parts it's like what uh allows seual chat to

1:57:23
display changes in real time uh I think

1:57:29
uh I also remember that like uh I wrote a smaller sample but kind of close to

1:57:36
real life as well board games and the number of invalidation calls there was

1:57:41
around I think 30 or so to the invalidation blocks so basically like

1:57:46
the Delta between an up you can write in like a week or so and an up but you like

1:57:53
your team may uh spend like years on uh

1:57:59
it's like not that big so what are some other benefits of

1:58:07
uh Fusion well that's one of such benefits right

1:58:12
typically you can't have the same code for implementing uh Blazer server up and

1:58:20
Blazer web assembly up but not with fusion with Fusion it's exactly the same

1:58:26
code

1:58:31
uh I already mentioned that but the same is applicable to monolith versus

1:58:37
microservice scenario and uh again it's

1:58:42
the same thing uh if every service you build behaves nearly the same way

1:58:50
whether it's local or mod then like what stops you from turning a system that is

1:58:59
completely local like running on single server into a distributed one

1:59:05
nothing um so and yeah speaking of like many other evils you may face otherwise

1:59:14
there are plenty of them uh it's um well literally a pleora of tools that

1:59:24
may help you to uh Implement real time Behavior or caching or

1:59:30
whatever but uh the problem is that like you need to learn all of them like all

1:59:36
of them in most of cases and all of them have their own issues and so so on and

1:59:43
even if you think about like such simple scenario as UI for example right with

1:59:48
Fusion you don't need any extra on UI because like the abstraction works

1:59:54
everywhere on UI or like on like server site it's the same thing right and uh if

2:00:01
you don't use it then you need something like fluer or whatever and uh the same about I mean

2:00:08
the same is applicable to caching and same is applicable to some like uh

2:00:14
transmission protocol like signal ER and stuff like this and like you really need

2:00:19
to kind of learn all of that and and Tackle problems associated with any of

2:00:25
these uh tools so okay we are getting back to uh

2:00:33
the repository slides I guess which means that we are almost in the end of

2:00:39
this talk yeah that's the URL uh you need
