using System.Text;
using DotnetDoctor.Cli;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
return DoctorCommands.Create().Parse(args).Invoke();
