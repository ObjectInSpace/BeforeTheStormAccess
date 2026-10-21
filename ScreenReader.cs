using System;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Screen reader announcements: duplicate suppression, coalesced interrupts and
    /// repeat-last on top of the Prism speech backend (PrismSpeech).
    /// Ported from Date Everything Access; net35-safe.
    /// </summary>
    public static class ScreenReader
    {
        private static readonly object _speechLock = new object();

        private static bool _available;
        private static bool _initialized;
        private static string _lastSpokenText;
        private static string _lastRepeatableText;

        // Duplicate suppression: multiple announcers (focus reader, window label
        // tracker, patches) can legitimately observe the same text change on the
        // same tick or across adjacent ticks. Speaking it once is correct; the
        // repeat-last key bypasses this via RepeatLastSpoken.
        private const int DuplicateWindowMs = 800;
        private static int _lastSpokenTick;

        // Coalesced-cycle interrupt. Ambient announcers run as a chain every poll tick.
        // They must NOT cut each other off WITHIN a tick, but the FIRST of them in a tick
        // SHOULD interrupt stale speech from earlier ticks. The watcher calls
        // BeginCoalescedCycle() once at the top of the ambient chain; the first
        // SayCoalesced() that actually emits consumes the flag and interrupts, the rest in
        // that tick append. Menus don't use this — they call Say(interrupt:true) directly.
        private static bool _coalescedInterruptPending;

        /// <summary>
        /// Loads Prism and selects the running screen reader, or the OS voice when none
        /// is running. Prism logs which backend it picked.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _available = PrismSpeech.Initialize();
            if (!_available)
                Main.Log.LogWarning("Speech output disabled; announcements are only logged.");

            _initialized = true;
        }

        /// <summary>
        /// Speaks text through the screen reader and optionally remembers it for replay.
        /// </summary>
        public static void Say(string text, bool interrupt = true, bool remember = true, bool rememberAsRepeatable = false)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
                return;

            lock (_speechLock)
            {
                int now = Environment.TickCount;
                if (text == _lastSpokenText && unchecked(now - _lastSpokenTick) < DuplicateWindowMs)
                {
                    DebugLogger.Log(LogCategory.State, "ScreenReader", "duplicate suppressed: " + text);
                    return;
                }

                _lastSpokenTick = now;
            }

            if (remember || rememberAsRepeatable)
            {
                lock (_speechLock)
                {
                    if (remember)
                    {
                        _lastSpokenText = text;
                    }

                    if (rememberAsRepeatable)
                    {
                        _lastRepeatableText = text;
                    }
                }
            }

            DebugLogger.LogScreenReader(text);

            if (!_available)
                return;

            try
            {
                Output(text, interrupt);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("ScreenReader.Say failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Arms the next coalesced announcement of this poll tick to interrupt. Call once
        /// at the start of the ambient announcer chain.
        /// </summary>
        public static void BeginCoalescedCycle()
        {
            _coalescedInterruptPending = true;
        }

        /// <summary>
        /// Speaks an ambient announcement that coalesces within a poll tick: the FIRST one
        /// to emit after BeginCoalescedCycle() interrupts stale speech from earlier ticks;
        /// later ones in the same tick append so co-occurring announcements all play.
        /// </summary>
        public static void SayCoalesced(string text, bool remember = true, bool rememberAsRepeatable = false)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
                return;

            bool interrupt = _coalescedInterruptPending;
            _coalescedInterruptPending = false;
            Say(text, interrupt, remember, rememberAsRepeatable);
        }

        /// <summary>
        /// Sends text to Prism, issuing an explicit stop first when interrupting.
        ///
        /// With Tolk, the bundled cancel in an interrupting output did not clear NVDA's
        /// speech queue while NVDA was in SLEEP MODE; a separate, ordered stop before the
        /// output did. Kept for Prism's NVDA backend, which uses the same controller
        /// client. Harmless when NVDA is awake.
        /// </summary>
        private static void Output(string text, bool interrupt)
        {
            if (interrupt)
            {
                try
                {
                    PrismSpeech.Stop();
                }
                catch
                {
                    // A failed pre-silence must not block the speak that follows.
                }
            }

            PrismSpeech.Output(text, interrupt);
        }

        /// <summary>
        /// Repeats the most recently spoken text when one is available.
        /// </summary>
        public static bool RepeatLastSpoken(bool interrupt = true)
        {
            string lastRepeatableText;
            string lastSpokenText;

            lock (_speechLock)
            {
                lastRepeatableText = _lastRepeatableText;
                lastSpokenText = _lastSpokenText;
            }

            if (!string.IsNullOrEmpty(lastRepeatableText))
            {
                lastSpokenText = lastRepeatableText;
            }

            if (string.IsNullOrEmpty(lastSpokenText))
                return false;

            DebugLogger.LogScreenReader(lastSpokenText);

            if (!_available)
                return true;

            try
            {
                Output(lastSpokenText, interrupt);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("ScreenReader.RepeatLastSpoken failed: " + ex.Message);
            }

            return true;
        }

        /// <summary>
        /// Stops any current speech output.
        /// </summary>
        public static void Stop()
        {
            if (!_available)
                return;

            try
            {
                PrismSpeech.Stop();
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("ScreenReader.Stop failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Releases the speech backend and clears cached speech state.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
                return;

            PrismSpeech.Shutdown();

            _initialized = false;
            _available = false;
            lock (_speechLock)
            {
                _lastSpokenText = null;
                _lastRepeatableText = null;
            }
        }
    }
}
