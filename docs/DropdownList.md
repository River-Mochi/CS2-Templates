# CO Dropdowns in CS2 — Two Correct Methods (Single-File Cheatsheet)

> **Core rule:** You have **two** valid patterns. Pick **one** per property.
>
> - **Method A — Simple Enum:** Property is an `enum`. **Do not** use `[SettingsUIDropdown]`.
> - **Method B — Custom Dropdown:** Property is **not** an enum (e.g., `string`, `int`). Add `[SettingsUIDropdown(...)]` and return `DropdownItem<T>[]` from a provider method.
>
> Mixing `enum` **and** `[SettingsUIDropdown]` on the **same** property produces a greyed-out (disabled) control.

---

## Table of Contents
- [Why dropdowns get greyed out](#why-dropdowns-get-greyed-out)
- [Method A — Simple Enum (no attribute)](#method-a--simple-enum-no-attribute)
- [Method B — Custom Dropdown (attribute + provider)](#method-b--custom-dropdown-attribute--provider)
- [Localization patterns](#localization-patterns)
- [Common gotchas](#common-gotchas)
- [Minimal working example (both methods together)](#minimal-working-example-both-methods-together)
- [Troubleshooting checklist](#troubleshooting-checklist)

---

## Why dropdowns get greyed out

CS2 exposes two separate mechanisms:

1. **Simple Enum Dropdown**  
   - Property’s type is an `enum`.  
   - UI builds the items automatically from the enum members.  
   - **You must NOT add** `[SettingsUIDropdown]` to this property.

2. **Custom Dropdown**  
   - Property’s type is **not** an enum (commonly `string` or `int`).  
   - You **do** add `[SettingsUIDropdown(typeof(Setting), nameof(GetItems))]`.  
   - You implement `DropdownItem<T>[] GetItems()` to provide values and display text.

**Greyed-out control happens when you combine enum + `[SettingsUIDropdown]`.** Remove the attribute (Method A), or change the property to a non-enum and implement a provider (Method B).

---

## Method A — Simple Enum (no attribute)

**Use when:** static options map 1:1 to an enum. No dynamic list needed.
**Pros:** simplest wiring, less code.  
**Cons:** items are fixed; item display is the enum member name (unless you localize the *option label/description*, not the individual enum members).
 You don’t control per-item localized names (except by switching to Method B).

### Setting.cs (excerpt)

```csharp
namespace YourModNS
{
    using Colossal.IO.AssetDatabase;
    using Game.Modding;
    using Game.Settings;

    public enum MyColorPreset
    {
        MediumGray = 0,
        MutedPurple = 1,
        GameDefault = 2,
        None = 3,
    }

    [FileLocation("ModsSettings/YourMod/YourMod")]
    public sealed class Setting : ModSetting
    {
        public const string kTabMain   = "Main";
        public const string kGroupLook = "Look";

        public Setting(IMod mod) : base(mod) { }

        // NO [SettingsUIDropdown] here because this is an enum.
        [SettingsUISection(kTabMain, kGroupLook)]
        public MyColorPreset ColorPreset { get; set; } = MyColorPreset.MediumGray;

        public override void SetDefaults()
        {
            ColorPreset = MyColorPreset.MediumGray;
        }
    }
}


---
namespace YourModNS
{
    using System.Collections.Generic;
    using Colossal; // IDictionarySource

    public sealed class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Your Mod" },
                { m_Setting.GetOptionTabLocaleID(Setting.kTabMain), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGroupLook), "Look" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ColorPreset)), "Color preset" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ColorPreset)),  "Pick a color preset." },
            };
        }

        public void Unload() { }
    }
}

---

namespace YourModNS
{
    using System.Collections.Generic;
    using Colossal;

    public sealed class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Your Mod" },
                { m_Setting.GetOptionTabLocaleID(Setting.kTabMain), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGroupLook), "Look" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ColorPreset)), "Color preset" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ColorPreset)),  "Pick a color preset." },
            };
        }

        public void Unload() { }
    }
}


If friendly names  are needed for each enum item in the UI itself, prefer Method B (custom dropdown), where you control each item’s displayName.
  Method B — Custom Dropdown (attribute + provider)

Use when: you want localized item strings, dynamic lists, or values that aren’t simple enum names.

Key idea: The UI property is not an enum (often string or int). You supply items via a provider method that returns DropdownItem<T>[]. Internally, you can still keep an enum and map UIValue ↔ enum in the property’s getter/setter.

Setting.cs (with enum mapping + localized item keys)
  namespace YourModNS
{
    using Colossal.IO.AssetDatabase;
    using Colossal.Localization;            // LocalizedString
    using Game.Modding;
    using Game.Settings;
    using Game.UI.Widgets;

    public enum MyColorPreset
    {
        MediumGray = 0,
        MutedPurple = 1,
        GameDefault = 2,
        None = 3,
    }

    [FileLocation("ModsSettings/YourMod/YourMod")]
    public sealed class Setting : ModSetting
    {
        public const string kTabMain   = "Main";
        public const string kGroupLook = "Look";

        // Runtime enum the rest of the mod consumes:
        public MyColorPreset ColorPreset { get; private set; } = MyColorPreset.MediumGray;

        // UI proxy bound to the dropdown (custom pattern):
        [SettingsUISection(kTabMain, kGroupLook)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetColorItems))]
        public string ColorPresetUI
        {
            get => ToKey(ColorPreset);
            set
            {
                ColorPreset = FromKey(value);
                // Optionally: notify systems here, e.g. MySystem.RequestApplyFromSettings(ColorPreset);
            }
        }

        // The dropdown items (value = a stable key; displayName = localized string id)
        public DropdownItem<string>[] GetColorItems() => new[]
        {
            new DropdownItem<string> { value = "YourMod.Color.MediumGray",  displayName = LocalizedString.Id("YourMod.Color.MediumGray")  },
            new DropdownItem<string> { value = "YourMod.Color.MutedPurple", displayName = LocalizedString.Id("YourMod.Color.MutedPurple") },
            new DropdownItem<string> { value = "YourMod.Color.GameDefault", displayName = LocalizedString.Id("YourMod.Color.GameDefault") },
            new DropdownItem<string> { value = "YourMod.Common.None",       displayName = LocalizedString.Id("YourMod.Common.None")       },
        };

        private static string ToKey(MyColorPreset p) => p switch
        {
            MyColorPreset.MediumGray  => "YourMod.Color.MediumGray",
            MyColorPreset.MutedPurple => "YourMod.Color.MutedPurple",
            MyColorPreset.GameDefault => "YourMod.Color.GameDefault",
            MyColorPreset.None        => "YourMod.Common.None",
            _                         => "YourMod.Color.MediumGray",
        };

        private static MyColorPreset FromKey(string key) => key switch
        {
            "YourMod.Color.MediumGray"  => MyColorPreset.MediumGray,
            "YourMod.Color.MutedPurple" => MyColorPreset.MutedPurple,
            "YourMod.Color.GameDefault" => MyColorPreset.GameDefault,
            "YourMod.Common.None"       => MyColorPreset.None,
            _                           => MyColorPreset.MediumGray,
        };

        public override void SetDefaults()
        {
            ColorPreset = MyColorPreset.MediumGray;
        }
    }
}


LocaleEN.cs (individual item keys live here)
  namespace YourModNS
{
    using System.Collections.Generic;
    using Colossal;

    public sealed class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Your Mod" },

                { m_Setting.GetOptionTabLocaleID(Setting.kTabMain), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGroupLook), "Look" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ColorPresetUI)), "Color preset" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ColorPresetUI)),  "Pick a color preset." },

                // Item labels:
                { "YourMod.Color.MediumGray",  "Medium Gray" },
                { "YourMod.Color.MutedPurple", "Muted Purple" },
                { "YourMod.Color.GameDefault", "Game Default (cyan)" },
                { "YourMod.Common.None",       "None" },
            };
        }

        public void Unload() { }
    }
}
You can use any T for DropdownItem<T> (e.g., string, int). Do not use an enum type with [SettingsUIDropdown]—that’s Method A.
Localization patterns

Option label/description (the text next to the control) always comes from:

m_Setting.GetOptionLabelLocaleID(nameof(Setting.YourProperty))

m_Setting.GetOptionDescLocaleID(nameof(Setting.YourProperty))

Custom dropdown item labels (Method B) are best provided as localized keys via LocalizedString.Id("Your.Key") so translators can add other languages without touching code.

Keep item keys stable (e.g., YourMod.Color.MediumGray) to avoid breaking saved choices.
  Localization patterns

Option label/description (the text next to the control) always comes from:

m_Setting.GetOptionLabelLocaleID(nameof(Setting.YourProperty))

m_Setting.GetOptionDescLocaleID(nameof(Setting.YourProperty))

Custom dropdown item labels (Method B) are best provided as localized keys via LocalizedString.Id("Your.Key") so translators can add other languages without touching code.

Keep item keys stable (e.g., YourMod.Color.MediumGray) to avoid breaking saved choices.

Common gotchas

Greyed-out dropdown:

You used [SettingsUIDropdown] on an enum property. Remove the attribute or change the property type to a non-enum and implement a provider.

Items show but are unselectable:

Your provider returns items whose value type doesn’t match the property type.

Locale text not showing:

Missing locale source registration, wrong keys, or you didn’t add the strings in Locale*.cs.

Two patterns on one property:

Don’t mix Method A and Method B on the same property. Pick one.

  ---
  Minimal working example (both methods together)
// Method A: simple enum
public enum Difficulty { Easy, Normal, Hard }

[SettingsUISection("Main", "Gameplay")]
public Difficulty GameDifficulty { get; set; } = Difficulty.Normal;

// Method B: custom dropdown (string)
[SettingsUISection("Main", "Look")]
[SettingsUIDropdown(typeof(Setting), nameof(GetThemeItems))]
public string Theme { get; set; } = "YourMod.Theme.Dark";

public DropdownItem<string>[] GetThemeItems() => new[]
{
    new DropdownItem<string> { value = "YourMod.Theme.Dark",  displayName = LocalizedString.Id("YourMod.Theme.Dark")  },
    new DropdownItem<string> { value = "YourMod.Theme.Light", displayName = LocalizedString.Id("YourMod.Theme.Light") },
};


Locale:

{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.GameDifficulty)), "Difficulty" },
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.Theme)),          "Theme" },

{ "YourMod.Theme.Dark",  "Dark" },
{ "YourMod.Theme.Light", "Light" },

Troubleshooting checklist

 Is the property an enum? → No attribute.

 Is the property not an enum? → Add [SettingsUIDropdown(typeof(Setting), nameof(GetItems))].

 Does DropdownItem<T>.value match the property type T?

 Are the locale keys used in LocalizedString.Id(...) present in Locale*.cs?

 Is your locale source added (e.g., GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(setting)))?

 No duplicate/overlapping patterns on the same property.

 Defaults in SetDefaults() match valid item values.

Copy this file into your repo (e.g., docs/CO-Dropdowns.md).
Next time, follow the two patterns above and you won’t get greyed-out controls.


  Two patterns on one property:

Don’t mix Method A and Method B on the same property. Pick one.
Minimal working example (both methods together)

// Method A: simple enum
public enum Difficulty { Easy, Normal, Hard }

[SettingsUISection("Main", "Gameplay")]
public Difficulty GameDifficulty { get; set; } = Difficulty.Normal;

// Method B: custom dropdown (string)
[SettingsUISection("Main", "Look")]
[SettingsUIDropdown(typeof(Setting), nameof(GetThemeItems))]
public string Theme { get; set; } = "YourMod.Theme.Dark";

public DropdownItem<string>[] GetThemeItems() => new[]
{
    new DropdownItem<string> { value = "YourMod.Theme.Dark",  displayName = LocalizedString.Id("YourMod.Theme.Dark")  },
    new DropdownItem<string> { value = "YourMod.Theme.Light", displayName = LocalizedString.Id("YourMod.Theme.Light") },
};

{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.GameDifficulty)), "Difficulty" },
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.Theme)),          "Theme" },

{ "YourMod.Theme.Dark",  "Dark" },
{ "YourMod.Theme.Light", "Light" },

---

// Method A: simple enum
public enum Difficulty { Easy, Normal, Hard }

[SettingsUISection("Main", "Gameplay")]
public Difficulty GameDifficulty { get; set; } = Difficulty.Normal;

// Method B: custom dropdown (string)
[SettingsUISection("Main", "Look")]
[SettingsUIDropdown(typeof(Setting), nameof(GetThemeItems))]
public string Theme { get; set; } = "YourMod.Theme.Dark";

public DropdownItem<string>[] GetThemeItems() => new[]
{
    new DropdownItem<string> { value = "YourMod.Theme.Dark",  displayName = LocalizedString.Id("YourMod.Theme.Dark")  },
    new DropdownItem<string> { value = "YourMod.Theme.Light", displayName = LocalizedString.Id("YourMod.Theme.Light") },
};

---

  Troubleshooting checklist

 Is the property an enum? → No attribute.

 Is the property not an enum? → Add [SettingsUIDropdown(typeof(Setting), nameof(GetItems))].

 Does DropdownItem<T>.value match the property type T?

 Are the locale keys used in LocalizedString.Id(...) present in Locale*.cs?

 Is your locale source added (e.g., GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(setting)))?

 No duplicate/overlapping patterns on the same property.

 Defaults in SetDefaults() match valid item values.
  
