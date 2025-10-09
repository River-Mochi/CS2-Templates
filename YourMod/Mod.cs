// Mod.cs
//
// Minimal, reusable Mod entry point for CS2 mods — now merged with input/keybinding plumbing.
// - Sets up logging (s_Log)
// - Creates settings + loads saved values
// - Registers locales (optional)
// - (NEW) Sets up example keybinding actions via Colossal's input system
// - (optional) Registers systems with UpdateSystem
//
// This template intentionally avoids any “one-shot after load” logic;
// systems should handle that themselves (if needed) via a gated approach.
// Input handlers shown here are illustrative and lightweight.
//
// Notes:
// * The settings persistence key uses `Name`, which controls the .coc filename.
// * Locale sources are added safely; CS2 manages their lifecycle (no manual removal needed).
// * Event handlers for input are subscribed on load and unsubscribed on dispose.

namespace Project2
{
    // Using directives inside the namespace (optional per style rules, could also place above namespace )
    using Colossal;                        // IDictionarySource
    using Colossal.IO.AssetDatabase;       // AssetDatabase.global.LoadSettings
    using Colossal.Logging;                // ILog, LogManager
    using Game;                            // UpdateSystem, SystemUpdatePhase
    using Game.Input;                      // ProxyAction, InputPhase, IInputAction
    using Game.Modding;                    // IMod
    using Game.SceneFlow;                  // GameManager
    using UnityEngine;                     // Vector2

    public sealed class Mod : IMod
    {
        // --- Meta (adjust these for the mod) ---
        public const string Name = "Your Mod Name";
        public const string VersionShort = "0.1.0";

        // --- Logging ---
        // Log file: C:\Users\<User>\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\
        public static readonly ILog s_Log =
            LogManager.GetLogger(Name).SetShowsErrorsInUI(false);

        // --- Settings instance (accessible to other classes if needed) ---
        public static Setting? s_Settings { get; private set; }

        // --- Input / keybinding (optional, uses Colossal standard pattern) ---
        // Action names should match those declared in Setting.RegisterKeyBindings()
        public const string kButtonActionName = "ButtonBinding";
        public const string kAxisActionName   = "FloatBinding";
        public const string kVectorActionName = "Vector2Binding";

        // Stored references to actions so other systems can inspect them if desired.
        public static ProxyAction? s_ButtonAction;
        public static ProxyAction? s_AxisAction;
        public static ProxyAction? s_VectorAction;

        // Stored delegates so they can be cleanly unsubscribed on dispose.
        private static System.Action<IInputAction, InputPhase>? s_OnButtonInteraction;
        private static System.Action<IInputAction, InputPhase>? s_OnAxisInteraction;
        private static System.Action<IInputAction, InputPhase>? s_OnVectorInteraction;

        public void OnLoad(UpdateSystem updateSystem)
        {
            s_Log.Info($"{Name} {VersionShort} - OnLoad. If this appears, logging is configured.");

            // Helpful: where this mod's package is located on disk (when available).
            if (GameManager.instance?.modManager != null &&
                GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                s_Log.Info($"Current mod asset at {asset.path}");
            }

            // Settings: create, load persisted values, then register in Options UI.
            // Using `Name` as the settings key defines the .coc filename.
            var settings = new Setting(this);
            s_Settings = settings;

            AssetDatabase.global.LoadSettings(Name, settings, new Setting(this));
            settings.RegisterInOptionsUI();

            // Locales (optional): add before Options UI if localized labels/descriptions are used.
            // Safe helper logs a warning if LocalizationManager is not available yet.
            TryAddLocale("en-US", new LocaleEN(settings));

            // --- Input setup (optional) ---
            // Requires Setting.RegisterKeyBindings() to declare actions with the names above.
            // If Setting does not implement keybinding, the try/catch cleanly skips this section.
            try
            {
                settings.RegisterKeyBindings(); // Colossal standard method in Setting.cs

                s_ButtonAction = settings.GetAction(kButtonActionName);
                s_AxisAction   = settings.GetAction(kAxisActionName);
                s_VectorAction = settings.GetAction(kVectorActionName);

                EnableAndSubscribe(s_ButtonAction, MakeFloatLogger(() => s_ButtonAction));
                EnableAndSubscribe(s_AxisAction,   MakeFloatLogger(() => s_AxisAction));
                EnableAndSubscribe(s_VectorAction, MakeVector2Logger(() => s_VectorAction));
            }
            catch (System.Exception ex)
            {
                s_Log.Warn($"Keybinding setup skipped: {ex.GetType().Name}: {ex.Message}");
            }

            // --- System registration examples (choose phases appropriate to the work) ---
            // Example: a system that drives UI changes:
            // updateSystem.UpdateAt<ExampleUiSystem>(SystemUpdatePhase.UIUpdate);
            //
            // Example: a system that runs in the main simulation loop:
            // updateSystem.UpdateAt<ExampleSimSystem>(SystemUpdatePhase.MainLoop);

            s_Log.Info($"{Name} initialized.");
        }

        public void OnDispose()
        {
            s_Log.Info($"{Name} - OnDispose");

            // Unsubscribe input handlers and disable actions to avoid residual callbacks on reload.
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

        private static void EnableAndSubscribe(ProxyAction? action,
            System.Action<IInputAction, InputPhase> handler)
        {
            if (action == null)
                return;

            action.shouldBeEnabled = true;
            action.onInteraction += handler;

            // Keep a reference so it can be unsubscribed later.
            if (ReferenceEquals(action, s_ButtonAction)) s_OnButtonInteraction = handler;
            else if (ReferenceEquals(action, s_AxisAction)) s_OnAxisInteraction = handler;
            else if (ReferenceEquals(action, s_VectorAction)) s_OnVectorInteraction = handler;
        }

        // Logs float-like inputs (buttons/axes). Uses the provided accessor to read the correct action.
        private static System.Action<IInputAction, InputPhase> MakeFloatLogger(
            System.Func<ProxyAction?> getAction)
        {
            return (_, phase) =>
            {
                var a = getAction();
                if (a != null)
                {
                    // ReadValue<float>() works for button (0/1) and axis actions.
                    s_Log.Info($"[{a.name}] On{phase} {a.ReadValue<float>()}");
                }
            };
        }

        // Logs Vector2 inputs. Uses the provided accessor to read the correct action.
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
