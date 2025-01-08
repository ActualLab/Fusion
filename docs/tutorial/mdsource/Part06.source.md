# Part 6: Real-time UI in Blazor Apps

You already know about `IState<T>` - it was described in [Part 3](./Part03.md).
It's an abstraction that "tracks" the most current version of some `Computed<T>`.
There are a few "flavors" of the `IState` - the most important ones are:

* `IMutableState<T>` - in fact, a variable exposed as `IState<T>`
* `IComputedState<T>` - a state that auto-updates once it becomes inconsistent,
  and the update delay is controlled by `UpdateDelayer` provided to it.

You can use these abstractions directly in your Blazor components, but
usually it's more convenient to use `ComputedStateComponent<TState>` and
`MixedStateComponent<TState, TMutableState>` from `ActualLab.Fusion.Blazor` NuGet package.
I'll describe how they work further, but since the classes
are tiny, the link to their source code might explain it even better:

- [StatefulComponentBase.cs](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor/Components/StatefulComponentBase.cs) (common base type)
- [ComputedStateComponent.cs](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor/Components/ComputedStateComponent.cs)
- [MixedStateComponent.cs](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor/Components/MixedStateComponent.cs) (inherits from `ComputedStateComponent<TState>`).

## StatefulComponentBase&lt;T&gt; ([source](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor/Components/StatefulComponentBase.cs))

Any `StatefulComponentBase` has `State` property, which can be
any `IState`.

When initialized, it tries to resolve the state via `ServiceProvider` -
unless was already assigned. And in addition to that, it attaches its
own event handler (`StateChanged` delegate - don't confuse it with Blazor's
`StateHasChanged` method) to all `State`'s events (by default):

```cs
protected override void OnInitialized()
{
    // ReSharper disable once ConstantNullCoalescingCondition
    State ??= CreateState();
    UntypedState.AddEventHandler(StateEventKind.All, StateChanged);
}

protected virtual TState CreateState()
    => Services.GetRequiredService<TState>();
```

And this is how the default `StateChanged` handler looks:

```cs
protected StateEventKind StateHasChangedTriggers { get; set; } = StateEventKind.Updated;

protected StatefulComponentBase()
{
    StateChanged = (_, eventKind) => {
        if ((eventKind & StateHasChangedTriggers) == 0)
            return;
        this.NotifyStateHasChanged();
    };
}
```

As you see, by default any `StatefulComponentBase` triggers `StateHasChanged`
once its `State` gets updated.

Finally, it also disposes the state once the component gets disposed -
unless its `OwnsState` property is set to `false`. And that's nearly all
it does.

## ComputedStateComponent&lt;T&gt; ([source](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor/Components/ComputedStateComponent.cs))

This class tweaks a behavior of `StatefulComponentBase` to deal `IComputedState<T>`.

This is literally all of its code:

```cs
public abstract class ComputedStateComponent<TState> : StatefulComponentBase<IComputedState<TState>>
{
    protected ComputedStateComponentOptions Options { get; set; } =
        ComputedStateComponentOptions.SynchronizeComputeState
        | ComputedStateComponentOptions.RecomputeOnParametersSet;

    // State frequently depends on component parameters, so...
    protected override Task OnParametersSetAsync()
    {
        if (0 == (Options & ComputedStateComponentOptions.RecomputeOnParametersSet))
            return Task.CompletedTask;
        State.Recompute();
        return Task.CompletedTask;
    }

    protected virtual ComputedState<TState>.Options GetStateOptions()
        => new();

    protected override IComputedState<TState> CreateState()
    {
        async Task<TState> SynchronizedComputeState(IComputedState<TState> _, CancellationToken cancellationToken)
        {
            // Synchronizes ComputeState call as per:
            // https://github.com/ActualLab/Fusion/issues/202
            var ts = TaskSource.New<TState>(false);
            await InvokeAsync(async () => {
                try {
                    ts.TrySetResult(await ComputeState(cancellationToken));
                }
                catch (OperationCanceledException) {
                    ts.TrySetCanceled();
                }
                catch (Exception e) {
                    ts.TrySetException(e);
                }
            });
            return await ts.Task.ConfigureAwait(false);
        }

        return StateFactory.NewComputed(GetStateOptions(),
            0 != (Options & ComputedStateComponentOptions.SynchronizeComputeState)
            ? SynchronizedComputeState
            : (_, ct) => ComputeState(ct));
    }

    protected abstract Task<TState> ComputeState(CancellationToken cancellationToken);
}
```

It doesn't try to resolve the state via DI container, but
constructs it using `StateFactory` - and moreover:

- It constructs a state that's computed using its own `ComputeState` method.
  As you remember from [Part 3](./Part03.md), state computation logic is
  always "wrapped" into a compute method - in other words, the `IComputed` instance
  it produces under the hood tracks all the dependencies and gets invalidated
  once any of them does, which triggers `Invalidated` event on a state, and
  consequently, `StateChanged` event on the component. And since we're using
  `IComputedState` here, the state itself will use its `UpdateDelayer` to wait
  a bit and recompute itself using the same `ComputeState` method.
- This state is configured by its own `GetStateOptions` method -
  in particular, you can provide its initial value, `UpdateDelayer`, etc.
- By default:
  - Change of component parameters triggers state recomputation
  - `ComputeState` call is synchronized (i.e. executed via Blazor's
    `InvokeAsync`), so it's safe to access and modify component fields
    there
  - You can disable any of these options in component constructor
    or `InitializeAsync`.

So to have a component that automatically updates once the output of some
Compute Service (or a set of such services) changes, all you need is to:

- Inherit it from `ComputedStateComponent<T>`
- Override its `ComputeState` method
- Possibly, override its `GetStateOptions` method.

A good example of such component is `Counter.razor` from "HelloBlazorServer" example -
check out [its source code](https://github.com/ActualLab/Fusion.Samples/blob/master/src/HelloBlazorServer/Pages/Counter.razor).
Note that it already computes a complex value using two compute methods
(`CounterService.GetCounterAsync` and `GetMomentsAgoAsync`):

```cs
protected override async Task<string> ComputeState(CancellationToken cancellationToken)
{
    var (count, changeTime) = await CounterService.Get();
    var momentsAgo = await Time.GetMomentsAgo(changeTime);
    return $"{count}, changed {momentsAgo}";
}
```

## MixedStateComponent&lt;T, TLocals&gt; ([source](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor/Components/MixedStateComponent.cs))

It's pretty common for UI components to have its own (local) state
(e.g. a text entered into a few form fields)
and compute their `State` using some values from this local state -
in other words, to have their `State` dependent on its local state.

There are a few ways to enforce `State` recomputation in such cases:

1. If all you use is component parameters, `State` recomputation
   will happen automatically if `ComputedStateComponentOptions.RecomputeOnParametersSet` option is on (and that's the default).
2. You may also use component fields and call `State.Recompute()`
   to trigger its invalidation and recomputation w/o an update delay.
   `State.Invalidate()` will work as well, but in this case the
   recomputation will happen with usual update delay.
3. Wrap full local state into e.g. `IMutableState<T> MutableState` and use
   it in `ComputeState` via `var locals = await MutableState.Use()`.
   As you might remember from [Part 3](./Part03.md), `MutableState.Use`
   is the same as `MutableState.Computed.Use`, and it makes state
   a dependency of what's computed now, so once `MutableState` gets changed,
   the recomputation of `State` will happen automatically.
   Though if you need to nullify the update delay in this case,
   it's going to be a bit more complex.

`MixedStateComponent<TState, TMutableState>` is a built-in implementation
of option 3:

- It assumes that `State` always depends on `MutableState`, so
  you don't have to call `MutableState.Use()` inside `ComputeState`
- Moreover, it calls `State.Recompute()` on `MutableState` changes,
  so there is no update delay for this chain.

Check out [its 30 lines of code](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor/Components/MixedStateComponent.cs) to see how it works.

## Real-time UI in Server-Side Blazor apps

As you might guess, all you need is to:

- Add your Compute Services to `IServiceProvider` used by ASP.NET Core
- Inherit your own components from `ComputedStateComponent<TState>` or
  `MixedStateComponent<TState, TMutableState>`.

Your server-side web host configuration should include at least these parts:

```cs
public void ConfigureServices(IServiceCollection services)
{
    // Fusion services
    var fusion = services.AddFusion();

    // ASP.NET Core / Blazor services 
    services.AddRazorPages();
    services.AddServerSideBlazor(o => o.DetailedErrors = true);
}

// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseStaticFiles();
    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapBlazorHub();
        endpoints.MapFallbackToPage("/_Host");
    });
}
```

## Real-time UI in Blazor WebAssembly apps

If you read about [Compute Service Clients in Part 4](./Part04.md), you
probably already know that WASM case actually isn't that different:

- Server-side should be configured to "share" Compute Services -
  i.e. its DI container should be able to resolve Compute Services and
  `ActualLab.Rpc.RpcHub` should expose them as servers (services available 
  for remote clients).
- Client-side should be configured to properly build Compute Service clients. 
  And since these clients behave exactly as Compute Services
  they replicate, you can use them the same way you'd use Compute Services
  with Server-Side Blazor.

So your server-side web host configuration should include these parts:

```cs
public void ConfigureServices(IServiceCollection services)
{
    // Fusion services
    var fusion = services.AddFusion();
    fusion.AddWebServer();
    
    // ASP.NET Core / Blazor services 
    services.AddRazorPages();
    services.AddServerSideBlazor(o => o.DetailedErrors = true);
}

public void Configure(IApplicationBuilder app, ILogger<Startup> log)
{
    if (Env.IsDevelopment()) {
        app.UseWebAssemblyDebugging(); // Only if you need this
    }
    app.UseWebSockets(new WebSocketOptions() {
        KeepAliveInterval = TimeSpan.FromSeconds(30), // You can change this
    });

    // Static files
    app.UseBlazorFrameworkFiles(); // Needed for Blazor WASM

    // Endpoints
    app.UseRouting();
    app.UseEndpoints(endpoints => {
        endpoints.MapRpcWebSocketServer();
        endpoints.MapFallbackToPage("/_Host"); // Typically needed for Blazor WASM
    });
}
```

And your client-side DI container configuration should look as follows:

```cs
public static Task Main(string[] args)
{
    var builder = WebAssemblyHostBuilder.CreateDefault(args);
    ConfigureServices(builder.Services, builder);
    builder.RootComponents.Add<App>("app");
    var host = builder.Build();
    // Blazor host doesn't start IHostedService-s by default,
    // so let's start them "manually" here
    host.Services.HostedServices().Start();
    return host.RunAsync();
}

public static void ConfigureServices(IServiceCollection services, WebAssemblyHostBuilder builder)
{
    var baseUri = new Uri(builder.HostEnvironment.BaseAddress);
    var fusion = services.AddFusion();
    fusion.Rpc.AddWebSocketClient(baseUri);
}
```

## Real-time UI in Blazor Hybrid apps

As you might guess, nothing prevents you from using both of above approaches
to implement Blazor apps that support both Server-Side Blazor (SSB) and
Blazor WebAssembly modes.

All you need is to:

- Ensure your Compute Services implement the same interface as their clients.
  [Part 4](./Part04.md) explains how to achieve that, but overall,
  you need to implement this interface on Compute Service 
  and register its client via `fusion.AddClient<IService>()` call.
- Ensure the server can host Blazor components from the client
  in SSB mode. You need to host Blazor hub + a bit
  [tweaked _Host.cshtml](https://github.com/ActualLab/Fusion.Samples/blob/master/src/Blazor/Server/Pages/_Host.cshtml)
  capable of serving the HTML of the Blazor app for both modes.
- Configure the server-side DI container to resolve 
  an actual implementation of your Compute Service. 
- Configure the client-side DI container to resolve 
  a client of Compute Service.
- And finally, implement something allowing clients to switch
  from SSB to WASM mode and vice versa.

Check out [Blazor Sample](https://github.com/ActualLab/Fusion.Samples/tree/master/src/Blazor)
to see how all of this works together.

#### [Next: Part 7 &raquo;](./Part07.md) | [Tutorial Home](./README.md)
