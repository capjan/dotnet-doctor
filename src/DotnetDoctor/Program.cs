using System.Reflection;

if (args.Length == 0 || args is ["--version"] || args is ["-v"])
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
    Console.WriteLine($"dotnet-doctor {version}");
    return 0;
}

Console.Error.WriteLine("Usage: dotnet doctor [--version]");
return 2;
