using ActualLab.Fusion.EntityFramework;
using ActualLab.IO;
using ActualLab.Reflection;
using ActualLab.Serialization.Internal;
using MessagePack;

namespace ActualLab.Tests.Serialization;

public class MessagePackSerializationTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void CombinedTest()
    {
        Test(default(Unit), "null");

        // Ulid
        Test(default(Ulid), "\"AAAAAAAAAAAAAAAAAAAAAA==\"");

        // Moment
        Test(default(Moment), "0");
        Test(new Moment(1), "1");

        // CpuTimestamp
        Test(default(CpuTimestamp), "0");
        Test(new CpuTimestamp(1), "1");

        // Option
        Test(default(Option<int>), "[]");
        Test(Option.Some(1), "[1]");

        // Symbol
        Test(default(Symbol), "\"\"");
        Test(new Symbol("01"), "\"01\"");

        // ByteString
        Test(default(ByteString), "\"\"");
        Test(new ByteString([0,1]), "\"AAE=\"");

        // JsonString
        Test(default(JsonString), "null");
        Test(new JsonString("01"), "\"01\"");

        // TypeRef
        Test(default(TypeRef), "\"\"");
        Test(new TypeRef(typeof(bool)), x => x.Should().StartWith("\"System.Boolean,"));

        // FilePath
        Test(default(FilePath), "\"\"");
        Test(new FilePath("01"), "\"01\"");

        // HostId
        Test(default(HostId), "null");
        Test(new HostId("01"), "\"01\"");

        // MessagePackData
        Test(default(MessagePackData), "null");
        Test(new MessagePackData([0,1]), "\"AAE=\"");

        // ApiOption
        Test(default(ApiOption<int>), "[]");
        Test(ApiOption.Some(1), "[1]");

        // ApiNullable
        Test(default(ApiNullable<int>), "null");
        Test(ApiNullable.Value(1), "1");

        // ApiNullable8
        Test(default(ApiNullable8<int>), "null");
        Test(ApiNullable8.Value(1), "1");

        // ApiArray
        Test(default(ApiArray<int>), "[]");
        Test(new ApiArray<int>([1,2]), "[1,2]");

        // ApiList
        Test(new ApiList<int>(), "[]");
        Test(new ApiList<int>([1,2]), "[1,2]");

        // ApiMap
        Test(new ApiMap<int, bool>(), "{}");
        Test(new ApiMap<int, bool>().With(0, false).With(1, true), "{\"0\":false,\"1\":true}");

        // ApiSet
        Test(new ApiSet<int>(), "[]");
        Test(new ApiSet<int>().With(0).With(1), "[0,1]");

        // Session
        Test(default(Session), "null");
        Test(new Session("01234567890"), "\"01234567890\"");

        // DbShard
        Test(default(DbShard), "\"\"");
        Test(new DbShard("01"), "\"01\"");
    }

    private void Test<T>(T value, string expectedJson)
    {
        var s = new MessagePackByteSerializer(MessagePackByteSerializer.DefaultOptions);
        var bytes = s.Write(value).WrittenSpan.ToArray();
        var json = MessagePackSerializer.ConvertToJson(bytes, MessagePackByteSerializer.DefaultOptions);
        json.Should().Be(expectedJson);
    }

    private void Test<T>(T value, Action<string> assertion)
    {
        var s = new MessagePackByteSerializer(MessagePackByteSerializer.DefaultOptions);
        var bytes = s.Write(value).WrittenSpan.ToArray();
        var json = MessagePackSerializer.ConvertToJson(bytes, MessagePackByteSerializer.DefaultOptions);
        assertion.Invoke(json);
    }
}
