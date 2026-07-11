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
        /// Speech-friendly key names for the common bindings; falls back to the
        /// KeyCode name with camel-case splits ("LeftShift" -> "Left Shift").
        /// </summary>
        internal static string PrettyKeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None:
                    return Loc.Get("key_unbound");
                case KeyCode.Mouse0:
                    return Loc.Get("key_left_mouse");
                case KeyCode.Mouse1:
                    return Loc.Get("key_right_mouse");
                case KeyCode.Mouse2:
                    return Loc.Get("key_middle_mouse");
                case KeyCode.BackQuote:
                    return Loc.Get("key_grave");
            }

            string name = key.ToString();
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
