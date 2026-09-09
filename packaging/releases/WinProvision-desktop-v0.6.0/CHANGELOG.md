# Changelog

## 0.6.0

- Added selectable Brazilian Portuguese and English to USB Builder and App Picker, including categories, descriptions, compatibility labels, progress, errors and USB confirmation.
- Preserved account settings, app selections and internal profile/package IDs when switching languages. The picker inherits the Builder language and allows its own saved choice.
- Embedded offline translation resources alongside the first-login picker.
- Recommended now removes Solitaire Collection, Microsoft News, Weather, Get Help, Feedback Hub and Microsoft To Do when present.
- Added ten optional built-in app removals to Custom.
- Removed the standalone App Picker preview button while retaining the pre-installation app selection flow.
- Validated both languages, generated XML, profile compatibility, UI layouts and mocked USB/application workers.

## 0.5.0

- Added the "Activate Windows" setup option to run activation after first login.
- Added FirstLogonCommands support for a bundled activation script alongside the app picker flow.
- Kept the activation script inside the project scripts folder instead of exposing it at the repository root.
- Continued the existing USB builder validation and XML generation checks.

## 0.4.0

- Categorized English application picker with bundled icons.
- Added automatic and after-first-boot application selection flows.
- Added expanded application catalog and Windows 10/11 setup profiles.
- Added USB builder validation and release packaging.
