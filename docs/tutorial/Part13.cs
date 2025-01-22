using System.Runtime.Serialization;
using MemoryPack;
using static System.Console;

namespace Tutorial;

#region Part13_Chat_Post
// 1. MemoryPack doesn't support nested types, so it has to be moved out of IXxxService; Rider/ReSharper has a refactoring for this, as for VS.NET, I am not sure.
// 2. All `[MemoryPackable]` types must be declared as `partial`
// 3. [MemoryPackable(GenerateType.VersionTolerant)] requires you to explicitly mark every serializable member with [MemoryPackOrder]
// 4. [DataContract] and [DataMember] are optional - you may want to have them if you end up using e.g. MessagePack serializer
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public partial record Chat_Post(
    [property: DataMember, MemoryPackOrder(0)] string Name,
    [property: DataMember, MemoryPackOrder(1)] string Text
    ) : ICommand<Unit>;
#endregion
public static class Part13{
    #region Part13_PostCommand
    public record PostCommand(string Name,string Text) : ICommand<Unit>;
    #endregion
}
