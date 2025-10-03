// LocaleEN.cs
namespace YourModNamespace
{
    using Colossal;                    // IDictionarySource
    using System.Collections.Generic;  // Dictionary, IList

    /// <summary>English locale entries for the Options UI.</summary>
    public sealed class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                // Mod name in Options list
                { m_Setting.GetSettingsLocaleID(), Mod.kName },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.MainTab),  "Main"  },
                { m_Setting.GetOptionTabLocaleID(Setting.AboutTab), "About" },

                // Groups
                { m_Setting.GetOptionGroupLocaleID(Setting.MainInfoGroup),     "Info"     },
                { m_Setting.GetOptionGroupLocaleID(Setting.MainControlsGroup), "Controls" },
                { m_Setting.GetOptionGroupLocaleID(Setting.AboutLinksGroup),   "Links"    },
                { m_Setting.GetOptionGroupLocaleID(Setting.AboutNotesGroup),   "Notes"    },

                // Displays (left column label; right column tooltip/desc)
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.NameDisplay)),    "Mod Name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.NameDisplay)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.VersionDisplay)), "Version"  },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.VersionDisplay)),  "Current mod version." },

                // Checkbox
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableFeature)), "Enable feature" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableFeature)),
                  "Turns the example feature on. Uncheck to disable." },

                // Dropdown
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SelectedOption)), "Example option" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SelectedOption)),
                  "Choose one of the example options to change behavior." },

                // About tab: link buttons
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenGitHub)),  "GitHub"  },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenGitHub)),   "Open the project repository in your browser." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenDiscord)), "Discord" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenDiscord)),  "Join the community Discord server." },

                // About tab: notes block
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AboutNotes)), "Notes" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AboutNotes)),
                  "Tips, usage notes, or disclaimers can go here." },
            };
        }

        public void Unload() { }
    }
}
