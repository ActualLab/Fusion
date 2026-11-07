![ActualLab.Fusion](https://raw.githubusercontent.com/ActualLab/Fusion/master/docs/img/Logo128.jpg)

# ActualLab.Rpc.TypeScript.Generator

An MSBuild target generating TypeScript contracts &mdash; service interfaces, DTOs, and
`defineRpcService` definitions &mdash; for ActualLab.Rpc and Fusion services
after each build of your contracts project, so the TypeScript client never drifts from the .NET server.

```xml
<PropertyGroup>
  <ActualLabRpcTypeScriptOutput>..\Web\src\contracts.generated.ts</ActualLabRpcTypeScriptOutput>
  <!-- Optional: comma-separated service type name patterns, '*' matches any sequence of characters -->
  <ActualLabRpcTypeScriptServices>ITodo*,IStockApi</ActualLabRpcTypeScriptServices>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="ActualLab.Rpc.TypeScript.Generator" PrivateAssets="all" />
</ItemGroup>
```

On CI (`CI=true`) the target only verifies the file and fails the build if it's out of date.

## Learn more

- 📖 [Documentation](https://fusion.actuallab.net/PartTS-Contracts)
- 🧩 [Source & samples on GitHub](https://github.com/ActualLab/Fusion)

---

Part of **[ActualLab.Fusion](https://fusion.actuallab.net/)** — an end-to-end reactive state sync and RPC framework for .NET and Blazor: automatic caching, dependency tracking, precise invalidation, and a fast RPC layer.
