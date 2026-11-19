namespace ActualLab.Tests.Collections;

public class RadixHeapSetTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void EmptyHeapTest()
    {
        var heap = new RadixHeapSet<int>();
        heap.Count.Should().Be(0);
        heap.MinPriority.Should().Be(0);
        heap.ExtractMin().IsNone.Should().BeTrue();
        Assert.Empty(heap);
    }

    [Fact]
    public void RandomHeapTest()
    {
        var rnd = new Random(10);
        for (var count = 1; count < 150; count++) {
            for (var iteration = 0; iteration < 500; iteration++) {
                var items = Enumerable
                    .Range(0, count)
                    .Select(_ => rnd.Next(1000))
                    .Distinct()
                    .ToList();
                var heap = new RadixHeapSet<int>();
                foreach (var item in items)
                    heap.Add(item, item);

                var itemsToRemove = new HashSet<int>(
                    items.Where(_ => rnd.NextDouble() < 0.33));
                foreach (var item in itemsToRemove)
                    heap.Remove(item, out _);
                items = items
                    .Where(i => !itemsToRemove.Contains(i))
                    .ToList();

                var sortedItems = items.OrderBy(i => i).ToList();
                var heapPriorities = heap.Select(i => i.Priority).ToList();
                var heapValues = heap.Select(i => i.Value).ToList();

                heap.Count.Should().Be(sortedItems.Count);
                heapPriorities.Count.Should().Be(sortedItems.Count);
                heapValues.Count.Should().Be(sortedItems.Count);
                for (var i = 0; i < sortedItems.Count; i++) {
                    heapPriorities[i].Should().Be(sortedItems[i]);
                    heapValues[i].Should().Be(sortedItems[i]);
                }
                var min = heap.PeekMin();
                var minSet = heap.PeekMinSet();
                if (sortedItems.Count == 0) {
                    min.IsNone.Should().BeTrue();
                    minSet.Count.Should().Be(0);
                } else {
                    min.Value.Priority.Should().Be(sortedItems[0]);
                    min.Value.Value.Should().Be(sortedItems[0]);
                    minSet.Should().NotBeNullOrEmpty();
                    foreach (var (value, priority) in minSet) {
                        priority.Should().Be(sortedItems[0]);
                        value.Should().Be(sortedItems[0]);
                    }
                }
            }
        }
    }

    [Fact]
    public void AddOrUpdateTest()
    {
        var heap = new RadixHeapSet<string>();
        heap.AddOrUpdate(5, "a");
        heap.AddOrUpdate(1, "b");
        heap.Count.Should().Be(2);
        heap.PeekMin().Value.Value.Should().Be("b");
        heap.ExtractMin().Value.Value.Should().Be("b");
        heap.Count.Should().Be(1);

        heap.AddOrUpdate(3, "a");
        heap.Count.Should().Be(1);
        heap.PeekMin().Value.Value.Should().Be("a");
        heap.ExtractMin().Value.Value.Should().Be("a");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5000L)] // Priorities cross zero, so the sign bit differs
    public void CursorExtractionModelTest(long startPriority)
    {
        // Mimics TimerSet: values are added at or after the cursor, the cursor advances by one,
        // or jumps to any priority up to the minimum one (e.g. to TryGetMinPriorityLowerBound)
        var rnd = new Random(42);
        for (var iteration = 0; iteration < 100; iteration++) {
            var heap = new RadixHeapSet<int>(65, startPriority);
            var model = new Dictionary<int, long>();
            var cursor = startPriority;
            var nextValue = 0;
            for (var step = 0; step < 300; step++) {
                var action = rnd.Next(10);
                if (action < 3) {
                    // A cluster of values, often large enough for the lower bound to skip the exact scan
                    var priority = cursor + (rnd.Next(4) == 0 ? rnd.Next(16) : 1L << rnd.Next(1, 20)) + rnd.Next(64);
                    var count = rnd.Next(3) == 0 ? 40 : rnd.Next(1, 4);
                    for (var i = 0; i < count; i++) {
                        var value = nextValue++;
                        var p = priority + rnd.Next(4);
                        heap.AddOrUpdate(p, value);
                        model[value] = p;
                    }
                }
                else if (action == 3 && model.Count != 0) {
                    var value = model.Keys.ElementAt(rnd.Next(model.Count));
                    heap.Remove(value, out var p).Should().BeTrue();
                    p.Should().Be(model[value]);
                    model.Remove(value);
                }
                else {
                    var trueMin = model.Count == 0 ? (long?)null : model.Values.Min();
                    var hasBound = heap.TryGetMinPriorityLowerBound(out var bound);
                    hasBound.Should().Be(model.Count != 0);
                    if (hasBound) {
                        bound.Should().BeLessThanOrEqualTo(trueMin!.Value);
                        bound.Should().BeGreaterThanOrEqualTo(heap.MinPriority);
                        // A cursor past the minimum priority is rejected
                        var heapCopy = new RadixHeapSet<int>(heap);
                        heapCopy.Invoking(h => h.ExtractMinSet(trueMin.Value + 1))
                            .Should().Throw<ArgumentOutOfRangeException>();
                    }

                    var minSet = heap.ExtractMinSet(cursor);
                    var expected = model.Where(x => x.Value == cursor).Select(x => x.Key).ToHashSet();
                    minSet.Keys.Should().BeEquivalentTo(expected);
                    foreach (var value in expected)
                        model.Remove(value);
                    heap.Count.Should().Be(model.Count);

                    trueMin = model.Count == 0 ? (long?)null : model.Values.Min();
                    var nextCursor = cursor + 1;
                    if (trueMin is { } min && min > nextCursor) {
                        nextCursor = rnd.Next(3) switch {
                            0 => nextCursor,
                            1 => heap.TryGetMinPriorityLowerBound(out var b) ? Math.Max(nextCursor, b) : nextCursor,
                            _ => nextCursor + (long)(rnd.NextDouble() * (min - nextCursor + 1)),
                        };
                    }
                    cursor = nextCursor;
                }
            }
            // Drain
            while (model.Count != 0) {
                var min = model.Values.Min();
                heap.TryGetMinPriorityLowerBound(out var bound).Should().BeTrue();
                bound.Should().BeLessThanOrEqualTo(min);
                cursor = Math.Max(cursor, bound);
                var minSet = heap.ExtractMinSet(cursor);
                var expected = model.Where(x => x.Value == cursor).Select(x => x.Key).ToHashSet();
                minSet.Keys.Should().BeEquivalentTo(expected);
                foreach (var value in expected)
                    model.Remove(value);
                cursor++;
            }
        }
    }

    [Fact]
    public void MinPriorityLowerBoundShouldHandleSignBit()
    {
        var heap = new RadixHeapSet<int>(65, -1);
        for (var i = 0; i < 40; i++)
            heap.Add(100 + i, i); // All values are in the last bucket: they differ from -1 in the sign bit
        heap.TryGetMinPriorityLowerBound(out var bound).Should().BeTrue();
        bound.Should().Be(0);
        heap.ExtractMinSet(0).Count.Should().Be(0);
        heap.TryGetMinPriorityLowerBound(out bound).Should().BeTrue();
        bound.Should().BeLessThanOrEqualTo(100);
        heap.ExtractMinSet(100).Keys.Should().BeEquivalentTo([0]);
    }

    [Fact]
    public void UpdateMinPriorityTest()
    {
        var heap = new RadixHeapSet<int>();
        for (var i = 1; i < 100; i += 5) {
            heap.AddOrUpdate(i, i);
            heap.Count.Should().Be((i + 4) / 5);
            heap.PeekMin().Value.Value.Should().Be(1);
        }
        for (var i = 0; i < 95; i++) {
            var minSet = heap.ExtractMinSet(i);
            var isFit = (i - 1) % 5 == 0;
            minSet.Count.Should().Be(isFit ? 1 : 0);
            if (isFit)
                minSet.Single().Value.Should().Be(i);
        }
    }
}
