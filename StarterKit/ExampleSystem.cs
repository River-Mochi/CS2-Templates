// ExampleSystem.cs
namespace YourModNamespace
{
    using Colossal.Logging;                 // ILog
    using Colossal.Serialization.Entities;  // Purpose, GameMode
    using Game;                             // GameSystemBase
    using Unity.Entities;                   // ECS base types

    /// <summary>
    /// Example ECS system with gameplay gating.
    /// - Enables only during actual gameplay (not main menu / editors).
    /// - Applies a one-shot action after a city finishes loading.
    /// - Runs per-frame work in OnUpdate() while enabled.
    /// </summary>
    public sealed class ExampleSystem : GameSystemBase
    {
        private ILog m_Log;

        // Only run in real gameplay
        private const GameMode kAllowedModes = GameMode.Game;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = Mod.s_Log;
            Enabled = false; // system decides when to run
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            UpdateEnabled(mode);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            UpdateEnabled(mode);

            if (Enabled)
            {
                ApplyOnceAfterLoad();
            }
        }

        protected override void OnUpdate()
        {
            if (!Enabled) return;

            // Per-frame work goes here.
            // Example: read settings via Mod.Settings
            // var settings = Mod.Settings;
        }

        private void UpdateEnabled(GameMode mode)
        {
            bool shouldRun = (kAllowedModes & mode) != GameMode.None;
            if (Enabled != shouldRun)
            {
                Enabled = shouldRun;
                m_Log?.Info($"[{nameof(ExampleSystem)}] {(Enabled ? "ENABLED" : "DISABLED")} for mode={mode}");
            }
        }

        private void ApplyOnceAfterLoad()
        {
            // One-shot work after a city is fully loaded.
            // Example: cache references, initialize data, write a log message, etc.
            m_Log?.Info($"[{nameof(ExampleSystem)}] ApplyOnceAfterLoad()");
        }
    }
}
