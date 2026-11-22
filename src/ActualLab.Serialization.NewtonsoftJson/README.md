![ActualLab.Fusion](https://raw.githubusercontent.com/ActualLab/Fusion/master/docs/img/Logo128.jpg)

# ActualLab.Serialization.NewtonsoftJson

ActualLab.Serialization.NewtonsoftJson - Newtonsoft.Json (JSON.NET) support for ActualLab.Fusion:

- `NewtonsoftJsonSerializer` - an `ITextSerializer` from ActualLab.Core backed by Newtonsoft.Json,
  registered as `SerializerKind.NewtonsoftJson` when the assembly loads
- `NewtonsoftJsonContractResolver` - makes Newtonsoft.Json honor the System.Text.Json and
  `[DataContract]` attributes of ActualLab types and supplies their converters
- `NewtonsoftJsonSerialized<T>` and `TypeDecoratingNewtonsoftJsonSerialized<TSchema, T>`
- `njson5` and `njson5np` RPC wire formats: `rpc.ConfigureWireFormats(f => f.AddNewtonsoftJson())`

## Learn more

- 📖 [Documentation](https://fusion.actuallab.net/)
- 🧩 [Source & samples on GitHub](https://github.com/ActualLab/Fusion)
- ▶️ [Fusion intro video](https://youtu.be/eMO7AmI6ui4)

---

Part of **[ActualLab.Fusion](https://fusion.actuallab.net/)** — an end-to-end reactive state sync and RPC framework for .NET and Blazor: automatic caching, dependency tracking, precise invalidation, and a fast RPC layer.
