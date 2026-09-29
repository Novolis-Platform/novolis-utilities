using System.Text.Json;
using Novolis.IO.Mobile.Android;

namespace Adb;

/// <summary>Headless read of adb + optional Books Mobile package presence.</summary>
internal static class AdbSmoke
{
    public const string BooksMobilePackage = "com.novolis.booksmobile";

    public static int Run() => Run([]);

    public static int Run(string[] args)
    {
        var options = SmokeOptions.Parse(args);
        if (options.Error is not null)
        {
            Console.Error.WriteLine($"AdbSmoke: {options.Error}");
            return 2;
        }

        var output = new List<string>();
        void WriteOutput(string text)
        {
            if (options.Json)
                output.Add(text);
            else
                Console.WriteLine(text);
        }

        try
        {
            var adb = new AndroidDebugBridge();
            WriteOutput($"transport: {adb.Transport}");
            WriteOutput($"adb: {adb.AdbPath}");
            if (!string.Equals(adb.Transport, "protocol", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("AdbSmoke: expected protocol transport.");
                return 1;
            }

            if (!File.Exists(adb.AdbPath))
            {
                Console.Error.WriteLine($"AdbSmoke: adb path missing: {adb.AdbPath}");
                return 1;
            }

            var devices = adb.ListDevices();
            WriteOutput($"devices: {devices.Count}");
            foreach (var d in devices)
                WriteOutput($"  {d.Serial}\t{d.State}\t{d.Model}");

            var selection = AndroidDeviceSelector.Resolve(
                devices,
                new AndroidTargetOptions
                {
                    Serial = options.Serial,
                    RequireExplicitWhenMultiple = true,
                    RequireReady = true,
                });
            if (!selection.Ok || selection.Device is null)
            {
                Console.Error.WriteLine(
                    $"AdbSmoke: {selection.Failure?.Message ?? "no ready device"}");
                return 1;
            }
            var ready = selection.Device;

            var installer = new AndroidAppInstaller(adb);
            var waited = installer.WaitForReadyDevice(options.Timeout, ready.Serial);
            WriteOutput($"wait: {waited.Serial} {waited.State}");

            var pkg = installer.TryGetPackage(options.PackageName, ready.Serial);
            if (pkg is { IsInstalled: true })
                WriteOutput($"package-info: {pkg.PackageName} {pkg.VersionName} ({pkg.VersionCode}) {pkg.ApkPath}");
            else
                WriteOutput($"package-info: {options.PackageName} not installed");

            var bad = installer.ValidateApk(Path.Combine(Path.GetTempPath(), "missing-novolis.apk"));
            if (bad.Ok)
            {
                Console.Error.WriteLine("AdbSmoke: expected missing APK validation to fail.");
                return 1;
            }

            WriteOutput($"validate-missing: {bad.Errors[0]}");

            var info = adb.GetDeviceInfo(ready.Serial);
            WriteOutput(AndroidOutputRedactor.Redact(info.FormatReport()));
            WriteOutput("");

            if (!options.NonDestructive)
            {
                // Protocol sync round-trip (tmp file).
                var local = Path.Combine(Path.GetTempPath(), $"novolis-adb-{Guid.NewGuid():N}.txt");
                var remote = $"/data/local/tmp/novolis-adb-{Guid.NewGuid():N}.txt";
                try
                {
                    File.WriteAllText(local, "novolis-protocol-ok");
                    var push = adb.Push(local, remote, ready.Serial);
                    if (!push.Ok)
                    {
                        Console.Error.WriteLine($"AdbSmoke: push failed: {push.Message}");
                        return 1;
                    }

                    var pulled = Path.Combine(Path.GetTempPath(), $"novolis-adb-pull-{Guid.NewGuid():N}.txt");
                    var pull = adb.Pull(remote, pulled, ready.Serial);
                    if (!pull.Ok)
                    {
                        Console.Error.WriteLine($"AdbSmoke: pull failed: {pull.Message}");
                        return 1;
                    }

                    var text = File.ReadAllText(pulled);
                    if (!text.Contains("novolis-protocol-ok", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine("AdbSmoke: pull content mismatch.");
                        return 1;
                    }

                    WriteOutput($"sync: push/pull OK ({remote})");
                    try { File.Delete(pulled); } catch { /* ignore */ }
                }
                finally
                {
                    try { File.Delete(local); } catch { /* ignore */ }
                    adb.Shell($"rm -f {AndroidInputValidator.QuoteShellArgument(remote)}", ready.Serial);
                }
            }

            var path = installer.TryGetPackage(options.PackageName, ready.Serial);
            if (path is { IsInstalled: true })
                WriteOutput($"package: {path.ApkPath ?? "installed"}");
            else
                WriteOutput($"package: {options.PackageName} not installed (ok for smoke)");

            if (options.Json)
                Console.WriteLine(JsonSerializer.Serialize(new { ok = true, serial = ready.Serial, output }));
            else
                Console.WriteLine("AdbSmoke OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AdbSmoke: {ex.Message}");
            return 1;
        }
    }

    private sealed class SmokeOptions
    {
        public string? Serial { get; private set; }
        public string PackageName { get; private set; } = BooksMobilePackage;
        public TimeSpan Timeout { get; private set; } = TimeSpan.FromSeconds(5);
        public bool NonDestructive { get; private set; }
        public bool Json { get; private set; }
        public string? Error { get; private set; }

        public static SmokeOptions Parse(string[] args)
        {
            var options = new SmokeOptions();
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--smoke":
                        break;
                    case "--serial" when i + 1 < args.Length:
                        options.Serial = args[++i];
                        break;
                    case "--package" when i + 1 < args.Length:
                        options.PackageName = args[++i];
                        break;
                    case "--timeout" when i + 1 < args.Length
                        && int.TryParse(args[++i], out var seconds)
                        && seconds > 0:
                        options.Timeout = TimeSpan.FromSeconds(seconds);
                        break;
                    case "--non-destructive":
                        options.NonDestructive = true;
                        break;
                    case "--json":
                        options.Json = true;
                        break;
                    default:
                        return new SmokeOptions { Error = $"Unknown or incomplete option '{args[i]}'." };
                }
            }

            if (!AndroidInputValidator.IsPackageName(options.PackageName))
                return new SmokeOptions { Error = $"Invalid package name '{options.PackageName}'." };
            return options;
        }
    }
}

