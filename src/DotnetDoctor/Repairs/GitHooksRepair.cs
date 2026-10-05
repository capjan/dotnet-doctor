using DotnetDoctor.Git;
using DotnetDoctor.Localization;
using DotnetDoctor.Reporting;

namespace DotnetDoctor.Repairs;

internal static class GitHooksRepair
{
    public static IReadOnlyList<DoctorResult> Run()
    {
        var results = new List<DoctorResult>();

        var repositoryRoot = GitRepository.GetRoot(out var error);
        if (repositoryRoot is null)
        {
            results.Add(new(false, Strings.GitHooksLabel, error));
            return results;
        }

        var hooksDirectory = GitRepository.GetHooksDirectoryPath(repositoryRoot);
        var localHooksPathResult = GitClient.Run("config", "--local", "--get", GitRepository.HooksPathConfigKey);
        if (localHooksPathResult is null)
        {
            results.Add(new(false, Strings.GitHooksLabel, Strings.GitCouldNotStart));
            return results;
        }

        if (localHooksPathResult.Value.ExitCode == 0 &&
            !GitRepository.HooksPathMatches(localHooksPathResult.Value.Output, repositoryRoot, hooksDirectory))
        {
            results.Add(new(false, Strings.GitHooksLabel, Strings.LocalHooksPathElsewhere));
            return results;
        }

        if (localHooksPathResult.Value.ExitCode != 0 && !string.IsNullOrWhiteSpace(localHooksPathResult.Value.Error))
        {
            results.Add(new(false, Strings.GitHooksLabel, Messages.Format(Strings.LocalHooksPathReadFailed, localHooksPathResult.Value.Error.Trim())));
            return results;
        }

        if (localHooksPathResult.Value.ExitCode != 0)
        {
            var inheritedHooksPathResult = GitClient.Run("config", "--get", GitRepository.HooksPathConfigKey);
            if (inheritedHooksPathResult is null)
            {
                results.Add(new(false, Strings.GitHooksLabel, Strings.EffectiveHooksPathCouldNotRead));
                return results;
            }

            if (inheritedHooksPathResult.Value.ExitCode == 0 &&
                !GitRepository.HooksPathMatches(inheritedHooksPathResult.Value.Output, repositoryRoot, hooksDirectory))
            {
                results.Add(new(false, Strings.GitHooksLabel, Strings.EffectiveHooksPathElsewhere));
                return results;
            }

            if (inheritedHooksPathResult.Value.ExitCode != 0 && !string.IsNullOrWhiteSpace(inheritedHooksPathResult.Value.Error))
            {
                results.Add(new(false, Strings.GitHooksLabel, Messages.Format(Strings.EffectiveHooksPathReadFailed, inheritedHooksPathResult.Value.Error.Trim())));
                return results;
            }
        }

        try
        {
            Directory.CreateDirectory(hooksDirectory);
            results.Add(new(true, Strings.GitHooksLabel, Strings.HooksDirectoryEnsured));
        }
        catch (IOException)
        {
            results.Add(new(false, Strings.GitHooksLabel, Strings.HooksDirectoryCreateFailed));
            return results;
        }
        catch (UnauthorizedAccessException)
        {
            results.Add(new(false, Strings.GitHooksLabel, Strings.HooksDirectoryCreateDenied));
            return results;
        }

        if (localHooksPathResult.Value.ExitCode != 0)
        {
            var configResult = GitClient.RunInDirectory(repositoryRoot, "config", "--local", GitRepository.HooksPathConfigKey, GitRepository.HooksDirectoryName);
            if (configResult is null || configResult.Value.ExitCode != 0)
            {
                results.Add(new(false, Strings.GitHooksLabel, Strings.LocalHooksPathSetFailed));
                return results;
            }

            results.Add(new(true, Strings.GitHooksLabel, Strings.LocalHooksPathSet));
        }

        var effectiveHooksPathResult = GitClient.RunInDirectory(repositoryRoot, "config", "--get", GitRepository.HooksPathConfigKey);
        if (effectiveHooksPathResult is null || effectiveHooksPathResult.Value.ExitCode != 0 ||
            !GitRepository.HooksPathMatches(effectiveHooksPathResult.Value.Output, repositoryRoot, hooksDirectory))
        {
            results.Add(new(false, Strings.GitHooksLabel, Strings.EffectiveHooksPathMismatch));
            return results;
        }

        results.Add(InstallCommitMessageHook(hooksDirectory));
        AddExecutablePermissions(hooksDirectory, results);
        return results;
    }

    private static DoctorResult InstallCommitMessageHook(string hooksDirectory)
    {
        var commitMsgPath = Path.Combine(hooksDirectory, ConventionalCommit.HookName);
        if (File.Exists(commitMsgPath))
        {
            return new(true, Strings.CommitMessageHookLabel, Strings.ExistingCommitHookKept);
        }

        try
        {
            using var stream = new FileStream(commitMsgPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(ConventionalCommit.HookScript);
            return new(true, Strings.CommitMessageHookLabel, Strings.DefaultCommitHookInstalled);
        }
        catch (IOException) when (File.Exists(commitMsgPath))
        {
            return new(true, Strings.CommitMessageHookLabel, Strings.ExistingCommitHookKept);
        }
        catch (IOException)
        {
            return new(false, Strings.CommitMessageHookLabel, Strings.DefaultCommitHookInstallFailed);
        }
        catch (UnauthorizedAccessException)
        {
            return new(false, Strings.CommitMessageHookLabel, Strings.DefaultCommitHookInstallDenied);
        }
    }

    private static void AddExecutablePermissions(string hooksDirectory, ICollection<DoctorResult> results)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var hookScripts = GitRepository.EnumerateHookScripts(hooksDirectory);
            foreach (var script in hookScripts)
            {
                try
                {
                    var mode = File.GetUnixFileMode(script);
                    File.SetUnixFileMode(script, mode | UnixFileMode.UserExecute);
                }
                catch (IOException)
                {
                    results.Add(new(false, Strings.HookPermissionsLabel, Messages.Format(Strings.HookPermissionSetFailed, Path.GetFileName(script))));
                }
                catch (UnauthorizedAccessException)
                {
                    results.Add(new(false, Strings.HookPermissionsLabel, Messages.Format(Strings.HookPermissionSetDenied, Path.GetFileName(script))));
                }
                catch (PlatformNotSupportedException)
                {
                    results.Add(new(false, Strings.HookPermissionsLabel, Strings.HookPermissionUpdateUnsupported));
                    break;
                }
            }
        }
        catch (IOException)
        {
            results.Add(new(false, Strings.HookPermissionsLabel, Strings.HookFilesReadFailed));
        }
        catch (UnauthorizedAccessException)
        {
            results.Add(new(false, Strings.HookPermissionsLabel, Strings.HookFilesReadDenied));
        }
    }
}
