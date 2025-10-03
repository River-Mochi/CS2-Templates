// Mod.cs
namespace YourModNamespace
{
    using Colossal;                     // IDictionarySource
    using Colossal.IO.AssetDatabase;    // AssetDatabase.global.LoadSettings
    using Colossal.Logging;             // ILog, LogManager
    using Game;                         // UpdateSystem, SystemUpdatePhase
    using Game.Modding;                 // IMod
    using Game.SceneFlow;               // GameManager

    /// <summary>Main entry point for your mod.</summary>
    public sealed class Mod : IMod
    {
        // ---- Mod meta (single source of truth) ----
        public const string kName         = "Your Mod Name";
        public const string kVersionShort = "1.0.0";
        // Persistent settings file path (visible under Cities2 folder)
        public const string kSettingsKey  = "ModsSettings/YourMod/YourMod";

        // ---- Shared logger (visible in game logs) ----
        public static readonly ILog s_Log =
            LogManager.GetLogger("YourModNamespace").SetShowsErrorsInUI(false);

        // ---- Settings instance shared with systems/UI ----
        public static Setting? Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            s_Log.Info($"{kName} {kVersionShort} OnLoad()");

            // 1) Create settings
            var setting = new Setting(this);
            Settings = setting;

            // 2) Register locales BEFORE Options UI so labels/desc resolve
            AddLocale("en-US", new LocaleEN(setting));
            // (Add more locales here later)

            // 3) Load saved settings + register Options UI
            AssetDatabase.global.LoadSettings(kSettingsKey, setting, new Setting(this));
            setting.RegisterInOptionsUI();

            // 4) Register your ECS systems (systems do their own gating)
            updateSystem.World.GetOrCreateSystemManaged<ExampleSystem>();
            updateSystem.UpdateAt<ExampleSystem>(SystemUpdatePhase.MainLoop);

            // If you need ordering:
            // updateSystem.UpdateAfter<ExampleSystem, SomeOtherSystem>(SystemUpdatePhase.MainLoop);
            // updateSystem.UpdateBefore<ExampleSystem, SomeOtherSystem>(SystemUpdatePhase.MainLoop);

            s_Log.Info("Systems registered.");
        }

        public void OnDispose()
        {
            s_Log.Info($"{kName} OnDispose()");

            // Unhook only what this mod created (don’t remove game-managed locales)
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }

        private static void AddLocale(string localeId, IDictionarySource source)
        {
            var lm = GameManager.instance?.localizationManager;
            if (lm == null)
            {
                s_Log.Warn($"LocalizationManager null; cannot add locale '{localeId}'.");
                return;
            }
            lm.AddSource(localeId, source);
        }
    }
}
