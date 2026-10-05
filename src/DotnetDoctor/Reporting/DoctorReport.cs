namespace DotnetDoctor.Reporting;

internal static class DoctorReport
{
    public static int Write(string heading, IEnumerable<DoctorResult> results)
    {
        Console.WriteLine(heading);
        return Write(results);
    }

    public static int Write(IEnumerable<DoctorResult> results)
    {
        var exitCode = 0;
        foreach (var result in results)
        {
            exitCode |= WriteRow(result);
        }

        return exitCode;
    }

    private static int WriteRow(DoctorResult result)
    {
        var marker = result.Success ? "[✓]" : "[✗]";
        if (!Console.IsOutputRedirected)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = result.Success ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write(marker);
            Console.ForegroundColor = originalColor;
        }
        else
        {
            Console.Write(marker);
        }

        Console.WriteLine($" {result.Name.PadRight(22)}: {result.Details}");
        return result.Success ? 0 : 1;
    }
}
