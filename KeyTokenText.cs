using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Turns the text of the game's dynamic labels (How to Play pages, tutorial
    /// prompts) into speech. The game writes key bindings into that text as inline
    /// tokens ("Use [W][A][S][D] to move.") which render as key icons, so read raw
    /// they're noise. Tokens become the keys' names in the game's language
    /// ("Use W, A, S, D to move."), icon tokens become words, and the "-" bullets
    /// that start each How to Play line are dropped.
    /// </summary>
    internal static class KeyTokenText
    {
        // NOTE: no RegexOptions.Compiled on the game's Mono (see TextUtil).
        private static readonly Regex _bullet = new Regex(@"(^|[\r\n])[ \t]*-[ \t]*");
        private static readonly Regex _tokenRun = new Regex(@"(?:\[[^\[\]\s]+\])+");
        private static readonly Regex _token = new Regex(@"\[([^\[\]\s]+)\]");

        // NGUI markup shares the bracket syntax; leave it for TextUtil.CleanLabel.
        private static readonly Regex _nguiMarkup = new Regex(@"^(?:[0-9A-Fa-f]{6}|-|/?[bius]|/?su[bp]|url=.*|/url)$");

        private static readonly HashSet<string> _loggedUnknown = new HashSet<string>();

        /// <summary>
        /// Speech text for a dynamic label's raw text; markup is left in place for
        /// TextUtil.CleanLabel, which callers still apply.
        /// </summary>
        public static string ToSpeech(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            string text = _bullet.Replace(raw, "$1");
            return _tokenRun.Replace(text, DescribeRun);
        }

        /// <summary>
        /// One run of adjacent tokens, e.g. "[W][A][S][D]" -> "W, A, S, D".
        /// </summary>
        private static string DescribeRun(Match run)
        {
            StringBuilder markup = new StringBuilder();
            List<string> names = new List<string>();

            foreach (Match token in _token.Matches(run.Value))
            {
                string inner = token.Groups[1].Value;
                if (_nguiMarkup.IsMatch(inner))
                {
                    markup.Append(token.Value);
                    continue;
                }

                string name = NameFor(token.Value);
                if (name.Length > 0)
                    names.Add(name);
            }

            return markup + string.Join(", ", names.ToArray());
        }

        private static string NameFor(string token)
        {
            switch (token)
            {
                case "[MOUSE]":
                    return Loc.Get("key_mouse");
                case "[CONSEQ]":
                    return Loc.Get("icon_consequence");
                case "[NEW]":
                    return Loc.Get("icon_new");
            }

            KeyCode key;
            if (GameRefs.InlineKeyTokens().TryGetValue(token, out key))
                return FocusHandler.PrettyKeyName(key);

            // Controller button icons ([PAD], [XR2]...) have no translated names in
            // the game; they're skipped rather than spoken as letter soup.
            if (_loggedUnknown.Add(token))
                DebugLogger.Log(LogCategory.Handler, "KeyTokenText", "no spoken name for icon token " + token);
            return string.Empty;
        }
    }
}
