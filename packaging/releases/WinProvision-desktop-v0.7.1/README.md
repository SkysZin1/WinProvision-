# WinProvision USB Builder

Native Windows desktop application (C# / WPF) for creating a personalized Windows installation USB. Version 0.7.1 expands the bilingual app catalog and adds installation summaries, retrying failed apps and pre-installation readiness checks. Full installation/boot testing is still pending.

## Open the app

Version **0.7.1** includes 36 apps, installation summaries, retrying only failed apps, and internet/App Installer readiness checks.

Download and extract **WinProvision-desktop-v0.7.1.zip** from [Releases](https://github.com/SkysZin1/WinProvision-/releases/tag/v0.7.1), then open `WinProvision.exe`. The repository copy is under `packaging\releases\WinProvision-desktop-v0.7.1`. After building from source, use `src\WinProvision\bin\Release\net8.0-windows\WinProvision.exe`. Keep the executable, DLLs, runtime configuration, `Assets` and `Scripts` folders together. The Builder requires the **.NET 8 Desktop Runtime**. The installed target Windows uses built-in Windows PowerShell/WPF and does not need .NET 8.

Choose **Português (Brasil)** or **English** using **Idioma / Language** in either app. The Builder remembers the choice and saves it in exported profiles and the installation media. The picker inherits that language on first boot and can remember a different choice on the installed PC. Changing the interface language preserves selections and does not change the Windows installation language, keyboard or time zone. No terminal is needed; administrator permission is requested when starting USB creation.

## Included

- ISO selection and explicit physical USB selection, including volume letters.
- Windows 10/11 target; edition selection; x64 UEFI media.
- Step 2 starts with Default, Recommended and Custom installation profiles. Custom opens the complete settings catalog.
- Identity, language, keyboard and time zone configuration.
- More than 50 optional tweaks with descriptions and OS compatibility labels; incompatible options are disabled and excluded from XML.
- Search and categories, JSON profile import/export, redacted XML preview.
- Review, backup acknowledgment and a typed `APAGAR DISCO n` / `ERASE DISK n` confirmation matching the selected interface language.
- Elevated writer: source/target validation, FAT32 formatting, Windows file copy, large WIM splitting, answer-file generation, progress, verification and logs.
- Bilingual app picker at first sign-in: 36 optional apps, search, categories, per-app progress, retry, logs and postponement. Use Applications in the Builder to select and authorize apps before installation, or choose after first boot. The standalone picker preview button has been removed; application selection remains available.

## Workflow

1. Download an unmodified Microsoft Windows ISO.
2. Open WinProvision, choose the ISO, Windows version and USB device.
3. Choose Default, Recommended or Custom. Recommended asks for the account password and regional details in the Builder, then generates automated OOBE and one-time sign-in. Default leaves optional settings untouched. Custom opens the full catalog.
4. Review the settings and destination. Confirm data deletion and type the displayed phrase.
5. Start creation and approve the Windows administrator prompt. The writer validates the ISO **before** erasing the USB.
6. Wait for completion, safely eject the media and boot the target PC in UEFI mode.
7. Choose the Windows installation disk manually in Setup.

## Current boundaries

### Installation profiles

Default and Recommended preserve the Windows version and edition selected in step 1. Default leaves accounts, region and login to Setup and enables no optional tweaks. Recommended automates the post-installation setup to reach the desktop:

- Creates a local administrator (suggested name `User`, editable) with the password entered in the Builder.
- Configures language, keyboard and time zone before installation; suggestions are derived from the builder PC and must be confirmed for the target PC/ISO.
- Supplies locale settings in both Windows PE and OOBE.
- Hides online-account, wireless, OEM registration and license screens; accepts the Setup EULA and sets `ProtectYourPC` to 3.
- Signs in automatically once, then disables automatic logon and removes its registry password value.
- Includes these six everyday adjustments:

- Show file extensions.
- Open File Explorer to This PC.
- Disable advertising ID.
- Disable tailored experiences.
- Disable Edge Startup Boost.
- Disable Edge background mode.

Recommended keeps Defender, SmartScreen, UAC, updates, encryption, appearance and power settings at their defaults, while using the specified OOBE privacy choices. It does not bypass hardware checks. Edge may take longer to launch and background extensions will stop when it closes. Disk and partition selection remains manual, as required by the selected installation profile. Edition/product-key prompts can still appear before disk selection; activation is separate. The configured OOBE is intended to run unattended after installation, but Windows builds, drivers or OEM screens can introduce extra prompts. Real installation testing is still required on Windows 10 and 11. No `SkipMachineOOBE` or `SkipUserOOBE` is generated. References: [Microsoft: automate OOBE](https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/automate-oobe), [one-time logon cleanup](https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/unattend/microsoft-windows-shell-setup-autologon-logoncount), [Windows privacy controls](https://learn.microsoft.com/en-gb/windows/privacy/windows-privacy-compliance-guide), [Edge StartupBoostEnabled](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/startupboostenabled), [Edge BackgroundModeEnabled](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/backgroundmodeenabled).

The selected profile's changes are listed before review. **Customize this profile** opens its exact settings in the existing editor. The **Custom** card restores a prior custom draft when available; otherwise it starts from the selected preset. Switching to Default or Recommended preserves a custom draft in memory for that session, including its unsaved password; only the selected configuration is exported, always without passwords. Old profile files load as Custom. Built-in profiles edited externally are relabelled Custom when their settings no longer match.

### Media and compatibility

- Hosts: Windows 10/11 with .NET 8 Desktop Runtime. Images: Windows 10/11 x64, clean-install media with `install.wim` or a FAT32-sized `install.esd`.
- UEFI / GPT / FAT32 only. No legacy BIOS, ARM64 or x86 image support in this preview.
- The boot partition is at most 30 GiB. On larger USB devices, remaining space is unallocated. All existing USB partitions are erased.
- Large WIM files are split with DISM. Large ESD files and ISOs with pre-split SWM images are rejected before erasure.
- ISO version, image architecture, edition, language and required boot files are checked. These checks do **not** authenticate an ISO against Microsoft's original hash.
- ISOs containing root answer files or `$OEM$` customizations are rejected to prevent unexpected script interactions.
- Windows installation target selection is intentionally interactive; this version never emits target-disk partitioning commands in the answer file.
- No permanent Windows Update, antivirus or UAC disabling, arbitrary-script editor, Wi-Fi credential import, wallpaper picker, custom partition editor or full generator parity yet. The catalog can expand without redesigning the app.
- A Windows-version label denotes intended applicability, not a certification on every build/edition. Tweaks and OOBE need VM validation. Some policies are edition-dependent, and hardware bypasses are experimental.
- Progress is stage-based. Closing is blocked during writing; stopping the elevated worker externally can leave incomplete media. No rollback after formatting.

## Credentials and logs

Local account passwords are never included in JSON profiles or XML previews. Actual media contains the password in plaintext when an account is configured. Keep that media private. Working jobs have ACLs restricted to the current user, administrators and SYSTEM; the local answer-file copy is removed when the worker finishes. Setup cleanup is attempted at the first logon and requires an administrator; standard-user first logon may leave Panther answer files behind. Cleanup does not erase the USB copy. A crash may leave working files; inspect `%LOCALAPPDATA%\WinProvision\Jobs` before sharing a machine or logs. Use a password and an administrator account for one-time automatic logon.

Configuration scripts are embedded in the XML and extracted to `%WINDIR%\Setup\Scripts\WinProvision`. Per-option successes/failures are recorded in `Configure.log`. Per-user settings edit the default user's offline registry during `specialize`; effects are intended for subsequently created profiles. No generated script is executed on the builder PC.

Writer jobs/logs: `%LOCALAPPDATA%\WinProvision\Jobs\<job-id>`. The app exposes an **Open build log** button after an attempt.

## Build and non-destructive tests

```powershell
dotnet build .\src\WinProvision\WinProvision.csproj -c Release
dotnet .\src\WinProvision\bin\Release\net8.0-windows\WinProvision.dll --self-test
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Writer-Guards.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Writer-Layout.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Apps-Worker.Tests.ps1 -UiLanguage pt-BR
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Apps-Worker.Tests.ps1 -UiLanguage en-US
powershell -NoProfile -STA -ExecutionPolicy Bypass -File .\src\WinProvision\Scripts\Provisioner.ps1 -ValidateUi
```

Self-tests check XML escaping, credential omission, OS filtering, account validation, language switching, selection preservation and embedded offline translations. UI checks render both languages without showing or installing anything. Guard tests mock disk cmdlets and reject system disks, non-USB disks, changed devices, source files on the destination and missing ISOs. No test writes a physical USB. Generated test files use synthetic data only.

Before a stable release, run a disposable-USB creation test and full installation tests for both Windows versions. Check UEFI boot, WIM split installation, OOBE, local accounts, first-login cleanup, tweak logs, power failure handling and a fresh host without the SDK.

Layout regression tests simulate RAW disks and disks that still report GPT/MBR after clearing, remaining partitions, device identity changes, initialization failures and unknown styles. The writer refreshes and revalidates the device after clearing, initializes only RAW disks, and requires an empty layout before converting an MBR disk to GPT. These simulations do not replace testing on a physical USB.

## References

- [Microsoft: installing Windows from a USB flash drive](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/install-windows-from-a-usb-flash-drive?view=windows-11)
- [Microsoft: splitting WIM images for a single USB drive](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/winpe--use-a-single-usb-key-for-winpe-and-a-wim-file---wim?view=windows-11)
- [Microsoft: unattended answer files](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/update-windows-settings-and-scripts-create-your-own-answer-file-sxs?view=windows-11)
- [Schneegans generator](https://schneegans.de/windows/unattend-generator/) and [sample scripts](https://schneegans.de/windows/unattend-generator/samples/) informed the feature catalog. This project is independent and does not bundle Rufus or the generator.

See docs/Expanded-App-Catalog.md for the new packages, official Store sources, development prerequisites and NVIDIA installer behavior.




## License

WinProvision is released under the [MIT License](LICENSE). Third-party icon notices are documented under src/WinProvision/Assets/AppIcons.


## Built-in app removal

Recommended removes Solitaire Collection, Microsoft News, Weather, Get Help, Feedback Hub and Microsoft To Do when provisioned in the Windows image.
Custom also offers Camera, Sound Recorder, Media Player / Groove Music, Movies & TV, Photos, Clock / Alarms, Calculator, Sticky Notes, Tips / Get Started and OneNote (Store app), alongside the existing removal options.
These optional removals are not preselected by Recommended. Package availability depends on the Windows image and version; missing packages are skipped. Removal targets provisioned Store packages for new accounts, not desktop editions of the same apps.
