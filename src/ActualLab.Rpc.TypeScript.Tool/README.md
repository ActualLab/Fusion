![ActualLab.Fusion](https://raw.githubusercontent.com/ActualLab/Fusion/master/docs/img/Logo128.jpg)

# actuallab-tsgen

A .NET tool generating TypeScript contracts &mdash; service interfaces, DTOs, and
`defineRpcService` definitions &mdash; for ActualLab.Rpc and Fusion services
from a compiled contracts assembly, so the TypeScript client never drifts from the .NET server.

```
dotnet tool install -g ActualLab.Rpc.TypeScript.Tool
actuallab-tsgen MyApp.Contracts.dll --output ../web/src/contracts.generated.ts
actuallab-tsgen MyApp.Contracts.dll --output ../web/src/contracts.generated.ts --check
```

## Learn more

- 📖 [Documentation](https://fusion.actuallab.net/PartTS-Contracts)
- 🧩 [Source & samples on GitHub](https://github.com/ActualLab/Fusion)

---

Part of **[ActualLab.Fusion](https://fusion.actuallab.net/)** — an end-to-end reactive state sync and RPC framework for .NET and Blazor: automatic caching, dependency tracking, precise invalidation, and a fast RPC layer.
