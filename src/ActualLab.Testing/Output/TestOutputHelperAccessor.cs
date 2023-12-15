using Xunit.Abstractions;
using Xunit.DependencyInjection;

namespace ActualLab.Testing.Output;

public class TestOutputHelperAccessor(ITestOutputHelper? output) : ITestOutputHelperAccessor
{
    public ITestOutputHelper? Output { get; set; } = output;
}
