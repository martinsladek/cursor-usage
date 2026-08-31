# Cursor Usage

A Windows 10 system tray utility that shows current Cursor included usage, the way Task Manager shows CPU.

- **Icon** — Task Manager-style sparkline of recent included-usage burn; color follows **Included in Pro %**
- **Hover** — Included / Auto / API percentages
- **Left click** — toggle the mini-dashboard (headline %, two pools, larger sparkline, recent tokens)
- **Right click** — refresh, open Cursor spending, start with Windows, About, Exit

The number that matters is [Included in Pro](https://cursor.com/dashboard/spending#included-in-pro) on a personal Pro plan with on-demand off. Usage-page token dollars are not a quota. Sign-in is the Cursor session already on this PC; there is no API key to paste.

The interface follows the Windows display language (`en`, `cs`). Other languages fall back to English.

### Download

**[Download here](https://github.com/martinsladek/cursor-usage/releases/latest/download/CursorUsage.exe)** — portable Windows 10 x64 executable (self-contained, no .NET install required).

### Notes

- Cache lives in `%LocalAppData%\CursorUsage\`. Session tokens are never stored there.
- Dashboard usage endpoints are unofficial and can change.

### Building

```powershell
dotnet publish src/CursorUsage/CursorUsage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

Requires the .NET 8 SDK.

Pushing a version tag (`v0.1.0`, `v1.0.0`, …) runs GitHub Actions: it publishes `CursorUsage.exe` and creates a GitHub Release. The download link above always follows the latest non-prerelease Release.

### Recreate from the idea

[SPEC.md](SPEC.md) is the full product contract. Give that file to a coding agent and ask it to implement the specification.
