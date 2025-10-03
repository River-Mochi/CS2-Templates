# CS2-Templates
CS2 templates: basic files to start a new mod

## Your Mod Name

Starter template for a Cities: Skylines II mod using the built-in Modding Toolchain.

- Clean `Mod.cs` entry point
- Optional `GatedSystemBase` for **gameplay gating** + **one-shot after-load hooks**
- `Settings/Setting.cs` wired for Options UI
- `Locale/LocaleEN.cs` ready for localization
- Example system under `Systems/ExampleSystem.cs`
- Project style aligned with Colossal conventions (`.editorconfig`)

## Requirements

- Visual Studio 2022 (or Rider)
- Cities: Skylines II installed
- **Modding Toolchain** installed (Colossal)
- Environment variable **`CSII_TOOLPATH`** pointing to the Toolchain folder  
  (the `.csproj` imports `Mod.props` / `Mod.targets` from there).

> If you don’t have the toolchain, install it via the official CS2 modding docs.

## Folder Layout

```text
YourMod/
├─ YourMod.csproj
├─ Mod.cs
├─ GatedSystemBase.cs             # optional helper (gameplay gating + one-shot hook)
├─ Systems/
│  └─ ExampleSystem.cs            # sample system that inherits the helper
├─ Settings/
│  └─ Setting.cs
├─ Locale/
│  └─ LocaleEN.cs
├─ .editorconfig
├─ .gitattributes
├─ .gitignore
├─ LICENSE
└─ README.md
```

> For small mods, keeping it flat like this is easiest. If your project grows, feel free to add `/docs`, `/tools`, etc.


## Getting Started

1. **Clone** this repo (or "Use this template" on GitHub).
2. **Rename** the namespace `YourMod` to your mod's namespace (global Search & Replace).
3. Update `Mod.cs`:
   - `Name` and `VersionShort`
   - (Optional) enable any systems with `updateSystem.UpdateAt<YourSystem>(SystemUpdatePhase.X)`
4. Update `Settings/Setting.cs`:
   - Add your checkboxes, dropdowns, buttons (use the attributes shown in the file).
5. Update `Locale/LocaleEN.cs`:
   - Provide labels/descriptions for your options using the built-in localization keys.
6. **Build** the solution:
   - If `CSII_TOOLPATH` is set correctly, the toolchain will handle references & publishing.
7. **Launch the game**, enable your mod in **Content Manager**, and open **Options** to see your UI.

## Example: A System That Only Runs in Gameplay

The template includes `GatedSystemBase.cs`. Inherit from it to restrict a system to gameplay and (optionally) do something once after a city loads:

```csharp
namespace YourMod.Systems
{
    using Game;
    using Unity.Entities;

    public sealed class ExampleSystem : GatedSystemBase
    {
        // Default kAllowedModes is Game only. Override if needed:
        // protected override GameMode kAllowedModes => GameMode.Game | GameMode.MapEditor;

        protected override void OnUpdate()
        {
            if (!Enabled) return;
            // per-frame work…
        }

        // Optional: run once when the city is ready after loading.
        // protected override void OnAfterLoadOnce()
        // {
        //     // one-shot setup…
        // }
    }
}
```

Register it in `Mod.OnLoad`:
updateSystem.UpdateAt<YourMod.Systems.ExampleSystem>(SystemUpdatePhase.MainLoop);

Localization

This template registers LocaleEN (English). Add more locales the same way:
Mod.OnLoad
`TryAddLocale("fr-FR", new LocaleFR(s_Settings));`

Write locale strings using the Setting helper methods, e.g.:
```csharp
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableFeature)), "Enable Feature" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableFeature)),  "Turns the feature on or off." },
```

### Style & Conventions
- Private instance fields: m_FieldName
- Private static fields: s_FieldName
- Constants: kName
- Put using directives inside the namespace block
- .editorconfig enforces these rules

### Troubleshooting

- Toolchain not found: Check csproj file, Ensure `CSII_TOOLPATH` points to the Modding Toolchain directory.
- Options not showing: Verify Setting.cs `RegisterInOptionsUI()` is called and the mod is enabled in-game.
- Locales not applied: in Mod.cs, make sure you called `TryAddLocale(localeId, source)` **before** `RegisterInOptionsUI()`.


### License
MIT — see [LICENSE](https://github.com/River-Mochi/CS2-Templates/blob/main/LICENSE) file. Add your name to the year you started contributing. MIT License means this is free to use, just keep copy of the license file in every fork of this mod.
Either keep the license file as it is written or add your name to it like below (in your own fork of this repo). 

Example at the top of the License file:

``` MIT License
Copyright (c) 2025 RiverMochi
Copyright (c) 2025 YourName (modifications)     

Permission is hereby granted, free of charge, to any person obtaining a copy...
```
- Adding your username (or name) is optional but you have to keep the rest of the License file as is with each fork of this repo per MIT.


