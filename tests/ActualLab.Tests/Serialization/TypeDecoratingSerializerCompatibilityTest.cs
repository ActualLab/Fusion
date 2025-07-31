namespace ActualLab.Tests.Serialization;

public class TypeDecoratingSerializerCompatibilityTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var legacy = TypeDecoratingTextSerializer.DefaultLegacy;
        var modern = TypeDecoratingTextSerializer.Default;

        var shapes = new Shape[] {
            null!,
            new(),
            new Circle(1),
            new Square(1),
            new Union<Circle>(new Circle(1), new Circle(2))
        };
        foreach (var shape in shapes) {
            AssertPassesThrough(shape, legacy, modern);
            AssertPassesThrough(shape, modern, modern);
            if (shape is not null)
                Assert.ThrowsAny<Exception>(
                    () => AssertPassesThrough(shape, modern, legacy));
        }
    }

    private void AssertPassesThrough<T>(T value, ITextSerializer serializer, ITextSerializer deserializer)
    {
        var data = serializer.Write(value);
        Out.WriteLine($"Serialized: {data}");
        var readValue = deserializer.Read<T>(data);
        readValue.Should().Be(value);
    }

    // Nested types

    public record Shape
    {
        public virtual double Area => 0;
    }

    public record Circle(double R) : Shape
    {
        public override double Area => Math.PI * R * R;
    }

    public record Square(double L) : Shape
    {
        public override double Area => L * L;
    }

    public record Union<T>(params T[] Shapes) : Shape
        where T : Shape
    {
        public override double Area => Shapes.Sum(x => x.Area);

        public virtual bool Equals(Union<T>? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return base.Equals(other) && Shapes.SequenceEqual(other.Shapes);
        }

        public override int GetHashCode() => HashCode.Combine(Shapes);
    }
}
