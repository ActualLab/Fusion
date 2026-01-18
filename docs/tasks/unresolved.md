# Unresolved Issues

## Part 10 (Multi-host Invalidation)

### ✅ FIXED: ServerSideCommandBase → IBackendCommand
Updated the documentation to explain the new `IBackendCommand` pattern instead of the old `ServerSideCommandBase`.

### ✅ FIXED: IMetaCommand/IServerSideCommand → ISystemCommand/IBackendCommand
Updated the `Completion.New` section to explain that `ICompletion` implements both `ISystemCommand` and `IBackendCommand`.

### ✅ FIXED: InvalidationInfoProvider → InvalidatingCommandCompletionHandler.IsRequired()
Updated the documentation to explain the new location of invalidation logic.

### ✅ FIXED: Grammar/Phrasing Issues
- "So far only commands could act" → "Currently only commands can act"
- "It worth mentioning" → "It's worth mentioning"
- "has a change to ignore" → "has a chance to ignore"
- "IComptuted" → "IComputed"

### ✅ FIXED: GitHub Links Verified
All GitHub links in Part10.md were verified against the actual file paths in the repository. All links are valid.

---

## Part 11 (Authentication)

### ✅ FIXED: _Host.cshtml → _HostPage.razor
Updated the documentation to use modern `_HostPage.razor` pattern with proper `@code` block, `SetParametersAsync`, and `ServerAuthHelper` integration.

### ✅ FIXED: BlazorCircuitContext → CircuitHub
Updated `App.razor` example to use `CircuitHubComponentBase` and `CircuitHub` instead of the old `BlazorCircuitContext` pattern.

### ✅ FIXED: App.razor Session Handling
Updated to show modern pattern inheriting from `CircuitHubComponentBase` with `CircuitHub.SessionResolver`.

### ✅ FIXED: OrderController Pattern Removed
Removed the obsolete `OrderController` example. Fusion services are now exposed via RPC, not controllers.

### ✅ FIXED: Authentication Setup API
Updated text to reference `fusion.AddDbAuthService<>()` and `fusion.AddAuthClient()` instead of old `fusion.AddAuthentication()`.

### ✅ FIXED: GetMyOrders Pattern
Updated `GetMyOrders` examples to use `_auth.GetUser(session, cancellationToken).Require()` pattern.

### ✅ FIXED: RpcDefaultSessionReplacer
Updated reference from `SessionModelBinder` to `RpcDefaultSessionReplacer` middleware with link.

### ✅ FIXED: Service and App Configuration
Updated service configuration to use `ConfigureAuthEndpoint` and `ConfigureServerAuthHelper`.
Updated app configuration to use modern `MapStaticAssets()`, `MapRazorComponents<>()`, `MapFusionAuthEndpoints()`, `MapFusionRenderModeEndpoints()`.

---

## Part 5 (CommandR)

### ✅ FIXED: Items Sharing Behavior
Updated documentation to correctly explain that each `CommandContext` has its own `Items` property (`MutablePropertyBag`). Added note explaining how to share data across nested commands using `context.OutermostContext.Items`.

### ✅ FIXED: Snippet Naming
Renamed all snippet names from `Part09_*` to `Part05_*` in both Part05.md and Part05.cs. Also renamed namespace from `Tutorial09` to `Tutorial05` and class from `Part09` to `Part05`.

---

## Remaining Items

All previously identified issues have been resolved.
