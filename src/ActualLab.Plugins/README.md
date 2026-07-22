![ActualLab.Fusion](https://raw.githubusercontent.com/ActualLab/Fusion/master/docs/img/Logo128.jpg)

# ActualLab.Plugins

MEF-style plugin library that focuses on core capabilities needed to add plugins to your application - namely, building an IoC container hosting them. It is designed to load plugins on demand - the assemblies hosting plugins are loaded only once you access the plugins via IoC container. Despite that, it tries to create IoC container as quickly as possible by caching reflected information about the plugins, which is updated only once you change them (i.e. basically, the startup is typically quite fast).

## Learn more

- 📖 [Documentation](https://fusion.actuallab.net/)
- 🧩 [Source & samples on GitHub](https://github.com/ActualLab/Fusion)
- ▶️ [Fusion intro video](https://youtu.be/eMO7AmI6ui4)

---

Part of **[ActualLab.Fusion](https://fusion.actuallab.net/)** — an end-to-end reactive state sync and RPC framework for .NET and Blazor: automatic caching, dependency tracking, precise invalidation, and a fast RPC layer.
