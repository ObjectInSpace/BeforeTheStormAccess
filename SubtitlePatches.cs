using System;
using HarmonyLib;
using _15C6DD6D9;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Speaks dialogue lines at the moment the game queues them for subtitling,
    /// independent of the player's subtitle display settings and of NGUI chunking.
    /// Extraction mirrors the game's own SubtitleWindow logic: localized text via
    /// Localization.Get(key, sheet) when localization is on, otherwise the clip's
    /// raw dialogText.
    /// </summary>
    public static class SubtitleAnnouncer
    {
        private static string _lastSpokenNormalized = string.Empty;

        /// <summary>
        /// Normalized form of the last spoken line; the polling fallback uses this
        /// to avoid double-speaking chunks of a line already announced here.
        /// </summary>
        public static string LastSpokenNormalized
        {
            get { return _lastSpokenNormalized; }
        }

        /// <summary>
        /// Extracts and speaks the text of a queued dialogue line.
        /// </summary>
        public static void AnnounceLine(T_E8819104.T_41023B3E line, uint voiceId)
        {
            try
            {
                if (line == null)
                    return;

                T_F95A4243 voice = line.GetVoice(voiceId);
                if (voice == null)
                    return;

                T_82A838F2 clip = voice.audioClip as T_82A838F2;
                if (clip == null)
                    return;

                string text = ExtractText(clip);
                string cleaned = TextUtil.CleanLabel(text);
                if (cleaned.Length == 0)
                    return;

                _lastSpokenNormalized = Normalize(cleaned);
                DebugLogger.Log(LogCategory.Handler, "Subtitle", "line: " + cleaned);
                ScreenReader.Say(cleaned, true, true, true);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("SubtitleAnnouncer.AnnounceLine failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Speaks a forced override subtitle (used by the game for non-dialogue text).
        /// </summary>
        public static void AnnounceOverride(string value)
        {
            string cleaned = TextUtil.CleanLabel(value);
            if (cleaned.Length == 0)
                return;

            _lastSpokenNormalized = Normalize(cleaned);
            DebugLogger.Log(LogCategory.Handler, "Subtitle", "override: " + cleaned);
            ScreenReader.Say(cleaned, true, true, true);
        }

        /// <summary>
        /// True when the given displayed-subtitle text was already covered by the
        /// last line spoken from the patch (equal or a chunk of it).
        /// </summary>
        public static bool AlreadySpoken(string displayedText)
        {
            if (string.IsNullOrEmpty(_lastSpokenNormalized))
                return false;

            string normalized = Normalize(displayedText);
            if (normalized.Length == 0)
                return true;

            return _lastSpokenNormalized == normalized
                || _lastSpokenNormalized.Contains(normalized);
        }

        private static string ExtractText(T_82A838F2 clip)
        {
            if (T_A7F32A9D.s_useLocalization)
            {
                string key = clip.localizationTextKey;
                string sheet = string.Empty;
                T_2AB4DCD8 project = clip.m_owningProject as T_2AB4DCD8;
                if (project != null)
                {
                    sheet = project.localizationSheet;
                }

                if (!IsBlank(key) && !IsBlank(sheet) && !IsBlank(clip.dialogText))
                {
                    return T_A7F32A9D.Get(key, sheet);
                }
            }

            return clip.dialogText ?? string.Empty;
        }

        private static bool IsBlank(string value)
        {
            return value == null || value.Trim().Length == 0;
        }

        private static string Normalize(string text)
        {
            return TextUtil.CleanLabel(text).ToLowerInvariant();
        }
    }

    /// <summary>Postfix on SubtitleWindow.SetupSubtitle: speak each queued line.</summary>
    [HarmonyPatch(typeof(T_2F0DA2D9), "SetupSubtitle")]
    internal static class SubtitleSetupPatch
    {
        private static void Postfix(T_E8819104.T_41023B3E __0, uint __1)
        {
            SubtitleAnnouncer.AnnounceLine(__0, __1);
        }
    }

    /// <summary>Postfix on SubtitleWindow.ForceOverrideSubtitle setter: speak overrides.</summary>
    [HarmonyPatch(typeof(T_2F0DA2D9), "ForceOverrideSubtitle", MethodType.Setter)]
    internal static class SubtitleOverridePatch
    {
        private static void Postfix(string value)
        {
            SubtitleAnnouncer.AnnounceOverride(value);
        }
    }
}
