# Blazor Integration: Diagrams

Diagrams for the Blazor integration concepts introduced in [Part 3](PartB.md).


## Component Hierarchy

```mermaid
flowchart TD
    CB["ComponentBase<br/>(Blazor)"] --> FCB["FusionComponentBase<br/>+ IHandleEvent"]
    FCB --> CHCB["CircuitHubComponentBase<br/>+ IHasCircuitHub"]
    CHCB --> SCB["StatefulComponentBase&lt;T&gt;"]
    SCB --> CSC["ComputedStateComponent&lt;T&gt;"]
    SCB --> MSC["MixedStateComponent&lt;T, TMutable&gt;"]
    CSC --> CRSC["ComputedRenderStateComponent&lt;T&gt;"]
```

| Component | Purpose |
|-----------|---------|
| `ComponentBase` | Blazor's base class |
| `FusionComponentBase` | Optimized parameter comparison & events |
| `CircuitHubComponentBase` | CircuitHub access & service shortcuts |
| `StatefulComponentBase<T>` | State management & auto-updates |
| `ComputedStateComponent<T>` | Auto-computed state, dependency tracking |
| `ComputedRenderStateComponent<T>` | Tracks render state snapshot, optimized re-rendering |
| `MixedStateComponent<T, TMutable>` | Computed state + mutable state for form inputs |


## FusionComponentBase Parameter Comparison Flow

```mermaid
flowchart TD
    Parent([Parent Component Renders]) --> SetParams["SetParametersAsync() called on child"]
    SetParams --> Check["Check ParameterComparisonMode"]
    Check --> Standard
    Check --> Custom

    Standard --> Process["Process Parameters<br/>(standard Blazor)"]

    Custom --> ShouldSet["ComponentInfo.ShouldSetParameters()"]
    ShouldSet --> Compare["For each parameter:<br/>1. Get ParameterComparer for param<br/>2. Compare old value vs new value<br/>3. If ANY changed → return true"]
    Compare --> AllSame["All Same"]
    Compare --> Changed["Something Changed"]
    AllSame --> Skip["SKIP - No render"]
    Changed --> Process
```

| Comparer Resolution Order | Description |
|---------------------------|-------------|
| 1. `[ParameterComparer]` on property | If found, use it |
| 2. `KnownComparerTypes[propertyType]` | If found, use it |
| 3. `[ParameterComparer]` on property's type | If found, use it |
| 4. `[ParameterComparer]` on declaring class | If found, use it |
| 5. `DefaultParameterComparer` | Fallback |


## CircuitHub Service Architecture

`CircuitHub` is a scoped service providing access to commonly used services and state:

| Category | Property | Description |
|----------|----------|-------------|
| **Fusion Services** | `Session` | Current user session |
| | `Commander` | Command executor |
| **Blazor Services** | `NavManager` | Navigation manager |
| | `JSRuntime` | JavaScript interop runtime |
| | `UICommander` | UI-aware command executor |
| | `JSRuntimeInfo` | Runtime type inspection |
| | `UIActionFailureTracker` | Tracks command failures |
| **State Info** | `RenderMode` | Current Blazor render mode |
| | `IsPrerendering` | Whether currently prerendering |


## ComputedStateComponent Lifecycle

```mermaid
flowchart TD
    Start([Component Created]) --> Init["OnInitialized()<br/>[sync init]"]
    Init --> Create["CreateState()<br/>[state created]"]
    Create --> InitAsync["OnInitializedAsync()"]
    InitAsync --> FirstRender["First Render"]
    FirstRender --> Loop

    subgraph Loop ["&nbsp;Update&nbsp;Loop&nbsp;"]
        direction TB
        Consistent["State.Value is consistent"]
        Consistent -->|Dependency invalidated| Inconsistent["State becomes inconsistent"]
        Inconsistent -->|"UpdateDelayer.Delay()"| Compute["ComputeState() executed"]
        Compute -->|State updated| Changed["StateChanged() event fires"]
        Changed -->|Triggers render| Render["Component re-renders"]
        Render --> Consistent
    end
```

| Step | Description |
|------|-------------|
| `CreateState()` | Creates `ComputedState<T>` with `ComputeState` as the computation function |
| First Render | `State.HasValue` determines what to render |
| `ComputeState()` | Calls your compute methods (tracked) |
| `StateChanged()` | Calls `NotifyStateHasChanged()` |


## MixedStateComponent Lifecycle

```mermaid
flowchart LR
    subgraph Component ["MixedStateComponent"]
        direction TB
        Mutable["MutableState&lt;string&gt;<br/>(local form state)"]
        Computed["ComputedState&lt;T&gt;<br/>(computed state)"]
        Mutable -->|"On update: triggers State.Recompute()"| Computed
    end

    Input["User types in search box"] --> Mutable
    Computed --> UI["UI renders search results"]
```

| State | Purpose |
|-------|---------|
| `MutableState<string>` | User input binding, always consistent, `Set()` updates value |
| `ComputedState<T>` | Reads `MutableState.Value`, calls compute services, returns computed result |

**Note:** `ComputeState` doesn't need to explicitly call `.Use()` on `MutableState` - the dependency is assumed. Any change to `MutableState` triggers an immediate re-render (no update delays).

**Example Flow:**
1. User types "react" in search box
2. `MutableState.Value` updates to "react"
3. `ComputeState()` executes: reads search term → calls `SearchService.Search()` → returns results
4. UI renders search results


## Authentication Flow

```mermaid
flowchart TD
    subgraph Server
        IAuth["IAuth<br/>(Compute Service)"]
    end

    subgraph Client
        ASP["AuthStateProvider<br/>(ComputedState&lt;AuthState&gt;)"]
        ASP -->|Computes| Methods["GetUser() + IsSignOutForced()"]
        ASP -->|On invalidation| Recompute["1. Recomputes auth state<br/>2. Fires NotifyAuthenticationStateChanged()"]
        Recompute --> CAS["CascadingAuthState Component"]
        CAS --> Normal["Normal update"]
        CAS --> Forced["IsSignOutForced = true"]
        Normal --> Children["Child Components<br/>(&lt;AuthorizeView&gt;, [CascadingParameter])"]
        Forced -->|Force page reload| Children
    end

    IAuth -->|"RPC / Invalidation"| ASP
```

| Method | Description |
|--------|-------------|
| `GetUser()` | Compute method returning current user |
| `IsSignOutForced()` | Compute method checking forced sign-out |


## PresenceReporter.Run Flow

```mermaid
flowchart LR
    Start([Start]) --> GetSession["Get Session"]
    GetSession --> Loop

    subgraph Loop ["&nbsp;Main&nbsp;Loop&nbsp;"]
        direction TB
        Wait["Wait UpdatePeriod<br/>(~3 min with 5% random variance)"]
        Wait --> Update["UpdatePresence()"]
        Update --> Success
        Update --> Failure
        Success --> Wait
        Failure -->|"Wait RetryDelay (10s, then 30s)"| Wait
    end
```

| Timeline | Sequence |
|----------|----------|
| Normal | Start → Update (~3min) → Update (~3min) → Update (~3min) |
| On Failure | Start → Update (fail) → Retry 10s (fail) → Retry 30s (success) → Update (~3min) |


## Render Mode Switching

```mermaid
flowchart TD
    Server["Blazor&nbsp;Server<br/>(running&nbsp;in&nbsp;circuit)"]
    Server -->|"User&nbsp;clicks&nbsp;'Switch&nbsp;to&nbsp;WASM'"| ChangeMode["RenderModeHelper.ChangeMode()<br/>Navigates&nbsp;to:&nbsp;/fusion/renderMode/w?redirectTo=/current-page"]
    ChangeMode --> Endpoint["Server&nbsp;Endpoint<br/>MapFusionRenderModeEndpoints"]
    Endpoint --> SetCookie["1.&nbsp;Sets&nbsp;render&nbsp;mode&nbsp;cookie<br/>2.&nbsp;Redirects&nbsp;to&nbsp;redirectTo"]
    SetCookie --> HostPage["_HostPage.razor<br/>Reads&nbsp;mode&nbsp;from&nbsp;cookie<br/>RenderModeDef.GetOrDefault"]
    HostPage --> WASM["Blazor&nbsp;WebAssembly<br/>(now&nbsp;running&nbsp;in&nbsp;browser)"]
```

| Mode | Code | Description |
|------|------|-------------|
| Auto | `a` | Automatic mode selection |
| Server | `s` | Blazor Server mode |
| WebAssembly | `w` | Blazor WebAssembly mode |
