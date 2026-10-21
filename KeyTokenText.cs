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

            // Keyboard first: "[P]", "[X]", "[PGUP]" are keys, not controller icons.
            KeyCode key;
            if (GameRefs.InlineKeyTokens().TryGetValue(token, out key))
                return FocusHandler.PrettyKeyName(key);

            string padName = ControllerName(token);
            if (padName != null)
                return padName;

            if (_loggedUnknown.Add(token))
                DebugLogger.Log(LogCategory.Handler, "KeyTokenText", "no spoken name for icon token " + token);
            return string.Empty;
        }

        /// <summary>
        /// Controller icon tokens: "[P" (PlayStation) or "[X" (Xbox) plus a button
        /// code (DawnUI's joystick token method; see docs\game-api.md). Labels printed
        /// on the pads (L1, RB, A, Options, Start, Back) are spoken as printed, in
        /// every language, like key letters. Shapes, sticks and the D-pad are
        /// translated. Null when the token isn't a controller icon.
        /// </summary>
        private static string ControllerName(string token)
        {
            if (token.Length < 4 || (token[1] != 'P' && token[1] != 'X'))
                return null;

            if (token == "[XOLDSEL]")
                return "Back"; // Xbox 360 Back, swapped in by the game's How to Play

            bool playStation = token[1] == 'P';
            eControlType pad = GameRefs.ControllerType;
            switch (token.Substring(2, token.Length - 3))
            {
                case "L":
                    return Loc.Get("pad_left_stick");
                case "R":
                    return Loc.Get("pad_right_stick");
                case "L1":
                    return playStation ? "L1" : "LB";
                case "R1":
                    return playStation ? "R1" : "RB";
                case "L2":
                    return playStation ? "L2" : "LT";
                case "R2":
                    return playStation ? "R2" : "RT";
                case "AL":
                    return playStation ? Loc.Get("pad_square") : "X";
                case "AU":
                    return playStation ? Loc.Get("pad_triangle") : "Y";
                case "AR":
                    return playStation ? Loc.Get("pad_circle") : "B";
                case "AD":
                    return playStation ? Loc.Get("pad_cross") : "A";
                case "DPU":
                    return Loc.Get("pad_dpad", Loc.Get("dir_up"));
                case "DPR":
                    return Loc.Get("pad_dpad", Loc.Get("dir_right"));
                case "DPD":
                    return Loc.Get("pad_dpad", Loc.Get("dir_down"));
                case "DPL":
                    return Loc.Get("pad_dpad", Loc.Get("dir_left"));
                case "ST":
                    if (playStation)
                        return pad == eControlType.kPS3 ? "Start" : "Options";
                    return pad == eControlType.kXbox360 ? "Start" : Loc.Get("pad_menu");
                case "SEL":
                    // PS4 reserves Share for the system, so the game's Select is the
                    // touch pad there.
                    if (playStation)
                        return pad == eControlType.kPS3 ? "Select" : Loc.Get("pad_touchpad");
                    return pad == eControlType.kXbox360 ? "Back" : Loc.Get("pad_view");
            }

            return null;
        }
    }
}
