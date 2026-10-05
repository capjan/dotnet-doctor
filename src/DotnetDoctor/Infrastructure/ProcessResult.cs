namespace DotnetDoctor.Infrastructure;

internal readonly record struct ProcessResult(int ExitCode, string Output, string Error);
