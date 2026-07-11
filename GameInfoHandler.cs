using HarmonyLib;
using UnityEngine;
using _1C948E24B;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// On-demand game state information:
    /// - O: current objective (localized, same source as the game's objective reminder)
    /// - Shift+O: current location name
    /// Objective changes are also announced automatically via a Harmony patch on
    /// GameStateModel.SetCurrentObjective.
    /// </summary>
    public class GameInfoHandler
    {
        /// <summary>Handles info keys. Called every frame from the watcher.</summary>
        public void Update()
        {
            if (!Input.GetKeyDown(KeyCode.O))
                return;

            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shiftHeld)
            {
                AnnounceLocation();
            }
            else
            {
                AnnounceObjective();
            }
        }

        /// <summary>Speaks the current objective, or a fallback when none is set.</summary>
        public static void AnnounceObjective()
        {
            string objective = GetObjectiveText();
            ScreenReader.Say(objective.Length > 0
                ? Loc.Get("objective_prefix", objective)
                : Loc.Get("no_objective"), true, true, true);
        }

        /// <summary>Speaks the current location name.</summary>
        public static void AnnounceLocation()
        {
            string location = GetLocationText();
            ScreenReader.Say(location.Length > 0
                ? Loc.Get("location_prefix", location)
                : Loc.Get("no_location"), true, true, true);
        }

        internal static string GetObjectiveText()
        {
            T_A6E913D1 gm = GameRefs.GameManager;
            if (gm == null || gm.m_gameStateManager == null)
                return string.Empty;

            GameStateModel model = gm.m_gameStateManager.GetCurrentModel();
            if (model == null)
                return string.Empty;

            string key = model.GetCurrentObjective();
            if (key == null || key.Trim().Length == 0)
                return string.Empty;

            return TextUtil.CleanLabel(T_A7F32A9D.Get(key));
        }

        internal static string GetLocationText()
        {
            T_A6E913D1 gm = GameRefs.GameManager;
            if (gm == null || gm.m_gameDataManager == null)
                return string.Empty;

            T_D81BBCF4 scene = gm.m_gameDataManager.currentScene;
            if (scene == null)
                return string.Empty;

            string name = null;
            if (scene.nameKey != null && scene.nameKey.Trim().Length > 0)
            {
                name = T_A7F32A9D.Get(scene.nameKey);
            }

            if (name == null || name.Trim().Length == 0)
            {
                name = scene.location.ToString();
            }

            return TextUtil.CleanLabel(name);
        }
    }

    /// <summary>
    /// Announces objective changes as they happen. Suppressed until the UI is up
    /// so save-load restoration doesn't speak stale objectives over the menus.
    /// </summary>
    [HarmonyPatch(typeof(GameStateModel), "SetCurrentObjective")]
    internal static class ObjectiveChangedPatch
    {
        private static string _lastAnnouncedKey;

        private static void Postfix(string _1817E8BEB)
        {
            try
            {
                if (!ModConfig.AnnounceObjectiveChanges.Value)
                    return;

                T_A6E913D1 gm = GameRefs.GameManager;
                if (gm == null || !gm.m_UILoaded)
                    return;

                string key = _1817E8BEB;
                if (key == null || key.Trim().Length == 0 || key == _lastAnnouncedKey)
                    return;

                _lastAnnouncedKey = key;
                string text = TextUtil.CleanLabel(T_A7F32A9D.Get(key));
                if (text.Length == 0)
                    return;

                DebugLogger.Log(LogCategory.Handler, "Objective", "changed: " + text);
                // Append (no interrupt): objectives usually change right after a
                // dialogue line that must not be cut off.
                ScreenReader.Say(Loc.Get("objective_new", text), false, true, true);
            }
            catch (System.Exception ex)
            {
                Main.Log.LogWarning("ObjectiveChangedPatch failed: " + ex.Message);
            }
        }
    }
}
