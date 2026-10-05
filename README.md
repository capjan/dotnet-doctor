<p align="center">
  <img src="https://raw.githubusercontent.com/capjan/dotnet-doctor/main/assets/dotnet-doctor-hero.jpg" alt="The purple .NET mascot scanning a software diagnostics dashboard" width="100%">
</p>

<h1 align="center">dotnet-doctor</h1>

<p align="center">
  Give your .NET workflow's Git setup a check-up.<br>
  Diagnose Git, hooks, and commit-message validation — then repair common setup issues.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/cap.dotnetdoctor"><img src="https://img.shields.io/nuget/v/cap.dotnetdoctor?logo=nuget&color=512BD4" alt="NuGet"></a>
  <a href="https://github.com/capjan/dotnet-doctor/actions/workflows/ci.yml"><img src="https://github.com/capjan/dotnet-doctor/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet" alt=".NET 10">
</p>

## Get started

Install the global tool and run it from the repository you want to check:

```sh
dotnet tool install --global cap.dotnetdoctor
dotnet doctor
```

The global tool requires the .NET 10 runtime.

`dotnet doctor` runs every diagnostic in order: Git, Git hooks, then the `commit-msg` hook. To set up supported hook configuration, run:

```sh
dotnet doctor fix
```

> [!TIP]
> Use `dotnet doctor diagnose git-hooks` or `dotnet doctor diagnose commit-msg` to focus on a single part of your setup.

## What it checks

| Check | What it verifies |
| --- | --- |
| Git | Git 2.9 or later is installed and available on `PATH`. |
| Git hooks | `core.hooksPath` points to `.githooks`, and hook files are executable where the platform supports that check. |
| `commit-msg` | The configured hook accepts a valid commit message and rejects an invalid one. |

## Commands

| Command | Description |
| --- | --- |
| `dotnet doctor` | Run all diagnostics. |
| `dotnet doctor diagnose all` | Run all diagnostics. |
| `dotnet doctor diagnose git` | Check the Git installation. |
| `dotnet doctor diagnose git-hooks` | Check the repository's Git hook configuration. |
| `dotnet doctor diagnose commit-msg` | Probe the `commit-msg` hook's behavior. |
| `dotnet doctor fix` | Repair supported Git hook configuration, then run all diagnostics again. |
| `dotnet doctor --version` | Print the installed tool version. |
| `dotnet doctor --help` | Show available options. |

> [!NOTE]
> Run `dotnet doctor` and `dotnet doctor fix` inside the repository you want to inspect. Repairs use repository-local Git configuration.

<details>
<summary>What <code>dotnet doctor fix</code> changes</summary>

The repair command creates `.githooks` if needed, points the repository-local `core.hooksPath` there when no hooks path is configured, installs a basic Conventional Commits `commit-msg` validator if one is missing, and adds the user-execute bit to hook files on Unix-like systems.

It preserves an existing custom hooks path and an existing `commit-msg` hook. It does not install or upgrade Git. If an existing hook fails the behavior check, it still needs manual repair.

</details>

<details>
<summary>Commit-message validation</summary>

The built-in Conventional Commit validator expects a lowercase type, such as `feat`, `fix`, or `build-ci`. Custom types are supported. Lowercase types keep common release-significant commits compatible with git-cliff's default parsing, without requiring git-cliff or a `cliff.toml` file in the repository being checked.

</details>

<details>
<summary>Platform and language notes</summary>

- On Windows, POSIX execute-bit checks cannot be verified, so the tool reports that limitation.
- The hook behavior probe uses `git hook run` when available (Git 2.43 or later). On older Git versions, it runs the hook directly on Unix-like systems.
- Diagnostic and repair messages follow the current UI culture. English is the fallback language, and German translations are included. Command names and arguments stay in English.

</details>

## Development

From the repository root:

```sh
dotnet build --configuration Release
dotnet test --configuration Release
```

CLI integration tests require Git on `PATH`. They create temporary repositories with isolated Git configuration and remove them after each test.

<details>
<summary>CI and releases</summary>

GitHub Actions runs on pushes, pull requests, and manual dispatches. The CI matrix covers Linux x64 (Ubuntu 24.04), Windows x64 (Windows Server 2025), and macOS ARM64 (macOS 15). Each job builds with warnings treated as errors, runs the full test suite, installs and checks the generated tool package, and publishes and checks a Native AOT executable.

Test reports and NuGet packages are available as workflow artifacts for 14 days. Test reports are uploaded even when tests fail. `global.json` keeps local builds and CI on stable .NET 10 SDKs.

After the CI matrix succeeds, a push to `main` that changes files under `src/DotnetDoctor/` may publish a NuGet package. git-cliff calculates the SemVer version from Conventional Commits, writes `CHANGELOG.md`, and creates the matching `v` tag. Commits that do not require a version bump do not publish a package.

To enable publishing, configure a NuGet.org Trusted Publishing policy for repository owner `capjan`, repository `dotnet-doctor`, workflow file `ci.yml`, and package `cap.dotnetdoctor`. Add the NuGet.org profile name of the account that created the policy (not its email address) as the GitHub Actions repository secret `NUGET_USER`.

</details>

<details>
<summary>Project structure and contribution notes</summary>

| Location | Responsibility |
| --- | --- |
| `src/DotnetDoctor/Program.cs` | Application entry point |
| `src/DotnetDoctor/Cli/` | Commands and options |
| `src/DotnetDoctor/DoctorApplication.cs` | Diagnostic order and repair workflow |
| `src/DotnetDoctor/Diagnostics/` | Checks that return results |
| `src/DotnetDoctor/Repairs/` | Repository changes that return results |
| `src/DotnetDoctor/Git/` | Shared Git operations and hook rules |
| `src/DotnetDoctor/Infrastructure/` | Process execution and timeouts |
| `src/DotnetDoctor/Reporting/` | Result model, console formatting, and exit codes |
| `src/DotnetDoctor/Localization/` | Formatting translated messages |
| `src/DotnetDoctor/Strings*.resx` | English and German text |
| `tests/DotnetDoctor.Tests/` | CLI regression, parsing, and translation tests |

To add a diagnostic, implement the check in `Diagnostics`, register its command in `DoctorCommands`, and add it to `DoctorApplication.CheckAll` in the required order. Checks return `DoctorResult` values; console output belongs in `DoctorReport`. Add regression tests for success and failure cases.

Add new text keys to both resource files and access them through `Strings`. The build generates the typed resource accessors under `obj`; do not edit those generated files. Translation tests check that German resources contain the same keys and format placeholders as the English resources.

Run `dotnet run --project src/DotnetDoctor -- fix` once in a clone to configure its repository-local Conventional Commit hook. The hook accepts lowercase Conventional Commit types and blocks messages that do not match the format. C# source and repository hook files use LF line endings so the embedded shell validator also works after a Windows checkout.

</details>

<details>
<summary>Shell completion</summary>

`System.CommandLine` supports shell completion through the `dotnet-suggest` global tool. Follow the [official setup instructions](https://learn.microsoft.com/dotnet/standard/commandline/how-to-enable-tab-completion) for your shell, then register the installed executable:

```sh
dotnet tool install --global dotnet-suggest
dotnet-suggest register --command-path "$HOME/.dotnet/tools/dotnet-doctor"
```

On Windows, use the path to `dotnet-doctor.exe` in `%USERPROFILE%\.dotnet\tools`.

</details>

<details>
<summary>Build the NuGet package or a Native AOT executable</summary>

Build the NuGet package:

```sh
dotnet pack src/DotnetDoctor/DotnetDoctor.csproj --configuration Release
```

The project enables AOT and trimming compatibility analysis. To publish a native executable for a specific runtime, for example macOS on Apple Silicon:

```sh
dotnet publish src/DotnetDoctor/DotnetDoctor.csproj --configuration Release --runtime osx-arm64 -p:PublishAot=true
```

</details>
