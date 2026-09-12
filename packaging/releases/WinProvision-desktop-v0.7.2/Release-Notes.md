# WinProvision 0.7.2

This release improves Windows setup, first-login configuration and application startup.

- Preserve embedded scripts and accented text during Windows Setup, with shorter loaders and improved error reporting.
- Apply general user preferences during setup and the optional classic context menu to the actual account at first login.
- Open App Picker without an attached terminal window.
- Add a bilingual activation confirmation window, an internet requirement notice and a dedicated desktop shortcut. The interactive terminal opens only after confirmation.
- Preserve the bundled CMD script's required line endings when creating media.
- Set the Recommended account's initial password to `123`, editable in the Builder.
- Update the ChatGPT catalog entry to the current desktop Store package.

The desktop package requires the .NET 8 Desktop Runtime. Windows installation disk selection remains manual. Compatibility details are displayed for each setting in the Builder.

Download `WinProvision-desktop-v0.7.2.zip`, extract it and run `WinProvision.exe`. Keep all extracted files together. A SHA-256 checksum accompanies the archive.
