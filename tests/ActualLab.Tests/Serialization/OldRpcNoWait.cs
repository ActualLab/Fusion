using MessagePack;

namespace ActualLab.Tests.Serialization;

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
[DataContract, MemoryPackable, MessagePackObject]
public readonly partial struct OldRpcNoWait
{ }
