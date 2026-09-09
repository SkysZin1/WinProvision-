# Applications: before or after first boot

Open **Applications…** in USB Builder:

- **Choose after first boot** (default): the new Windows opens the picker with no preselected apps. The user selects apps and clicks Install.
- **Choose now**: opens the same category/icon catalog in selection-only mode. Save the choices, then save application settings. This authorizes automatic installation after the new Windows signs in. Nothing is installed on the Builder PC.

The review page lists the automatic apps. JSON profiles preserve both the mode and selected package IDs. Changing Windows presets keeps the app selection. The app plan is embedded into the answer file; no internet is required to carry the plan or display icons.

After first sign-in, the normal user-session picker starts the authorized queue automatically, records that an automatic attempt was made, and displays results. It does not keep automatically retrying failed installs on subsequent launches. The desktop shortcut allows manual retries. Existing users who already attempted automatic installation keep their saved results.

Use **Recommended** for automated Windows OOBE and one-time sign-in. Installation target disk selection is still manual. Automatic app installation does not bypass UAC, installer requirements, internet availability or errors. It cannot guarantee a time to completion or zero prompts from every installer.

Validation: profile round-trip, invalid selections, XML plan embedding, script parsing, category/filter UI loading and six mocked worker scenarios.
