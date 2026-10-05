using System.Diagnostics;

namespace DotnetDoctor.Tests;

internal sealed class TemporaryRepository : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"dotnet-doctor tests {Guid.NewGuid():N}");
    private readonly string globalConfigPath;

    public string Root { get; }

    public TemporaryRepository(bool initialize = true)
    {
        Root = Path.Combine(temporaryDirectory, "repository");
        globalConfigPath = Path.Combine(temporaryDirectory, "gitconfig");
        Directory.CreateDirectory(Root);
        File.WriteAllText(globalConfigPath, string.Empty);
        if (initialize)
        {
            var result = Git("init", "--quiet");
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.Error);
            }
        }
    }

    public CommandResult Git(params string[] arguments) => Run("git", Root, "en-US", arguments);

    public CommandResult Doctor(params string[] arguments) => DoctorIn(Root, "en-US", arguments);

    public CommandResult DoctorIn(string workingDirectory, string culture, params string[] arguments)
    {
        var application = Path.Combine(AppContext.BaseDirectory, "DotnetDoctor.Tests.dll");
        return Run("dotnet", workingDirectory, culture, [application, .. arguments]);
    }

    public string WriteHook(string script, bool executable = true)
    {
        var directory = Path.Combine(Root, ".githooks");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "commit-msg");
        File.WriteAllText(path, script);
        if (!OperatingSystem.IsWindows())
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            File.SetUnixFileMode(path, executable ? mode | UnixFileMode.UserExecute : mode);
        }

        Git("config", "--local", "core.hooksPath", ".githooks");
        return path;
    }

    public void Dispose() => Directory.Delete(temporaryDirectory, recursive: true);

    private CommandResult Run(string fileName, string workingDirectory, string culture, string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var name in new[] { "GIT_DIR", "GIT_COMMON_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_CONFIG_PARAMETERS" })
        {
            startInfo.Environment.Remove(name);
        }

        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = globalConfigPath;
        startInfo.Environment["GIT_CONFIG_COUNT"] = "0";
        startInfo.Environment["DOTNET_DOCTOR_TEST_CULTURE"] = culture;
        foreach (var name in new[] { "LANG", "LC_ALL", "LC_MESSAGES", "LC_CTYPE" })
        {
            startInfo.Environment[name] = $"{culture.Replace('-', '_')}.UTF-8";
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(45_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"{fileName} did not finish within 45 seconds.");
        }

        return new(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}

internal readonly record struct CommandResult(int ExitCode, string Output, string Error);
