# Part 8: Scaling Fusion Services

> NOTE: Nearly everything described in this section (and even more)
> is already implemented. Sections covering CommandR
> and Operations Framework describe this in details,
> but it still makes sense to read this section to better
> understand the problem they solve.
> 
> If you want to see it in action, check out
> [http://boardgames.alexyakunin.com](http://boardgames.alexyakunin.com)

Scaling Fusion services is actually simpler than it may seem
at first - and mostly, you should take into account two
key factors:

1. You should treat any Fusion-based service host as one of your
   caching servers and scale it accordingly - but most importantly
   you should ensure these servers share a limited subset of data.
   The more data they share, the lower is cache hit ratio (assuming
   the amount of RAM is fixed).

2. You need to ensure that invalidations from any of such
   servers are "replicated" on any other server that may serve
   the same piece of data.

Let's look at a few specific cases to understand this better.

## Scaling Multi-Tenant Services

### Load Balancing

This is the simplest case. Let's assume we're building a Fusion-based
multi-tenant service, and:

- There are `H` hosts (VMs, containers, etc.) running the service
- We want to ensure each tenant can be served by at least `K` of them.

The simplest way to achieve a desirable distribution of
load in this case is to use [Rendezvous Hashing] or [Consistent Hashing].
Almost any industry standard load balancer supports the later one -
in particular, you can use:

* [`hash` directive](http://nginx.org/en/docs/stream/ngx_stream_upstream_module.html#hash)
  on NGINX
* [`hash-type consistent`](https://www.haproxy.com/blog/haproxys-load-balancing-algorithm-for-static-content-delivery-with-varnish/)

The main downside of these two options is that they support only `K == 1` scenario.
Both directives allow you to bind a tenant to a single backend server only,
so in case this server goes down, users from tenants hosted there are going to
experience a slow-down, because none of other servers were serving these tenants,
and thus they don't have any of their data cached.

A code below shows how to use [Rendezvous Hashing] to implement more
efficient mapping of users to backend servers and support `K > 1` scenario:

```cs
Host GetHost(string tenantId, string userIdOrIP) 
    => Hosts
        .Select(host => (
            Host: host, 
            Weight: Hash(host.Id, tenantId)
        ))
        .OrderBy(p.Weight)
        .Select(p => p.Host)
        .Skip(Hash(userIdOrIP) % K)
        .First();
```

As you see, in this case we select a "stable" set of `K` hosts for
every tenant and route a specific user to `(Hash(userIdOrIP) % K)`-th
host in this set. Once one of hosts goes down, its load will be picked
up by `K - 1` hosts from the same set plus one extra host, so the
% of users experiencing slowdown in this case (`1 / K`) could be reduced
to any desirable number at cost of extra RAM.

Above code is pretty inefficient - its time complexity is `O(N*log(N))`,
but notice that while your set of hosts is stable,
you can cache the following list per each tenant to reduce the complexity
to `O(1)`:

```cs
Host[] GetTenantHosts(string tenantId) 
    => Hosts
        .Select(host => (
            Host: host, 
            Weight: Hash(host.Id, tenantId)
        ))
        .OrderBy(p.Weight)
        .Select(p => p.Host)
        .Take(K)
        .ToArray();
```

In practice, such load balancing can be implemented by having this logic on
your own proxy - and you can use e.g.
[AspNetCore.Proxy](https://github.com/twitchax/AspNetCore.Proxy)
or [YARP](https://devblogs.microsoft.com/dotnet/introducing-yarp-preview-1/)
to implement it.

And if you prefer safer, but maybe a bit less flexible option, almost any
industry standard load balancer supports consistent hash-based mapping too.
In particular, you can use:

* [`hash` directive](http://nginx.org/en/docs/stream/ngx_stream_upstream_module.html#hash)
  on NGINX (it supports only `K == 1` scenario)
* [`hash-type consistent`](https://www.haproxy.com/blog/haproxys-load-balancing-algorithm-for-static-content-delivery-with-varnish/)
  or [dynamically updated maps](https://www.haproxy.com/blog/introduction-to-haproxy-maps/)
  on HAProxy.

A few important things to keep in mind:

* Your load balancer somehow has to identify a tenant for every request.
  And it makes sense to think about this in advance - and agree to
  use a request header or cookie bearing tenant ID or token, or even use a
  subdomain name for this.
* Supporting multiple ways of identifying a tenant might be a good idea
  as well - especially if your tenants are actually partitions
  (the following section explains this).
* Finally, if you use [Compute Service Clients], keep in mind the requests
  they send should bear the same token, and moreover, WebSocket
  connections to server should be properly routed to the right servers via
  load balancers.

### Distributed Invalidation

The problem:

> Assuming `hs = GetTenantHosts(tenantId)` is the set of hosts serving the data
> of a specific tenant, invalidation happening on any of these hosts should be
> reproduced on every other host in the same set.

Note that skipping a single invalidation in Fusion's case is actually a big
problem: invalidation is the only way to indicate the data is stale / obsolete,
and unless it's triggered, there is always a chance the old data could be reused,
because all you need to "enforce" this is to somehow keep the reference to
underlying `IComputed`.

In other words, invalidations must be quite reliable, if you don't want to resort
to hacks like periodically scanning every `IComputed` in Fusion's `ComputedRegistry`
and e.g. invalidating the "oldest" ones.

For now Fusion doesn't offer abstractions helping to implement distributed
invalidation (this is certianly temporary), but the problem to solve here
is a typical application of
[publish-subscribe pattern](https://en.wikipedia.org/wiki/Publish%E2%80%93subscribe_pattern),
so you can use a service like
[Azure Service Bus](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dotnet-how-to-use-topics-subscriptions)
and have a single "invalidation topic" per tenant to deliver these messages
to every host that serves its data.

Now, some tricky aspects:

* The invalidation must happen *eventually* - in other words, it's fine to delay it,
  but it's not fine to skip it at all.
* The smaller is the invalidation delay, the lower is the probability to observe
  an inconsistent state.
* BUT: Too early invalidation (happening before the change is committed) is the same
  as no invalidation at all, because the recomputation might also happens before
  the change is committed.

All of this means that if your system is distributed and transactional, you
need to use a bit more complex protocol to replicate invalidations reliably:

**Inside transaction**:

* Create an object (let's call it *operation*) describing the current operation
* Store it to the same DB where you run the transaction (likely, tenant's DB).

**Once transaction committed**:

* Publish the *operation* to the tenant's pub/sub topic
* Run invalidations locally.

And we need two extra services:

1. **Invalidation service** listens for new *operation*
   messages in topics matching every tenant it serves and runs
   corresponding invalidations (except for the messages originating
   from the same host).
   This service should start before your host starts to process
   any read requests.
2. **"Recovery pump" service** similarly listens for *operation*
   messages and removes corresponding *operation* entries from
   tenant's DB.
   But in addition to that, it also looks for *operation*
   entries in tenant's DB that weren't removed for *a while*,
   and once it sees such an entry, it sets its creation time
   in DB to the current one and pumps back the identical *operation*
   to the matching pub/sub topic.
   "A while" here should be long enough to ensure a very high chance
   of message propagation through the pub/sub pipeline - e.g.
   you may set it to 99.9999 percentile of message propagation time.

As you might guess, the "recovery pump" service might be a part
of invalidation service - all you need is to ensure that if such
service runs on every host, they don't race with each other
and don't overload the DB with identical "delete operation"
requests (+ batching these requests is a good idea anyway).

P.S. If you know about
[Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html),
you could instantly spot this pattern perfectly fits to implement
all of this.

## Scaling Single-Tenant Services

Overall, you can't horizontally scale a service that doesn't
allow some kind of partitioning. This isn't a limitation
imposed by Fusion - it's just what horizontal scaling implies.

The process of scaling such a service is actually quite similar
to scaling the monolith:

* Identify the subsets of data that could be served by dedicated
  microservices.
* Identify partitioning dimensions for each of these subsets.
* Extract microservices and view partitions there the same way
  we viewed tenants in the previous section.

Identifying partitioning dimensions is the most interesting part
here. Ideally, you want to ensure that a single partition can be:

* **Stored on a single device** - i.e. you won't ever need to
  tear it apart onto multiple devices to be able to store its data.
  As an example, a singe Twitter or Facebook account could be
  viewed as a partition, but e.g. a single Google Drive account
  probably can't play the same role (though e.g. a single file can).
* **Served to a single user from a single service host** -
  this is almost always the case.

These two criteria ensure you can horizontally scale both the data
and compute capacity without a need to repartition existing
partitions - in other words, this is what allows you to view
your partitions as "tenants".

## Scaling Reads via Replica Hosts

Fusion brings a pretty unique way of scaling read workloads:
since [Compute Service Clients] are almost identical to the original
[Compute Services] including the way they cache the outputs,
you can re-expose them as the next layer of hosts (let's call them
"replica hosts") to scale read workload.

The downsides of such an approach are straightforward:

- Slower queries & invalidations - basically, you add ~ a network
  round-trip time to both timings by adding an extra layer of replicas.
- Currently replicas are transitioned to failed state once the connection
  to publisher gets broken, so shutting down the host of the original
  service will temporarily expose this state at every "outgoing" replica.
  Though in future we'll introduce a mechanism allowing replicas
  to switch to another publisher transparently without entering
  failed or inconsistent state to address this.

As for the upsides:

- You can use different caching rules for replica services -
  they "inherit" caching attributes from service interface,
  though the original service can "override" them by applying
  the same attributes (but with different options) to its own methods.
- Since replica hosts spend nearly nothing on compute,
  their hardware selection is way more straightforward
  (just optimize for RAM & IO capacity).
- When you spin up a regular service, it's expected to be slower
  in the beginning - because nothing is cached there yet.
  But a set of new "replica hosts" connected to a "regular host"
  that's already spun up should improve the performance instantly,
  because the cache of the original host is still going to be used,
  and moreover, shortly whatever is there is going to be replicated
  on "replica hosts" and served w/o hitting the original host.
- As you probably know, the max. duration of STW (stop-the-world)
  pauses caused by GC on .NET Core is
  [proportional to the size of working set](https://medium.com/servicetitan-engineering/go-vs-c-part-2-garbage-collection-9384677f86f1),
  which means that ideally you don't want to cache a lot
  in a single process. And "replica hosts" provide one
  of easy ways to "redistribute" the cache among multiple
  processes. Note that you can run these processes on the
  same host too - in other words, this might be a totally
  viable option to run hosts with > 32 GB RAM.

Alleviation of instant fluxes in traffic to certain content is a good example
of when this could be useful. Imagine a scenario when a post
of a regular user gets shared by an influencer and gains a lot of traction.
If you track partitions that are on the verge of their compute or IO capacity,
you can almost instantly re-route the traffic hitting them to a dedicated
pool of "replica hosts" to get 10x read capacity almost instantly.
And once the load is gone, you can remap them back to the original pool.

## Large Working Sets and GC Pauses

The problem:

> Max. duration of STW (stop-the-world) pauses caused by GC on .NET Core is
> [proportional to the size of working set](https://medium.com/servicetitan-engineering/go-vs-c-part-2-garbage-collection-9384677f86f1),
> which means you probably need to limit it by 32-64 GB per host.

For the sake of clarity:

- Full GC pause time is proportional much more to the number of alive
  objects rather than to their total size.
- The [GCBurn](https://github.com/alexyakunin/GCBurn) test referenced
  above generates objects whose sizes are distributed nearly as you'd
  expect in a regular web service, and the estimate on max. pause time
  it provides is ~ **1 second per every 10 GB of working set**.
- It worth mentioning that even though GCBurn sees full GC STW pauses pretty
  frequently (~ once every minute), that's solely because allocation
  is all it does, i.e. full GCs should be way more rare in normal
  circumstances, and any optimization of allocation patterns would make
  them shorter and less frequent.

There is no silver bullet resolving this issue completely,
but there are plenty of workarounds you can use to nearly
eliminate it:

Decrease the number of objects in heap - by:

- Reusing existing objects (immutable or [`IFrozen` objects](./Part05))
- Returning more of serialized data (byte arrays or strings)
- Returning structs pointing to serialized data in large buffers
- Relying on Fusion's [`[Swap]` attribute](./Part05.md).

Limit the size of your working set to run `N` processes per host - by:

- Setting `COMPlus_GCHeapHardLimit` environment variable
- Using Docker containers
- As it was mentioned earlier, you may run 1 "master host" process
  and `N - 1` "replica host" processes per actual host.

Making pauses less visible:

- You can use [GC.RegisterForFullGCNotification](https://docs.microsoft.com/en-us/dotnet/api/system.gc.registerforfullgcnotification?view=netcore-3.1)
  to remove the host that's expected to have a full GC pause
  from the load balancer, trigger full GC manually, and add it back.

## Wait, but what about invalidations and updates?

Interestingly, one of the most frequent question about Fusion is:

> Wait, but how can it scale if it recomputes every output
> once any of its dependencies changes?

And the answer is actually simpler than it seems:

1. Fusion **doesn't** recompute anything once something changes.
   It just invalidates every dependency of what's changed.
   But could invalidation alone be costly enough? No:
2. Using a dependency (calling a function + creating a single dependency link)
   requires `O(1)` time at best (i.e. if its output is already cached).
   And that's also a minimum amount of time you spend to call a function
   if there would be no Fusion at all, which means that
   **dependency tracking is ~ free**.
3. Processing a single invalidation link during the invalidation pass
   requires `O(1)` time too, and this happens just once for every link.
   In other words, **invalidations are, basically, free as well!**

This is why Fusion services should scale *at least* as well as similar
services w/o Fusion. "At least" here means that Fusion certainly makes
you to pay a fixed, but much higher cost per every call
to provide automatic dependency tracking, caching, etc.,
plus you should take into account such factors as the amount of RAM
your new service will need with a given caching options, and so on.
In other words, of course there are details you need to factor in
to use it efficiently.

And if you look at its [Compute Service Clients], you'll quickly conclude
all the same statements are equally applicable to them as well -
the only difference is that this `O(1)` cost can have a much higher
(but still fixed) absolute value there, because every computation and
invalidation requires an extra network roundtrip there.

Finally, notice that [IState] &ndash; an abstraction that powers most
of UI updates &ndash; uses `IUpdateDelayer`, which, in fact, controls the
max. possible update rate, and you can change its settings at any time.
So:

1. You have all the levers to control the frequency of such updates,
   and in particular, you can throttle them down on any popular piece of
   content or when your service experiences high load.
2. "Update" rarely triggers actual recomputation - it triggers the
   recomputation only when it's the first update request after some change,
   otherwise it just delivers the cached value.
3. And finally, note that recomputations are incremental with Fusion -
   [as with incremental builds](https://medium.com/@alexyakunin/stl-fusion-in-simple-terms-65b1975967ab?source=friends_link&sk=04e73e75a52768cf7c3330744a9b1e38),
   you rarely recompute anything from scratch with Fusion.
   You recompute just what's changed.

Of course, this isn't a complete set of options you have - e.g. you can
also trade consistency for performance by delaying invalidations. But
the main point is: **yes, Fusion-based services scale**.

#### [Part 9: CommandR - Intro &raquo;](./Part09.md) | [Tutorial Home](./README.md)

[Consistent Hashing]: https://en.wikipedia.org/wiki/Consistent_hashing
[Rendezvous Hashing]: https://medium.com/i0exception/rendezvous-hashing-8c00e2fb58b0
[Compute Service Clients]: ./Part04.md
[Compute Services]: ./Part01.md
[IState]: ./Part03.md
