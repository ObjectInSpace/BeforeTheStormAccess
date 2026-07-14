using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using _15C6DD6D9;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Reads SMS (phone text) threads in the journal for a screen reader. When a
    /// thread is opened, only the UNREAD portion is spoken if there are unread
    /// messages; a thread with nothing new is read in full (the user deliberately
    /// reopened it).
    ///
    /// The unread state is captured from the game's own "Seen" flags at the moment
    /// each message is added to the thread. The journal SMS tab (T_6BD5080F.SetupPage)
    /// marks every SMS node Seen = true while building the page, so we must observe
    /// the pre-open value inside AddMessage — which runs during each node's traversal,
    /// BEFORE that node is flagged seen. The game itself relies on this ordering
    /// (AddMessage reads !SMSParent.Seen to decide whether to animate a message in).
    ///
    /// AddMessage postfix records each message (sender, content, unread?) in order.
    /// SetupPage postfix — the point where a fully built thread exists — flushes the
    /// collected messages to speech.
    /// </summary>
    public static class SmsHandler
    {
        private struct SmsLine
        {
            public string Sender;
            public string Content;
            public string TimeStamp;
            public bool Unread;
        }

        // Messages collected during the current SetupPage build, in reading order.
        private static readonly List<SmsLine> _pending = new List<SmsLine>();

        /// <summary>
        /// Records one message row as it is built. Called from the T_AD5CB3F7.Setup
        /// postfix, after ShowFullMessage has resolved the label, while SMSParent.Seen
        /// still holds its pre-open value.
        ///
        /// The body is read from the row's resolved label text, NOT the node's raw
        /// content: content is a localization KEY (e.g. "SMS_083") that the label
        /// resolves through the game's localization sheet. Reading the label mirrors
        /// exactly what the player sees.
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
                // Fall back to the node content only if the label is somehow empty;
                // still better than nothing, even if it may be a raw key.
                if (string.IsNullOrEmpty(body))
                    body = message.content;

                // Timestamp is a separate resolved label on the row (may be empty).
                string timeStamp = row.m_timeStamp != null ? row.m_timeStamp.text : null;

                SmsLine line = new SmsLine();
                line.Sender = ResolveSenderName(message.sender);
                line.Content = TextUtil.CleanLabel(body);
                line.TimeStamp = TextUtil.CleanLabel(timeStamp);
                line.Unread = unread;
                _pending.Add(line);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("SmsHandler.RecordRow failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Speaks the thread that was just built. Only the unread messages are read
        /// when the thread has any; otherwise the whole thread is read.
        /// </summary>
        public static void AnnounceThread()
        {
            try
            {
                if (_pending.Count == 0)
                    return;

                bool hasUnread = false;
                for (int i = 0; i < _pending.Count; i++)
                {
                    if (_pending[i].Unread)
                    {
                        hasUnread = true;
                        break;
                    }
                }

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < _pending.Count; i++)
                {
                    SmsLine line = _pending[i];
                    // When there are unread messages, skip the already-read history.
                    if (hasUnread && !line.Unread)
                        continue;

                    if (line.Content.Length == 0)
                        continue;

                    AppendLine(builder, line);
                }

                DebugLogger.Log(LogCategory.Handler, "SMS",
                    "thread announce count=" + _pending.Count + " unreadOnly=" + hasUnread);

                if (builder.Length > 0)
                    ScreenReader.Say(builder.ToString(), true, true, true);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("SmsHandler.AnnounceThread failed: " + ex.Message);
            }
            finally
            {
                _pending.Clear();
            }
        }

        /// <summary>Drops any half-collected thread state (window closed, reset).</summary>
        public static void Reset()
        {
            _pending.Clear();
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

            // Timestamp trails the message, as a short spoken aside.
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
        /// Localizes a sender token to a readable name, mirroring the game's own
        /// SMS label logic (Chloe's own texts read as "Me").
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
                // Localization.Get returns the key unchanged when it has no entry.
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

    // Threads are read via the JournalWindow SMS tab (T_6BD5080F). The standalone
    // burner phone (T_3FE8D8BE) shares the same row/SetupPage shape but is left
    // unpatched until it's actually encountered in-game.

    /// <summary>
    /// Postfix on the SMS message row Setup (T_AD5CB3F7, shared by both SMS windows):
    /// record the row's resolved body text and unread state. The journal SetupPage
    /// prefix clears state before its own rows build, so rows from any unpatched
    /// window never leak into a journal announcement.
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
    /// Patches on the journal SMS tab SetupPage: the prefix clears any prior rows so
    /// only this thread's rows are collected; the postfix speaks the built thread.
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

    /// <summary>Postfix on the journal SMS tab ResetTab: drop stale collected state.</summary>
    [HarmonyPatch(typeof(T_6BD5080F), "ResetTab")]
    internal static class JournalSmsResetTabPatch
    {
        private static void Postfix()
        {
            SmsHandler.Reset();
        }
    }
}
