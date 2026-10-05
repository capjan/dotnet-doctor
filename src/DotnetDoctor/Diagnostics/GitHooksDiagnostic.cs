using DotnetDoctor.Git;
using DotnetDoctor.Localization;
using DotnetDoctor.Reporting;

namespace DotnetDoctor.Diagnostics;

internal static class GitHooksDiagnostic
{
    public static IReadOnlyList<DoctorResult> Check()
    {
        var hooksDirectory = GitRepository.GetLocalHooksDirectory(out _, out var error);
        if (hooksDirectory is null)
        {
            return [new(false, Strings.GitHooksLabel, error)];
        }

        return
        [
            new(true, Strings.GitHooksLabel, Strings.GitHooksConfigured),
            CheckPermissions(hooksDirectory)
        ];
    }

    private static DoctorResult CheckPermissions(string hooksDirectory)
    {
        string[] hookScripts;
        try
        {
            hookScripts = GitRepository.EnumerateHookScripts(hooksDirectory)
                .ToArray();
        }
        catch (IOException)
        {
            return new(false, Strings.HookPermissionsLabel, Strings.HookFilesReadFailed);
        }
        catch (UnauthorizedAccessException)
        {
            return new(false, Strings.HookPermissionsLabel, Strings.HookFilesReadDenied);
        }

        if (hookScripts.Length == 0)
        {
            return new(false, Strings.HookPermissionsLabel, Strings.HookFilesMissing);
        }

        if (OperatingSystem.IsWindows())
        {
            return new(false, Strings.HookPermissionsLabel, Strings.HookPermissionsWindows);
        }

        var nonExecutableScripts = new List<string>();
        try
        {
            foreach (var script in hookScripts)
            {
                var mode = File.GetUnixFileMode(script);
                var executableBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                if ((mode & executableBits) == 0)
                {
                    nonExecutableScripts.Add(Path.GetFileName(script));
                }
            }
        }
        catch (IOException)
        {
            return new(false, Strings.HookPermissionsLabel, Strings.HookPermissionsReadFailed);
        }
        catch (UnauthorizedAccessException)
        {
            return new(false, Strings.HookPermissionsLabel, Strings.HookPermissionsReadDenied);
        }
        catch (PlatformNotSupportedException)
        {
            return new(false, Strings.HookPermissionsLabel, Strings.HookPermissionsUnsupported);
        }

        if (nonExecutableScripts.Count > 0)
        {
            return new(false, Strings.HookPermissionsLabel, Messages.Format(Strings.HookFilesNotExecutable, string.Join(", ", nonExecutableScripts)));
        }

        var scriptSummary = hookScripts.Length == 1
            ? Strings.OneHookExecutable
            : Messages.Format(Strings.ManyHooksExecutable, hookScripts.Length);
        return new(true, Strings.HookPermissionsLabel, scriptSummary);
    }
}
