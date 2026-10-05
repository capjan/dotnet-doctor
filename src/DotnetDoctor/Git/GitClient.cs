using System.Diagnostics;
using DotnetDoctor.Infrastructure;

namespace DotnetDoctor.Git;

internal static class GitClient
{
    private static readonly Version HookRunMinimumVersion = new(2, 43);

    public static ProcessResult? Run(params string[] arguments)
    {
        return RunInDirectory(null, arguments);
    }

    public static ProcessResult? RunInDirectory(string? workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return ProcessRunner.Run(startInfo);
    }

    public static ProcessResult? RunCommitMessageHook(string hookPath, string messagePath, string repositoryRoot, Version gitVersion)
    {
        if (gitVersion >= HookRunMinimumVersion)
        {
            return RunInDirectory(repositoryRoot, "hook", "run", ConventionalCommit.HookName, "--", messagePath);
        }

        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = hookPath,
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(messagePath);
        return ProcessRunner.Run(startInfo, 15_000);
    }

    public static Version? ParseVersion(string output)
    {
        const string prefix = "git version ";
        var normalizedOutput = output.Trim();
        if (!normalizedOutput.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var versionText = normalizedOutput[prefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var parts = versionText?.Split('.');
        if (parts is not { Length: >= 2 } ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor))
        {
            return null;
        }

        var patch = parts.Length >= 3 && int.TryParse(parts[2], out var parsedPatch) ? parsedPatch : 0;
        return new Version(major, minor, patch);
    }
}
