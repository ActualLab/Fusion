# Blazor Integration: Diagrams

Text-based diagrams for the Blazor integration concepts introduced in [Part 3](Part03.md).


## Component Hierarchy

```
┌───────────────────────────────────────────────────────────────────────┐
│                  Fusion Blazor Component Hierarchy                    │
└───────────────────────────────────────────────────────────────────────┘

                       ┌───────────────────────┐
                       │    ComponentBase      │  Blazor's base class
                       │       (Blazor)        │
                       └───────────┬───────────┘
                                   │
                                   ▼
                       ┌───────────────────────┐
                       │  FusionComponentBase  │  Optimized parameter
                       │    + IHandleEvent     │  comparison & events
                       └───────────┬───────────┘
                                   │
                                   ▼
                       ┌───────────────────────┐
                       │CircuitHubComponentBase│  CircuitHub access
                       │   + IHasCircuitHub    │  & service shortcuts
                       └───────────┬───────────┘
                                   │
                                   ▼
                       ┌───────────────────────┐
                       │ StatefulComponentBase │  State management
                       │         <T>           │  & auto-updates
                       └───────────┬───────────┘
                                   │
         ┌─────────────────────────┼─────────────────────────┐
         │                         │                         │
         ▼                         ▼                         ▼
┌─────────────────────┐ ┌─────────────────────┐ ┌─────────────────────┐
│ComputedStateComponent│ │ComputedRenderState │ │ MixedStateComponent │
│         <T>         │ │   Component<T>      │ │   <T, TMutable>     │
├─────────────────────┤ ├─────────────────────┤ ├─────────────────────┤
│ • Auto-computed     │ │ • Tracks render     │ │ • Computed state    │
│   state             │ │   state snapshot    │ │ • + Mutable state   │
│ • Dependency        │ │ • Optimized         │ │ • For form inputs   │
│   tracking          │ │   re-rendering      │ │                     │
└─────────────────────┘ └─────────────────────┘ └─────────────────────┘
```


## ComputedStateComponent Lifecycle

```
┌───────────────────────────────────────────────────────────────┐
│             ComputedStateComponent<T> Lifecycle               │
└───────────────────────────────────────────────────────────────┘

       Component Created
             │
             ▼
  ┌─────────────────────┐
  │   OnInitialized()   │
  │     [sync init]     │
  └──────────┬──────────┘
             │
             ▼
  ┌─────────────────────┐
  │    CreateState()    │ ──► Creates ComputedState<T>
  │   [state created]   │     with ComputeState as the
  └──────────┬──────────┘     computation function
             │
             ▼
  ┌─────────────────────┐
  │OnInitializedAsync() │
  └──────────┬──────────┘
             │
             ▼
  ┌─────────────────────┐
  │    First Render     │ ──► State.HasValue determines
  └──────────┬──────────┘     what to render
             │
             │
  ╔══════════╧════════════════════════════════════════════════╗
  ║                      UPDATE LOOP                          ║
  ╠═══════════════════════════════════════════════════════════╣
  ║                                                           ║
  ║    ┌─────────────────┐                                    ║
  ║    │  State.Value    │◄───────────────────────────┐       ║
  ║    │  is consistent  │                            │       ║
  ║    └────────┬────────┘                            │       ║
  ║             │                                     │       ║
  ║             │ Dependency invalidated              │       ║
  ║             ▼                                     │       ║
  ║    ┌─────────────────┐                            │       ║
  ║    │ State becomes   │                            │       ║
  ║    │  inconsistent   │                            │       ║
  ║    └────────┬────────┘                            │       ║
  ║             │                                     │       ║
  ║             │ UpdateDelayer.Delay()               │       ║
  ║             ▼                                     │       ║
  ║    ┌─────────────────┐                            │       ║
  ║    │ ComputeState()  │ ──► Calls your compute     │       ║
  ║    │    executed     │     methods (tracked)      │       ║
  ║    └────────┬────────┘                            │       ║
  ║             │                                     │       ║
  ║             │ State updated                       │       ║
  ║             ▼                                     │       ║
  ║    ┌─────────────────┐                            │       ║
  ║    │ StateChanged()  │ ──► Calls                  │       ║
  ║    │  event fires    │     NotifyStateHasChanged()│       ║
  ║    └────────┬────────┘                            │       ║
  ║             │                                     │       ║
  ║             │ Triggers render                     │       ║
  ║             ▼                                     │       ║
  ║    ┌─────────────────┐                            │       ║
  ║    │   Component     │────────────────────────────┘       ║
  ║    │   re-renders    │                                    ║
  ║    └─────────────────┘                                    ║
  ╚═══════════════════════════════════════════════════════════╝
```


## CircuitHub Service Architecture

```
┌───────────────────────────────────────────────────────────────┐
│                   CircuitHub Architecture                     │
└───────────────────────────────────────────────────────────────┘

                      ┌─────────────────────────┐
                      │       CircuitHub        │
                      │    (Scoped Service)     │
                      └────────────┬────────────┘
                                   │
         ┌─────────────────────────┼─────────────────────────┐
         │                         │                         │
         ▼                         ▼                         ▼
  ┌─────────────┐          ┌─────────────┐          ┌─────────────┐
  │   Fusion    │          │   Blazor    │          │   State     │
  │  Services   │          │  Services   │          │    Info     │
  └─────────────┘          └─────────────┘          └─────────────┘
         │                        │                        │
  ┌──────┴──────┐          ┌──────┴──────┐          ┌──────┴──────┐
  │             │          │             │          │             │
  ▼             ▼          ▼             ▼          ▼             ▼
┌───────┐ ┌─────────┐ ┌────────┐ ┌─────────┐ ┌────────┐ ┌─────────┐
│Session│ │Commander│ │  Nav   │ │   JS    │ │Render  │ │  IsPre- │
│       │ │         │ │Manager │ │ Runtime │ │ Mode   │ │rendering│
└───────┘ └─────────┘ └────────┘ └─────────┘ └────────┘ └─────────┘

                            │
                   ┌────────┴────────┐
                   │                 │
                   ▼                 ▼
            ┌───────────┐    ┌───────────────┐
            │UICommander│    │ JSRuntimeInfo │
            └───────────┘    └───────────────┘
                   │                 │
                   │                 │ Inspects runtime type
                   │                 │ to detect prerendering
                   ▼                 ▼
            ┌─────────────────────────────┐
            │  UIActionFailureTracker     │
            │  (tracks command failures)  │
            └─────────────────────────────┘
```


## Authentication Flow

```
┌───────────────────────────────────────────────────────────────┐
│                  Authentication Data Flow                     │
└───────────────────────────────────────────────────────────────┘


  ┌──────────────────┐
  │     Server       │
  │ ┌──────────────┐ │
  │ │    IAuth     │ │ ──► GetUser(), IsSignOutForced()
  │ │  (Compute    │ │     are compute methods
  │ │   Service)   │ │
  │ └──────┬───────┘ │
  └────────│─────────┘
           │
           │ RPC / Invalidation
           │
  ╔════════╧══════════════════════════════════════════════════╗
  ║                        Client                             ║
  ╠═══════════════════════════════════════════════════════════╣
  ║                                                           ║
  ║  ┌────────────────────────┐                               ║
  ║  │   AuthStateProvider    │                               ║
  ║  │  (ComputedState<       │                               ║
  ║  │    AuthState>)         │                               ║
  ║  └───────────┬────────────┘                               ║
  ║              │                                            ║
  ║              │ Computes: GetUser() + IsSignOutForced()    ║
  ║              │                                            ║
  ║              │ On invalidation:                           ║
  ║              │ 1. Recomputes auth state                   ║
  ║              │ 2. Fires NotifyAuthenticationStateChanged()║
  ║              ▼                                            ║
  ║  ┌────────────────────────┐                               ║
  ║  │   CascadingAuthState   │                               ║
  ║  │      Component         │                               ║
  ║  └───────────┬────────────┘                               ║
  ║              │                                            ║
  ║       ┌──────┴──────┐                                     ║
  ║       │             │                                     ║
  ║       ▼             ▼                                     ║
  ║  ┌─────────┐  ┌──────────────┐                            ║
  ║  │ Normal  │  │IsSignOutForced                            ║
  ║  │ update  │  │   = true     │                            ║
  ║  └────┬────┘  └──────┬───────┘                            ║
  ║       │              │                                    ║
  ║       │              │ Force page reload                  ║
  ║       ▼              ▼                                    ║
  ║  ┌────────────────────────┐                               ║
  ║  │    Child Components    │                               ║
  ║  │  (<AuthorizeView>,     │                               ║
  ║  │   [CascadingParameter])│                               ║
  ║  └────────────────────────┘                               ║
  ║                                                           ║
  ╚═══════════════════════════════════════════════════════════╝
```


## PresenceReporter Timing

```
┌───────────────────────────────────────────────────────────────┐
│                   PresenceReporter Timing                     │
└───────────────────────────────────────────────────────────────┘

  Time ─────────────────────────────────────────────────────────►

       Start()
          │
          ▼
  ┌───────────────┐
  │  Get Session  │
  └───────┬───────┘
          │
  ╔═══════╧═════════════════════════════════════════════════════╗
  ║                       Main Loop                             ║
  ╠═════════════════════════════════════════════════════════════╣
  ║                                                             ║
  ║  ▼                                                          ║
  ║ ─┴─    Wait UpdatePeriod                                    ║
  ║       (default: ~3 min with 5% random variance)             ║
  ║  │                                                          ║
  ║  ▼                                                          ║
  ║ ┌────────────────────┐                                      ║
  ║ │ UpdatePresence()   │ ──► Calls IAuth.UpdatePresence()     ║
  ║ └─────────┬──────────┘                                      ║
  ║           │                                                 ║
  ║    ┌──────┴──────┐                                          ║
  ║    │             │                                          ║
  ║    ▼             ▼                                          ║
  ║ Success       Failure                                       ║
  ║    │             │                                          ║
  ║    │             │ Wait RetryDelay (10s, then 30s)          ║
  ║    │             │                                          ║
  ║    └──────┬──────┘                                          ║
  ║           │                                                 ║
  ║           │ Loop back                                       ║
  ║                                                             ║
  ╚═════════════════════════════════════════════════════════════╝


  Timeline Example:
  ──────────────────────────────────────────────────────────────►
  │           │                    │                    │
  Start    Update               Update               Update
           (~3min)             (~3min)              (~3min)

  On Failure:
  ──────────────────────────────────────────────────────────────►
  │           │       │            │                  │
  Start    Update   Retry        Retry             Update
          (fail)   (10s)        (30s)            (success)
                   (fail)      (success)          (~3min)
```


## Render Mode Switching

```
┌───────────────────────────────────────────────────────────────┐
│                   Render Mode Switching                       │
└───────────────────────────────────────────────────────────────┘

       Available Modes:
       ┌─────────────────────────────────────────────────────┐
       │  "a" (Auto)  │  "s" (Server)  │  "w" (WebAssembly)  │
       └─────────────────────────────────────────────────────┘


  Current: Server Mode               User clicks "Switch to WASM"
  ┌─────────────────────┐                      │
  │   Blazor Server     │                      │
  │   (running in       │                      │
  │    circuit)         │                      │
  └─────────────────────┘                      │
                                               ▼
                              ┌──────────────────────────────┐
                              │ RenderModeHelper.ChangeMode()│
                              │                              │
                              │ Navigates to:                │
                              │ /fusion/renderMode/w         │
                              │ ?redirectTo=/current-page    │
                              └──────────────┬───────────────┘
                                             │
                                             ▼
                              ┌──────────────────────────────┐
                              │   Server Endpoint            │
                              │  MapFusionRenderModeEndpoints│
                              │                              │
                              │  1. Sets render mode cookie  │
                              │  2. Redirects to redirectTo  │
                              └──────────────┬───────────────┘
                                             │
                                             ▼
                              ┌──────────────────────────────┐
                              │   _HostPage.razor            │
                              │                              │
                              │   Reads mode from cookie     │
                              │   RenderModeDef.GetOrDefault │
                              │                              │
                              │   <App @rendermode="mode"/>  │
                              └──────────────┬───────────────┘
                                             │
                                             ▼
                              ┌──────────────────────────────┐
                              │   Blazor WebAssembly         │
                              │   (now running in browser)   │
                              └──────────────────────────────┘
```


## Parameter Comparison Flow

```
┌───────────────────────────────────────────────────────────────┐
│                  Parameter Comparison Flow                    │
└───────────────────────────────────────────────────────────────┘

  Parent Component Renders
           │
           ▼
  ┌───────────────────────┐
  │  SetParametersAsync() │
  │   called on child     │
  └───────────┬───────────┘
              │
              ▼
  ┌───────────────────────────────────────────────────────────┐
  │            Check ParameterComparisonMode                  │
  └───────────┬───────────────────────────────────────────────┘
              │
      ┌───────┴───────┐
      │               │
      ▼               ▼
  Standard         Custom
      │               │
      │               ▼
      │     ┌───────────────────────────────────────────────┐
      │     │  ComponentInfo.ShouldSetParameters()          │
      │     │                                               │
      │     │  For each parameter:                          │
      │     │  ┌───────────────────────────────────────┐    │
      │     │  │ 1. Get ParameterComparer for param    │    │
      │     │  │ 2. Compare old value vs new value     │    │
      │     │  │ 3. If ANY changed → return true       │    │
      │     │  └───────────────────────────────────────┘    │
      │     └────────────────┬──────────────────────────────┘
      │                      │
      │               ┌──────┴──────┐
      │               │             │
      │               ▼             ▼
      │          All Same    Something Changed
      │               │             │
      │               ▼             │
      │       ┌─────────────┐       │
      │       │    SKIP     │       │
      │       │  No render  │       │
      │       └─────────────┘       │
      │                             │
      └─────────────────────────────┘
                      │
                      ▼
            ┌───────────────────────┐
            │  Process Parameters   │
            │   (standard Blazor)   │
            └───────────────────────┘


  Comparer Resolution (ParameterComparerProvider):
  ────────────────────────────────────────────────

  ┌───────────────────────────────────────────────────────────┐
  │ 1. [ParameterComparer] on property                        │
  │    └─► if found, use it                                   │
  │                                                           │
  │ 2. KnownComparerTypes[propertyType]                       │
  │    └─► if found, use it                                   │
  │                                                           │
  │ 3. [ParameterComparer] on property's type                 │
  │    └─► if found, use it                                   │
  │                                                           │
  │ 4. [ParameterComparer] on declaring class                 │
  │    └─► if found, use it                                   │
  │                                                           │
  │ 5. DefaultParameterComparer                               │
  │    └─► fallback                                           │
  └───────────────────────────────────────────────────────────┘
```


## MixedStateComponent Data Flow

```
┌───────────────────────────────────────────────────────────────┐
│            MixedStateComponent<T, TMutableState>              │
└───────────────────────────────────────────────────────────────┘

                  ┌─────────────────────────────────────┐
                  │        MixedStateComponent          │
                  │                                     │
                  │  ┌───────────────────────────────┐  │
                  │  │  MutableState<TMutableState>  │  │
                  │  │      (local form state)       │  │
                  │  │                               │  │
                  │  │  • User input binding         │  │
                  │  │  • Always consistent          │  │
                  │  │  • Set() updates value        │  │
                  │  └──────────────┬────────────────┘  │
                  │                 │                   │
                  │                 │ On update:        │
                  │                 │ triggers          │
                  │                 │ State.Recompute() │
                  │                 │                   │
                  │                 ▼                   │
                  │  ┌───────────────────────────────┐  │
                  │  │      ComputedState<T>         │  │
                  │  │       (computed state)        │  │
                  │  │                               │  │
                  │  │  ComputeState():              │  │
                  │  │  • Reads MutableState.Value   │  │
                  │  │  • Calls compute services     │  │
                  │  │  • Returns computed result    │  │
                  │  └───────────────────────────────┘  │
                  │                                     │
                  └─────────────────────────────────────┘


  Example Flow:
  ───────────────────────────────────────────────────────────────

  User types in           MutableState              ComputedState
  search box             updates                    recomputes
       │                      │                           │
       ▼                      ▼                           ▼
  ┌─────────┐          ┌────────────┐            ┌─────────────────┐
  │"react"  │─────────►│ .Value =   │───────────►│ ComputeState()  │
  └─────────┘          │ "react"    │            │                 │
                       └────────────┘            │ 1. Read search  │
                                                 │    term         │
                                                 │ 2. Call         │
                                                 │    SearchService│
                                                 │    .Search()    │
                                                 │ 3. Return       │
                                                 │    results      │
                                                 └────────┬────────┘
                                                          │
                                                          ▼
                                                 ┌─────────────────┐
                                                 │   UI renders    │
                                                 │  search results │
                                                 └─────────────────┘
```
