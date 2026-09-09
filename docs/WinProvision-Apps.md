# WinProvision Apps

The picker opens at the first sign-in for each account created from the Windows default profile. Users choose applications and start installation. No application is selected by default.

## Interface

- English WPF interface running on Windows PowerShell 5.1; the newly installed Windows does not require .NET 8.
- Search by name, category, or description, with category sections and filters.
- Sequential queue with per-application results and logs.
- A failed item remains selected for retry; one failure does not stop the queue.
- **Stop after current app** finishes the active installer and then stops the queue.
- **Maybe later** saves the selection. The **WinProvision Apps** desktop shortcut reopens the picker.
- **Preview app picker** in the Builder opens a non-installing preview.

## Catalog

The catalog is a closed list of exact WinGet identifiers embedded in `src/WinProvision/Scripts/Provisioner.ps1`. It accepts no user-supplied commands or URLs. Package versions and requirements follow their publishers. WinGet requires Windows 10 1809 or later. Accounts, subscriptions, and games are not purchased or configured automatically.

## Installation flow

1. The Builder embeds the picker and its catalog in the generated answer file.
2. During `specialize`, Windows extracts `Provisioner.ps1` to `%WINDIR%\Setup\Scripts\WinProvision` and creates a public desktop shortcut.
3. A `RunOnce` entry prepared in the default profile opens the picker once for each new account. Windows may defer `RunOnce`; the shortcut remains available.
4. Clicking **Install** starts a separate process, so the interface remains responsive. Installers that require elevation may show UAC.

## Network and failures

The worker calls `winget install` with an exact ID, the `winget` source, silent installation, accepted terms, disabled terminal interaction, and `--no-upgrade`. An already-installed package is treated as completed. No automatic upgrades are performed.

If WinGet is not registered, the worker requests registration of the existing App Installer and waits briefly. If it remains unavailable, the picker directs the user to install or update App Installer from Microsoft Store. It does not download alternate executables or PowerShell modules.

Internet outages, UAC refusal, unavailable packages, incompatibilities, and installer errors may require a retry. WinProvision does not force-restart or terminate installers.

Selections and history are stored in `%LOCALAPPDATA%\WinProvision\Apps\selection.json`. Each run has its own request, progress, exit codes, and package logs. Account data and passwords are not copied into these records.

## Validation

The project validates XML generation, XAML loading, search and category filtering, preserved selections, and mocked worker scenarios. Real first-login installation still requires a clean Windows 10 and Windows 11 validation run with network, UAC, WinGet registration, offline recovery, and shortcut reopening.

## Offline icons

Application icons are bundled as vector data in the executable and answer file. No internet connection is required to display them. Source files and license information are in `src/WinProvision/Assets/AppIcons`.

Version 0.5.0 uses category buttons matching the Builder settings navigation. The Builder logo is bundled in the executable; its source is `src/WinProvision/Assets/Logo.xaml` and its generation helper is `tools/Build-Logo.ps1`.
