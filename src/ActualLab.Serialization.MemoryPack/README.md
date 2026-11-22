![ActualLab.Fusion](https://raw.githubusercontent.com/ActualLab/Fusion/master/docs/img/Logo128.jpg)

# ActualLab.Serialization.MemoryPack

ActualLab.Serialization.MemoryPack - MemoryPack support for ActualLab.Fusion: an IByteSerializer, formatters for ActualLab types, and MemoryPack-based RPC wire formats.

The formatters produce exactly the bytes earlier Fusion versions produced with the MemoryPack source generator, so the `mempack5` and `mempack6` RPC formats stay compatible with older peers.

## Usage

```cs
// Adds "mempack5", "mempack5c", "mempack6" and "mempack6c"
services.AddRpc().ConfigureWireFormats(f => f.AddMemoryPack());
```

Referencing the package registers its formatters on startup. If your own `[MemoryPackable]` types have members like `Dictionary<string, Option<MyStruct>>`, call `MemoryPackSerialization.Register<T>()` for them, so that `Option<MyStruct>` is serialized the same way as before.

## Learn more

- 📖 [Documentation](https://fusion.actuallab.net/)
- 🧩 [Source & samples on GitHub](https://github.com/ActualLab/Fusion)
- ▶️ [Fusion intro video](https://youtu.be/eMO7AmI6ui4)

---

Part of **[ActualLab.Fusion](https://fusion.actuallab.net/)** — an end-to-end reactive state sync and RPC framework for .NET and Blazor: automatic caching, dependency tracking, precise invalidation, and a fast RPC layer.
