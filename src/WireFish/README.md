# WireFish

Avalonia WireShark-style packet viewer for **Novolis.Transports.WireFish**, **Novolis.Messaging.Channels**, **Novolis.Avalonia.Controls**, and **Novolis.Avalonia.Layout**.

Requires **Npcap** for live capture. The app relaunches elevated (UAC) when needed for capture driver access.

## Run

```powershell
dotnet run --project d:\novolis\novolis-utilities\src\WireFish\WireFish.csproj
```

Toolbar: **Start Npcap** (if driver stopped), pick interface, **Start** / **Stop** capture. Packet list and detail panes update as frames arrive.

No `--smoke` CLI flag. Unit tests are not included in this first utility move.

## ProjectRef note

For local iteration against sibling repos, open `Novolis.Platform.slnx` or pass `-p:NovolisUseProjectReferences=true`. Committed `.csproj` files use `PackageReference` from GitHub Packages only.

