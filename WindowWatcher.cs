using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Generic coverage net for on-screen text. Watches every game window
    /// (T_D25B278 subclasses) and:
    /// - announces when an unhandled window becomes visible, reading its visible labels once
    /// - tracks label text changes inside visible windows (tutorial prompts, objectives,
    ///   SMS toasts, notes) and speaks new text, with a flap guard against animated labels
    /// Windows with dedicated handlers (subtitles, dialogue choices) are excluded.
    /// This is intentionally broad: it discovers text sources (journal pages, phone
    /// texts, posters) which then graduate to dedicated handlers as they're understood.
    /// </summary>
    public class WindowWatcher
    {
        private const float PollInterval = 0.4f;
        private const int MaxLabelsPerAnnouncement = 14;
        // The flap guard exists for self-animating labels (timers, tickers), not
        // deliberate page flipping: 6 changes in 10s before muting, and a short
        // 4s mute so a page the user actually wants re-reads almost immediately.
        private const int FlapChangeLimit = 6;
        private const float FlapWindowSeconds = 10f;
        private const float FlapMuteSeconds = 4f;

        // Windows covered by dedicated logic or intentionally silent.
        private static readonly string[] _excludedWindows =
        {
            "SubtitleWindow",   // SubtitlePatches + poll fallback
            "DialogWindow",     // choice reader in AccessibilityWatcher
            "TransitionWindow", // visual fade only
            "MovieWindow",      // video playback
            "FreeRoamWindow",   // Phase 3 (hotspots); its HUD labels flap constantly
            "ChoicesWindow",    // end-of-episode recap read by ChoicesHandler
        };

        private float _nextPollTime;
        private readonly Dictionary<string, bool> _windowWasVisible = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _labelLastText = new Dictionary<string, string>();
        private readonly Dictionary<string, FlapState> _labelFlap = new Dictionary<string, FlapState>();

        private class FlapState
        {
            public int Changes;
            public float WindowStart;
            public float MutedUntil;
        }

        /// <summary>
        /// Polls window visibility and label content. Call once per frame.
        /// </summary>
        public void Update()
        {
            if (Time.unscaledTime < _nextPollTime)
                return;

            _nextPollTime = Time.unscaledTime + PollInterval;

            T_D25B278[] windows = UnityEngine.Object.FindObjectsOfType<T_D25B278>();
            if (windows == null)
                return;

            for (int i = 0; i < windows.Length; i++)
            {
                T_D25B278 window = windows[i];
                if (window == null)
                    continue;

                string name = window.gameObject.name;
                if (IsExcluded(name))
                    continue;

                bool visible = IsWindowVisible(window);
                bool wasVisible;
                _windowWasVisible.TryGetValue(name, out wasVisible);

                if (visible && !wasVisible)
                {
                    _windowWasVisible[name] = true;
                    AnnounceWindowOpened(window, name);
                }
                else if (!visible && wasVisible)
                {
                    _windowWasVisible[name] = false;
                    ClearLabelStateFor(name);
                }
                else if (visible)
                {
                    AnnounceLabelChanges(window, name);
                }
            }
        }

        /// <summary>
        /// Writes a full diagnostic dump of all windows and labels to the log (F10).
        /// </summary>
        public static void DumpDiagnostics()
        {
            Main.Log.LogInfo("===== DIAGNOSTIC DUMP =====");
            Main.Log.LogInfo("WindowManager singleton: " + (GameRefs.WindowManager != null ? "present" : "NULL"));
            Main.Log.LogInfo("GameManager singleton: " + (GameRefs.GameManager != null ? "present" : "NULL"));

            T_2F0DA2D9 subtitle = GameRefs.SubtitleWindow;
            if (subtitle == null)
            {
                Main.Log.LogInfo("SubtitleWindow lookup: NULL");
            }
            else
            {
                Main.Log.LogInfo("SubtitleWindow: activeInHierarchy=" + subtitle.gameObject.activeInHierarchy
                    + " panelAlpha=" + (subtitle.m_panel != null ? subtitle.m_panel.alpha.ToString("0.00") : "no-panel")
                    + " speechLabel=" + (subtitle.m_speech != null
                        ? "'" + subtitle.m_speech.text + "' alpha=" + subtitle.m_speech.alpha.ToString("0.00")
                        : "NULL"));
            }

            T_D25B278[] allWindows = Resources.FindObjectsOfTypeAll<T_D25B278>();
            Main.Log.LogInfo("Total window objects (incl. inactive): " + (allWindows != null ? allWindows.Length : 0));
            if (allWindows != null)
            {
                for (int i = 0; i < allWindows.Length; i++)
                {
                    T_D25B278 w = allWindows[i];
                    if (w == null)
                        continue;

                    bool active = w.gameObject.activeInHierarchy;
                    string alpha = w.m_panel != null ? w.m_panel.alpha.ToString("0.00") : "?";
                    Main.Log.LogInfo("Window '" + w.gameObject.name + "' type=" + w.GetType().Name
                        + " active=" + active + " panelAlpha=" + alpha);

                    if (!active)
                        continue;

                    T_A243E23D[] labels = w.GetComponentsInChildren<T_A243E23D>(false);
                    for (int j = 0; j < labels.Length; j++)
                    {
                        T_A243E23D label = labels[j];
                        if (label == null)
                            continue;

                        string text = TextUtil.CleanLabel(label.text);
                        if (text.Length == 0)
                            continue;

                        Main.Log.LogInfo("  label path=" + GetPath(label.transform, w.transform)
                            + " alpha=" + label.alpha.ToString("0.00") + " text='" + text + "'");
                    }
                }
            }

            HotspotHandler.DumpInteractState();

            Main.Log.LogInfo("===== END DIAGNOSTIC DUMP =====");
            ScreenReader.Say(Loc.Get("diagnostics_dumped"));
        }

        private static bool IsExcluded(string windowName)
        {
            for (int i = 0; i < _excludedWindows.Length; i++)
            {
                if (_excludedWindows[i] == windowName)
                    return true;
            }

            return false;
        }

        private static bool IsWindowVisible(T_D25B278 window)
        {
            if (!window.gameObject.activeInHierarchy)
                return false;

            // NGUI frequently hides windows by panel alpha instead of deactivating them.
            return window.m_panel == null || window.m_panel.alpha > 0.01f;
        }

        private void AnnounceWindowOpened(T_D25B278 window, string name)
        {
            StringBuilder builder = new StringBuilder();
            if (ModConfig.AnnounceWindowNames.Value)
            {
                string title = WindowTitle(name);
                if (title.Length > 0)
                    builder.Append(Loc.Get("window_opened", title));
            }

            List<LabelEntry> labels = CollectVisibleLabels(window);
            SeedLabelState(name, labels);

            if (ModConfig.ReadWindowTextOnOpen.Value)
            {
                int spoken = 0;
                for (int i = 0; i < labels.Count && spoken < MaxLabelsPerAnnouncement; i++)
                {
                    if (builder.Length > 0)
                        builder.Append(" ");
                    builder.Append(labels[i].Text).Append(".");
                    spoken++;
                }
            }

            DebugLogger.Log(LogCategory.Handler, "WindowWatcher",
                "opened '" + name + "' labels=" + labels.Count);
            if (builder.Length > 0)
            {
                ScreenReader.Say(builder.ToString(), true, true, true);
            }
        }

        /// <summary>
        /// Announces labels that changed text OR became visible again inside an
        /// open window. Visibility matters for paged content (journal, SMS): when
        /// the user flips back to an earlier page its labels re-enter the visible
        /// set with unchanged text and must be re-read, so labels that leave the
        /// visible set are forgotten. New/changed labels are batched into one
        /// announcement in reading order rather than spoken fragment by fragment.
        /// </summary>
        private void AnnounceLabelChanges(T_D25B278 window, string windowName)
        {
            List<LabelEntry> labels = CollectVisibleLabels(window);
            Dictionary<string, bool> currentKeys = new Dictionary<string, bool>();
            StringBuilder batch = new StringBuilder();

            for (int i = 0; i < labels.Count; i++)
            {
                LabelEntry entry = labels[i];
                string key = windowName + "/" + entry.Path;
                currentKeys[key] = true;

                string last;
                bool wasVisible = _labelLastText.TryGetValue(key, out last);
                bool newOrChanged = !wasVisible || entry.Text != last;
                _labelLastText[key] = entry.Text;

                if (!newOrChanged)
                    continue;

                // Labels inside menu-list buttons (settings values, button captions)
                // are read by the focus handler with their row context; announcing
                // them here too caused multi-repeats on value changes.
                if (entry.OwnedByListButton)
                    continue;

                if (IsFlapping(key))
                    continue;

                DebugLogger.Log(LogCategory.Handler, "WindowWatcher",
                    (wasVisible ? "label change " : "label visible ") + key + " -> " + entry.Text);
                if (batch.Length > 0)
                    batch.Append(" ");
                batch.Append(entry.Text).Append(".");
            }

            // Forget labels that left the visible set (hidden pages/tabs) so they
            // re-announce when they come back.
            List<string> hidden = new List<string>();
            string prefix = windowName + "/";
            foreach (KeyValuePair<string, string> pair in _labelLastText)
            {
                if (pair.Key.StartsWith(prefix) && !currentKeys.ContainsKey(pair.Key))
                    hidden.Add(pair.Key);
            }

            for (int i = 0; i < hidden.Count; i++)
            {
                _labelLastText.Remove(hidden[i]);
            }

            if (batch.Length > 0)
            {
                ScreenReader.SayCoalesced(batch.ToString(), true, true);
            }
        }

        private void SeedLabelState(string windowName, List<LabelEntry> labels)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                _labelLastText[windowName + "/" + labels[i].Path] = labels[i].Text;
            }
        }

        private void ClearLabelStateFor(string windowName)
        {
            // Remove per-label memory when a window hides so reopening reads fresh.
            List<string> stale = new List<string>();
            string prefix = windowName + "/";
            foreach (KeyValuePair<string, string> pair in _labelLastText)
            {
                if (pair.Key.StartsWith(prefix))
                    stale.Add(pair.Key);
            }

            for (int i = 0; i < stale.Count; i++)
            {
                _labelLastText.Remove(stale[i]);
                _labelFlap.Remove(stale[i]);
            }
        }

        private bool IsFlapping(string key)
        {
            FlapState state;
            if (!_labelFlap.TryGetValue(key, out state))
            {
                state = new FlapState();
                state.WindowStart = Time.unscaledTime;
                _labelFlap[key] = state;
            }

            if (Time.unscaledTime < state.MutedUntil)
                return true;

            if (Time.unscaledTime - state.WindowStart > FlapWindowSeconds)
            {
                state.WindowStart = Time.unscaledTime;
                state.Changes = 0;
            }

            state.Changes++;
            if (state.Changes > FlapChangeLimit)
            {
                state.MutedUntil = Time.unscaledTime + FlapMuteSeconds;
                DebugLogger.Log(LogCategory.Handler, "WindowWatcher", "muting flapping label " + key);
                return true;
            }

            return false;
        }

        private struct LabelEntry
        {
            public string Path;
            public string Text;
            public Vector3 Position;

            /// <summary>
            /// True when the label sits inside a menu-list button; its changes are
            /// announced by the focus handler, not the generic change tracker.
            /// </summary>
            public bool OwnedByListButton;

            /// <summary>
            /// For labels on a journal page (T_32AFC3D1), the label's index within the
            /// page's authored m_labels array — the narrative reading order the page is
            /// filled in. -1 for non-page labels, which fall back to positional order.
            /// </summary>
            public int PageOrder;

            /// <summary>
            /// A stable per-page grouping value (the page root's screen y) so multiple
            /// journal pages stay in top-to-bottom order while each page's own rows keep
            /// their authored sequence. 0 for non-page labels.
            /// </summary>
            public float PageGroupY;
        }

        private static List<LabelEntry> CollectVisibleLabels(T_D25B278 window)
        {
            List<LabelEntry> result = new List<LabelEntry>();
            T_A243E23D[] labels = window.GetComponentsInChildren<T_A243E23D>(false);

            for (int i = 0; i < labels.Length; i++)
            {
                T_A243E23D label = labels[i];
                if (label == null || label.alpha <= 0.01f)
                    continue;

                string text = TextUtil.CleanLabel(label.text);
                if (text.Length == 0)
                    continue;

                // SMS message rows (T_AD5CB3F7) are read by SmsPatches, which speaks
                // only the unread portion of a thread. Skip their labels here so the
                // journal's SMS tab isn't also read in full by this generic net.
                if (label.GetComponentInParent<T_AD5CB3F7>() != null)
                    continue;

                // The global options bar (T_1FBA79F9) holds persistent action prompts
                // (Select / Back / Exit). It re-enters the visible set on every page
                // and, spoken as "new" content, interrupts whatever is being read
                // (e.g. the SMS thread). Focused buttons are narrated by FocusHandler.
                if (label.GetComponentInParent<T_1FBA79F9>() != null)
                    continue;

                LabelEntry entry = new LabelEntry();
                entry.Path = GetPath(label.transform, window.transform);
                entry.Text = text;
                entry.Position = label.transform.position;

                T_9E2FDFCF owningButton = label.GetComponentInParent<T_9E2FDFCF>();
                entry.OwnedByListButton = owningButton != null && owningButton.m_menu != null;

                // Journal pages fill their m_labels array in narrative order, and that
                // order does NOT always match screen layout — choice-dependent rows get
                // inserted at positions that read out of sequence under a pure top-to-
                // bottom sort. Anchor page labels to their authored array index so each
                // page reads in the order it was written.
                entry.PageOrder = -1;
                T_32AFC3D1 page = label.GetComponentInParent<T_32AFC3D1>();
                if (page != null && page.m_labels != null)
                {
                    for (int j = 0; j < page.m_labels.Length; j++)
                    {
                        if (page.m_labels[j] == label)
                        {
                            entry.PageOrder = j;
                            entry.PageGroupY = page.transform.position.y;
                            break;
                        }
                    }
                }

                result.Add(entry);
            }

            // Reading order: journal page rows keep their authored array order (grouped
            // per page, pages top to bottom); everything else is top to bottom, then
            // left to right (NGUI y grows upward).
            result.Sort(CompareForReading);
            return result;
        }

        private static int CompareForReading(LabelEntry a, LabelEntry b)
        {
            // Each entry gets one vertical anchor: a page label uses its page root's y
            // so all of a page's rows group together; a loose label uses its own y.
            // NGUI y grows upward, so larger y reads first (top to bottom).
            float anchorA = a.PageOrder >= 0 ? a.PageGroupY : a.Position.y;
            float anchorB = b.PageOrder >= 0 ? b.PageGroupY : b.Position.y;
            if (anchorA != anchorB)
                return anchorB.CompareTo(anchorA);

            // Same vertical group: within one page, authored array order; otherwise
            // left to right. (Two labels of the same page share PageGroupY, so they land
            // here and order by index — the narrative sequence.)
            if (a.PageOrder >= 0 && b.PageOrder >= 0)
                return a.PageOrder.CompareTo(b.PageOrder);

            return a.Position.x.CompareTo(b.Position.x);
        }

        private static int CompareByPosition(LabelEntry a, LabelEntry b)
        {
            int byY = b.Position.y.CompareTo(a.Position.y);
            if (byY != 0)
                return byY;

            return a.Position.x.CompareTo(b.Position.x);
        }

        private static string GetPath(Transform node, Transform root)
        {
            StringBuilder builder = new StringBuilder(node.name);
            Transform current = node.parent;
            while (current != null && current != root)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Window name -> the game's own localization key for its title, so window
        /// names are spoken in the game's language with its official wording.
        /// </summary>
        private static readonly Dictionary<string, string> _windowTitleKeys = new Dictionary<string, string>
        {
            { "PauseWindow", "SETTINGS_UI_KEYBIND_PAUSE" },
            { "SettingsWindow", "MAINMENU_UI_SETTINGS" },
            { "DarkSettingsWindow", "MAINMENU_UI_SETTINGS" },
            { "JournalWindow", "GLOBAL_CODE_JOURNAL" },
            { "SceneSelectionWindow", "MAINMENU_UI_EPISODES" },
            { "ExtrasWindow", "MAINMENU_UI_EXTRAS" },
            { "CreditsWindow", "EXTRAS_UI_CREDITS" },
            { "HowToPlayWindow", "HOWTOPLAY_UI_HOWIMOVE_TITLE_PC" },
            { "TreasureMapWindow", "GLOBAL_CODE_MAP" },
            { "ZenJukeboxWindow", "EXTRAS_UI_ZEN_JUKEBOX" },
            { "LoadingWindow", "LOADING_CODE_LOADING" },
        };

        /// <summary>
        /// Spoken title for a window, or empty to announce only its content. Windows
        /// the game gives no title (tutorials, popups, posters, the phone, whose
        /// screens carry their own headings) are not named: a name invented from the
        /// GameObject would be English in every language and add nothing the
        /// content doesn't already say.
        /// </summary>
        private static string WindowTitle(string windowName)
        {
            if (windowName == "MainMenuWindow")
                return Loc.Get("window_main_menu");

            string key;
            if (_windowTitleKeys.TryGetValue(windowName, out key))
                return GameRefs.GameText(key);

            return string.Empty;
        }
    }
}
