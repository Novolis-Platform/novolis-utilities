using System.Diagnostics;
using System.Text;

namespace Novolis.Utilities.Manifest;

internal static class ProcessRunner
{
    internal static void Run(string fileName, string workingDirectory, IReadOnlyList<string> arguments)
    {
        var exitCode = RunCapture(fileName, workingDirectory, arguments, out _, out _);
        if (exitCode != 0)
            throw new InvalidOperationException($"{fileName} failed with exit code {exitCode}.");
    }

    private static int RunCapture(
        string fileName,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        out string stdout,
        out string stderr)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
            stdoutBuilder.AppendLine(e.Data);
            Console.Error.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
            stderrBuilder.AppendLine(e.Data);
            Console.Error.WriteLine(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        stdout = stdoutBuilder.ToString();
        stderr = stderrBuilder.ToString();
        return process.ExitCode;
    }
}
