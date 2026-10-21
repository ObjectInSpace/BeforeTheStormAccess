using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BeforeTheStormAccess
{
    internal static class PluginMetadata
    {
        internal const string Guid = "com.amock.beforethestormaccess";
        internal const string Name = "Before the Storm Access";
        internal const string Version = "0.8.0";
    }

    /// <summary>
    /// Plugin bootstrap: initializes speech, localization, Harmony patches, and the
    /// per-frame accessibility watcher.
    ///
    /// Unity 5.5 notes: Application.quitting does not exist (added in 2018.1), so
    /// cleanup runs in OnApplicationQuit/OnDestroy. All mod keys are polled with
    /// UnityEngine.Input inside AccessibilityWatcher rather than OS-global hotkeys.
    /// </summary>
    [BepInPlugin(PluginMetadata.Guid, PluginMetadata.Name, PluginMetadata.Version)]
    public class Main : BaseUnityPlugin
    {
        private Harmony _harmony;
        private bool _cleanupCompleted;

        public static bool DebugMode { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static Main Instance { get; private set; }
        public static bool IsShuttingDown { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ScreenReader.Initialize();
            Loc.Initialize();
            ModConfig.Initialize(Config);

            _harmony = new Harmony(PluginMetadata.Guid);
            _harmony.PatchAll();

            AccessibilityWatcher.EnsureCreated();

            Logger.LogInfo("Before the Storm Access initialized version=" + PluginMetadata.Version);
            ScreenReader.Say(Loc.Get("mod_loaded"));
        }

        /// <summary>Toggles verbose logging; announced so the state is always known.</summary>
        public static void ToggleDebugMode()
        {
            DebugMode = !DebugMode;
            Log.LogInfo("Debug mode " + (DebugMode ? "enabled" : "disabled"));
            ScreenReader.Say(Loc.Get(DebugMode ? "debug_mode_enabled" : "debug_mode_disabled"));
        }

        private void OnApplicationQuit()
        {
            IsShuttingDown = true;
            Cleanup();
        }

        private void OnDestroy()
        {
            if (!IsShuttingDown)
                return;

            Cleanup();
        }

        private void Cleanup()
        {
            if (_cleanupCompleted)
                return;

            _cleanupCompleted = true;

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            ScreenReader.Stop();
            ScreenReader.Shutdown();
        }
    }
}
