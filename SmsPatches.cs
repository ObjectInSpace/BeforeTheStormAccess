using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using _15C6DD6D9;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Reads SMS (phone text) threads for a screen reader, in both places they appear:
    /// the JournalWindow SMS tab (T_6BD5080F) and the standalone burner phone
    /// (T_3FE8D8BE). Both share the same message row (T_AD5CB3F7) and the same page
    /// lifecycle (SetupPage builds the rows, CR_ActiveMessages animates new ones in),
    /// so one handler serves both.
    ///
    /// Two reading modes, decided per thread at build time:
    ///
    /// STATIC open — reopening a thread with no message currently "typing in". Every
    /// row is visible at once. Read as one batch in chronological order: only the
    /// UNREAD messages when the thread has any, otherwise the whole thread (the user
    /// deliberately reopened it).
    ///
    /// LIVE conversation — a new text arrives and the game plays it back one message at
    /// a time (typing dots, then the message pops in). The queued messages are built
    /// hidden (gameObject inactive) and CR_ActiveMessages activates + reveals each in
    /// turn. Reading the whole thread up front would race that animation and read
    /// messages out of order / before they "arrive". Instead each live message is
    /// spoken at the moment it reveals, matching the on-screen pace and order.
    ///
    /// The discriminator between an already-visible row and a queued live one is the
    /// row's own gameObject.activeInHierarchy: AddMessage builds queued rows inactive
    /// (SetActive(_1F2CFCB7.Count == 0)) and CR_ActiveMessages activates them as it
    /// reveals them. So the SetupPage flush reads the currently-active rows as history,
    /// and a row's ShowFullMessage firing while it is (now) active and armed is a live
    /// reveal to be spoken on its own.
    /// </summary>
    public static class SmsHandler
    {
        private struct SmsLine
        {
            public T_AD5CB3F7 Row;   // the row this line came from (for live matching)
            public string Sender;
            public string Content;
            public string TimeStamp;
            public bool Unread;
        }

        // Messages collected during the current SetupPage build, in build (chronological)
        // order. Rebuilt each page; the flush consumes it.
        private static readonly List<SmsLine> _pending = new List<SmsLine>();

        // While armed, a row's ShowFullMessage reveal is spoken on its own (live pace).
        // Armed by the page flush only when the thread actually has queued live rows.
        private static bool _liveArmed;

        // Instance IDs of rows already spoken (as history or live) this page, so a
        // row can't be read twice when ShowFullMessage fires again.
        private static readonly HashSet<int> _spokenRowIds = new HashSet<int>();

        /// <summary>
        /// Records one message row as it is built (T_AD5CB3F7.Setup postfix), while
        /// SMSParent.Seen still holds its pre-open value. The body is read from the
        /// row's resolved label, NOT the node's raw content: content is a localization
        /// KEY (e.g. "SMS_083") the label resolves through the localization sheet, so
        /// reading the label mirrors exactly what the player sees.
        /// </summary>
        public static void RecordRow(T_AD5CB3F7 row, T_200B34A4 message)
        {
            try
            {
                if (row == null || message == null)
                    return;

                bool unread = false;
                T_BFD9C065 parent = message.SMSParent;
                if (parent != null)
                    unread = !parent.Seen;

                string body = row.m_messageLabel != null ? row.m_messageLabel.text : null;
                if (string.IsNullOrEmpty(body))
                    body = message.content;

                string timeStamp = row.m_timeStamp != null ? row.m_timeStamp.text : null;

                SmsLine line = new SmsLine();
                line.Row = row;
                line.Sender = ResolveSenderName(message.sender);
                line.Content = TextUtil.CleanLabel(body);
                line.TimeStamp = TextUtil.CleanLabel(timeStamp);
                line.Unread = unread;
                // NOTE: whether this row is a queued live message can't be read here —
                // AddMessage calls Setup (this postfix) BEFORE it applies
                // SetActive(...) that hides queued rows. The queued/active state is only
                // settled by the SetupPage flush, so liveness is judged there from the
                // row's activeInHierarchy, not captured now.
                _pending.Add(line);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("SmsHandler.RecordRow failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Flushes the built thread (SetupPage postfix, journal or burner). Speaks the
        /// static history now (unread-only when the thread has unread messages); if the
        /// thread has queued live rows, arms per-reveal speech for them instead of
        /// reading them here.
        /// </summary>
        public static void AnnounceThread()
        {
            try
            {
                if (_pending.Count == 0)
                {
                    _liveArmed = false;
                    return;
                }

                // Liveness is settled now (all of AddMessage's SetActive calls have run):
                // a queued live row is currently inactive and will be revealed by
                // CR_ActiveMessages; an already-visible row is static history.
                bool hasLive = false;
                bool hasUnread = false;
                for (int i = 0; i < _pending.Count; i++)
                {
                    if (IsLive(_pending[i]))
                        hasLive = true;
                    if (_pending[i].Unread)
                        hasUnread = true;
                }

                // Live rows speak themselves as they reveal; arm that path.
                _liveArmed = hasLive;

                StringBuilder builder = new StringBuilder();
                int historyCount = 0;
                for (int i = 0; i < _pending.Count; i++)
                {
                    SmsLine line = _pending[i];

                    // Queued live messages are not part of the history batch — each is
                    // spoken on its own when it animates in.
                    if (IsLive(line))
                        continue;

                    // When there are unread messages, skip the already-read history.
                    if (hasUnread && !line.Unread)
                        continue;

                    if (line.Content.Length == 0)
                        continue;

                    AppendLine(builder, line);
                    historyCount++;
                    if (line.Row != null)
                        _spokenRowIds.Add(line.Row.GetInstanceID());
                }

                DebugLogger.Log(LogCategory.Handler, "SMS",
                    "thread flush total=" + _pending.Count + " history=" + historyCount
                    + " live=" + hasLive + " unreadOnly=" + hasUnread);

                if (builder.Length > 0)
                    ScreenReader.Say(builder.ToString(), true, true, true);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("SmsHandler.AnnounceThread failed: " + ex.Message);
            }
            finally
            {
                // _pending is consumed; live rows are matched by instance ID on reveal,
                // so the list itself is no longer needed after the flush.
                _pending.Clear();
            }
        }

        /// <summary>
        /// A message row revealed its full text (T_AD5CB3F7.ShowFullMessage postfix).
        /// During a live conversation this fires as CR_ActiveMessages pops each queued
        /// message in; speak that single message then, in reveal order. Ignored for the
        /// Setup-time call (row not yet active / not armed) and for rows already spoken.
        /// </summary>
        public static void OnRowRevealed(T_AD5CB3F7 row)
        {
            try
            {
                if (!_liveArmed || row == null || row.gameObject == null)
                    return;

                // Only an on-screen row is actually being revealed to the player. The
                // Setup-time ShowFullMessage on a queued row runs while it is inactive.
                if (!row.gameObject.activeInHierarchy)
                    return;

                int id = row.GetInstanceID();
                if (_spokenRowIds.Contains(id))
                    return;

                string body = row.m_messageLabel != null ? row.m_messageLabel.text : null;
                // Guard against the typing-dots placeholder ("." / ".." / "...").
                if (string.IsNullOrEmpty(body) || IsTypingDots(body))
                    return;

                SmsLine line = new SmsLine();
                line.Sender = ResolveSenderName(row.sender);
                line.Content = TextUtil.CleanLabel(body);
                line.TimeStamp = row.m_timeStamp != null ? TextUtil.CleanLabel(row.m_timeStamp.text) : string.Empty;

                if (line.Content.Length == 0)
                    return;

                _spokenRowIds.Add(id);

                StringBuilder builder = new StringBuilder();
                AppendLine(builder, line);
                DebugLogger.Log(LogCategory.Handler, "SMS", "live reveal -> " + builder);

                // Append (interrupt:false) so successive reveals queue in order and
                // don't cut each other off.
                if (builder.Length > 0)
                    ScreenReader.Say(builder.ToString(), false, true, true);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("SmsHandler.OnRowRevealed failed: " + ex.Message);
            }
        }

        /// <summary>Drops any half-collected thread state (window closed, tab reset, back).</summary>
        public static void Reset()
        {
            _pending.Clear();
            _spokenRowIds.Clear();
            _liveArmed = false;
        }

        /// <summary>
        /// A row queued to animate in (a live message) is currently inactive: the
        /// coroutine reveals it later. An already-visible row is static history.
        /// </summary>
        private static bool IsLive(SmsLine line)
        {
            return line.Row != null && line.Row.gameObject != null
                && !line.Row.gameObject.activeInHierarchy;
        }

        private static bool IsTypingDots(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '.')
                    return false;
            }
            return text.Length > 0;
        }

        private static void AppendLine(StringBuilder builder, SmsLine line)
        {
            if (builder.Length > 0 && !EndsWithSpace(builder))
                builder.Append(" ");

            if (!string.IsNullOrEmpty(line.Sender))
                builder.Append(line.Sender).Append(": ");
            builder.Append(line.Content);
            if (!EndsWithSentencePunctuation(line.Content))
                builder.Append(".");

            if (!string.IsNullOrEmpty(line.TimeStamp))
            {
                builder.Append(" ").Append(line.TimeStamp);
                if (!EndsWithSentencePunctuation(line.TimeStamp))
                    builder.Append(".");
            }
        }

        private static bool EndsWithSentencePunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            char last = text[text.Length - 1];
            return last == '.' || last == '!' || last == '?';
        }

        private static bool EndsWithSpace(StringBuilder builder)
        {
            return builder.Length > 0 && builder[builder.Length - 1] == ' ';
        }

        /// <summary>
        /// Localizes a sender token to a readable name, mirroring the game's own SMS
        /// label logic (Chloe's own texts read as "Me").
        /// </summary>
        private static string ResolveSenderName(string sender)
        {
            if (string.IsNullOrEmpty(sender))
                return string.Empty;

            try
            {
                string lower = sender.ToLowerInvariant();
                if (lower.Equals("chloe") || sender.ToUpperInvariant().Equals("SMS_CHARACTER_CHLOE"))
                    return T_A7F32A9D.Get("JOURNAL_CODE_SMS_ME");

                string localized = T_A7F32A9D.Get(sender);
                if (string.IsNullOrEmpty(localized) || localized == sender)
                    return sender;
                return localized;
            }
            catch
            {
                return sender;
            }
        }
    }

    /// <summary>
    /// Postfix on the SMS message row Setup (T_AD5CB3F7, shared by the journal SMS tab
    /// and the burner phone): record the row's resolved body, unread state, and whether
    /// it was built hidden (a queued live message). The page-build prefixes clear state
    /// first, so rows never leak between threads.
    /// </summary>
    [HarmonyPatch(typeof(T_AD5CB3F7), "Setup")]
    internal static class SmsRowSetupPatch
    {
        private static void Postfix(T_AD5CB3F7 __instance, T_200B34A4 __0)
        {
            SmsHandler.RecordRow(__instance, __0);
        }
    }

    /// <summary>
    /// Postfix on the SMS message row ShowFullMessage (T_AD5CB3F7): a live message
    /// revealing its text during CR_ActiveMessages. Spoken on its own in reveal order.
    /// </summary>
    [HarmonyPatch(typeof(T_AD5CB3F7), "ShowFullMessage")]
    internal static class SmsRowShowFullMessagePatch
    {
        private static void Postfix(T_AD5CB3F7 __instance)
        {
            SmsHandler.OnRowRevealed(__instance);
        }
    }

    // ----- Journal SMS tab (T_6BD5080F) -----

    /// <summary>
    /// Journal SMS tab SetupPage: prefix clears prior rows so only this thread collects;
    /// postfix flushes the built thread (history batch + arm live reveals).
    /// </summary>
    [HarmonyPatch(typeof(T_6BD5080F), "SetupPage")]
    internal static class JournalSmsSetupPagePatch
    {
        private static void Prefix()
        {
            SmsHandler.Reset();
        }

        private static void Postfix()
        {
            SmsHandler.AnnounceThread();
        }
    }

    /// <summary>Journal SMS tab ResetTab: drop stale collected state.</summary>
    [HarmonyPatch(typeof(T_6BD5080F), "ResetTab")]
    internal static class JournalSmsResetTabPatch
    {
        private static void Postfix()
        {
            SmsHandler.Reset();
        }
    }

    // ----- Standalone burner phone (T_3FE8D8BE) -----
    // Same row + page shape as the journal tab, so the same SmsHandler covers it.
    // Viewed normally (contacts -> a thread) it reads like the journal; a live incoming
    // text (s_newSMS) reveals message by message through CR_ActiveMessages.

    /// <summary>
    /// Burner phone SetupPage: prefix clears prior rows; postfix flushes the thread.
    /// Mirrors the journal tab so both windows read identically.
    /// </summary>
    [HarmonyPatch(typeof(T_3FE8D8BE), "SetupPage")]
    internal static class BurnerSmsSetupPagePatch
    {
        private static void Prefix()
        {
            SmsHandler.Reset();
        }

        private static void Postfix()
        {
            SmsHandler.AnnounceThread();
        }
    }

    /// <summary>Burner phone BackInSpecific / ResetWindow: drop stale state on leaving a thread.</summary>
    [HarmonyPatch(typeof(T_3FE8D8BE), "BackInSpecific")]
    internal static class BurnerSmsBackPatch
    {
        private static void Postfix()
        {
            SmsHandler.Reset();
        }
    }
}
