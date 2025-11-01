# Simple Starter

- This is a tiny starter for **Cities: Skylines II** mods.
- It compiles cleanly, shows a **sample Options UI** (checkbox, dropdown, buttons, friendly text).
- hasoptional helper file that will wire in an **ECS DOTS system** with a gameplay gate and a one‑shot “after load” hook.
- this is not meant to be a full guide as we already have Guides and Wiki for CS2 modding.

## :bulb: Want a working mod right now?
- ⚡ [Quick Start Guide](./QuickStart.md) — Quick Start sample mod in 15 minutes.

---

## :books: What you get
- **Mod.cs** entry that sets up logging, settings, and a test system.
- **Settings UI** with real Game.Settings attributes (checkbox, dropdown, buttons, multiline text).
- **LocaleEN.cs** so you keep strings out of code.
- **ExampleSystem.cs** with a minimal **Unity.Entities GameSystemBase** pattern:
  - Gate to **GameMode.Game** only
  - Optional **ApplyOnceAfterLoad()** example
- **.editorconfig** and **.gitignore** tuned for CS2 & VS2022.
- **MIT license** ready.

---

## Folder Tree
```text
YourMod/
├─ Locale/
│  └─ LocaleEN.cs
├─ Settings/
│  └─ Setting.cs
├─ Systems/
│  └─ ExampleSystem.cs
├─ Mod.cs
├─ YourMod.csproj
├─ README.md
├─ .editorconfig
├─ .gitignore
└─ .gitattributes
```

---

## Rename it (fast)
1. Rename the folder `YourMod` → `MyCoolMod`
2. Edit `YourMod.csproj` → set `<RootNamespace>MyCoolMod</RootNamespace>`
3. Replace namespace **YourMod** → **MyCoolMod** across the solution
4. Update `[FileLocation("ModsSettings/MyCoolMod/MyCoolMod")]` in `Setting.cs`
5. Update `Name` and `VersionShort` in `Mod.cs`

Build → copy happens via the CS2 toolchain (`CSII_TOOLPATH`).

---

## Build & test
- Build in **Release**
- Start CS2 → Options → Mods → *Your mod name*
- You should see a small UI with a checkbox, a dropdown, buttons, and a friendly message.
- Check the mod log here:
  `%LOCALAPPDATA%\Colossal Order\Cities Skylines II\logs\MyCoolMod.log`

---

## Make it yours
- Use **Settings.cs** to add more toggles/sliders/dropdowns.
- Put your logic in new **Systems/** classes.
- Add more **LocaleXX.cs** files for translations, then register them in `Mod.cs`.

> Tip: if you don't like usings **inside** namespaces, edit the .editorconfig

---

## License
MIT — do what you want, just keep the copyright header in the LICENSE file with the fork repo. Then your choice, add your username or real name to the top above River-Mochi.

Have fun & share cool stuff with the community! 🎈

---

### 📘 More Resources

- ⚡ [Quick Start Guide](./QuickStart.md) — Quick step-by-step setup & build for Sample working Mod in 15 minutes.
- 🧠 [Detailed Modding Guide](./docs/DETAILED_README.md) — full breakdown of systems, settings, and localization in this repo.
- [Modding Wiki here](https://cs2.paradoxwikis.com/Options_UI)
- **Support / feedback:** https://discord.gg/HTav7ARPs2
  
---
