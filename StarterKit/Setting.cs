// Setting.cs
namespace YourModNamespace
{
    using Colossal.IO.AssetDatabase;   // [FileLocation]
    using Game.Modding;                // IMod
    using Game.Settings;               // ModSetting + SettingsUI attributes
    using Game.UI.Widgets;             // DropdownItem<T>
    using UnityEngine;                 // Application.OpenURL

    /// <summary>Options UI & state for your mod.</summary>
    [FileLocation(Mod.kSettingsKey)]
    [SettingsUITabOrder(MainTab, AboutTab)]
    [SettingsUIGroupOrder(MainInfoGroup, MainControlsGroup, AboutLinksGroup, AboutNotesGroup)]
    [SettingsUIShowGroupName(MainInfoGroup, MainControlsGroup, AboutLinksGroup)]
    public sealed class Setting : ModSetting
    {
        // ---- Tabs ----
        public const string MainTab  = "Main";
        public const string AboutTab = "About";

        // ---- Groups ----
        public const string MainInfoGroup      = "Info";
        public const string MainControlsGroup  = "Controls";
        public const string AboutLinksGroup    = "Links";
        public const string AboutNotesGroup    = "Notes";

        // ---- Private backing fields (saved) ----
        private bool m_EnableFeature = true;

        private ExampleOption m_SelectedOption = ExampleOption.OptionA;

        // ---- Construction ----
        public Setting(IMod mod) : base(mod) { }

        // ---- Meta (read-only display rows) ----
        [SettingsUISection(MainTab, MainInfoGroup)]
        public string NameDisplay => Mod.kName;

        [SettingsUISection(MainTab, MainInfoGroup)]
        public string VersionDisplay => Mod.kVersionShort;

        // ---- Example: checkbox (filter/toggle style) ----
        [SettingsUISection(MainTab, MainControlsGroup)]
        public bool EnableFeature
        {
            get => m_EnableFeature;
            set
            {
                if (m_EnableFeature == value) return;
                m_EnableFeature = value;
                Apply(); // notify systems (don’t Save yet)
            }
        }

        // ---- Example: dropdown list (enum-backed) ----
        [SettingsUISection(MainTab, MainControlsGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetExampleChoices))]
        public ExampleOption SelectedOption
        {
            get => m_SelectedOption;
            set
            {
                if (m_SelectedOption == value) return;
                m_SelectedOption = value;
                Apply();
            }
        }

        public enum ExampleOption
        {
            OptionA = 0,
            OptionB = 1,
            OptionC = 2,
        }

        public static DropdownItem<ExampleOption>[] GetExampleChoices()
        {
            return new[]
            {
                new DropdownItem<ExampleOption> { value = ExampleOption.OptionA, displayName = "Option A" },
                new DropdownItem<ExampleOption> { value = ExampleOption.OptionB, displayName = "Option B" },
                new DropdownItem<ExampleOption> { value = ExampleOption.OptionC, displayName = "Option C" },
            };
        }

        // ---- About tab: links (URL buttons) ----
        [SettingsUIButtonGroup("LinksRow")]
        [SettingsUIButton]
        [SettingsUISection(AboutTab, AboutLinksGroup)]
        public bool OpenGitHub
        {
            set
            {
                if (!value) return;
                try { Application.OpenURL("https://example.com/your-repo"); }
                catch (System.Exception ex) { Mod.s_Log.Warn($"OpenGitHub failed: {ex.Message}"); }
            }
        }

        [SettingsUIButtonGroup("LinksRow")]
        [SettingsUIButton]
        [SettingsUISection(AboutTab, AboutLinksGroup)]
        public bool OpenDiscord
        {
            set
            {
                if (!value) return;
                try { Application.OpenURL("https://discord.gg/your-server"); }
                catch (System.Exception ex) { Mod.s_Log.Warn($"OpenDiscord failed: {ex.Message}"); }
            }
        }

        // ---- About tab: notes (multiline; content via Locale) ----
        [SettingsUIMultilineText]
        [SettingsUISection(AboutTab, AboutNotesGroup)]
        public string AboutNotes => string.Empty;

        public override void SetDefaults()
        {
            m_EnableFeature = true;
            m_SelectedOption = ExampleOption.OptionA;
        }
    }
}
