using System.Reflection;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Extensions;
using ActualLab.Interception;
using ActualLab.Redis;
using ActualLab.Reflection;
using ActualLab.Rpc;
using ActualLab.Tests;
using MessagePack;

namespace ActualLab.Fusion.Tests.Serialization;

// [DataContract]/[DataMember] stay on the framework types, so they must describe the same contract
// as the MessagePack attributes do - otherwise DataContract-driven serializers silently diverge
public class DataContractSyncTest(ITestOutputHelper @out) : TestBase(@out)
{
    private static readonly Assembly[] Assemblies = [
        typeof(Symbol).Assembly, // ActualLab.Core
        typeof(ArgumentList).Assembly, // ActualLab.Interception
        typeof(ServiceCall).Assembly, // ActualLab.CommandR
        typeof(RpcHub).Assembly, // ActualLab.Rpc
        typeof(Session).Assembly, // ActualLab.Fusion
        typeof(User).Assembly, // ActualLab.Fusion.Ext.Contracts
        typeof(IAuthBackend).Assembly, // ActualLab.Fusion.Ext.Backend.Contracts
        typeof(DbHub<>).Assembly, // ActualLab.Fusion.EntityFramework
        typeof(RedisDb).Assembly, // ActualLab.Redis
    ];

    [Fact]
    public void DataMemberShouldMatchMessagePackKey()
    {
        // act
        var mismatches = Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetCustomAttribute<DataContractAttribute>() is not null)
            .Where(t => t.GetCustomAttribute<MessagePackObjectAttribute>() is { KeyAsPropertyName: false })
            .SelectMany(GetMismatches)
            .ToList();

        // assert
        foreach (var mismatch in mismatches)
            Out.WriteLine(mismatch);
        mismatches.Should().BeEmpty();
    }

    // Private methods

    private static IEnumerable<string> GetMismatches(Type type)
    {
        const BindingFlags bindingFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var members = type.GetProperties(bindingFlags).Cast<MemberInfo>().Concat(type.GetFields(bindingFlags));
        foreach (var member in members) {
            var key = member.GetCustomAttribute<KeyAttribute>();
            var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
            var isIgnored = member.GetCustomAttribute<IgnoreMemberAttribute>() is not null;
            var name = $"{type.GetName()}.{member.Name}";
            if (key?.IntKey is { } intKey && (dataMember is null || dataMember.Order != intKey))
                yield return $"{name}: [Key({intKey})] without [DataMember(Order = {intKey})]";
            else if (key is null && dataMember is not null)
                yield return $"{name}: [DataMember] without [Key]";
            if (isIgnored && dataMember is not null)
                yield return $"{name}: [IgnoreMember] with [DataMember]";
        }
    }
}
