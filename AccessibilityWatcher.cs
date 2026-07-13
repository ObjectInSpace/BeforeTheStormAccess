using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Per-frame accessibility watcher: polls the live UI state and announces changes.
    /// Every announcer runs isolated — one failing subsystem logs (once per distinct
    /// error) and never silences the others.
    /// </summary>
    public class AccessibilityWatcher : MonoBehaviour
    {
        private static AccessibilityWatcher _instance;

        private readonly WindowWatcher _windowWatcher = new WindowWatcher();
        private readonly FocusHandler _focusHandler = new FocusHandler();
        private readonly HotspotHandler _hotspotHandler = new HotspotHandler();
        private readonly GameInfoHandler _gameInfoHandler = new GameInfoHandler();
        private readonly BacktalkHandler _backtalkHandler = new BacktalkHandler();
        private readonly NavigationHandler _navigationHandler;

        public AccessibilityWatcher()
        {
            _navigationHandler = new NavigationHandler(_hotspotHandler);
        }
        private readonly Dictionary<string, bool> _loggedErrors = new Dictionary<string, bool>();

        private string _lastSubtitle = string.Empty;
        private string _lastChoiceSignature = string.Empty;
        private int _lastSelectedChoice = -1;

        /// <summary>
        /// Creates the persistent watcher object once.
        /// </summary>
        public static void EnsureCreated()
        {
            if (_instance != null)
                return;

            GameObject go = new GameObject("BeforeTheStormAccessWatcher");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<AccessibilityWatcher>();
            Main.Log.LogInfo("AccessibilityWatcher created");
        }

        private void Update()
        {
            if (Main.IsShuttingDown)
                return;

            Guarded("ModKeys", HandleModKeys);

            // Ambient chain: first announcement this tick interrupts stale speech,
            // co-occurring ones append. See ScreenReader.SayCoalesced.
            ScreenReader.BeginCoalescedCycle();
            Guarded("Subtitles", AnnounceSubtitleChanges);
            Guarded("Choices", AnnounceChoiceChanges);
            Guarded("Windows", _windowWatcher.Update);
            Guarded("Focus", _focusHandler.Update);
            Guarded("Hotspots", _hotspotHandler.Update);
            Guarded("Navigation", _navigationHandler.Update);
            Guarded("GameInfo", _gameInfoHandler.Update);
            Guarded("Backtalk", _backtalkHandler.Update);
        }

        /// <summary>
        /// Runs one announcer isolated; logs each distinct failure once so a broken
        /// subsystem cannot flood the log or take down the rest of the watcher.
        /// </summary>
        private void Guarded(string subsystem, Action announcer)
        {
            try
            {
                announcer();
            }
            catch (Exception ex)
            {
                string key = subsystem + ":" + ex.GetType().Name + ":" + ex.Message;
                if (!_loggedErrors.ContainsKey(key))
                {
                    _loggedErrors[key] = true;
                    Main.Log.LogError("Watcher subsystem '" + subsystem + "' failed: " + ex);
                }
            }
        }

        private void HandleModKeys()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ScreenReader.Say(Loc.Get("help_text"));
                return;
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                Main.ToggleDebugMode();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                WindowWatcher.DumpDiagnostics();
                return;
            }

            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftHeld)
                {
                    // Tilde: toggle automatic dialogue reading (persisted setting).
                    ModConfig.SpeakDialogue.Value = !ModConfig.SpeakDialogue.Value;
                    ScreenReader.Say(Loc.Get(ModConfig.SpeakDialogue.Value
                        ? "dialogue_reading_on"
                        : "dialogue_reading_off"));
                    return;
                }

                if (!ScreenReader.RepeatLastSpoken())
                {
                    ScreenReader.Say(Loc.Get("nothing_to_repeat"), true, false);
                }
            }
        }

        /// <summary>
        /// Polling fallback for displayed subtitle text. Primary speech comes from
        /// SubtitlePatches (SetupSubtitle postfix); this catches anything that path
        /// misses, skipping text the patch already spoke.
        /// </summary>
        private void AnnounceSubtitleChanges()
        {
            if (!ModConfig.SpeakDialogue.Value)
                return;

            T_2F0DA2D9 subtitleWindow = GameRefs.SubtitleWindow;
            if (subtitleWindow == null || !subtitleWindow.gameObject.activeInHierarchy)
            {
                _lastSubtitle = string.Empty;
                return;
            }

            T_A243E23D speechLabel = subtitleWindow.m_speech;
            if (speechLabel == null)
                return;

            string text = TextUtil.CleanLabel(speechLabel.text);
            if (text == _lastSubtitle)
                return;

            _lastSubtitle = text;
            if (text.Length == 0)
                return;

            if (SubtitleAnnouncer.AlreadySpoken(text))
            {
                DebugLogger.Log(LogCategory.Handler, "SubtitlePoll", "skip (patch spoke): " + text);
                return;
            }

            DebugLogger.Log(LogCategory.Handler, "SubtitlePoll", "fallback speak: " + text);
            ScreenReader.SayCoalesced(text, true, true);
        }

        private void AnnounceChoiceChanges()
        {
            T_A35DE5E1 dialogWindow = GameRefs.DialogWindow;
            if (dialogWindow == null || !dialogWindow.gameObject.activeInHierarchy)
            {
                ResetChoiceState();
                return;
            }

            // Regular conversation choices and backtalk choices use the same 4-way
            // radial component; whichever is active in the hierarchy is live.
            T_6876113C activeChoiceUI = null;
            bool isBacktalk = false;

            if (dialogWindow.m_choiceUI != null && dialogWindow.m_choiceUI.gameObject.activeInHierarchy)
            {
                activeChoiceUI = dialogWindow.m_choiceUI;
            }
            else if (dialogWindow.m_btChoiceUI != null && dialogWindow.m_btChoiceUI.gameObject.activeInHierarchy)
            {
                activeChoiceUI = dialogWindow.m_btChoiceUI;
                isBacktalk = true;
            }

            if (activeChoiceUI == null || activeChoiceUI.m_choices == null)
            {
                ResetChoiceState();
                return;
            }

            StringBuilder signatureBuilder = new StringBuilder();
            StringBuilder announceBuilder = new StringBuilder();
            int selectedIndex = -1;

            for (int i = 0; i < activeChoiceUI.m_choices.Length; i++)
            {
                T_B611E532 choice = activeChoiceUI.m_choices[i];
                if (choice == null || !choice.gameObject.activeInHierarchy || !choice.enabled)
                    continue;

                string label = choice.m_label != null ? TextUtil.CleanLabel(choice.m_label.text) : string.Empty;
                if (label.Length == 0)
                    continue;

                signatureBuilder.Append(i).Append(':').Append(label).Append('|');

                if (announceBuilder.Length > 0)
                    announceBuilder.Append(" ");
                announceBuilder.Append(Loc.Get("choice_entry", DirectionName(i), label)).Append(".");

                if (choice.Selected)
                    selectedIndex = i;
            }

            string signature = signatureBuilder.ToString();
            if (signature != _lastChoiceSignature)
            {
                _lastChoiceSignature = signature;
                _lastSelectedChoice = selectedIndex;

                if (announceBuilder.Length > 0)
                {
                    string prefix = Loc.Get(isBacktalk ? "backtalk_prefix" : "choices_prefix");
                    ScreenReader.SayCoalesced(prefix + " " + announceBuilder, true, true);
                }
                return;
            }

            // Same choice set: announce highlight movement (mouse hover / joystick aim).
            if (selectedIndex != _lastSelectedChoice)
            {
                _lastSelectedChoice = selectedIndex;
                if (selectedIndex >= 0)
                {
                    T_B611E532 selected = activeChoiceUI.m_choices[selectedIndex];
                    string label = selected != null && selected.m_label != null
                        ? TextUtil.CleanLabel(selected.m_label.text)
                        : string.Empty;
                    if (label.Length > 0)
                    {
                        ScreenReader.SayCoalesced(
                            Loc.Get("choice_entry", DirectionName(selectedIndex), label), true, false);
                    }
                }
            }
        }

        private void ResetChoiceState()
        {
            _lastChoiceSignature = string.Empty;
            _lastSelectedChoice = -1;
        }

        private static string DirectionName(int interactMenuIndex)
        {
            switch (interactMenuIndex)
            {
                case 0:
                    return Loc.Get("dir_up");
                case 1:
                    return Loc.Get("dir_right");
                case 2:
                    return Loc.Get("dir_down");
                case 3:
                    return Loc.Get("dir_left");
                default:
                    return string.Empty;
            }
        }
    }
}
