using System;

namespace Stl.Testing
{
    public static class TestRunnerInfo
    {
        public static class Docker
        {
            public static readonly bool IsDotnetRunningInContainer =
                "" != (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") ?? "");
        }

        public static class TeamCity
        {
            public static readonly Version? Version;
            public static readonly string ProjectName;
            public static readonly string BuildConfigurationName;

            static TeamCity()
            {
                var version = Environment.GetEnvironmentVariable("TEAMCITY_VERSION");
                if (!string.IsNullOrEmpty(version))
                    Version.TryParse(version, out Version);
                ProjectName = Environment.GetEnvironmentVariable("TEAMCITY_PROJECT_NAME") ?? "";
                BuildConfigurationName = Environment.GetEnvironmentVariable("TEAMCITY_BUILDCONF_NAME") ?? "";
            }
        }

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
                var isActionRunning = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") ?? "";
                bool.TryParse(isActionRunning, out IsActionRunning);
            }
        }

        public static bool IsBuildAgent()
            => TeamCity.Version != null || GitHub.IsActionRunning;
    }
}
