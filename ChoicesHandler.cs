using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Reads the end-of-episode / main-menu "Choices" recap screen
    /// (ChoicesWindow, T_35562F97). That screen lists every major/minor decision
    /// of an episode with, for each, the two options, a marker on the one the
    /// player picked, and — when connected — the worldwide/friends percentage that
    /// picked each option.
    ///
    /// The generic WindowWatcher dumped this whole screen top to bottom (episode
    /// title, both option texts of every decision, tab captions, the options bar),
    /// which read as an undifferentiated wall of text and never conveyed WHICH
    /// option the player chose or the stats — the percentage labels are blanked on
    /// screen; the numbers live only in the option components. This handler reads
    /// the structured data instead: per decision it names the chosen option, the
    /// alternative, and each option's stat when populated.
    ///
    /// The window is excluded from WindowWatcher so the two don't both speak.
    /// </summary>
    public class ChoicesHandler
    {
        private string _lastSignature = string.Empty;

        /// <summary>Polls the choices recap. Called every frame from the watcher.</summary>
        public void Update()
        {
            T_35562F97 window = GameRefs.ChoicesWindow;
            if (window == null || !IsWindowVisible(window))
            {
                _lastSignature = string.Empty;
                return;
            }

            string signature;
            string readout = BuildReadout(window, out signature);
            if (readout.Length == 0)
                return;

            if (signature == _lastSignature)
                return;

            _lastSignature = signature;
            DebugLogger.Log(LogCategory.Handler, "Choices", "recap: " + readout);
            ScreenReader.Say(readout, true, true, true);
        }

        /// <summary>
        /// Re-speaks the currently displayed recap (for the repeat key), or returns
        /// false when the choices recap isn't the active screen.
        /// </summary>
        public static bool RepeatCurrent()
        {
            T_35562F97 window = GameRefs.ChoicesWindow;
            if (window == null || !IsWindowVisible(window))
                return false;

            string signature;
            string readout = BuildReadout(window, out signature);
            if (readout.Length == 0)
                return false;

            ScreenReader.Say(readout, true, true, true);
            return true;
        }

        private static bool IsWindowVisible(T_35562F97 window)
        {
            if (!window.gameObject.activeInHierarchy)
                return false;

            return window.m_panel == null || window.m_panel.alpha > 0.01f;
        }

        /// <summary>
        /// Builds the spoken recap of the currently shown episode and its decisions,
        /// plus a signature that changes whenever the visible content changes (tab
        /// switch, episode change, stats arriving from the server).
        /// </summary>
        private static string BuildReadout(T_35562F97 window, out string signature)
        {
            signature = string.Empty;

            T_AF816971 group = ActiveGroup(window);
            if (group == null || group.m_choices == null)
                return string.Empty;

            StringBuilder sig = new StringBuilder();
            StringBuilder speech = new StringBuilder();

            string title = window.m_episodeTitle != null
                ? TextUtil.CleanLabel(window.m_episodeTitle.text)
                : string.Empty;

            speech.Append(Loc.Get("choices_recap_prefix"));
            if (title.Length > 0)
            {
                speech.Append(" ").Append(title).Append(".");
                sig.Append(title).Append('|');
            }

            int decisionNumber = 0;
            bool anyStat = false;
            for (int i = 0; i < group.m_choices.Length; i++)
            {
                T_8F7CA4EC decision = group.m_choices[i];
                if (decision == null || !decision.gameObject.activeInHierarchy || decision.m_options == null)
                    continue;

                string decisionText = BuildDecision(decision, ref decisionNumber, sig, ref anyStat);
                if (decisionText.Length > 0)
                {
                    speech.Append(" ").Append(decisionText);
                }
            }

            if (decisionNumber == 0)
                return string.Empty;

            // The world/friends percentages arrive from the online stats server. On an
            // offline or not-signed-in session they never populate (every option's
            // m_worldPercent/m_friendPercent stays 0), so a sighted player sees blank
            // percentages too. Rather than omit them silently — which reads as "the mod
            // skipped the stats" — say once why there are none. anyStat feeds the
            // signature, so if stats later arrive the recap re-speaks with them.
            if (!anyStat)
            {
                speech.Append(" ").Append(StatsUnavailableMessage());
                sig.Append("|nostats");
            }

            signature = sig.ToString();
            return speech.ToString();
        }

        /// <summary>
        /// Appends one decision's chosen/alternative options (with stats when
        /// populated) to the signature and returns its spoken form.
        /// </summary>
        private static string BuildDecision(T_8F7CA4EC decision, ref int decisionNumber, StringBuilder sig, ref bool anyStat)
        {
            // Collect the readable options first so an empty decision (no resolved
            // text yet) is skipped rather than announced as a bare number.
            List<OptionInfo> options = new List<OptionInfo>();
            for (int j = 0; j < decision.m_options.Length; j++)
            {
                T_80D0C1C9 option = decision.m_options[j];
                if (option == null || !option.gameObject.activeInHierarchy)
                    continue;

                string text = option.m_choice != null ? TextUtil.CleanLabel(option.m_choice.text) : string.Empty;
                if (text.Length == 0)
                    continue;

                OptionInfo info = new OptionInfo();
                info.Text = text;
                info.Chosen = option.m_isChosen;
                // World stats are shown when the world icon is active, friend stats
                // otherwise; only one set is populated at a time. SetWorld/FriendPercent
                // store a whole percentage point (server "0.67" -> 67), and Setup
                // rebalances the pair to sum to 100 — so the field IS the percentage.
                info.Stat = option.m_worldPercent > 0 ? option.m_worldPercent : option.m_friendPercent;
                if (info.Stat > 0)
                    anyStat = true;

                DebugLogger.Log(LogCategory.Handler, "Choices",
                    "option '" + text + "' world=" + option.m_worldPercent
                    + " friend=" + option.m_friendPercent + " chosen=" + option.m_isChosen);

                options.Add(info);
            }

            if (options.Count == 0)
                return string.Empty;

            decisionNumber++;
            sig.Append('#').Append(decisionNumber).Append(':');

            StringBuilder builder = new StringBuilder();
            builder.Append(Loc.Get("choices_decision", decisionNumber)).Append(" ");

            // Speak the chosen option first, then the alternative(s).
            bool spokeChosen = false;
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantChosen = pass == 0;
                for (int k = 0; k < options.Count; k++)
                {
                    OptionInfo info = options[k];
                    if (info.Chosen != wantChosen)
                        continue;

                    sig.Append(info.Chosen ? 'C' : 'o').Append(info.Text).Append('=').Append(info.Stat).Append('|');

                    string body = info.Stat > 0
                        ? Loc.Get("choices_option_stat", info.Text, info.Stat)
                        : info.Text;

                    if (info.Chosen)
                    {
                        builder.Append(Loc.Get("choices_you_chose", body)).Append(".");
                        spokeChosen = true;
                    }
                    else
                    {
                        builder.Append(" ").Append(Loc.Get("choices_alternative", body)).Append(".");
                    }
                }
            }

            // A decision with no chosen flag (shouldn't happen once Setup ran) still
            // reads its options so nothing is silently dropped.
            if (!spokeChosen)
            {
                DebugLogger.Log(LogCategory.Handler, "Choices",
                    "decision " + decisionNumber + " had no chosen option");
            }

            return builder.ToString();
        }

        /// <summary>
        /// The reason the world/friends percentages are missing, chosen from the
        /// game's network state: offline, signed out, or a generic fallback when the
        /// manager isn't available to say which.
        /// </summary>
        private static string StatsUnavailableMessage()
        {
            T_4CC5666A net = GameRefs.NetworkManager;
            if (net == null)
                return Loc.Get("choices_stats_unavailable");

            if (net.CurrentNetworkState != T_4CC5666A.NetworkState.Online)
                return Loc.Get("choices_stats_offline");

            if (net.CurrentSignInState != T_4CC5666A.NetworkState.Online)
                return Loc.Get("choices_stats_not_signed_in");

            return Loc.Get("choices_stats_unavailable");
        }

        /// <summary>
        /// The choice group currently on screen. ShowMajorChoices/ShowMinorChoices
        /// call ResetWindow (deactivating every group) then activate exactly one, so
        /// the single active group is the visible tab and episode.
        /// </summary>
        private static T_AF816971 ActiveGroup(T_35562F97 window)
        {
            T_AF816971 found = FirstActive(window.m_majorChoices);
            if (found != null)
                return found;

            return FirstActive(window.m_minorChoices);
        }

        private static T_AF816971 FirstActive(T_AF816971[] groups)
        {
            if (groups == null)
                return null;

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].gameObject.activeInHierarchy)
                    return groups[i];
            }

            return null;
        }

        private struct OptionInfo
        {
            public string Text;
            public bool Chosen;
            public int Stat;
        }
    }
}
