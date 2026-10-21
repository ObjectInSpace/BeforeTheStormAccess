using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Announces the focused menu element and its value.
    ///
    /// The game does NOT use NGUI's hover for keyboard/controller menus: every menu
    /// owns a button list (T_51AF6A60) with SelectedButtonIndex, updated by both
    /// keyboard navigation and mouse hover. We poll every active list and announce
    /// selection changes. While focus stays on one element, its full text (labels +
    /// slider percentage + bound key) is re-polled so value changes speak too.
    /// NGUI's UICamera.hoveredObject is kept as a secondary source for elements
    /// outside any list.
    /// </summary>
    public class FocusHandler
    {
        private const float PollInterval = 0.12f;

        private float _nextPollTime;
        private GameObject _currentFocus;
        private string _currentFocusText = string.Empty;
        private int _lastHoveredId;
        private readonly Dictionary<int, int> _listSelection = new Dictionary<int, int>();

        /// <summary>
        /// Polls menu button lists, NGUI hover, and the focused element's value.
        /// Runs inside the watcher's coalesced ambient chain.
        /// </summary>
        public void Update()
        {
            if (Time.unscaledTime < _nextPollTime)
                return;

            _nextPollTime = Time.unscaledTime + PollInterval;

            PollButtonLists();
            PollNguiHover();
            PollFocusedValue();
        }

        private void PollButtonLists()
        {
            T_51AF6A60[] lists = Object.FindObjectsOfType<T_51AF6A60>();
            if (lists == null)
                return;

            for (int i = 0; i < lists.Length; i++)
            {
                T_51AF6A60 list = lists[i];
                if (list == null || !list.enabled)
                    continue;

                int listId = list.GetInstanceID();
                int index = list.SelectedButtonIndex;

                int lastIndex;
                bool seen = _listSelection.TryGetValue(listId, out lastIndex);
                if (seen && lastIndex == index)
                    continue;

                _listSelection[listId] = index;

                T_9E2FDFCF selected = list.SelectedButton;
                if (selected == null)
                    continue;

                Announce(selected.gameObject);
            }
        }

        private void PollNguiHover()
        {
            GameObject hovered = T_D90B5C2A.hoveredObject;
            int id = hovered != null ? hovered.GetInstanceID() : 0;
            if (id == _lastHoveredId)
                return;

            _lastHoveredId = id;
            if (hovered == null || (_currentFocus != null && hovered == _currentFocus))
                return;

            // Elements inside a button list are announced by the primary poll.
            T_9E2FDFCF button = hovered.GetComponent<T_9E2FDFCF>();
            if (button != null && button.m_menu != null)
                return;

            Announce(hovered);
        }

        /// <summary>
        /// Re-reads the focused element while focus is steady: option cycling,
        /// slider drags, and key rebinding all change its full text in place.
        /// </summary>
        private void PollFocusedValue()
        {
            if (_currentFocus == null)
                return;

            T_9E2FDFCF button = _currentFocus.GetComponent<T_9E2FDFCF>();
            if (button != null && !button.Selected)
                return;

            string fullText = BuildFullText(_currentFocus);
            if (fullText.Length == 0 || fullText == _currentFocusText)
                return;

            _currentFocusText = fullText;
            DebugLogger.Log(LogCategory.Handler, "Focus", "value change -> " + fullText);
            ScreenReader.SayCoalesced(fullText, true, true);
        }

        private void Announce(GameObject focused)
        {
            if (focused == _currentFocus)
                return;

            // Dialogue/backtalk choice buttons are read by the choice announcer
            // with their direction names; skip them here to avoid double speech.
            if (focused.GetComponent<T_B611E532>() != null)
                return;

            string text = BuildFullText(focused);
            if (text.Length == 0)
                return;

            _currentFocus = focused;
            _currentFocusText = text;
            DebugLogger.Log(LogCategory.Handler, "Focus", focused.name + " -> " + text);
            ScreenReader.SayCoalesced(text, true, true);
        }

        /// <summary>
        /// Full spoken text for an element: visible labels left-to-right, then
        /// slider percentage and bound key name when those components are present.
        /// </summary>
        private static string BuildFullText(GameObject focused)
        {
            StringBuilder builder = new StringBuilder(ComposeLabelText(focused));

            T_AD683E92 sliderButton = focused.GetComponent<T_AD683E92>();
            if (sliderButton != null && sliderButton.m_slider != null)
            {
                int percent = Mathf.RoundToInt(sliderButton.m_slider.value * 100f);
                if (builder.Length > 0)
                    builder.Append(", ");
                builder.Append(Loc.Get("slider_percent", percent));
            }

            T_55D727DD keybind = focused.GetComponent<T_55D727DD>();
            if (keybind == null)
                keybind = focused.GetComponentInParent<T_55D727DD>();
            if (keybind != null)
            {
                if (builder.Length > 0)
                    builder.Append(", ");
                builder.Append(keybind.m_isKeybinding
                    ? Loc.Get("press_key_to_bind")
                    : PrettyKeyName(keybind.m_boundKey));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Builds text from an element's visible labels, left to right
        /// (settings rows: Title label left, value label right).
        /// </summary>
        private static string ComposeLabelText(GameObject focused)
        {
            // SMS contact rows (burner phone + journal SMS tab) carry the contact name
            // and unread count on the sibling T_EF99895D component, not under the
            // button's own GameObject, so the generic label sweep finds nothing. Read
            // the contact directly when the focused button belongs to one.
            string contact = ComposeSmsContact(focused);
            if (contact.Length > 0)
                return contact;

            T_A243E23D[] labels = focused.GetComponentsInChildren<T_A243E23D>(false);
            if (labels == null || labels.Length == 0)
                return string.Empty;

            List<LabeledText> parts = new List<LabeledText>();
            for (int i = 0; i < labels.Length; i++)
            {
                T_A243E23D label = labels[i];
                if (label == null || label.alpha <= 0.01f)
                    continue;

                string text = TextUtil.CleanLabel(label.text);
                if (text.Length == 0)
                    continue;

                LabeledText part = new LabeledText();
                part.Text = text;
                part.X = label.transform.position.x;
                parts.Add(part);
            }

            if (parts.Count == 0)
                return string.Empty;

            parts.Sort(CompareByX);

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(parts[i].Text);
            }

            return builder.ToString();
        }

        /// <summary>
        /// "{contact name}[, N new messages]" when the focused element is an SMS
        /// contact row, else empty. The contact name and unread count live on the
        /// T_EF99895D component (found on the focused object or a parent), whose
        /// name/new-message labels are toggled by selection — so read the resolved
        /// name and unread count from its fields rather than sweeping child labels.
        /// </summary>
        private static string ComposeSmsContact(GameObject focused)
        {
            T_EF99895D contact = focused.GetComponent<T_EF99895D>();
            if (contact == null)
                contact = focused.GetComponentInParent<T_EF99895D>();
            if (contact == null)
                return string.Empty;

            string name = contact.m_name != null ? TextUtil.CleanLabel(contact.m_name.text) : string.Empty;
            if (name.Length == 0)
                return string.Empty;

            // The unread-count label ("1 new message" / "N new messages") is resolved
            // and localized by the game; read whichever variant is currently shown.
            string unread = ReadNewMessageLabel(contact);
            if (unread.Length > 0)
                return Loc.Get("sms_contact_unread", name, unread);

            return name;
        }

        /// <summary>The active new-message label text on a contact row, or empty.</summary>
        private static string ReadNewMessageLabel(T_EF99895D contact)
        {
            string text = LabelIfActive(contact.m_highlightedNewMessage);
            if (text.Length == 0)
                text = LabelIfActive(contact.m_newMessage);
            return text;
        }

        private static string LabelIfActive(T_A243E23D label)
        {
            if (label == null || !label.gameObject.activeInHierarchy)
                return string.Empty;
            return TextUtil.CleanLabel(label.text);
        }

        /// <summary>
        /// Localization keys the game's own keybinding row uses to name keys, so the
        /// mod speaks the same translated names the settings screen shows.
        /// </summary>
        private static readonly Dictionary<KeyCode, string> _gameKeyNames = new Dictionary<KeyCode, string>
        {
            { KeyCode.Mouse0, "GLOBAL_CODE_LEFT_MOUSE_BUTTON" },
            { KeyCode.Mouse1, "GLOBAL_CODE_RIGHT_MOUSE_BUTTON" },
            { KeyCode.Mouse2, "GLOBAL_CODE_MIDDLE_MOUSE_BUTTON" },
            { KeyCode.Mouse3, "GLOBAL_CODE_MOUSE_BUTTON_3" },
            { KeyCode.Mouse4, "GLOBAL_CODE_MOUSE_BUTTON_4" },
            { KeyCode.Mouse5, "GLOBAL_CODE_MOUSE_BUTTON_5" },
            { KeyCode.UpArrow, "GLOBAL_CODE_KEY_UPARROW" },
            { KeyCode.DownArrow, "GLOBAL_CODE_KEY_DOWNARROW" },
            { KeyCode.LeftArrow, "GLOBAL_CODE_KEY_LEFTARROW" },
            { KeyCode.RightArrow, "GLOBAL_CODE_KEY_RIGHTARROW" },
            { KeyCode.RightAlt, "GLOBAL_CODE_KEY_RIGHTALT" },
            { KeyCode.LeftAlt, "GLOBAL_CODE_KEY_LEFTALT" },
            { KeyCode.RightControl, "GLOBAL_CODE_KEY_RIGHTCTRL" },
            { KeyCode.LeftControl, "GLOBAL_CODE_KEY_LEFTCTRL" },
            { KeyCode.Delete, "GLOBAL_CODE_KEY_DELETE" },
            { KeyCode.End, "GLOBAL_CODE_KEY_END" },
            { KeyCode.Return, "GLOBAL_CODE_KEY_ENTER" },
            { KeyCode.Home, "GLOBAL_CODE_KEY_HOME" },
            { KeyCode.Insert, "GLOBAL_CODE_KEY_INSERT" },
            { KeyCode.PageUp, "GLOBAL_CODE_KEY_PAGEUP" },
            { KeyCode.PageDown, "GLOBAL_CODE_KEY_PAGEDOWN" },
            { KeyCode.RightShift, "GLOBAL_CODE_KEY_RIGHTSHIFT" },
            { KeyCode.LeftShift, "GLOBAL_CODE_KEY_LEFTSHIFT" },
            { KeyCode.Space, "GLOBAL_CODE_KEY_SPACE" },
            { KeyCode.Tab, "GLOBAL_CODE_KEY_TAB" },
        };

        /// <summary>
        /// Speech-friendly key name in the game's text language: the game's own
        /// translated name where it has one, digits for number-row keys, otherwise
        /// the KeyCode name with camel-case splits (letters and F-keys need no
        /// translation).
        /// </summary>
        internal static string PrettyKeyName(KeyCode key)
        {
            if (key == KeyCode.None)
                return Loc.Get("key_unbound");
            if (key == KeyCode.BackQuote)
                return Loc.Get("key_grave");

            string name = key.ToString();
            string gameKey;
            if (_gameKeyNames.TryGetValue(key, out gameKey))
            {
                string gameName = GameRefs.GameText(gameKey);
                if (gameName.Length > 0)
                    return gameName;
            }

            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return ((int)(key - KeyCode.Alpha0)).ToString();

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

        private struct LabeledText
        {
            public string Text;
            public float X;
        }

        private static int CompareByX(LabeledText a, LabeledText b)
        {
            return a.X.CompareTo(b.X);
        }
    }
}
