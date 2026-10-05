using Xunit;

namespace DotnetDoctor.Tests;

public sealed class CommandTests
{
    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void Version_does_not_require_a_repository(string option)
    {
        using var repository = new TemporaryRepository(initialize: false);

        var result = repository.Doctor(option);

        Assert.Equal(0, result.ExitCode);
        var version = typeof(DoctorApplication).Assembly.GetName().Version!.ToString(3);
        Assert.Equal($"dotnet-doctor {version}{Environment.NewLine}", result.Output);
        Assert.False(Directory.Exists(Path.Combine(repository.Root, ".githooks")));
    }

    [Fact]
    public void Help_lists_the_existing_commands()
    {
        using var repository = new TemporaryRepository(initialize: false);

        var result = repository.Doctor("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("diagnose", result.Output);
        Assert.Contains("fix", result.Output);
        Assert.DoesNotContain("Doctor summary:", result.Output);
    }

    [Fact]
    public void Missing_hooks_fail_without_modifying_the_repository()
    {
        using var repository = new TemporaryRepository();

        var result = repository.Doctor("diagnose", "git-hooks");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("[✗] Git hooks", result.Output);
        Assert.Contains("core.hooksPath is not set", result.Output);
        Assert.False(Directory.Exists(Path.Combine(repository.Root, ".githooks")));
        Assert.Equal(1, repository.Git("config", "--local", "--get", "core.hooksPath").ExitCode);
    }

    [Fact]
    public void Repository_diagnostics_fail_outside_a_working_tree()
    {
        using var repository = new TemporaryRepository(initialize: false);

        var result = repository.Doctor("diagnose", "git-hooks");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not inside a Git working tree", result.Output);
    }

    [Theory]
    [InlineData("de-DE", "Diagnose:", "Git-Version", "erforderlich: 2.9+")]
    [InlineData("en-US", "Doctor summary:", "Git version", "2.9 or later required")]
    [InlineData("fr-FR", "Doctor summary:", "Git version", "2.9 or later required")]
    public void Diagnostic_output_uses_the_ui_culture_or_English_fallback(string culture, string heading, string label, string detail)
    {
        using var repository = new TemporaryRepository(initialize: false);

        var result = repository.DoctorIn(repository.Root, culture, "diagnose", "git");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith(heading, result.Output);
        Assert.Contains(label, result.Output);
        Assert.Contains(detail, result.Output);
    }

    [Fact]
    public void Fix_installs_a_hook_and_default_command_runs_all_diagnostics_in_order()
    {
        using var repository = new TemporaryRepository();

        var repair = repository.Doctor("fix");
        var defaultCommand = repository.Doctor();
        var all = repository.Doctor("diagnose", "all");

        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 0, repair.ExitCode);
        Assert.Contains("Repair summary:", repair.Output);
        Assert.Contains("installed the default Conventional Commits validator", repair.Output);
        Assert.Equal(".githooks", repository.Git("config", "--local", "--get", "core.hooksPath").Output.Trim());
        Assert.True(File.Exists(Path.Combine(repository.Root, ".githooks", "commit-msg")));
        Assert.Equal(all.ExitCode, defaultCommand.ExitCode);
        Assert.Equal(all.Output, defaultCommand.Output);
        var labels = new[] { "Git version", "Git hooks", "Hook permissions", "Commit message hook" };
        var previousIndex = -1;
        foreach (var label in labels)
        {
            var index = all.Output.IndexOf(label, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Unexpected diagnostic order: {all.Output}");
            previousIndex = index;
        }
    }

    [Fact]
    public void Fix_is_repeatable_and_keeps_the_installed_hook()
    {
        using var repository = new TemporaryRepository();
        repository.Doctor("fix");
        var path = Path.Combine(repository.Root, ".githooks", "commit-msg");
        var original = File.ReadAllBytes(path);

        var result = repository.Doctor("fix");

        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 0, result.ExitCode);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Contains("kept the existing hook", result.Output);
        Assert.DoesNotContain("installed the default", result.Output);
    }

    [Theory]
    [InlineData("--local")]
    [InlineData("--global")]
    public void Fix_preserves_custom_hooks_paths(string scope)
    {
        using var repository = new TemporaryRepository();
        repository.Git("config", scope, "core.hooksPath", "custom-hooks");

        var result = repository.Doctor("fix");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("left unchanged", result.Output);
        Assert.Equal("custom-hooks", repository.Git("config", scope, "--get", "core.hooksPath").Output.Trim());
        Assert.False(Directory.Exists(Path.Combine(repository.Root, ".githooks")));
        if (scope == "--global")
        {
            Assert.Equal(1, repository.Git("config", "--local", "--get", "core.hooksPath").ExitCode);
        }
    }

    [Fact]
    public void Fix_preserves_an_existing_hook_that_accepts_invalid_messages()
    {
        using var repository = new TemporaryRepository();
        const string script = "#!/bin/sh\nexit 0\n";
        var path = repository.WriteHook(script, executable: false);

        var result = repository.Doctor("fix");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(script, File.ReadAllText(path));
        Assert.Contains("kept the existing hook", result.Output);
        Assert.Contains("accepted an invalid message without correcting it", result.Output);
    }

    [Fact]
    public void Commit_hook_may_normalize_an_invalid_message()
    {
        using var repository = new TemporaryRepository();
        repository.WriteHook("#!/bin/sh\nprintf 'fix: normalized by test\\n' > \"$1\"\nexit 0\n");

        var result = repository.Doctor("diagnose", "commit-msg");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("enforces the Conventional Commits format", result.Output);
    }

    [Fact]
    public void Fix_handles_platform_permissions_and_ignores_samples()
    {
        using var repository = new TemporaryRepository();
        repository.Doctor("fix");
        var path = Path.Combine(repository.Root, ".githooks", "commit-msg");
        var sample = Path.Combine(repository.Root, ".githooks", "example.sample");
        File.WriteAllText(sample, "sample");
        if (OperatingSystem.IsWindows())
        {
            var windowsResult = repository.Doctor("fix");

            Assert.Equal(1, windowsResult.ExitCode);
            Assert.Contains("cannot verify executable permissions on Windows", windowsResult.Output);
            Assert.Equal("sample", File.ReadAllText(sample));
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var sampleMode = File.GetUnixFileMode(sample);
        Assert.Equal(1, repository.Doctor("diagnose", "git-hooks").ExitCode);

        var result = repository.Doctor("fix");

        Assert.Equal(0, result.ExitCode);
        Assert.NotEqual((UnixFileMode)0, File.GetUnixFileMode(path) & UnixFileMode.UserExecute);
        Assert.Equal(sampleMode, File.GetUnixFileMode(sample));
    }

    [Fact]
    public void Sample_files_do_not_count_as_hook_scripts()
    {
        using var repository = new TemporaryRepository();
        var directory = Path.Combine(repository.Root, ".githooks");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "commit-msg.sample"), "sample");
        repository.Git("config", "--local", "core.hooksPath", ".githooks");

        var result = repository.Doctor("diagnose", "git-hooks");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("no hook scripts found in .githooks", result.Output);
    }

    [Fact]
    public void Fix_works_from_a_nested_directory()
    {
        using var repository = new TemporaryRepository();
        var directory = Path.Combine(repository.Root, "src", "nested");
        Directory.CreateDirectory(directory);

        var result = repository.DoctorIn(directory, "en-US", "fix");

        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(repository.Root, ".githooks", "commit-msg")));
        Assert.False(Directory.Exists(Path.Combine(directory, ".githooks")));
    }
}
