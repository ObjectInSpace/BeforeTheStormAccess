// Central rename map between the obfuscated Assembly-CSharp type names and the
// real (or inferred) class names. The game is obfuscated at the CLASS level only:
// method, property, and serialized field names are readable, and debug strings
// leak several original names (e.g. "ST_Hotspot"). Every obfuscated type the mod
// touches MUST be aliased here once and referred to by its friendly alias
// everywhere else, so new discoveries only ever update this file and
// docs\game-api.md.
//
// Verified mappings (from decompiled\ analysis, 2026-07-11):
//   T_A6E913D1  GameManager        singleton `Instance`; owns every manager field
//   T_408CFC35  DawnUI             global UI/flow controller, s_inHotspotRange
//   T_E7B3064D  WindowManager      `Singleton.GetWindow<T>("name")`
//   T_A67E7A0   WindowBase         base class of all UI windows
//   T_2F0DA2D9  SubtitleWindow     m_speech label, SetupSubtitle(), ForceOverrideSubtitle
//   T_A35DE5E1  DialogWindow       SetupChoices()/SetupBacktalk(), m_choiceUI
//   T_6876113C  ChoiceUI           4-way radial; m_choices[4], choice callbacks
//   T_B611E532  ChoiceButton       m_label; Selected/Chosen/FadeOut
//   T_F8FE3E1C  FreeRoamWindow     static s_currentTriggers (in-range hotspots)
//   T_6FD30C1C  ST_Hotspot         m_friendlyName, m_activeVerbs, m_hotspotObj
//   T_244D769F  HotspotObj         Enabled, tagReference
//   T_6FCAE66C  InputManager       GetInputState(eGameInput), GetKeyBinding
//   T_6B664603  GameModeManager    CurrentMode (eGameMode), blockInput
//   T_6D7C1D07  BacktalkManager    InBacktalk, BacktalkRunning
//   T_C3DD66D9  PlayerController   m_navAgent (NavMeshAgent!), s_mainCharacterMesh
//   T_884A92DB  FollowCamera       m_isInteractionBlocked, Halt()
//   T_A243E23D  UILabel (NGUI)     text, textKey, processedText
//   T_A7F32A9D  Localization(NGUI) static Get(key), Get(key, sheet), s_useLocalization
//   T_D90B5C2A  UICamera (NGUI)    Notify(go, "OnHover", bool), IsHighlighted(go)
//   T_9E2FDFCF  UIButtonBase       virtual OnHover(bool)
//   T_1005C221  GameSettings       Get/SetSubtitlesToggled etc.
//   T_91FF9D92  LevelManager
//   T_5160A5A7  GameStateManager
//   T_5E38271D  GameDataSaveManager
//   T_56F32994  TagManager
//   T_E6DAC959  ST_TagReference    m_friendlyName source component
//
// Window name -> class (WindowManager.GetWindow):
//   SubtitleWindow, DialogWindow, FreeRoamWindow, MainMenuWindow (T_6BD00C7B),
//   PauseWindow (T_E7AEA2C5), SettingsWindow/DarkSettingsWindow (T_CCEFA2EA),
//   JournalWindow (T_7F8868E), BurnerSMSWindow (T_3FE8D8BE), ChoicesWindow
//   (T_35562F97), ConsequenceWindow (T_B6500484), TutorialWindow (T_64B68373),
//   BTTutorialWindow (T_7A847831), ModalPopup (T_B64E1BAE), SceneSelectionWindow
//   (T_63A31627), EndOfEpisodeWindow (T_C3DBAF42), ExtrasWindow (T_94254EC4),
//   CreditsWindow (T_6025074F), MovieWindow (T_27B78397), UserInformWindow
//   (T_859A152B), HowToPlayWindow (T_CF3FA5DC), PosterWindow (T_2CD30DA2),
//   TreasureMapWindow (T_2FD302B0), ZenJukeboxWindow (T_2433FC36).

// C# using-aliases are per-file, so shared access goes through this helper
// class instead: strongly typed accessors with null-safety, one place to fix
// when a mapping is wrong.

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Typed accessors for the game's obfuscated singletons and windows.
    /// All lookups are live (no caching of destroyed objects).
    /// </summary>
    public static class GameRefs
    {
        /// <summary>The central game manager singleton, or null before boot.</summary>
        public static T_A6E913D1 GameManager => T_A6E913D1.Instance;

        /// <summary>The NGUI window manager singleton, or null before the UI scene loads.</summary>
        public static T_E7B3064D WindowManager => T_E7B3064D.Singleton;

        /// <summary>Input manager, or null.</summary>
        public static T_6FCAE66C InputManager
        {
            get
            {
                T_A6E913D1 gm = GameManager;
                return gm != null ? gm.m_inputManager : null;
            }
        }

        /// <summary>Game mode manager (free roam, dialogue, etc.), or null.</summary>
        public static T_6B664603 GameModeManager
        {
            get
            {
                T_A6E913D1 gm = GameManager;
                return gm != null ? gm.m_gameModeManager : null;
            }
        }

        /// <summary>Backtalk manager, or null.</summary>
        public static T_6D7C1D07 BacktalkManager
        {
            get
            {
                T_A6E913D1 gm = GameManager;
                return gm != null ? gm.m_backtalkManager : null;
            }
        }

        /// <summary>Fetches a UI window by its registered name, or null when unavailable.</summary>
        public static TWindow GetWindow<TWindow>(string name) where TWindow : T_D25B278
        {
            T_E7B3064D wm = WindowManager;
            if (wm == null)
                return null;

            try
            {
                return wm.GetWindow<TWindow>(name);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The subtitle window, or null.</summary>
        public static T_2F0DA2D9 SubtitleWindow => GetWindow<T_2F0DA2D9>("SubtitleWindow");

        /// <summary>The dialog (choice/backtalk) window, or null.</summary>
        public static T_A35DE5E1 DialogWindow => GetWindow<T_A35DE5E1>("DialogWindow");

        /// <summary>The free roam HUD window, or null.</summary>
        public static T_F8FE3E1C FreeRoamWindow => GetWindow<T_F8FE3E1C>("FreeRoamWindow");

        /// <summary>
        /// The follow camera's transform, or null. This is the origin the game uses
        /// for its own interactability tests (distance and angle-interact), so the
        /// mod must measure from the same point to match the game's reach gate.
        /// </summary>
        public static UnityEngine.Transform FollowCameraTransform
        {
            get
            {
                T_A6E913D1 gm = GameManager;
                if (gm != null && gm.m_followCamera != null)
                    return gm.m_followCamera.gameObject.transform;
                return null;
            }
        }
    }
}
