# Torrent

Avalonia utility for `Novolis.Transports.Torrent` and `TorrentSessionPanel` / `TorrentProgressView`.

## Sample payload

Tiny Core Linux **Core-current.iso** (~18 MB) lives under `samples/`. Create the `.torrent` and prove local seed→leech:

```powershell
dotnet run --project d:\novolis\novolis-utilities\src\Torrent\Torrent.csproj -c Release -- --smoke
```

## UI

```powershell
dotnet run --project d:\novolis\novolis-utilities\src\Torrent\Torrent.csproj -c Release
```

Then **Load Core sample…** → **Start**.

## What it exercises

| Component | Role |
|-----------|------|
| `Novolis.Transports.Torrent` | BitTorrent session engine |
| `TorrentSessionPanel` | Session controls in Avalonia |
| `TorrentProgressView` | Transfer progress UI |

## Related

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Controls` | Torrent UI controls |

