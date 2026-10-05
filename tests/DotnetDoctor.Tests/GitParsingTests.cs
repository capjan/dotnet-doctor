using DotnetDoctor.Git;
using Xunit;

namespace DotnetDoctor.Tests;

public sealed class GitParsingTests
{
    [Fact]
    public void Installed_hook_uses_Unix_line_endings()
    {
        Assert.StartsWith("#!/bin/sh\n", ConventionalCommit.HookScript);
        Assert.DoesNotContain("\r", ConventionalCommit.HookScript);
    }

    [Theory]
    [InlineData("git version 2.54.0", "2.54.0")]
    [InlineData("git version 2.39.5 (Apple Git-154)", "2.39.5")]
    [InlineData("git version 2.9.0.windows.1", "2.9.0")]
    [InlineData("git version 2.9", "2.9.0")]
    [InlineData(" git version 2.54.0\n", "2.54.0")]
    public void Parses_platform_specific_Git_versions(string output, string expected)
    {
        Assert.Equal(Version.Parse(expected), GitClient.ParseVersion(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2.54.0")]
    [InlineData("git version unknown")]
    [InlineData("git version 2")]
    [InlineData("git version 2.invalid")]
    public void Unrecognized_Git_versions_return_no_version(string output)
    {
        Assert.Null(GitClient.ParseVersion(output));
    }

    [Theory]
    [InlineData("feat: add a command", true)]
    [InlineData("fix(cli): handle missing hooks", true)]
    [InlineData("feat!: change the interface", true)]
    [InlineData("feat(cli)!: change the interface", true)]
    [InlineData("build-ci: update the workflow", true)]
    [InlineData("feat: add a command\n\nA longer description.", true)]
    [InlineData("add a command", false)]
    [InlineData("feat:add a command", false)]
    [InlineData("feat: ", false)]
    [InlineData(": add a command", false)]
    [InlineData("feat(): add a command", false)]
    [InlineData("feat(cli: add a command", false)]
    public void Recognizes_Conventional_Commit_headers(string message, bool expected)
    {
        Assert.Equal(expected, ConventionalCommit.IsValidMessage(message));
    }
}
