# Blazor Integration Cheat Sheet

Quick reference for Fusion + Blazor.

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

## Multiple States

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

## Parameter Change Handling

State automatically recomputes when parameters change:

```razor
@code {
    [Parameter] public long Id { get; set; }

    // ComputeState is called automatically when Id changes
    protected override Task<MyData> ComputeState(CancellationToken cancellationToken)
        => MyService.Get(Id, cancellationToken);
}
```
