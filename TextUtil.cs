using System.Text.RegularExpressions;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Text cleanup helpers for NGUI-encoded strings.
    /// </summary>
    public static class TextUtil
    {
        // NGUI BBCode-style markup: [RRGGBB] color open, [-] color close, [b]/[i]/[u]/[s]
        // style tags, [sub]/[sup]. Subtitles and labels embed these freely.
        // NOTE: RegexOptions.Compiled is NOT supported by the game's ancient Mono
        // (Unity 5.5) — it throws ArgumentOutOfRangeException at type init. Interpreted
        // regexes only.
        private static readonly Regex _nguiMarkup = new Regex(
            @"\[[0-9A-Fa-f]{6}\]|\[-\]|\[/?[bius]\]|\[/?su[bp]\]|\[url=[^\]]*\]|\[/url\]");

        private static readonly Regex _whitespace = new Regex(@"\s+");

        /// <summary>
        /// Strips NGUI markup and collapses whitespace/newlines for clean speech output.
        /// Returns an empty string for null input.
        /// </summary>
        public static string CleanLabel(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            string text = _nguiMarkup.Replace(raw, string.Empty);
            text = _whitespace.Replace(text, " ");
            return text.Trim();
        }
    }
}
