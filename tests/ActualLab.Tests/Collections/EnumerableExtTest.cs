namespace ActualLab.Tests.Collections;

public class EnumerableExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void SkipNullItemsTest()
    {
        var seq1 = new object?[] {"", null, "x"}.SkipNullItems().ToList();
        seq1.Should().Equal(["", "x"]);

        var seq2 = new int?[] {0, null, 1}.SkipNullItems().ToList();
        seq2.Should().Equal([0, 1]);
    }

    [Fact]
    public void SuppressExceptionsTest()
    {
        TestSeq(0).SuppressExceptions().Should().Equal([]);
        TestSeq(0, true).SuppressExceptions().Should().Equal([]);

        TestSeq(2).SuppressExceptions().Should().Equal([0, 1]);
        TestSeq(2, true).SuppressExceptions().Should().Equal([0, 1]);
    }

    // Private methods

    private IEnumerable<int> TestSeq(int count, bool mustFail = false)
    {
        for (var i = 0; i < count; i++) {
            yield return i;
        }
        if (mustFail)
            throw new InvalidOperationException();
    }
}
