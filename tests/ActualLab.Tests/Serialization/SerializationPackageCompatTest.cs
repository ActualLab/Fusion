namespace ActualLab.Tests.Serialization;

public partial class SerializationPackageCompatTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void NewtonsoftJsonShouldReadPreV16TypeNames()
    {
        // arrange
        var value = (object)NewtonsoftJsonSerialized.New(5);
        var json = NewtonsoftJsonSerializer.Default.Write(value, typeof(object));
        var oldJson = json.Replace(", ActualLab.Serialization.NewtonsoftJson\"", ", ActualLab.Core\"");
        Out.WriteLine(oldJson);

        // act
        var result = NewtonsoftJsonSerializer.Default.Read(oldJson, typeof(object));

        // assert
        oldJson.Should().Contain(", ActualLab.Core");
        result.Should().BeOfType<NewtonsoftJsonSerialized<int>>().Which.Value.Should().Be(5);
    }

    [Fact]
    public void PackageSerializerKindsShouldBeRegistered()
    {
        // assert
        SerializerKind.NewtonsoftJson.GetDefaultSerializer().Should().BeSameAs(NewtonsoftJsonSerializer.Default);
#if !NETFRAMEWORK
        SerializerKind.MemoryPack.GetDefaultSerializer().Should().BeSameAs(MemoryPackByteSerializer.Default);
#endif
#if NET8_0_OR_GREATER
        SerializerKind.NerdbankMessagePack.GetDefaultSerializer()
            .Should().BeSameAs(NerdbankMessagePackByteSerializer.Default);
        SerializerKind.NerdbankMessagePack.GetDefaultTypeDecoratingSerializer()
            .Should().BeSameAs(NerdbankMessagePackByteSerializer.DefaultTypeDecorating);
#endif
    }

    [Fact]
    public void CustomSerializerKindShouldResolveRegisteredSerializer()
    {
        // arrange
        var serializer = new SystemJsonSerializer(SystemJsonSerializer.DefaultOptions);
        var typeDecoratingSerializer = new TypeDecoratingTextSerializer(serializer);

        // act
        SerializerKind.Custom7.Register(() => serializer, () => typeDecoratingSerializer);
        var filteredSerializer = TypeSchema<TypeSchema.PrimitiveOnly>.GetTypeDecoratingSerializer(SerializerKind.Custom7);

        // assert
        SerializerKind.Custom7.IsAvailable().Should().BeTrue();
        SerializerKind.Custom6.IsAvailable().Should().BeFalse();
        SerializerKind.Custom7.GetDefaultSerializer().Should().BeSameAs(serializer);
        SerializerKind.Custom7.GetDefaultTypeDecoratingSerializer().Should().BeSameAs(typeDecoratingSerializer);
        filteredSerializer.Should().BeOfType<TypeDecoratingTextSerializer>();
        Assert.Throws<NotSupportedException>(() => SerializerKind.Custom6.GetDefaultSerializer());
    }

#if !NETFRAMEWORK
    [Fact]
    public void MemoryPackRegistrationShouldHandleRecursiveTypes()
    {
        // arrange
        var node = new RecursiveNode { Children = [new RecursiveNode()] };
        var genericNode = new GenericNode<int> { Value = 3 };

        // act
        MemoryPackSerialization.Register<RecursiveNode>();
        MemoryPackSerialization.Register<GenericNode<int>>();
        var nodeCopy = node.PassThrough(new TestSerializer("MemoryPack", () => MemoryPackByteSerializer.Default));
        var genericNodeCopy = genericNode.PassThrough(
            new TestSerializer("MemoryPack", () => MemoryPackByteSerializer.Default));

        // assert
        nodeCopy.Children.Count.Should().Be(1);
        genericNodeCopy.Value.Should().Be(3);
    }

    // Nested types

    [MemoryPackable]
    public partial class RecursiveNode
    {
        [MemoryPackAllowSerialize]
        public ApiArray<RecursiveNode> Children { get; set; } = ApiArray<RecursiveNode>.Empty;
    }

    // The ignored member would make the registration walk expand forever: Node<int> -> Node<List<int>> -> ...
    [MemoryPackable]
    public partial class GenericNode<T>
    {
        public T Value { get; set; } = default!;
        [MemoryPackIgnore]
        public GenericNode<List<T>>? Metadata { get; set; }
    }
#endif
}
