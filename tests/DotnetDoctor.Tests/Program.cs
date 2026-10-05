using System.Globalization;
using DotnetDoctor;

// LANG and LC_ALL do not select the .NET UI culture on Windows.
var culture = Environment.GetEnvironmentVariable("DOTNET_DOCTOR_TEST_CULTURE") ?? "en-US";
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

var entryPoint = typeof(DoctorApplication).Assembly.EntryPoint!;
return (int)entryPoint.Invoke(null, [args])!;
