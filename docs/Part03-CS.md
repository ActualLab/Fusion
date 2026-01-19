# Blazor: Cheat Sheet

Quick reference for Fusion + Blazor.


## DI Setup

```csharp
// Server-side Blazor
services.AddFusion()
    .AddService<MyService>()
    .AddBlazor();

// Hybrid (Server + WASM) with auth
services.AddFusion(RpcServiceMode.Server, true)
    .AddServer<IMyService, MyService>()
    .AddBlazor()
    .AddAuthentication()
    .AddPresenceReporter();

// WebAssembly client
services.AddFusion()
    .AddClient<IMyService>()
    .AddBlazor()
    .AddAuthentication()
    .AddPresenceReporter();
fusion.Rpc.AddWebSocketClient(baseAddress);
```


## `ComputedStateComponent<T>`

Basic usage:

```razor
@inherits ComputedStateComponent<MyData>
@inject IMyService MyService

@if (State.HasValue) {
    <div>@State.Value.Name</div>
}

@code {
    [Parameter] public long Id { get; set; }

    protected override Task<MyData> ComputeState(CancellationToken cancellationToken)
        => MyService.Get(Id, cancellationToken);
}
```

Configure update delay:

```razor
@code {
    protected override ComputedState<MyData>.Options GetStateOptions()
        => new() {
            UpdateDelayer = FixedDelayer.Get(0.5), // 0.5 second delay
        };
}
```

Force immediate update:

```razor
@code {
    private async Task OnButtonClick() {
        await SomeAction();
        _ = State.Recompute(); // Trigger immediate recomputation
    }
}
```


## Handling Loading and Errors

```razor
@inherits ComputedStateComponent<MyData>

@if (!State.HasValue) {
    <Loading />
} else if (State.Error != null) {
    <Error Message="@State.Error.Message" />
} else {
    <Content Data="@State.Value" />
}
```


## Using `LastNonErrorValue`

Keep showing last valid data while error occurs:

```razor
@if (State.LastNonErrorValue is { } data) {
    <Content Data="@data" />
}
@if (State.Error != null) {
    <ErrorBanner Message="@State.Error.Message" />
}
```


## Multiple States (Tuple)

```razor
@inherits ComputedStateComponent<(User User, List<Order> Orders)>

@code {
    [Parameter] public long UserId { get; set; }

    protected override async Task<(User, List<Order>)> ComputeState(CancellationToken ct)
    {
        var user = await UserService.Get(UserId, ct);
        var orders = await OrderService.GetByUser(UserId, ct);
        return (user, orders);
    }
}
```


## `MixedStateComponent` (Forms)

```razor
@inherits MixedStateComponent<SearchResults, SearchForm>
@inject ISearchService SearchService

<input @bind="MutableState.Value.Query" @bind:event="oninput" />

@if (State.HasValue) {
    @foreach (var item in State.Value.Items) {
        <div>@item.Name</div>
    }
}

@code {
    protected override async Task<SearchResults> ComputeState(CancellationToken ct)
    {
        var form = MutableState.Value;
        return await SearchService.Search(form.Query, ct);
    }
}
```


## CircuitHub Access

```razor
@inherits CircuitHubComponentBase

@code {
    protected override void OnInitialized()
    {
        // All available via CircuitHub:
        var session = Session;          // CircuitHub.Session
        var commander = UICommander;    // CircuitHub.UICommander
        var nav = Nav;                  // CircuitHub.Nav
        var js = JS;                    // CircuitHub.JS

        // Check render mode
        if (CircuitHub.IsPrerendering) {
            // Skip expensive operations
        }
    }
}
```


## Authentication

```razor
<CascadingAuthState UsePresenceReporter="true">
    <Router AppAssembly="typeof(App).Assembly">
        <!-- ... -->
    </Router>
</CascadingAuthState>
```

Access auth state in components:

```razor
@code {
    [CascadingParameter]
    private Task<AuthState>? AuthStateTask { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask!;
        var user = authState.User; // Fusion User or null
    }
}
```


## Sign In/Out

```razor
@inject ClientAuthHelper AuthHelper

<button @onclick="() => AuthHelper.SignIn()">Sign In</button>
<button @onclick="() => AuthHelper.SignOut()">Sign Out</button>
```


## Render Mode Switching

```razor
@inject RenderModeHelper RenderModeHelper

<span>Mode: @RenderModeHelper.GetCurrentModeTitle()</span>

@foreach (var mode in RenderModeDef.All) {
    <button @onclick="() => RenderModeHelper.ChangeMode(mode)">
        @mode.Title
    </button>
}
```


## Component Options

```csharp
// Set default options globally
ComputedStateComponent.DefaultOptions =
    ComputedStateComponentOptions.RecomputeStateOnParameterChange |
    ComputedStateComponentOptions.UseAllRenderPoints;

// Per-component override
protected override void OnInitialized()
{
    Options = ComputedStateComponentOptions.RecomputeStateOnParameterChange;
}
```


## Parameter Comparison

```csharp
// Set default comparison mode
FusionComponentBase.DefaultParameterComparisonMode = ParameterComparisonMode.Custom;

// Per-component attribute
[FusionComponent(ParameterComparisonMode.Custom)]
public class MyComponent : ComputedStateComponent<MyData> { }

// Per-parameter comparer
[Parameter]
[ParameterComparer(typeof(ByValueParameterComparer))]
public MyStruct Value { get; set; }
```
