# dotnet-doctor

A .NET global tool built with `System.CommandLine` for diagnosing .NET development environments.

## Install

```sh
dotnet tool install --global cap.dotnetdoctor
```

## Use

```sh
dotnet doctor
dotnet doctor --version
dotnet doctor diagnose git
dotnet doctor diagnose git-hooks
dotnet doctor diagnose commit-msg
dotnet doctor diagnose all
dotnet doctor fix
```

Running `dotnet doctor` or `dotnet doctor diagnose all` runs all diagnostics in order: Git, Git hooks, then `commit-msg`. Use `dotnet doctor --version` to print the installed tool version. `dotnet doctor diagnose git` checks that Git 2.9 or later is installed and available on `PATH`. `dotnet doctor diagnose git-hooks` checks that the repository-local `core.hooksPath` points to `.githooks` and that all non-`.sample` files in that directory are executable. `dotnet doctor diagnose commit-msg` probes the hook with temporary valid and invalid messages; this executes the configured hook. Windows does not expose POSIX execute bits, so the tool reports that permission check as unverifiable there. The behavior probe uses `git hook run` when available (Git 2.43 or later); on older Git versions it runs the hook directly on Unix-like systems.

Diagnostic and repair messages follow the current UI culture. English is the fallback language, and German translations are included. Command names and arguments remain in English.

The built-in Conventional Commit validator expects a lowercase type, such as `feat`, `fix` or `build-ci`. Custom types remain supported. Lowercase types keep common release-significant commits compatible with git-cliff's default parsing without requiring git-cliff or a `cliff.toml` file.

Use `dotnet doctor fix` to repair supported Git-hook diagnostics. It creates `.githooks`, sets the repository-local `core.hooksPath` to `.githooks` when it is not configured, installs a basic Conventional Commits `commit-msg` validator if one is missing, and adds the user-execute bit to hook files on Unix-like systems. It preserves an existing custom hooks path and an existing `commit-msg` hook. It does not install or upgrade Git, and an existing hook that fails the behavior check still needs manual repair. The command runs all diagnostics again after attempting repairs.

Use `dotnet doctor --help` to see the available options.

## Development

The solution includes the CLI and its tests. From the repository root:

```sh
dotnet build --configuration Release
dotnet test --configuration Release
```

CLI integration tests require Git on `PATH`. They create temporary repositories with isolated Git configuration and remove them after each test.

Run `dotnet run --project src/DotnetDoctor -- fix` once in a clone to configure its repository-local Conventional Commit hook. The hook accepts lowercase Conventional Commit types and blocks commit messages that do not match the format.

The test executable invokes the CLI entry point after explicitly setting its UI culture. This keeps English, German and language-fallback tests independent of the host OS language, including on Windows.

### Continuous integration

GitHub Actions runs on pushes, pull requests and manual dispatches. The CI matrix covers Linux x64 (Ubuntu 24.04), Windows x64 (Windows Server 2025) and macOS ARM64 (macOS 15). Each job builds with warnings treated as errors, runs the full test suite, installs and checks the generated tool package, and publishes and checks a Native AOT executable.

POSIX permission checks run on Linux and macOS. Windows tests verify the reported permission limitation. Test reports and NuGet packages are available as workflow artifacts for 14 days; test reports are uploaded even when tests fail. `global.json` keeps local builds and CI on stable .NET 10 SDKs.

C# source and repository hook files use LF line endings so the embedded shell validator also works after a Windows checkout.

After the CI matrix succeeds, a push to `main` that changes files under `src/DotnetDoctor/` publishes a NuGet package. git-cliff calculates the SemVer version from Conventional Commits, writes `CHANGELOG.md`, and creates the matching `v` tag. Configure a NuGet.org Trusted Publishing policy for repository owner `capjan`, repository `dotnet-doctor`, workflow file `ci.yml`, and package `cap.dotnetdoctor`; add the NuGet.org profile name of the account that created the policy (not its email address) as the GitHub Actions repository variable `NUGET_USER`. Commits that do not require a version bump do not publish a package.

### Project structure

| Location | Responsibility |
| --- | --- |
| `src/DotnetDoctor/Program.cs` | Application entry point |
| `src/DotnetDoctor/Cli/` | Commands and options |
| `src/DotnetDoctor/DoctorApplication.cs` | Diagnostic order and repair workflow |
| `src/DotnetDoctor/Diagnostics/` | Checks that return results |
| `src/DotnetDoctor/Repairs/` | Repository changes that return results |
| `src/DotnetDoctor/Git/` | Shared Git operations and hook rules |
| `src/DotnetDoctor/Infrastructure/` | Process execution and timeouts |
| `src/DotnetDoctor/Reporting/` | Result model, console formatting and exit codes |
| `src/DotnetDoctor/Localization/` | Formatting translated messages |
| `src/DotnetDoctor/Strings*.resx` | English and German text |
| `tests/DotnetDoctor.Tests/` | CLI regression, parsing and translation tests |

To add a diagnostic, implement the check in `Diagnostics`, register its command in `DoctorCommands`, and add it to `DoctorApplication.CheckAll` in the required order. Checks return `DoctorResult` values; console output belongs in `DoctorReport`. Add regression tests for success and failure cases.

Add new text keys to both resource files and access them through `Strings`. The build generates the typed resource accessors under `obj`; do not edit those generated files. Translation tests check that German resources contain the same keys and format placeholders as the English resources.

## Shell completion

`System.CommandLine` supports shell completion through the `dotnet-suggest` global tool. Follow the [official setup instructions](https://learn.microsoft.com/dotnet/standard/commandline/how-to-enable-tab-completion) for your shell, then register the installed `dotnet-doctor` executable:

```sh
dotnet tool install --global dotnet-suggest
dotnet-suggest register --command-path "$HOME/.dotnet/tools/dotnet-doctor"
```

On Windows, use the path to `dotnet-doctor.exe` in `%USERPROFILE%\.dotnet\tools`.

## Build the NuGet package

```sh
dotnet pack src/DotnetDoctor/DotnetDoctor.csproj --configuration Release
```

## Native AOT

The project enables AOT and trimming compatibility analysis. To publish a native executable for a specific runtime, for example macOS on Apple Silicon:

```sh
dotnet publish src/DotnetDoctor/DotnetDoctor.csproj --configuration Release --runtime osx-arm64 -p:PublishAot=true
```
