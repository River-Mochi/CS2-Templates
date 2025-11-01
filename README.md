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
- **Modding Toolchain** installed from in-game (explained more later)
  - Environment variable **`CSII_TOOLPATH`** pointing to the Toolchain folder  (default is usually correct nothing you need to do)
  - the `.csproj` file imports `Mod.props` / `Mod.targets` from there.
  - don't worry, it's given to you as part of the Colossal templates and works if you never changed default locations.
  - do not alter these two files and for the most part your mod will compile.

> If you don’t have the toolchain, install it FIRST via the in-game Options > Mods before doing anything else.

## Folder Layout

```text
YourMod/
├─ YourMod.csproj
├─ Mod.cs
├─ GatedSystem.cs             # optional helper (gameplay gating + one-shot hook)
├─ Systems/
│  └─ ExampleSystem.cs        # sample system that inherits the helper
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

Register it in `Mod.OnLoad`:
`updateSystem.UpdateAt<YourMod.Systems.ExampleSystem>(SystemUpdatePhase.MainLoop);`

Localization

This template registers LocaleEN.cs (English). Add more locales the same way:
Example, in Mod.cs `OnLoad`
`TryAddLocale("fr-FR", new LocaleFR(s_Settings));`

In LocaleEN.cs: write locale strings using the Setting helper methods, e.g.:
```csharp
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableFeature)), "Enable Feature" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableFeature)),  "Turns the feature on or off." },
```

## Example: A System That only runs in active Gameplay

The template includes `GatedSystemBase.cs` helper. Inherit from it to restrict a system to gameplay and (optionally) do something once after a city loads:

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

## “One-shot after load” Helper

Think of two kinds of code:
- Every-frame code: runs constantly while you’re in a city (like Unity’s Update()).
- One-time code: runs once right after a city finishes loading, then stops.
    - Get this by overriding OnGameLoadingComplete().

Timeline (mental model)
Mod that runs one-time after load.

1. **Mod.OnLoad** — your mod is loaded in the menu (no city yet).
2. **OnGamePreload** — loading screen (stuff is still spawning).
3. **OnGameLoadingComplete** — city ready → run your one-shot helper setup.
4. **OnUpdate** — ticks every frame while playing.

That’s all “one-shot after load” means: a safe place to run setup exactly once per city load, instead of doing it repeatedly in the per-frame loop.


### Style & Conventions
- Private instance fields: `m_FieldName`
- Private static fields: `s_FieldName`
- Constants: `kName`
- Put `using` directives inside the namespace block (optional, but many Colossal game files do this).
- .editorconfig enforces these naming rules (remove or change it if you like different styles)

### Troubleshooting

- Toolchain not found: Check csproj file, Ensure `CSII_TOOLPATH` points to the Modding Toolchain directory.
- Options not showing: Verify Setting.cs `RegisterInOptionsUI()` is called and the mod is enabled in-game.
- Locales not applied: in Mod.cs, make sure you called `TryAddLocale(localeId, source)` **before** `RegisterInOptionsUI()`.


### License
MIT — see [LICENSE](https://github.com/River-Mochi/CS2-Templates/blob/main/LICENSE) file. Add your name to the year you started contributing. MIT License means this is free for anyone to use and modify in their own fork later, just keep copy of the license file in every fork of this mod.
Either keep the license file as it is written or add your name or username to to the toplike below (in your own fork of this repo). 
This is good to do for any Mod because if you stop playing, then other people can fork the mod, patch it in the future keep it working for all players in the future.

Example at the top of the License file:

``` MIT License
Copyright (c) 2025 YourName (modifications)    
Copyright (c) 2025 River-Mochi original repo   

Permission is hereby granted, free of charge, to any person obtaining a copy...
```
- Adding your username (or name) is optional but you have to keep the rest of the License file as is with each fork of this repo per MIT.


----
## 1. Clone & open in VS2022

VS2022: **File → Clone Repository… → paste your repo URL → Clone**

---

## 2. Open the project

In Visual Studio, open `YourMod/YourMod.csproj`  
*(There’s no `.sln` on purpose; a single-project `.csproj` is fine.)*

---

## One-time setup (toolchain)

Install the **CS2 Modding Toolchain** (if you haven’t yet) and set the user environment variable:

| Variable name | Value |
|----------------|--------|
| `CSII_TOOLPATH` | Folder where the Toolchain put `Mod.props` and `Mod.targets` (e.g. `C:\Colossal\CS2\ModdingToolchain`) |

After setting the variable, **restart Visual Studio**.

---

## 3. First Build

**Restore & build**  
Visual Studio will auto-restore dependencies. Build with **Ctrl + Shift + B**.

You should see the game/Unity references resolve via `Mod.props` and `Mod.targets`.

---

## Rename it to your mod

### Project name & namespace

In **Solution Explorer**, rename the folder `YourMod` to your real mod name (optional).  
Right-click the project → **Edit Project File** → change anything needed in the  YourMod.csproj file.

