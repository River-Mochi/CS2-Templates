// Mod.cs
//
// Minimal, reusable Mod entry point for CS2 mods — aligned with Colossal Setting.cs (with keybinding) template.
// - Sets up logging (s_Log)
// - Creates settings + loads saved values + registers in Options UI
// - Registers locales (optional)
// - Reads input actions declared via attributes in Setting.cs (no keybind creation here)
// - (optional) Registers systems with UpdateSystem
//
// This template intentionally avoids any “one-shot after load” logic;
// systems should handle that themselves (if needed) via a gated approach.
//
// Notes:
// * Keybinding *definitions* live in Setting.cs via [SettingsUI*Action]/[SettingsUI*Binding] attributes.
// * This Mod.cs only retrieves ProxyAction instances by name and (optionally) logs interactions.
// * ProxtAction is the runtime handle to a Colossal input action (button/axis/vector).
//   * It lets code read the current value (e.g., ReadValue<float>() or ReadValue<Vector2>()) and subscribe to onInteraction events.
//   * Contrast: ProxyBinding is the saved binding shown in Options UI; ProxyAction is the live action used at runtime.*
// * Settings persistence key below uses nameof(Project2) to match [FileLocation(nameof(Project2))] in Setting.cs.

namespace Project2
{
    // Using directives inside the namespace (style).
    using Colossal;                        // IDictionarySource
    using Colossal.IO.AssetDatabase;       // AssetDatabase.global.LoadSettings
    using Colossal.Logging;                // ILog, LogManager
    using Game;                            // UpdateSystem, SystemUpdatePhase
    using Game.Input;                      // ProxyAction, IInputAction, InputPhase
    using Game.Modding;                    // IMod
    using Game.SceneFlow;                  // GameManager
    using UnityEngine;                     // Vector2

    public sealed class Mod : IMod
    {
        // --- Meta (adjust as desired) ---
        public const string Name = "Your Mod Name";
        public const string VersionShort = "0.1.0";

        // --- Action names (must match those referenced by attributes in Setting.cs) ---
        public const string kButtonActionName = "ButtonBinding";
        public const string kAxisActionName   = "FloatBinding";
        public const string kVectorActionName = "Vector2Binding";

        // --- Logging ---
        // Log file: %LocalAppData%\..\LocalLow\Colossal Order\Cities Skylines II\Logs\
        public static readonly ILog s_Log =
            LogManager.GetLogger(Name).SetShowsErrorsInUI(false);

        // --- Settings instance (created here; all option definitions stay in Setting.cs) ---
        public static Setting? s_Settings { get; private set; }

        // --- Optional: cached input actions (declared in Setting.cs via attributes) ---
        public static ProxyAction? s_ButtonAction;
        public static ProxyAction? s_AxisAction;
        public static ProxyAction? s_VectorAction;

        // Keep delegates so they can be unsubscribed cleanly on dispose.
        private static System.Action<IInputAction, InputPhase>? s_OnButtonInteraction;
        private static System.Action<IInputAction, InputPhase>? s_OnAxisInteraction;
        private static System.Action<IInputAction, InputPhase>? s_OnVectorInteraction;

        public void OnLoad(UpdateSystem updateSystem)
        {
            s_Log.Info($"{Name} {VersionShort} - OnLoad. If this appears, logging is configured.");

            // Helpful: where this mod’s package is located (when available).
            if (GameManager.instance?.modManager != null &&
                GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                s_Log.Info($"Mod asset path: {asset.path}");
            }

            // Settings: create + load saved values, then register in Options UI.
            // Use nameof(Project2) to match [FileLocation(nameof(Project2))] in Setting.cs.
            var settings = new Setting(this);
            s_Settings = settings;

            AssetDatabase.global.LoadSettings(nameof(Project2), settings, new Setting(this));

            // Locales (optional) — add before Options UI if localized labels/descriptions are used.
            TryAddLocale("en-US", new LocaleEN(settings));

            settings.RegisterInOptionsUI();

            // --- Input hookup (optional) ---
            // Actions are declared by attributes in Setting.cs; call RegisterKeyBindings() to ensure
            // the actions exist, then retrieve them by name and (optionally) log interactions.
            try
            {
                settings.RegisterKeyBindings(); // REQUIRED with the attribute-based template

                s_ButtonAction = settings.GetAction(kButtonActionName);
                s_AxisAction   = settings.GetAction(kAxisActionName);
                s_VectorAction = settings.GetAction(kVectorActionName);

                EnableAndSubscribe(s_ButtonAction, MakeFloatLogger(() => s_ButtonAction));
                EnableAndSubscribe(s_AxisAction,   MakeFloatLogger(() => s_AxisAction));
                EnableAndSubscribe(s_VectorAction, MakeVector2Logger(() => s_VectorAction));
            }
            catch (System.Exception ex)
            {
                s_Log.Warn($"Input action hookup skipped: {ex.GetType().Name}: {ex.Message}");
            }

            // --- Example system registrations (uncomment and replace with actual systems) ---
            // updateSystem.UpdateAt<ExampleUiSystem>(SystemUpdatePhase.UIUpdate);
            // updateSystem.UpdateAt<ExampleSimSystem>(SystemUpdatePhase.MainLoop);

            s_Log.Info($"{Name} initialized.");
        }

        public void OnDispose()
        {
            s_Log.Info($"{Name} - OnDispose");

            // Clean input subscriptions.
            SafeUnsubscribeAndDisable(ref s_ButtonAction, ref s_OnButtonInteraction);
            SafeUnsubscribeAndDisable(ref s_AxisAction,   ref s_OnAxisInteraction);
            SafeUnsubscribeAndDisable(ref s_VectorAction, ref s_OnVectorInteraction);

            // Do NOT remove localization sources; CS2 manages them.

            if (s_Settings != null)
            {
                s_Settings.UnregisterInOptionsUI();
                s_Settings = null;
            }
        }

        // ---------- Helpers ----------

        private static void TryAddLocale(string localeId, IDictionarySource source)
        {
            var lm = GameManager.instance?.localizationManager;
            if (lm == null)
            {
                s_Log.Warn($"LocalizationManager not available; cannot add locale '{localeId}'.");
                return;
            }
            lm.AddSource(localeId, source);
        }

        private static void EnableAndSubscribe(
            ProxyAction? action,
            System.Action<IInputAction, InputPhase> handler)
        {
            if (action == null)
                return;

            action.shouldBeEnabled = true;
            action.onInteraction += handler;

            if (ReferenceEquals(action, s_ButtonAction)) s_OnButtonInteraction = handler;
            else if (ReferenceEquals(action, s_AxisAction)) s_OnAxisInteraction = handler;
            else if (ReferenceEquals(action, s_VectorAction)) s_OnVectorInteraction = handler;
        }

        // Logs float-like inputs (button: 0/1; axis: continuous).
        private static System.Action<IInputAction, InputPhase> MakeFloatLogger(
            System.Func<ProxyAction?> getAction)
        {
            return (_, phase) =>
            {
                var a = getAction();
                if (a != null)
                {
                    s_Log.Info($"[{a.name}] On{phase} {a.ReadValue<float>()}");
                }
            };
        }

        // Logs Vector2 inputs.
        private static System.Action<IInputAction, InputPhase> MakeVector2Logger(
            System.Func<ProxyAction?> getAction)
        {
            return (_, phase) =>
            {
                var a = getAction();
                if (a != null)
                {
                    Vector2 v = a.ReadValue<Vector2>();
                    s_Log.Info($"[{a.name}] On{phase} {v}");
                }
            };
        }

        private static void SafeUnsubscribeAndDisable(
            ref ProxyAction? action,
            ref System.Action<IInputAction, InputPhase>? handler)
        {
            if (action != null && handler != null)
            {
                action.onInteraction -= handler;
            }
            if (action != null)
            {
                action.shouldBeEnabled = false;
            }

            handler = null;
            action  = null;
        }
    }
}
