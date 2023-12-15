using Xunit;

namespace ActualLab.Testing;

public sealed class SkipOnGitHubFact : FactAttribute
{
    public SkipOnGitHubFact() {
        if (TestRunnerInfo.IsGitHubAction())
            Skip = "Ignored on GitHub";
    }
}
