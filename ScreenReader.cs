using System;
using System.Runtime.InteropServices;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Minimal Tolk wrapper for screen reader announcements.
    /// Ported from Date Everything Access; net35-safe.
    /// </summary>
    public static class ScreenReader
    {
        private static readonly object _speechLock = new object();

        [DllImport("Tolk.dll")]
        private static extern void Tolk_Load();

        [DllImport("Tolk.dll")]
        private static extern void Tolk_Unload();

        [DllImport("Tolk.dll")]
        private static extern bool Tolk_IsLoaded();

        [DllImport("Tolk.dll")]
        private static extern bool Tolk_HasSpeech();

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Output(string text, bool interrupt);

        [DllImport("Tolk.dll")]
        private static extern bool Tolk_Silence();

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr Tolk_DetectScreenReader();

        // Enables Tolk's built-in Windows SAPI driver as a fallback voice. Must be called
        // BEFORE Tolk_Load() to take effect. When no screen reader is running, SAPI lets the
        // mod still speak aloud through the OS voice instead of going silent.
        [DllImport("Tolk.dll")]
        private static extern bool Tolk_TrySAPI(bool trySAPI);

        private static bool _available;
        private static bool _usingSapiFallback;

        /// <summary>
        /// True when Tolk has speech but no live screen reader is running, i.e. output is
        /// going through the built-in SAPI voice fallback.
        /// </summary>
        public static bool IsUsingSapiFallback
        {
            get { return _usingSapiFallback; }
        }

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
        /// Loads Tolk and detects the active screen reader.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                // Enable the SAPI fallback BEFORE loading so Tolk can speak through the OS
                // voice when no screen reader is running. Tolk still prefers a live screen
                // reader when one is present.
                Tolk_TrySAPI(true);

                Tolk_Load();
                _available = Tolk_IsLoaded() && Tolk_HasSpeech();

                if (_available)
                {
                    IntPtr srNamePtr = Tolk_DetectScreenReader();
                    if (srNamePtr != IntPtr.Zero)
                    {
                        string srName = Marshal.PtrToStringUni(srNamePtr);
                        Main.Log.LogInfo("Screen reader detected: " + srName);
                    }
                    else
                    {
                        // No screen reader, but Tolk still has speech — that's the SAPI voice.
                        _usingSapiFallback = true;
                        Main.Log.LogInfo("No screen reader detected; using SAPI voice fallback.");
                    }
                }
                else
                {
                    Main.Log.LogWarning("No screen reader detected and no SAPI voice available; Tolk output disabled.");
                }
            }
            catch (DllNotFoundException)
            {
                Main.Log.LogError("Tolk.dll or nvdaControllerClient64.dll is missing from the game directory.");
                _available = false;
            }
            catch (Exception ex)
            {
                Main.Log.LogError("Failed to initialize Tolk: " + ex.Message);
                _available = false;
            }

            _initialized = true;
        }

        /// <summary>
        /// Speaks text through Tolk and optionally remembers it for replay.
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
        /// Sends text to Tolk, issuing an explicit silence first when interrupting.
        ///
        /// Tolk's own interrupt flag (Tolk_Output(text, true)) bundles a cancel + speak, but with
        /// NVDA in SLEEP MODE that bundled cancel does not clear NVDA's speech queue. Calling
        /// Tolk_Silence() as a separate, ordered call before the output reliably flushes the queue
        /// first. Harmless when NVDA is awake.
        /// </summary>
        private static void Output(string text, bool interrupt)
        {
            if (interrupt)
            {
                try
                {
                    Tolk_Silence();
                }
                catch
                {
                    // A failed pre-silence must not block the speak that follows.
                }
            }

            Tolk_Output(text, interrupt);
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
                Tolk_Silence();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Unloads Tolk and clears cached speech state.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
                return;

            try
            {
                Tolk_Unload();
            }
            catch
            {
            }

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
