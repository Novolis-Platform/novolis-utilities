# Adb

Small Avalonia utility for **Novolis.IO.Mobile.Android**.

## What it provides

| Action | Library surface |
|--------|-----------------|
| Refresh devices | `AndroidDebugBridge.ListDevices` (ADB protocol) |
| Device stats panel | `GetDeviceInfoAsync().FormatReport()` — identity, build, CPU, display, battery, RAM, storage |
| Inspect package | Typed package queries and activity resolution; no device-side `grep`/`head` pipeline |
| Install APK… | `AndroidAppInstaller.InstallAsync` — validate, wait, install `-r -g`, verify, and report cancellation |
| App controls | Launch, force-stop, and clear data with visible status |
| Evidence panes | Filtered logcat, PNG screenshot, and UIAutomator XML |
| Headless `--smoke` | `--serial`, `--package`, `--timeout`, `--non-destructive`, and protocol sync coverage |

For a scriptable installed command, use the PackAsTool
`novolis-android`. This utility remains the visual operator and dogfood host.

Transport line in the UI: `protocol · <path-to-adb.exe>` (adb hosts the server only).

## Prerequisites

1. Android SDK **platform-tools** (`ANDROID_HOME` / default `%LOCALAPPDATA%\Android\Sdk`)
2. Phone with **USB debugging** authorized (`device` state)
3. Restore from GitHub Packages; local platform iteration can use ProjectReference mode

## Run

```powershell
# UI
dotnet run --project d:\novolis\novolis-utilities\src\Adb\Adb.csproj

# Headless (exit 0 when a ready device is present)
dotnet run --project d:\novolis\novolis-utilities\src\Adb\Adb.csproj -- --smoke

# Pin a real phone and avoid push/pull changes
dotnet run --project d:\novolis\novolis-utilities\src\Adb\Adb.csproj -- --smoke --serial R58M12ABCDE --non-destructive
```

ProjectRef mode is non-transitive for NuGet: the app also PackageReferences `AdvancedSharpAdbClient` explicitly.

## Tips

- Enter a valid Android package name when inspecting or controlling an app.
- Select a device explicitly when more than one ready device is attached.
- The screenshot and UI XML actions write evidence under the user's Pictures/Documents folders.
- Read-only by default — install and clear-data are always explicit actions.

