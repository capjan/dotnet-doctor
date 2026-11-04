using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotnetDoctor.Git;
using DotnetDoctor.Infrastructure;
using DotnetDoctor.Localization;
using DotnetDoctor.Reporting;

namespace DotnetDoctor.Diagnostics;

internal static partial class GlobalJsonDiagnostic
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "bin",
        "obj"
    };

    public static IEnumerable<DoctorResult> Check()
    {
        var globalJsonPath = FindGlobalJson(Environment.CurrentDirectory);
        if (globalJsonPath is null)
        {
            return [new(true, Strings.GlobalJsonLabel, Strings.GlobalJsonNotFound)];
        }

        string? configuredSdkVersion;
        try
        {
            configuredSdkVersion = ReadConfiguredSdkVersion(globalJsonPath);
        }
        catch (JsonException)
        {
            return [new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonInvalid, globalJsonPath))];
        }
        catch (IOException)
        {
            return [new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonReadFailed, globalJsonPath))];
        }
        catch (UnauthorizedAccessException)
        {
            return [new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonReadFailed, globalJsonPath))];
        }

        var sdkResult = RunDotnetVersion();
        if (sdkResult is null)
        {
            return [new(false, Strings.GlobalJsonLabel, Strings.GlobalJsonCliNotFound)];
        }

        if (sdkResult.Value.ExitCode != 0 || !TryParseMajorMinor(sdkResult.Value.Output, out var resolvedSdkVersion))
        {
            return [new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonSdkResolutionFailed, configuredSdkVersion ?? Strings.GlobalJsonLatestSdk, globalJsonPath))];
        }

        var repositoryRoot = GitRepository.GetRoot(out _) ?? Environment.CurrentDirectory;
        string[] projectFiles;
        try
        {
            projectFiles = Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(repositoryRoot, path)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(IgnoredDirectories.Contains))
                .ToArray();
        }
        catch (IOException)
        {
            return [new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonProjectsReadFailed, repositoryRoot))];
        }
        catch (UnauthorizedAccessException)
        {
            return [new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonProjectsReadFailed, repositoryRoot))];
        }

        if (projectFiles.Length == 0)
        {
            return [new(true, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonNoProjects, globalJsonPath, resolvedSdkVersion))];
        }

        return projectFiles.SelectMany(path => CheckProject(path, globalJsonPath, resolvedSdkVersion));
    }

    private static IEnumerable<DoctorResult> CheckProject(string projectPath, string globalJsonPath, Version resolvedSdkVersion)
    {
        List<string> frameworks;
        try
        {
            frameworks = ReadTargetFrameworks(projectPath);
        }
        catch (IOException)
        {
            return [new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonProjectReadFailed, projectPath))];
        }
        catch (UnauthorizedAccessException)
        {
            return [new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonProjectReadFailed, projectPath))];
        }
        catch (System.Xml.XmlException)
        {
            return [new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonProjectReadFailed, projectPath))];
        }

        if (frameworks.Count == 0)
        {
            return [new(true, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonFrameworksUnknown, projectPath, globalJsonPath, resolvedSdkVersion))];
        }

        var checkedFrameworks = frameworks.Where(framework => TryGetRequiredSdkVersion(framework, out _)).ToArray();
        var unverifiedFrameworks = frameworks.Except(checkedFrameworks, StringComparer.OrdinalIgnoreCase).ToArray();
        var incompatibleFrameworks = checkedFrameworks
            .Where(framework => TryGetRequiredSdkVersion(framework, out var requiredSdkVersion) && resolvedSdkVersion < requiredSdkVersion)
            .ToArray();
        if (incompatibleFrameworks.Length > 0)
        {
            var result = new List<DoctorResult>
            {
                new(false, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonSdkTooOld, resolvedSdkVersion, string.Join(", ", incompatibleFrameworks), projectPath, globalJsonPath))
            };
            if (unverifiedFrameworks.Length > 0)
            {
                result.Add(new(true, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonFrameworksUnverified, string.Join(", ", unverifiedFrameworks), projectPath)));
            }

            return result;
        }

        if (checkedFrameworks.Length == 0)
        {
            return [new(true, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonFrameworksUnverified, string.Join(", ", unverifiedFrameworks), projectPath))];
        }

        var results = new List<DoctorResult>
        {
            new(true, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonSdkCompatible, resolvedSdkVersion, string.Join(", ", checkedFrameworks), projectPath, globalJsonPath))
        };
        if (unverifiedFrameworks.Length > 0)
        {
            results.Add(new(true, Strings.GlobalJsonLabel, Messages.Format(Strings.GlobalJsonFrameworksUnverified, string.Join(", ", unverifiedFrameworks), projectPath)));
        }

        return results;
    }

    private static string? FindGlobalJson(string directory)
    {
        for (DirectoryInfo? current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "global.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ReadConfiguredSdkVersion(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        if (!document.RootElement.TryGetProperty("sdk", out var sdk))
        {
            return null;
        }

        if (sdk.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        if (!sdk.TryGetProperty("version", out var version))
        {
            return null;
        }

        if (version.ValueKind != JsonValueKind.String)
        {
            throw new JsonException();
        }

        return version.GetString();
    }

    private static ProcessResult? RunDotnetVersion()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--version");
        return ProcessRunner.Run(startInfo);
    }

    private static bool TryParseMajorMinor(string value, out Version version)
    {
        var numericVersion = value.Trim().Split('-', '+')[0];
        if (Version.TryParse(numericVersion, out var parsedVersion) && parsedVersion.Major >= 0 && parsedVersion.Minor >= 0)
        {
            version = parsedVersion;
            return true;
        }

        version = new Version(0, 0);
        return false;
    }

    private static bool TryGetRequiredSdkVersion(string framework, out Version version)
    {
        var match = TargetFrameworkVersionRegex().Match(framework.Trim());
        if (match.Success && int.TryParse(match.Groups[1].Value, out var major) && int.TryParse(match.Groups[2].Value, out var minor))
        {
            version = new Version(major, minor);
            return true;
        }

        version = new Version(0, 0);
        return false;
    }

    private static List<string> ReadTargetFrameworks(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("TargetFramework", StringComparison.OrdinalIgnoreCase) ||
                              element.Name.LocalName.Equals("TargetFrameworks", StringComparison.OrdinalIgnoreCase))
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [GeneratedRegex("^net(?:coreapp)?(\\d+)\\.(\\d+)(?:-|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TargetFrameworkVersionRegex();
}
