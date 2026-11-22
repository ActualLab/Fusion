using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.Extensions;

[assembly: TypeForwardedTo(typeof(IAuthBackend))]
[assembly: TypeForwardedTo(typeof(AuthBackend_SetupSession))]
[assembly: TypeForwardedTo(typeof(AuthBackend_SignIn))]
[assembly: TypeForwardedTo(typeof(IKeyValueStore))]
[assembly: TypeForwardedTo(typeof(KeyValueStore_Set))]
[assembly: TypeForwardedTo(typeof(KeyValueStore_Remove))]
