using DotnetDoctor.Diagnostics;
using DotnetDoctor.Reporting;
using DotnetDoctor.Repairs;

namespace DotnetDoctor;

internal static class DoctorApplication
{
    public static int DiagnoseGit() =>
        DoctorReport.Write(Strings.DoctorSummary, [GitVersionDiagnostic.Check()]);

    public static int DiagnoseGitHooks() =>
        DoctorReport.Write(Strings.DoctorSummary, GitHooksDiagnostic.Check());

    public static int DiagnoseCommitMessageHook() =>
        DoctorReport.Write(Strings.DoctorSummary, [CommitMessageHookDiagnostic.Check()]);

    public static int DiagnoseAll() => DoctorReport.Write(Strings.DoctorSummary, CheckAll());

    public static int Fix()
    {
        Console.WriteLine(Strings.RepairSummary);
        DoctorReport.Write(GitHooksRepair.Run());
        Console.WriteLine();
        return DiagnoseAll();
    }

    private static IEnumerable<DoctorResult> CheckAll()
    {
        yield return GitVersionDiagnostic.Check();
        foreach (var result in GitHooksDiagnostic.Check())
        {
            yield return result;
        }

        yield return CommitMessageHookDiagnostic.Check();
    }
}
