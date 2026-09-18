using System.Text.Json;
using System.Xml.Linq;

namespace Novolis.Utilities.Manifest;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static int Main(string[] args)
    {
        var command = args.FirstOrDefault()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(command))
        {
            PrintUsage();
            return 2;
        }

        var repo = GetOption(args, "--repo") ?? FindRepoRoot();
        var manifestPath = GetOption(args, "--manifest")
            ?? Path.Combine(repo, "build", "utilities.json");

        try
        {
            var manifest = Load(manifestPath);
            return command switch
            {
                "validate" => Validate(manifest, repo),
                "list" => List(manifest),
                "generate-solutions" => GenerateSolutions(manifest, repo),
                "ci-matrix" => EmitCiMatrix(manifest, GetChangedFiles(args)),
                "release-matrix" => EmitReleaseMatrix(manifest, GetOption(args, "--utility")),
                _ => Unknown(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static UtilityManifest Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Utilities manifest not found: {path}");

        return JsonSerializer.Deserialize<UtilityManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse utilities manifest.");
    }

    private static int Validate(UtilityManifest manifest, string repo)
    {
        var errors = new List<string>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var utility in manifest.Utilities)
        {
            if (!keys.Add(utility.Key))
                errors.Add($"Duplicate utility key: {utility.Key}");
            if (utility.Ship.Count == 0)
                errors.Add($"{utility.Key}: ship must declare windows-zip");
            if (utility.Ship.Any(channel => !string.Equals(channel, "windows-zip", StringComparison.OrdinalIgnoreCase)))
                errors.Add($"{utility.Key}: only windows-zip is allowed in phase one");
            if (utility.ChangedPathGlobs.Count == 0)
                errors.Add($"{utility.Key}: changedPathGlobs must not be empty");

            var projectPath = Resolve(repo, utility.Project);
            if (!File.Exists(projectPath))
            {
                errors.Add($"{utility.Key}: project does not exist: {utility.Project}");
                continue;
            }

            var xml = XDocument.Load(projectPath);
            var properties = xml.Descendants("PropertyGroup")
                .Elements()
                .ToDictionary(
                    element => element.Name.LocalName,
                    element => element.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase);

            if (!string.Equals(properties.GetValueOrDefault("OutputType"), "Exe", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{utility.Key}: host must set OutputType=Exe");
            if (!string.Equals(properties.GetValueOrDefault("IsPackable"), "false", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{utility.Key}: host must explicitly set IsPackable=false");
            if (string.Equals(properties.GetValueOrDefault("PackAsTool"), "true", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{utility.Key}: PackAsTool is forbidden");

            var projectText = File.ReadAllText(projectPath);
            if (projectText.Contains("Novolis.Dogfooding.", StringComparison.OrdinalIgnoreCase)
                || projectText.Contains("Novolis.Lab.Shared", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{utility.Key}: lab/shared project references must be removed before graduation");
            }

            if (!string.IsNullOrWhiteSpace(utility.Solution)
                && !File.Exists(Resolve(repo, utility.Solution)))
            {
                errors.Add($"{utility.Key}: solution does not exist: {utility.Solution}");
            }

            foreach (var test in utility.Tests)
            {
                if (!File.Exists(Resolve(repo, test)))
                    errors.Add($"{utility.Key}: test project does not exist: {test}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Console.Error.WriteLine(error);
            return 1;
        }

        Console.WriteLine($"utilities manifest valid ({manifest.Utilities.Count} utilities)");
        return 0;
    }

    private static int List(UtilityManifest manifest)
    {
        foreach (var utility in manifest.Utilities.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"{utility.Key}\t{utility.Project}\t{string.Join(',', utility.Ship)}");
        return 0;
    }

    private static int GenerateSolutions(UtilityManifest manifest, string repo)
    {
        foreach (var utility in manifest.Utilities)
        {
            var solution = utility.Solution;
            if (string.IsNullOrWhiteSpace(solution))
            {
                solution = $"src/{utility.Key}/{utility.Key}.slnx";
                utility.Solution = solution;
            }

            var solutionPath = Resolve(repo, solution);
            Directory.CreateDirectory(Path.GetDirectoryName(solutionPath)!);
            var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
            var lines = new List<string>
            {
                "<Solution>",
                "  <Folder Name=\"/src/\">",
                $"    <Project Path=\"{RelativeProject(solutionDirectory, Resolve(repo, utility.Project))}\" />",
                "  </Folder>",
            };

            if (utility.Tests.Count > 0)
            {
                lines.Add("  <Folder Name=\"/tests/\">");
                foreach (var test in utility.Tests)
                    lines.Add($"    <Project Path=\"{RelativeProject(solutionDirectory, Resolve(repo, test))}\" />");
                lines.Add("  </Folder>");
            }

            lines.Add("</Solution>");
            File.WriteAllText(solutionPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            Console.WriteLine(solutionPath);
        }

        return 0;
    }

    private static int EmitCiMatrix(UtilityManifest manifest, IReadOnlyList<string> changedFiles)
    {
        var selected = changedFiles.Count == 0
            ? manifest.Utilities
            : manifest.Utilities
                .Where(utility => utility.ChangedPathGlobs.Any(glob =>
                    changedFiles.Any(file => Matches(file, glob))))
                .ToList();

        var selectedList = selected.ToList();
        var matrix = new
        {
            skip_build = selectedList.Count == 0,
            include = selectedList.Select(utility => new
            {
                key = utility.Key,
                project = Normalize(utility.Project),
                solution = Normalize(utility.Solution),
                tests = utility.Tests.Select(Normalize).ToArray(),
            }).ToArray(),
        };

        Console.WriteLine(JsonSerializer.Serialize(matrix));
        return 0;
    }

    private static int EmitReleaseMatrix(UtilityManifest manifest, string? requestedUtility)
    {
        if (string.IsNullOrWhiteSpace(requestedUtility))
            throw new InvalidOperationException("release-matrix requires --utility KEY or --utility All");

        var selected = string.Equals(requestedUtility, "All", StringComparison.OrdinalIgnoreCase)
            ? manifest.Utilities
            : manifest.Utilities.Where(utility =>
                string.Equals(utility.Key, requestedUtility, StringComparison.OrdinalIgnoreCase)).ToList();

        var selectedList = selected.ToList();
        if (selectedList.Count == 0)
            throw new InvalidOperationException($"Unknown utility: {requestedUtility}");

        var matrix = new
        {
            include = selectedList.Select(utility => new
            {
                key = utility.Key,
                project = Normalize(utility.Project),
                ship = utility.Ship.Select(channel => channel.ToLowerInvariant()).ToArray(),
            }).ToArray(),
        };

        Console.WriteLine(JsonSerializer.Serialize(matrix));
        return 0;
    }

    private static IReadOnlyList<string> GetChangedFiles(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--all", StringComparison.OrdinalIgnoreCase)))
            return [];

        var files = GetRepeatedOption(args, "--changed-file").ToList();
        var changedFileList = GetOption(args, "--changed-files");
        if (!string.IsNullOrWhiteSpace(changedFileList) && File.Exists(changedFileList))
            files.AddRange(File.ReadLines(changedFileList));
        return files;
    }

    private static bool Matches(string file, string glob)
    {
        var normalizedGlob = Normalize(glob)
            .Replace(".", "\\.")
            .Replace("**", "\u0000")
            .Replace("*", "[^/]*")
            .Replace("\u0000", ".*");
        return System.Text.RegularExpressions.Regex.IsMatch(
            Normalize(file),
            $"^{normalizedGlob.TrimEnd('/')}(?:/.*)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string Resolve(string repo, string relative) =>
        Path.GetFullPath(Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string RelativeProject(string from, string target) =>
        Normalize(Path.GetRelativePath(from, target));

    private static string Normalize(string value) => value.Replace('\\', '/').TrimStart('/');

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static IReadOnlyList<string> GetRepeatedOption(string[] args, string name)
    {
        var values = new List<string>();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                values.Add(args[i + 1]);
        }

        return values;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "build", "utilities.json")))
                return current.FullName;
            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage() =>
        Console.Error.WriteLine(
            "Usage: UtilitiesManifest <validate|list|generate-solutions|ci-matrix|release-matrix> [options]");

    private sealed class UtilityManifest
    {
        public List<UtilityEntry> Utilities { get; init; } = [];
    }

    private sealed class UtilityEntry
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Project { get; init; } = string.Empty;
        public string Solution { get; set; } = string.Empty;
        public List<string> Ship { get; init; } = [];
        public List<string> Tests { get; init; } = [];
        public List<string> ChangedPathGlobs { get; init; } = [];
    }
}
