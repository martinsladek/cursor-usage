# Agent notes

## Stop the running EXE first

`CursorUsage.exe` locks the published file. Before **every** local `dotnet publish`, rebuild, or release prep on this machine: stop the running process, then publish, then start `dist\CursorUsage.exe` if you still want a local try.

```powershell
Stop-Process -Name CursorUsage -ErrorAction SilentlyContinue
dotnet publish src/CursorUsage/CursorUsage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
Start-Process dist\CursorUsage.exe
```

GitHub Actions builds on a runner, so a `v*` tag does not need the local EXE stopped. Stop it anyway when this PC will write `dist/`.

## Releases

Do **not** attach a locally built EXE to GitHub Releases and do not commit `dist/`.

GitHub Actions publishes `CursorUsage.exe` when a tag matching `v*` is pushed (see `.github/workflows/release.yml`). The stable URL is `/releases/latest/download/CursorUsage.exe`.

To ship a build: commit to `main`, then `git tag vX.Y.Z` and `git push origin vX.Y.Z`. Local try does not need a tag.

## Secrets

The Cursor session JWT is read live from `%APPDATA%\Cursor\User\globalStorage\state.vscdb`. Never copy it into `config.json`, the repo, or logs.

## Build

.NET 8 SDK (`global.json`). Target `net8.0-windows10.0.19041.0`.
