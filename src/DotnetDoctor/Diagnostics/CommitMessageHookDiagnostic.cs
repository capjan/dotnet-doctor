using DotnetDoctor.Git;
using DotnetDoctor.Localization;
using DotnetDoctor.Reporting;

namespace DotnetDoctor.Diagnostics;

internal static class CommitMessageHookDiagnostic
{
    public static DoctorResult Check()
    {
        var hooksDirectory = GitRepository.GetLocalHooksDirectory(out var repositoryRoot, out var setupError);
        if (hooksDirectory is null)
        {
            return new(false, Strings.CommitMessageHookLabel, Messages.Format(Strings.CommitHookSetupUnavailable, setupError));
        }

        var hookPath = Path.Combine(hooksDirectory, ConventionalCommit.HookName);
        if (!File.Exists(hookPath))
        {
            return new(false, Strings.CommitMessageHookLabel, Strings.CommitHookMissing);
        }

        var permissionResult = CheckExecutablePermission(hookPath);
        if (permissionResult is not null)
        {
            return permissionResult.Value;
        }

        var gitVersionResult = GitClient.Run("--version");
        var gitVersion = gitVersionResult is { } result && result.ExitCode == 0
            ? GitClient.ParseVersion(result.Output.Trim())
            : null;
        if (gitVersion is null)
        {
            return new(false, Strings.CommitMessageHookLabel, Strings.CommitHookGitVersionUnknown);
        }

        string? validMessagePath = null;
        string? invalidMessagePath = null;
        try
        {
            validMessagePath = Path.GetTempFileName();
            invalidMessagePath = Path.GetTempFileName();

            var validMessages = new[]
            {
                "feat(cli): add a command\n",
                "fix(parser): handle an empty message\n",
                "feat(api)!: change the output format\n\nBREAKING CHANGE: consumers must update their parser\n",
                "build-ci: update the workflow\n"
            };

            foreach (var validMessage in validMessages)
            {
                File.WriteAllText(validMessagePath, validMessage);
                var validResult = GitClient.RunCommitMessageHook(hookPath, validMessagePath, repositoryRoot, gitVersion);
                if (validResult is null)
                {
                    return new(false, Strings.CommitMessageHookLabel, Strings.CommitHookCouldNotRun);
                }

                if (validResult.Value.ExitCode == -1)
                {
                    return new(false, Strings.CommitMessageHookLabel, validResult.Value.Error);
                }

                if (validResult.Value.ExitCode != 0)
                {
                    var rejectedHeader = validMessage.Split('\n', 2)[0];
                    return new(false, Strings.CommitMessageHookLabel, Messages.Format(Strings.CommitHookValidRejected, rejectedHeader));
                }

                if (!ConventionalCommit.IsValidMessage(File.ReadAllText(validMessagePath)))
                {
                    return new(false, Strings.CommitMessageHookLabel, Strings.CommitHookValidChanged);
                }
            }

            var invalidMessages = new[]
            {
                "This is not a Conventional Commit message\n",
                "FEAT: uppercase type\n"
            };

            foreach (var invalidMessage in invalidMessages)
            {
                File.WriteAllText(invalidMessagePath, invalidMessage);
                var invalidResult = GitClient.RunCommitMessageHook(hookPath, invalidMessagePath, repositoryRoot, gitVersion);
                if (invalidResult is null)
                {
                    return new(false, Strings.CommitMessageHookLabel, Strings.CommitHookCouldNotRun);
                }

                if (invalidResult.Value.ExitCode == -1)
                {
                    return new(false, Strings.CommitMessageHookLabel, invalidResult.Value.Error);
                }

                var invalidMessageWasNormalized = ConventionalCommit.IsValidMessage(File.ReadAllText(invalidMessagePath));
                if (invalidResult.Value.ExitCode == 0 && !invalidMessageWasNormalized)
                {
                    return new(false, Strings.CommitMessageHookLabel, Strings.CommitHookInvalidAccepted);
                }
            }

            return new(true, Strings.CommitMessageHookLabel, Strings.CommitHookSuccess);
        }
        catch (IOException)
        {
            return new(false, Strings.CommitMessageHookLabel, Strings.CommitHookTempReadFailed);
        }
        catch (UnauthorizedAccessException)
        {
            return new(false, Strings.CommitMessageHookLabel, Strings.CommitHookTempReadDenied);
        }
        finally
        {
            DeleteTemporaryFile(validMessagePath);
            DeleteTemporaryFile(invalidMessagePath);
        }
    }

    private static void DeleteTemporaryFile(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static DoctorResult? CheckExecutablePermission(string hookPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var mode = File.GetUnixFileMode(hookPath);
            var executableBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (mode & executableBits) == 0
                ? new(false, Strings.CommitMessageHookLabel, Strings.CommitHookNotExecutable)
                : null;
        }
        catch (IOException)
        {
            return new(false, Strings.CommitMessageHookLabel, Strings.HookPermissionsReadFailed);
        }
        catch (UnauthorizedAccessException)
        {
            return new(false, Strings.CommitMessageHookLabel, Strings.HookPermissionsReadDenied);
        }
        catch (PlatformNotSupportedException)
        {
            return new(false, Strings.CommitMessageHookLabel, Strings.HookPermissionsUnsupported);
        }
    }
}
