using DotnetDoctor.Git;
using DotnetDoctor.Localization;
using DotnetDoctor.Reporting;

namespace DotnetDoctor.Diagnostics;

internal static class GitVersionDiagnostic
{
    private static readonly Version MinimumVersion = new(2, 9);

    public static DoctorResult Check()
    {
        var result = GitClient.Run("--version");
        if (result is null)
        {
            return new(false, Strings.GitVersionLabel, Strings.GitNotFound);
        }

        if (result.Value.ExitCode != 0)
        {
            return new(false, Strings.GitVersionLabel, Strings.GitVersionReportFailed);
        }

        var version = GitClient.ParseVersion(result.Value.Output.Trim());
        if (version is null)
        {
            return new(false, Strings.GitVersionLabel, Strings.GitVersionUnreadable);
        }

        if (version < MinimumVersion)
        {
            return new(false, Strings.GitVersionLabel, Messages.Format(Strings.GitVersionTooOld, version));
        }

        return new(true, Strings.GitVersionLabel, Messages.Format(Strings.GitVersionInstalled, version));
    }
}
