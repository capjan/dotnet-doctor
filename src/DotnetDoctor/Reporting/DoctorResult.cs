namespace DotnetDoctor.Reporting;

internal readonly record struct DoctorResult(bool Success, string Name, string Details);
