using DotnetDoctor.Localization;

namespace DotnetDoctor.Git;

internal static class GitRepository
{
    public const string HooksDirectoryName = ".githooks";
    public const string HooksPathConfigKey = "core.hooksPath";

    public static string GetHooksDirectoryPath(string repositoryRoot) =>
        Path.GetFullPath(Path.Combine(repositoryRoot, HooksDirectoryName));

    public static IEnumerable<string> EnumerateHookScripts(string hooksDirectory) =>
        Directory.EnumerateFiles(hooksDirectory)
            .Where(path => !Path.GetFileName(path).EndsWith(".sample", StringComparison.OrdinalIgnoreCase));

    public static string? GetRoot(out string error)
    {
        error = string.Empty;
        var repositoryResult = GitClient.Run("rev-parse", "--show-toplevel");
        if (repositoryResult is null)
        {
            error = Strings.GitNotFoundOnPath;
            return null;
        }

        if (repositoryResult.Value.ExitCode != 0)
        {
            error = Strings.NotGitWorkingTree;
            return null;
        }

        return repositoryResult.Value.Output.TrimEnd('\r', '\n');
    }

    public static string? GetLocalHooksDirectory(out string repositoryRoot, out string error)
    {
        var resolvedRepositoryRoot = GetRoot(out error);
        if (resolvedRepositoryRoot is null)
        {
            repositoryRoot = string.Empty;
            return null;
        }

        repositoryRoot = resolvedRepositoryRoot;
        var localHooksPathResult = GitClient.Run("config", "--local", "--get", HooksPathConfigKey);
        if (localHooksPathResult is null)
        {
            error = Strings.GitCouldNotStart;
            return null;
        }

        if (localHooksPathResult.Value.ExitCode != 0)
        {
            error = string.IsNullOrWhiteSpace(localHooksPathResult.Value.Error)
                ? Strings.HooksPathNotSet
                : Messages.Format(Strings.HooksPathReadFailed, localHooksPathResult.Value.Error.Trim());
            return null;
        }

        var expectedHooksDirectory = GetHooksDirectoryPath(repositoryRoot);
        if (!HooksPathMatches(localHooksPathResult.Value.Output, repositoryRoot, expectedHooksDirectory))
        {
            error = Messages.Format(Strings.HooksPathShouldBeLocal, localHooksPathResult.Value.Output.TrimEnd('\r', '\n'));
            return null;
        }

        var effectiveHooksPathResult = GitClient.Run("config", "--get", HooksPathConfigKey);
        if (effectiveHooksPathResult is null || effectiveHooksPathResult.Value.ExitCode != 0)
        {
            error = Strings.HooksPathEffectiveReadFailed;
            return null;
        }

        if (!HooksPathMatches(effectiveHooksPathResult.Value.Output, repositoryRoot, expectedHooksDirectory))
        {
            error = Strings.HooksPathEffectiveWrong;
            return null;
        }

        if (!Directory.Exists(expectedHooksDirectory))
        {
            error = Strings.HooksDirectoryMissing;
            return null;
        }

        return expectedHooksDirectory;
    }

    public static bool HooksPathMatches(string configuredPath, string repositoryRoot, string expectedHooksDirectory)
    {
        try
        {
            var resolvedPath = Path.GetFullPath(configuredPath.TrimEnd('\r', '\n'), repositoryRoot);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(resolvedPath, expectedHooksDirectory, comparison);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }
}
