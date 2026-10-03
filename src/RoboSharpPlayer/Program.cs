using Novolis.Language.RoboSharp;

namespace Novolis.RoboSharpPlayer;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var source = args.Length == 0
            ? "move 2\nturn right\nprint \"hello\""
            : await File.ReadAllTextAsync(Path.GetFullPath(args[0]));
        var compilation = new RoboSharpCompiler().Compile(source);
        if (!compilation.Succeeded)
        {
            foreach (var diagnostic in compilation.Diagnostics)
            {
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            }

            return 1;
        }

        var session = new RoboSharpExecutionSession(compilation.Program!);
        while (!session.Snapshot.IsCompleted)
        {
            var step = session.Step();
            Console.WriteLine($"{step.Kind}: {step.Description}");
        }

        Console.WriteLine($"x={session.Snapshot.Variables["x"]}, y={session.Snapshot.Variables["y"]}");
        foreach (var line in session.Snapshot.Output)
        {
            Console.WriteLine(line);
        }

        return 0;
    }
}
