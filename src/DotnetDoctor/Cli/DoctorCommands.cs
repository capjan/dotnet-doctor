using System.CommandLine;
using System.CommandLine.Help;
using System.Reflection;

namespace DotnetDoctor.Cli;

internal static class DoctorCommands
{
    public static RootCommand Create()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        var rootCommand = new RootCommand(Strings.RootDescription);

        rootCommand.SetAction(_ => DoctorApplication.DiagnoseAll());

        rootCommand.Options.OfType<HelpOption>().Single().Description = Strings.HelpOptionDescription;
        var versionOption = rootCommand.Options.OfType<VersionOption>().Single();
        versionOption.Aliases.Add("-v");
        versionOption.Action = new PrintVersionAction(version);

        var diagnoseCommand = new Command("diagnose", Strings.DiagnoseCommandDescription);
        var gitCommand = new Command("git", Strings.GitCommandDescription);
        gitCommand.SetAction(_ => DoctorApplication.DiagnoseGit());
        var gitHooksCommand = new Command("git-hooks", Strings.GitHooksCommandDescription);
        gitHooksCommand.SetAction(_ => DoctorApplication.DiagnoseGitHooks());
        var commitMsgCommand = new Command("commit-msg", Strings.CommitMsgCommandDescription);
        commitMsgCommand.SetAction(_ => DoctorApplication.DiagnoseCommitMessageHook());
        var allCommand = new Command("all", Strings.AllCommandDescription);
        allCommand.SetAction(_ => DoctorApplication.DiagnoseAll());
        var fixCommand = new Command("fix", Strings.FixCommandDescription);
        fixCommand.SetAction(_ => DoctorApplication.Fix());
        diagnoseCommand.Subcommands.Add(gitCommand);
        diagnoseCommand.Subcommands.Add(gitHooksCommand);
        diagnoseCommand.Subcommands.Add(commitMsgCommand);
        diagnoseCommand.Subcommands.Add(allCommand);
        rootCommand.Subcommands.Add(diagnoseCommand);
        rootCommand.Subcommands.Add(fixCommand);

        return rootCommand;
    }

    private sealed class PrintVersionAction : System.CommandLine.Invocation.SynchronousCommandLineAction
    {
        private readonly string version;

        public PrintVersionAction(string version)
        {
            this.version = version;
        }

        public override int Invoke(ParseResult parseResult)
        {
            Console.WriteLine($"dotnet-doctor {version}");
            return 0;
        }
    }
}
