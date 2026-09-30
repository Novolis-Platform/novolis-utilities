using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Utilities.Manifest;

internal static class UtilityPublishCommand
{
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    internal static int Publish(Program.UtilityManifest manifest, string repoRoot, string[] args)
    {
        var utilityKey = GetRequiredOption(args, "--utility");
        var packageVersion = GetRequiredOption(args, "--version");

        var utility = manifest.Utilities.FirstOrDefault(u => u.Key.Equals(utilityKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown utility key: {utilityKey}");

        if (!utility.Ship.Any(channel => channel.Equals("windows-zip", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Utility '{utility.Key}' does not declare the windows-zip channel.");

        var projectPath = Resolve(repoRoot, utility.Project);
        if (!File.Exists(projectPath))
            throw new InvalidOperationException($"Utility project does not exist: {utility.Project}");

        var artifactRoot = Path.Combine(repoRoot, "artifacts", utility.Key);
        var publishRoot = Path.Combine(artifactRoot, "publish");
        var zipPath = Path.Combine(artifactRoot, $"{utility.Key}-{packageVersion}-win-x64.zip");

        if (Directory.Exists(artifactRoot))
            Directory.Delete(artifactRoot, recursive: true);
        Directory.CreateDirectory(publishRoot);

        var nugetConfig = Path.Combine(repoRoot, "nuget.config");
        var arguments = new List<string>
        {
            "publish", projectPath,
            "--configuration", "Release",
            "--runtime", "win-x64",
            "--self-contained", "true",
            "--output", publishRoot,
        };
        if (File.Exists(nugetConfig))
        {
            arguments.Add("--configfile");
            arguments.Add(nugetConfig);
        }

        Console.Error.WriteLine($"Publishing utility {utility.Key} {packageVersion} (win-x64)...");
        ProcessRunner.Run("dotnet", repoRoot, arguments);

        if (File.Exists(zipPath))
            File.Delete(zipPath);
        ZipFile.CreateFromDirectory(publishRoot, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        Directory.Delete(publishRoot, recursive: true);
        Console.Error.WriteLine($"Utility zip: {zipPath}");

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zipPath))).ToLowerInvariant();
        var checksumsPath = Path.Combine(artifactRoot, "SHA256SUMS.txt");
        File.WriteAllText(checksumsPath, $"{hash}  {Path.GetFileName(zipPath)}{Environment.NewLine}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var result = new PublishResult { ZipPath = zipPath };
        Console.WriteLine(JsonSerializer.Serialize(result, ResultJsonOptions));
        return 0;
    }

    private static string Resolve(string repo, string relative) =>
        Path.GetFullPath(Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string GetRequiredOption(string[] args, string name)
    {
        var value = GetOption(args, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required option {name}.");
        return value;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private sealed class PublishResult
    {
        [JsonPropertyName("zipPath")]
        public string? ZipPath { get; set; }
    }
}
