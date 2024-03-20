namespace ActualLab.Fusion.Authentication;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
// ReSharper disable once InconsistentNaming
public partial record AuthBackend_SetupSession(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] string IPAddress,
    [property: DataMember, MemoryPackOrder(2)] string UserAgent,
    [property: DataMember, MemoryPackOrder(3)] ImmutableOptionSet Options
) : ISessionCommand<SessionInfo>, IBackendCommand, INotLogged
{
    public AuthBackend_SetupSession(Session session, string ipAddress = "", string userAgent = "")
        : this(session, ipAddress, userAgent, default) { }
}
