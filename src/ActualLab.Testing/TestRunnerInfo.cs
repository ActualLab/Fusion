namespace ActualLab.Testing;

/// <summary>
/// Detects the current test runner environment (Docker, TeamCity, GitHub Actions)
/// by inspecting environment variables.
/// </summary>
public static class TestRunnerInfo
{
    /// <summary>
    /// Detects whether tests are running inside a Docker container.
    /// </summary>
    public static class Docker
    {
        public static readonly bool IsDotnetRunningInContainer =
            !(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") ?? "").IsNullOrEmpty();
    }

    /// <summary>
    /// Detects TeamCity build agent environment from environment variables.
    /// </summary>
    public static class TeamCity
    {
        public static readonly Version? Version;
        public static readonly string ProjectName;
        public static readonly string BuildConfigurationName;

        static TeamCity()
        {
            var version = Environment.GetEnvironmentVariable("TEAMCITY_VERSION");
            if (version.IsNullOrEmpty() || !Version.TryParse(version, out Version))
                Version = null;
            ProjectName = Environment.GetEnvironmentVariable("TEAMCITY_PROJECT_NAME") ?? "";
            BuildConfigurationName = Environment.GetEnvironmentVariable("TEAMCITY_BUILDCONF_NAME") ?? "";
        }
    }

    /// <summary>
    /// Detects GitHub Actions environment from environment variables.
    /// </summary>
    public static class GitHub
    {
        public static readonly string Workflow;
        public static readonly string Action;
        public static readonly string RunId;
        public static readonly bool IsActionRunning;

        static GitHub()
        {
            Workflow = Environment.GetEnvironmentVariable("GITHUB_WORKFLOW") ?? "";
            Action = Environment.GetEnvironmentVariable("GITHUB_ACTION") ?? "";
            RunId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? "";
            IsActionRunning = !RunId.IsNullOrEmpty();
        }
    }

    public static bool IsBuildAgent()
        => IsGitHubAction() || IsTeamCityAgent();

    public static bool IsTeamCityAgent()
        => TeamCity.Version is not null;

    public static bool IsGitHubAction()
        => GitHub.IsActionRunning;
}
