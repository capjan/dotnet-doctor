using System.Globalization;

namespace DotnetDoctor.Localization;

internal static class Messages
{
    public static string Format(string message, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentUICulture, message, arguments);
}
