# Expanded app catalog — v0.5.0

The picker now includes 30 apps across 11 categories. Choices work both in the Builder and after first boot. Nothing is preselected by default.

## Added applications

- Development: GitHub Desktop, CMake, Postman, Windows Terminal.
- Languages & SDKs: Python 3.14, Node.js LTS, .NET SDK 10, Java JDK 21 (Temurin), Go, Rust/rustup.
- Utilities: WinRAR (trial; license required after evaluation).
- Communication: official WhatsApp from Microsoft Store.
- Gaming: Epic Games Store / Epic Games Launcher.
- AI assistants: official ChatGPT from Microsoft Store and Claude from Anthropic.
- Customization: Windhawk. Mods must be chosen separately.
- Drivers: NVIDIA app and Intel Driver & Support Assistant.

Existing apps remain available: Chrome, Firefox, Brave, 7-Zip, VLC, Spotify, Discord, Steam, LibreOffice, Visual Studio Code, Notepad++ and Git.

## Installation sources

Regular packages use the Microsoft community WinGet source. WhatsApp uses Store ID `9NKSQGP7F2NH`; ChatGPT uses the official Store ID `9NT1R1C2HH7J`. WinGet selects the source from the bundled catalog, never from a user-supplied command. No unofficial ChatGPT wrapper is included.

The NVIDIA app was absent from the WinGet repository when checked. It uses the official NVIDIA download URL for version 11.0.9.251 instead. Before execution, Windows must report a valid Authenticode signature whose subject is NVIDIA Corporation. Invalid signatures stop installation. Its vendor installer is interactive and may show UAC or other prompts; it is not guaranteed unattended. This URL is pinned and should be reviewed when updating the catalog.

## Requirements and boundaries

- Installing a driver assistant does not mean every driver has been updated. NVIDIA requires a supported NVIDIA GPU; Intel DSA covers supported Intel hardware. Driver scans and update choices happen inside those applications.
- For other manufacturers, use Windows Update or the computer/motherboard vendor support site. The picker does not install generic third-party driver updaters.
- Rust native builds may require Visual Studio C++ Build Tools. CMake does not include a compiler. Python libraries, Node packages and other project dependencies are separate.
- Package and SDK releases have their own Windows build requirements. New releases are selected by the package source; availability of a catalog entry does not certify every Windows 10/11 build.
- Microsoft Store availability, accounts, regional policies and internet access may affect installation. Product accounts, subscriptions and game purchases are not configured automatically.

## Maintenance and validation

New catalog entries live in `Assets/AdditionalApps.json`, embedded in the Builder. Run `Scripts/Bundle-AppIcons.ps1` after changes to update the matching PowerShell catalog and offline vector data. The two original generic icons, archive and settings, are WinProvision assets; other icon credits are under `Assets/AppIcons`.

Checked package identifiers against their installer manifests in the official Microsoft repository. Tests cover 30-icon loading, category filters, saved profiles, embedded plans, Store source selection and rejection of an unsigned NVIDIA installer, with all installation/download operations mocked.

Sources: [Microsoft WinGet manifests](https://github.com/microsoft/winget-pkgs), [official ChatGPT deployment command](https://help.openai.com/en/articles/9982051), [WhatsApp Store listing](https://apps.microsoft.com/detail/9nksqgp7f2nh), [NVIDIA app](https://www.nvidia.com/en-us/software/nvidia-app/), [Intel Driver & Support Assistant](https://www.intel.com/content/www/us/en/support/detect.html).
