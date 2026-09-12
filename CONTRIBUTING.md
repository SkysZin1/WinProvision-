# Contributing

1. Install the .NET 8 SDK on Windows.
2. Build with `dotnet build src/WinProvision/WinProvision.csproj`.
3. Run the PowerShell checks in `tests/` before opening a pull request.
4. Keep user-facing changes documented in `docs/` and `CHANGELOG.md`.

Create a distribution package with `powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Build-Release.ps1`. The script reads the project version, publishes the desktop app, verifies the ZIP contents and writes its SHA-256 checksum. Reviewed release packages can be explicitly added under `packaging/releases`; temporary builds and backups remain ignored. Virtual machines, installation media and diagnostic artifacts do not belong in source control.

UI text uses `src/WinProvision/Assets/Translations.json` (English keys, Brazilian Portuguese values). Use `L.T` / `L.F` in C# and `T` / `TF` in PowerShell; format parameters after translation. Keep profile values, package IDs and worker status codes unchanged. Store PowerShell scripts as UTF-8 with BOM for Windows PowerShell 5.1. Run UI checks in both languages before packaging.
