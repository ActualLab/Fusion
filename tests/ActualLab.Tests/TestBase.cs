using ActualLab.Testing.Output;

namespace ActualLab.Tests;

public abstract class TestBase(ITestOutputHelper @out) : IAsyncLifetime
{
    public ITestOutputHelper Out { get; set; } = @out;

    public virtual Task InitializeAsync() => Task.CompletedTask;
    public virtual Task DisposeAsync() => Task.CompletedTask;

    protected Disposable<TestOutputCapture> CaptureOutput()
    {
        var testOutputCapture = new TestOutputCapture(Out);
        var oldOut = Out;
        Out = testOutputCapture;
        return new Disposable<TestOutputCapture>(
            testOutputCapture,
            _ => Out = oldOut);
    }
}
