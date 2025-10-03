namespace YourMod
{
    using Colossal.IO.AssetDatabase;   // AssetDatabase.global.LoadSettings
    using Colossal.Logging;            // ILog, LogManager
    using Game;                        // UpdateSystem, SystemUpdatePhase
    using Game.Modding;                // IMod

    public sealed class Mod : IMod
    {
        // ---- Meta ----
        public const string Name = "Your Mod Name";
        public const string VersionShort = "1.0.0";

        // ---- Shared state ----
        public static readonly ILog s_Log =
            LogManager.GetLogger(Name).SetShowsErrorsInUI(true);

        public static Setting? Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            s_Log.Info($"{Name} {VersionShort} loading…");

            // Settings
            var settings = new Setting(this);
            Settings = settings;

            // Register locale(s) BEFORE Options UI so labels/descriptions render localized
            Locale.AddAll(settings); // see LocaleEN.cs

            // Load saved + register in Options
            AssetDatabase.global.LoadSettings(Name, settings, new Setting(this));
            settings.RegisterInOptionsUI();

            // Example system registration (choose a phase that fits your logic)
            updateSystem.UpdateAt<Systems.ExampleSystem>(SystemUpdatePhase.MainLoop);

            s_Log.Info($"{Name} loaded.");
        }

        public void OnDispose()
        {
            s_Log.Info($"{Name} disposing…");

            // Only dispose what this mod owns (settings UI, events, etc.)
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}
