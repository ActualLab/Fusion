namespace ActualLab.Tests;

public sealed class SkipOnGitHubFact : FactAttribute
{
    public SkipOnGitHubFact() {
        if (TestRunnerInfo.IsGitHubAction())
            Skip = "Ignored on GitHub";
    }
}
