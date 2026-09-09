# App picker icons

Source: [Simple Icons 11.15.0](https://github.com/simple-icons/simple-icons/tree/11.15.0/icons).
The original SVGs are included here, with the upstream CC0 license in LICENSE.md.
Brand names and logos identify their respective applications; WinProvision is independent of their owners.

Run `Scripts/Bundle-AppIcons.ps1` after changing the SVGs or color mapping. It embeds the vector paths in `Scripts/Provisioner.ps1`, which is itself embedded in the Builder and generated answer file. No runtime icon downloads are needed.

Icons use a single color and are rendered as WPF DrawingImage objects. Selection files store only Id, Selected and Status, never drawing objects.

archive.svg and settings.svg are original generic WinProvision symbols. The expanded catalog contains 30 icons. Additional entries are defined in Assets/AdditionalApps.json.

