using System.ComponentModel;
using System.Diagnostics;
using DotnetDoctor.Localization;

namespace DotnetDoctor.Infrastructure;

internal static class ProcessRunner
{
    public static ProcessResult? Run(ProcessStartInfo startInfo, int timeoutMilliseconds = 30_000)
    {
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return null;
            }
        }
        catch (Win32Exception)
        {
            return null;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }

            Task.WaitAll(outputTask, errorTask);
            return new(-1, outputTask.GetAwaiter().GetResult(), Messages.Format(Strings.ProcessTimedOut, timeoutMilliseconds / 1000));
        }

        return new(process.ExitCode, outputTask.GetAwaiter().GetResult(), errorTask.GetAwaiter().GetResult());
    }
}
