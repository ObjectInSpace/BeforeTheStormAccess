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
        private const int FlapChangeLimit = 4;
        private const float FlapWindowSeconds = 10f;
        private const float FlapMuteSeconds = 15f;

        // Windows covered by dedicated logic or intentionally silent.
        private static readonly string[] _excludedWindows =
        {
            "SubtitleWindow",   // SubtitlePatches + poll fallback
            "DialogWindow",     // choice reader in AccessibilityWatcher
            "TransitionWindow", // visual fade only
            "MovieWindow",      // video playback
            "FreeRoamWindow",   // Phase 3 (hotspots); its HUD labels flap constantly
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
                builder.Append(Loc.Get("window_opened", PrettyWindowName(name)));
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

        private void AnnounceLabelChanges(T_D25B278 window, string windowName)
        {
            List<LabelEntry> labels = CollectVisibleLabels(window);
            for (int i = 0; i < labels.Count; i++)
            {
                LabelEntry entry = labels[i];
                string key = windowName + "/" + entry.Path;

                string last;
                _labelLastText.TryGetValue(key, out last);
                if (entry.Text == last)
                    continue;

                _labelLastText[key] = entry.Text;

                // Labels inside menu-list buttons (settings values, button captions)
                // are read by the focus handler with their row context; announcing
                // them here too caused multi-repeats on value changes.
                if (entry.OwnedByListButton)
                    continue;

                if (IsFlapping(key))
                    continue;

                DebugLogger.Log(LogCategory.Handler, "WindowWatcher",
                    "label change " + key + " -> " + entry.Text);
                ScreenReader.SayCoalesced(entry.Text, true, false);
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

                LabelEntry entry = new LabelEntry();
                entry.Path = GetPath(label.transform, window.transform);
                entry.Text = text;
                entry.Position = label.transform.position;

                T_9E2FDFCF owningButton = label.GetComponentInParent<T_9E2FDFCF>();
                entry.OwnedByListButton = owningButton != null && owningButton.m_menu != null;

                result.Add(entry);
            }

            // Reading order: top to bottom, then left to right (NGUI y grows upward).
            result.Sort(CompareByPosition);
            return result;
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

        private static string PrettyWindowName(string rawName)
        {
            string name = rawName;
            if (name.EndsWith("Window"))
            {
                name = name.Substring(0, name.Length - "Window".Length);
            }

            // Split camel case for speech: "BurnerSMS" -> "Burner SMS".
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                    builder.Append(' ');
                builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
