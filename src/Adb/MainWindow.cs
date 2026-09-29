using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Novolis.IO.Mobile.Android;

namespace Adb;

internal sealed class MainWindow : Window
{
    static readonly IBrush Bg = new SolidColorBrush(Color.FromRgb(14, 20, 28));
    static readonly IBrush Panel = new SolidColorBrush(Color.FromRgb(22, 32, 42));
    static readonly IBrush BorderC = new SolidColorBrush(Color.FromRgb(40, 60, 75));
    static readonly IBrush Text = new SolidColorBrush(Color.FromRgb(230, 236, 242));
    static readonly IBrush Muted = new SolidColorBrush(Color.FromRgb(150, 168, 184));
    static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(46, 160, 140));

    readonly AndroidDebugBridge? _adb;
    AndroidDeviceDiagnostics? _diagnostics;
    readonly ListBox _deviceList = new();
    readonly TextBlock _adbPath = new();
    readonly TextBlock _status = new();
    readonly TextBox _deviceInfo = new();
    readonly TextBox _packageBox = new();
    readonly TextBox _log = new();
    readonly Button _refreshBtn;
    readonly Button _statsBtn;
    readonly Button _inspectBtn;
    readonly Button _installBtn;
    readonly Button _launchBtn;
    readonly Button _stopBtn;
    readonly Button _clearBtn;
    readonly Button _logcatBtn;
    readonly Button _screenshotBtn;
    readonly Button _uiDumpBtn;
    readonly Button _pushBtn;
    readonly Button _pullBtn;
    readonly Button _cancelBtn;
    readonly TextBox _uiDump = new();
    readonly TextBox _remotePathBox = new();
    readonly Image _screenshot = new();
    CancellationTokenSource? _operationCts;
    bool _busy;
    string? _selectedSerial;

    public MainWindow()
    {
        Title = "Novolis Adb Lab";
        Width = 1180;
        Height = 820;
        MinWidth = 800;
        MinHeight = 520;
        Background = Bg;

        _refreshBtn = PrimaryButton("Refresh devices", () => _ = RefreshAsync());
        _statsBtn = PrimaryButton("Refresh stats", () => _ = ReloadStatsAsync());
        _inspectBtn = PrimaryButton("Inspect package", () => _ = InspectPackageAsync());
        _installBtn = SecondaryButton("Install APK…", () => _ = InstallApkAsync());
        _launchBtn = SecondaryButton("Launch", () => _ = LaunchPackageAsync());
        _stopBtn = SecondaryButton("Stop", () => _ = StopPackageAsync());
        _clearBtn = SecondaryButton("Clear data", () => _ = ClearPackageAsync());
        _logcatBtn = SecondaryButton("Capture logcat", () => _ = CaptureLogcatAsync());
        _screenshotBtn = SecondaryButton("Screenshot", () => _ = CaptureScreenshotAsync());
        _uiDumpBtn = SecondaryButton("UI dump", () => _ = DumpUiAsync());
        _pushBtn = SecondaryButton("Push file…", () => _ = PushFileAsync());
        _pullBtn = SecondaryButton("Pull file…", () => _ = PullFileAsync());
        _cancelBtn = SecondaryButton("Cancel", CancelOperation);
        _cancelBtn.IsEnabled = false;

        try
        {
            _adb = new AndroidDebugBridge();
        }
        catch (Exception ex)
        {
            Content = ErrorBody($"Could not locate adb.\n\n{ex.Message}");
            return;
        }

        _adbPath.Text = $"{_adb.Transport} · {_adb.AdbPath}";
        _adbPath.FontSize = 12;
        _adbPath.Foreground = Muted;
        _diagnostics = new AndroidDeviceDiagnostics(_adb);

        _status.Text = "Ready.";
        _status.FontSize = 12;
        _status.Foreground = Muted;
        _status.VerticalAlignment = VerticalAlignment.Center;

        _deviceInfo.IsReadOnly = true;
        _deviceInfo.AcceptsReturn = true;
        _deviceInfo.TextWrapping = TextWrapping.NoWrap;
        _deviceInfo.FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace");
        _deviceInfo.FontSize = 12;
        _deviceInfo.Background = Panel;
        _deviceInfo.BorderBrush = BorderC;
        _deviceInfo.BorderThickness = new Thickness(1);
        _deviceInfo.Foreground = Text;
        _deviceInfo.Text = "Select a device.";
        _deviceInfo.MinHeight = 280;

        _deviceList.Background = Panel;
        _deviceList.BorderBrush = BorderC;
        _deviceList.BorderThickness = new Thickness(1);
        _deviceList.MinHeight = 160;
        _deviceList.SelectionChanged += (_, _) => OnDeviceSelected();

        _packageBox.PlaceholderText = "package name";
        _packageBox.MinWidth = 280;
        _remotePathBox.PlaceholderText = "remote path";
        _remotePathBox.MinWidth = 280;

        _uiDump.IsReadOnly = true;
        _uiDump.AcceptsReturn = true;
        _uiDump.TextWrapping = TextWrapping.NoWrap;
        _uiDump.FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace");
        _uiDump.FontSize = 12;
        _uiDump.Background = Panel;
        _uiDump.BorderBrush = BorderC;
        _uiDump.BorderThickness = new Thickness(1);
        _uiDump.MinHeight = 180;

        _screenshot.Stretch = Avalonia.Media.Stretch.Uniform;
        _screenshot.HorizontalAlignment = HorizontalAlignment.Center;
        _screenshot.VerticalAlignment = VerticalAlignment.Center;
        _screenshot.MinHeight = 180;

        _log.IsReadOnly = true;
        _log.AcceptsReturn = true;
        _log.TextWrapping = TextWrapping.Wrap;
        _log.FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace");
        _log.FontSize = 12;
        _log.Background = Panel;
        _log.BorderBrush = BorderC;
        _log.BorderThickness = new Thickness(1);
        _log.MinHeight = 160;

        Content = BuildLayout();
        Opened += (_, _) => _ = RefreshAsync();
    }

    Control BuildLayout()
    {
        var header = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(16, 14, 16, 8),
            Children =
            {
                new TextBlock
                {
                    Text = "Adb Lab",
                    FontSize = 22,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Text,
                },
                new TextBlock
                {
                    Text = "Dogfood Novolis.IO.Mobile.Android — tethered device discovery and package read.",
                    FontSize = 13,
                    Foreground = Muted,
                },
                _adbPath,
            },
        };

        var left = new DockPanel
        {
            Margin = new Thickness(16, 8, 8, 16),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Margin = new Thickness(0, 0, 0, 8),
                    [DockPanel.DockProperty] = Dock.Top,
                    Children = { _refreshBtn, _statsBtn, _status },
                },
                Section("Devices", _deviceList),
            },
        };

        var rightTop = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(8, 8, 16, 8),
            Children =
            {
                Section("Device stats", _deviceInfo),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        _packageBox,
                        _inspectBtn,
                        _installBtn,
                    },
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        _launchBtn,
                        _stopBtn,
                        _clearBtn,
                        _cancelBtn,
                    },
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        _logcatBtn,
                        _screenshotBtn,
                        _uiDumpBtn,
                    },
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        _remotePathBox,
                        _pushBtn,
                        _pullBtn,
                    },
                },
                Section("Screenshot", new Border
                {
                    Background = Panel,
                    BorderBrush = BorderC,
                    BorderThickness = new Thickness(1),
                    MinHeight = 180,
                    Child = _screenshot,
                }),
                Section("UI hierarchy", _uiDump),
            },
        };

        var right = new DockPanel
        {
            Margin = new Thickness(8, 0, 16, 16),
            Children =
            {
                new Border
                {
                    Child = new ScrollViewer
                    {
                        Content = rightTop,
                        Height = 390,
                        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    },
                    [DockPanel.DockProperty] = Dock.Top,
                },
                Section("Log", _log),
            },
        };

        var split = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("340,*"),
            RowDefinitions = new RowDefinitions("*"),
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        split.Children.Add(left);
        split.Children.Add(right);

        return new DockPanel
        {
            Children =
            {
                new Border
                {
                    Background = Panel,
                    BorderBrush = BorderC,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = header,
                    [DockPanel.DockProperty] = Dock.Top,
                },
                split,
            },
        };
    }

    async Task RefreshAsync()
    {
        if (_adb is null || !BeginBusy("Listing devices…"))
            return;
        try
        {
            var devices = await _adb.ListDevicesAsync(_operationCts!.Token).ConfigureAwait(true);
            _deviceList.ItemsSource = devices
                .Select(d => new DeviceRow(d))
                .ToList();
            AppendLog($"devices: {devices.Count}");
            foreach (var d in devices)
                AppendLog($"  {d.Serial}  {d.State}  {d.Model}");

            if (_deviceList.ItemCount > 0 && _deviceList.SelectedIndex < 0)
                _deviceList.SelectedIndex = 0;
            if (_deviceList.ItemCount == 0)
                _selectedSerial = null;

            SetStatus(devices.Count == 0
                ? "No devices. Enable USB debugging / authorize this PC."
                : $"{devices.Count} device(s).");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }

        if (_selectedSerial is { } selected)
            await LoadInfoAsync(selected);
    }

    void OnDeviceSelected()
    {
        if (_deviceList.SelectedItem is not DeviceRow row)
        {
            _selectedSerial = null;
            _deviceInfo.Text = "Select a device.";
            return;
        }

        _selectedSerial = row.Device.Serial;
        if (!_busy)
            _ = LoadInfoAsync(row.Device.Serial);
    }

    Task ReloadStatsAsync()
    {
        if (_selectedSerial is null && SelectedSerial() is { } s)
            _selectedSerial = s;
        if (_selectedSerial is null)
        {
            SetStatus("Select a device first.");
            return Task.CompletedTask;
        }

        return LoadInfoAsync(_selectedSerial);
    }

    async Task LoadInfoAsync(string serial)
    {
        if (_adb is null || !BeginBusy($"Reading stats for {serial}…"))
            return;
        try
        {
            var info = await _adb.GetDeviceInfoAsync(
                    serial,
                    _operationCts!.Token)
                .ConfigureAwait(true);
            _deviceInfo.Text = info.FormatReport();
            AppendLog(
                $"stats {serial}: {info.Manufacturer} {info.Model} · " +
                $"A{info.AndroidVersion}/SDK{info.SdkVersion} · " +
                $"bat {info.Battery?.Level?.ToString() ?? "?"}%" +
                (info.Battery is { } b ? $" {b.StatusLabel}" : "") +
                $" · {info.Display?.PhysicalSize ?? "?"} @{info.Display?.DensityDpi?.ToString() ?? "?"}dpi · " +
                $"RAM {FormatShortMb(info.Memory?.MemAvailableKb)}/{FormatShortMb(info.Memory?.MemTotalKb)} avail");
            SetStatus($"Loaded stats for {serial}.");
        }
        catch (Exception ex)
        {
            _deviceInfo.Text = ex.Message;
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    static string FormatShortMb(long? kb) =>
        kb is null ? "?" : $"{kb.Value / 1024.0:0.#}MiB";

    async Task InspectPackageAsync()
    {
        var serial = SelectedSerial();
        if (serial is null)
        {
            SetStatus("Select a device first.");
            return;
        }

        var package = _packageBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(package))
        {
            SetStatus("Enter a package name.");
            return;
        }

        if (_adb is null || !BeginBusy($"Inspecting {package}…"))
            return;
        try
        {
            AndroidInputValidator.RequirePackageName(package);
            var adb = _adb;
            var info = await adb.TryGetPackageInfoAsync(
                    package,
                    serial,
                    _operationCts!.Token)
                .ConfigureAwait(true);
            var activity = await adb.ShellAsync(
                    $"cmd package resolve-activity --brief {package}",
                    serial,
                    _operationCts.Token)
                .ConfigureAwait(true);
            var sb = new StringBuilder();
            sb.AppendLine($"package: {package}");
            sb.AppendLine($"installed: {info?.IsInstalled == true}");
            sb.AppendLine($"versionName: {info?.VersionName ?? "—"}");
            sb.AppendLine($"versionCode: {info?.VersionCode?.ToString() ?? "—"}");
            sb.AppendLine($"apk: {info?.ApkPath ?? "—"}");
            sb.AppendLine();
            sb.AppendLine("resolved activity:");
            sb.AppendLine(activity.Ok ? activity.StdOut.Trim() : activity.Diagnostic);
            var text = sb.ToString();

            AppendLog(text.TrimEnd());
            SetStatus(info?.IsInstalled == true
                ? $"Package {package} found."
                : $"Package {package} not found.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    async Task InstallApkAsync()
    {
        var serial = SelectedSerial();
        if (serial is null)
        {
            SetStatus("Select a device first.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Install APK",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Android package") { Patterns = ["*.apk"] },
            ],
        }).ConfigureAwait(true);

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Could not resolve APK path.");
            return;
        }

        if (_adb is null || !BeginBusy($"Installing {Path.GetFileName(path)}…"))
            return;
        try
        {
            var installer = new AndroidAppInstaller(_adb);
            var expected = _packageBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(expected) || expected.Contains(' ', StringComparison.Ordinal))
                expected = null;

            var result = await installer.InstallAsync(
                    path,
                    new ApkInstallOptions
                    {
                        Serial = serial,
                        Reinstall = true,
                        GrantPermissions = true,
                        ExpectedPackageName = expected,
                        VerifyInstalled = expected is not null,
                    },
                    _operationCts!.Token)
                .ConfigureAwait(true);

            AppendLog(result.Message);
            if (result.Validation is { Warnings.Count: > 0 } v)
            {
                foreach (var w in v.Warnings)
                    AppendLog($"warn: {w}");
            }

            if (result.Package is { } pkg)
                AppendLog($"package {pkg.PackageName} path={pkg.ApkPath} version={pkg.VersionName} ({pkg.VersionCode})");

            SetStatus(result.Ok ? "Install succeeded." : "Install failed.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    async Task LaunchPackageAsync()
    {
        var serial = SelectedSerial();
        var package = _packageBox.Text?.Trim();
        if (serial is null || string.IsNullOrWhiteSpace(package))
        {
            SetStatus("Select a device and enter a package name first.");
            return;
        }

        if (_adb is null || !BeginBusy($"Launching {package}…"))
            return;
        try
        {
            var result = await _adb.StartAppAsync(
                    package,
                    serial,
                    _operationCts!.Token)
                .ConfigureAwait(true);
            AppendLog(result.Message);
            SetStatus(result.Ok ? "Launch succeeded." : result.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    async Task StopPackageAsync()
    {
        var serial = SelectedSerial();
        var package = _packageBox.Text?.Trim();
        if (serial is null || string.IsNullOrWhiteSpace(package))
        {
            SetStatus("Select a device and enter a package name first.");
            return;
        }

        if (_adb is null || !BeginBusy($"Stopping {package}…"))
            return;
        try
        {
            var result = await _adb.ForceStopAsync(
                    package,
                    serial,
                    _operationCts!.Token)
                .ConfigureAwait(true);
            AppendLog(result.Message);
            SetStatus(result.Ok ? "Stop succeeded." : result.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    async Task ClearPackageAsync()
    {
        var serial = SelectedSerial();
        var package = _packageBox.Text?.Trim();
        if (serial is null || string.IsNullOrWhiteSpace(package))
        {
            SetStatus("Select a device and enter a package name first.");
            return;
        }

        if (_diagnostics is null || !BeginBusy($"Clearing {package} data…"))
            return;
        try
        {
            var result = await _diagnostics.ClearDataAsync(
                    package,
                    serial,
                    _operationCts!.Token)
                .ConfigureAwait(true);
            AppendLog(result.Diagnostic);
            SetStatus(result.Ok ? "Clear data succeeded." : result.Diagnostic);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    async Task CaptureLogcatAsync()
    {
        var serial = SelectedSerial();
        if (_diagnostics is null || serial is null || !BeginBusy("Capturing logcat…"))
            return;
        try
        {
            var package = _packageBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(package))
                AndroidInputValidator.RequirePackageName(package);
            var result = await _diagnostics.CaptureLogcatAsync(
                    new AndroidLogcatOptions
                    {
                        Serial = serial,
                        PackageName = string.IsNullOrWhiteSpace(package) ? null : package,
                        LastLines = 500,
                    },
                    _operationCts!.Token)
                .ConfigureAwait(true);
            AppendLog(result.Text);
            SetStatus(result.Ok ? "Captured logcat." : result.Failure?.Message ?? "Logcat failed.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    async Task CaptureScreenshotAsync()
    {
        var serial = SelectedSerial();
        if (_diagnostics is null || serial is null || !BeginBusy("Capturing screenshot…"))
            return;
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Novolis",
                $"adb-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            var result = await _diagnostics.CaptureScreenshotAsync(
                    path,
                    serial,
                    cancellationToken: _operationCts!.Token)
                .ConfigureAwait(true);
            if (result.Ok)
            {
                _screenshot.Source = new Bitmap(result.Path);
                AppendLog($"screenshot: {result.Path}");
                SetStatus("Screenshot captured.");
            }
            else
            {
                SetStatus(result.Failure?.Message ?? "Screenshot failed.");
            }
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    async Task DumpUiAsync()
    {
        var serial = SelectedSerial();
        if (_diagnostics is null || serial is null || !BeginBusy("Dumping UI hierarchy…"))
            return;
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Novolis",
                $"adb-ui-{DateTime.Now:yyyyMMdd-HHmmss}.xml");
            var result = await _diagnostics.DumpUiAsync(
                    path,
                    serial,
                    cancellationToken: _operationCts!.Token)
                .ConfigureAwait(true);
            _uiDump.Text = result.Xml ?? result.Failure?.Message ?? "UI dump failed.";
            SetStatus(result.Ok ? $"UI hierarchy written to {path}." : "UI dump failed.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    async Task PushFileAsync()
    {
        var serial = SelectedSerial();
        var remote = _remotePathBox.Text?.Trim();
        if (_adb is null || serial is null || string.IsNullOrWhiteSpace(remote))
        {
            SetStatus("Select a device and enter a remote path first.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Push file to device",
            AllowMultiple = false,
        }).ConfigureAwait(true);
        if (files.Count == 0)
            return;
        var local = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(local))
        {
            SetStatus("Could not resolve the local file path.");
            return;
        }

        if (!BeginBusy($"Pushing {Path.GetFileName(local)}…"))
            return;
        try
        {
            var result = await _adb.PushAsync(
                    local,
                    remote,
                    serial,
                    _operationCts!.Token)
                .ConfigureAwait(true);
            AppendLog(result.Message);
            SetStatus(result.Ok ? "Push succeeded." : result.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    async Task PullFileAsync()
    {
        var serial = SelectedSerial();
        var remote = _remotePathBox.Text?.Trim();
        if (_adb is null || serial is null || string.IsNullOrWhiteSpace(remote))
        {
            SetStatus("Select a device and enter a remote path first.");
            return;
        }

        var fileName = Path.GetFileName(remote.Replace('/', Path.DirectorySeparatorChar));
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Pull file from device",
            SuggestedFileName = string.IsNullOrWhiteSpace(fileName) ? "adb-pull.bin" : fileName,
        }).ConfigureAwait(true);
        var local = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(local))
        {
            SetStatus("Could not resolve the local output path.");
            return;
        }

        if (!BeginBusy($"Pulling {remote}…"))
            return;
        try
        {
            var result = await _adb.PullAsync(
                    remote,
                    local,
                    serial,
                    _operationCts!.Token)
                .ConfigureAwait(true);
            AppendLog(result.Message);
            SetStatus(result.Ok ? "Pull succeeded." : result.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            AppendLog($"ERROR {ex.Message}");
        }
        finally
        {
            EndBusy();
        }
    }

    string? SelectedSerial() =>
        _deviceList.SelectedItem is DeviceRow row ? row.Device.Serial : null;

    bool BeginBusy(string message)
    {
        if (_busy)
            return false;
        _busy = true;
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();
        _refreshBtn.IsEnabled = false;
        _statsBtn.IsEnabled = false;
        _inspectBtn.IsEnabled = false;
        _installBtn.IsEnabled = false;
        _launchBtn.IsEnabled = false;
        _stopBtn.IsEnabled = false;
        _clearBtn.IsEnabled = false;
        _logcatBtn.IsEnabled = false;
        _screenshotBtn.IsEnabled = false;
        _uiDumpBtn.IsEnabled = false;
        _pushBtn.IsEnabled = false;
        _pullBtn.IsEnabled = false;
        _cancelBtn.IsEnabled = true;
        SetStatus(message);
        return true;
    }

    void EndBusy()
    {
        _busy = false;
        _operationCts?.Dispose();
        _operationCts = null;
        _refreshBtn.IsEnabled = true;
        _statsBtn.IsEnabled = true;
        _inspectBtn.IsEnabled = true;
        _installBtn.IsEnabled = true;
        _launchBtn.IsEnabled = true;
        _stopBtn.IsEnabled = true;
        _clearBtn.IsEnabled = true;
        _logcatBtn.IsEnabled = true;
        _screenshotBtn.IsEnabled = true;
        _uiDumpBtn.IsEnabled = true;
        _pushBtn.IsEnabled = true;
        _pullBtn.IsEnabled = true;
        _cancelBtn.IsEnabled = false;
    }

    void CancelOperation() => _operationCts?.Cancel();

    void SetStatus(string text) => _status.Text = text;

    void AppendLog(string text)
    {
        if (_log.Text?.Length > 0)
            _log.Text += Environment.NewLine;
        _log.Text += text;
        _log.CaretIndex = _log.Text?.Length ?? 0;
    }

    static Control ErrorBody(string message) =>
        new TextBlock
        {
            Text = message,
            Margin = new Thickness(24),
            Foreground = Text,
            TextWrapping = TextWrapping.Wrap,
        };

    static Control Section(string title, Control child) =>
        new DockPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Muted,
                    Margin = new Thickness(0, 0, 0, 6),
                    [DockPanel.DockProperty] = Dock.Top,
                },
                child,
            },
        };

    static Button PrimaryButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            Background = Accent,
            Foreground = Brushes.White,
            Padding = new Thickness(12, 6),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    static Button SecondaryButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            Background = Panel,
            Foreground = Text,
            BorderBrush = BorderC,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    sealed class DeviceRow(AdbDevice device)
    {
        public AdbDevice Device { get; } = device;

        public override string ToString() =>
            $"{Device.Serial}  ·  {Device.State}  ·  {Device.Model ?? Device.Product ?? "—"}";
    }
}

