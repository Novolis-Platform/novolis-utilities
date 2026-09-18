<!-- novolis-marketing:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-brand-transparent.svg" width="360" alt="Novolis"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/banners/novolis-utilities.svg" width="100%" alt="novolis-utilities"/>
</p>

<p align="center">
  <strong>Small technical executables</strong><br/>
  No-installer utilities for files, wires, devices, and focused technical jobs.
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-utilities/"><img src="https://img.shields.io/badge/docs-portfolio-0a7ea3" alt="docs"/></a>
  <a href="https://github.com/Novolis-Platform/novolis-utilities/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-utilities/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-utilities"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-utilities/">Docs</a>
  ·
  <a href="https://nuget.pkg.github.com/Novolis-Platform/index.json"><code>https://nuget.pkg.github.com/Novolis-Platform/index.json</code></a>
  ·
  <a href="https://github.com/Novolis-Platform/.github/blob/main/profile/README.md">Org landing</a>
  ·
  <a href="https://github.com/Novolis-Platform/novolis-governance">Governance</a>
</p>

---
<!-- novolis-marketing:end -->
# novolis-utilities

Small technical executables for the Novolis platform: one focused job, no
installer, and no NuGet tool package.

Utilities are framework-dependent or SDK-assisted executables. “Self-contained”
means one host project; it does not require a self-contained runtime bundle.

## Catalog

`build/utilities.json` is authoritative. Each utility has:

- one `OutputType=Exe` project under `src/<UtilityKey>/`;
- `IsPackable=false`;
- one generated `src/<UtilityKey>/<UtilityKey>.slnx`;
- optional tests under `tests/`;
- `ship: ["windows-zip"]` in phase one.

The catalog validator rejects `PackAsTool`, installer channels, lab shared
references, and packable projects.

## Build

```powershell
dotnet run --project d:\novolis\novolis-utilities\tools\UtilitiesManifest\UtilitiesManifest.csproj -- validate --repo d:\novolis\novolis-utilities
dotnet run --project d:\novolis\novolis-utilities\tools\UtilitiesManifest\UtilitiesManifest.csproj -- generate-solutions --repo d:\novolis\novolis-utilities
dotnet build d:\novolis\novolis-utilities\Novolis.Utilities.slnx
```

Restore uses only nuget.org and GitHub Packages. For local multi-repository
library iteration, open `d:\novolis\Novolis.Platform.slnx` or pass
`-p:NovolisUseProjectReferences=true`; do not add a local feed or a committed
cross-repository `ProjectReference`.

## Release

Pull requests and merges validate only changed utilities. A maintainer selects
one utility or `All` through `release.yml`; the workflow publishes a
framework-dependent Windows zip and `SHA256SUMS.txt` to a GitHub Release. No
Inno Setup, APK, `PackAsTool`, file association, or uninstall registration is
used.

```powershell
pwsh -File d:\novolis\novolis-utilities\scripts\Publish-NovolisUtility.ps1 -Utility Adb
```

## Dependencies and privacy

Utilities may write under `%LOCALAPPDATA%\Novolis\<utility-key>`. Each utility
README documents extra native dependencies such as `adb` on `PATH` or Npcap.
Because utilities are not installed, the app uninstall-data policy does not
apply.

## Related policy

- [Executable grains](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/executable-grains.md)
- [Executable repositories](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/apps-repos.md)

