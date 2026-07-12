using System.Collections.Generic;
using System.Text;
using UnityEngine;
using _15C6DD6D9;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// World interaction access:
    /// - Announces the hotspot and its verb options (with action directions) when the
    ///   interact UI targets one, and "out of range" when it clears.
    /// - Hotspot browser on Left/Right bracket: cycles interactable hotspots in the
    ///   scene sorted by distance, announcing name, distance in meters, clock
    ///   direction relative to the player's facing, and available verbs.
    /// - Backslash: re-announces the current browser target with fresh distance and
    ///   direction (for homing in while walking).
    /// Hotspots are browseable when ANY of their three verb lists (interact,
    /// non-interact, local trigger) has an active entry — signs and ambient
    /// look-only objects live outside m_activeVerbs.
    /// </summary>
    public class HotspotHandler
    {
        private const float PollInterval = 0.15f;

        private float _nextPollTime;
        private T_6FD30C1C _lastInteractHotspot;
        private T_6FD30C1C _browserTarget;
        private int _interactHintsRemaining = -1;

        /// <summary>The object selected in the browser; navigation routes to it.</summary>
        public T_6FD30C1C CurrentTarget
        {
            get { return _browserTarget != null && _browserTarget.gameObject.activeInHierarchy ? _browserTarget : null; }
        }

        /// <summary>
        /// Handles browser keys and polls the interact UI. Called every frame from
        /// the watcher (key handling must not be throttled).
        /// </summary>
        public void Update()
        {
            HandleBrowserKeys();

            if (Time.unscaledTime < _nextPollTime)
                return;

            _nextPollTime = Time.unscaledTime + PollInterval;
            PollInteractUI();
        }

        private void HandleBrowserKeys()
        {
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                CycleBrowser(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                CycleBrowser(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                AnnounceBrowserTarget(refreshOnly: true);
            }
        }

        /// <summary>
        /// Announces verb options when the interact UI acquires or switches hotspots.
        /// </summary>
        private void PollInteractUI()
        {
            T_6FD30C1C current = GetInteractHotspot();
            if (current == _lastInteractHotspot)
                return;

            bool hadHotspot = _lastInteractHotspot != null;
            _lastInteractHotspot = current;

            if (current == null)
            {
                if (hadHotspot && ModConfig.AnnounceOutOfRange.Value)
                {
                    ScreenReader.SayCoalesced(Loc.Get("out_of_range"), true, false);
                }
                return;
            }

            string announcement = GetHotspotName(current) + ". " + DescribeVerbs(current);

            // The interact mechanic is hold-then-direction and is invisible to a
            // screen reader user: teach it with the live binding the first few times.
            if (_interactHintsRemaining < 0)
                _interactHintsRemaining = ModConfig.InteractHintCount.Value;
            if (_interactHintsRemaining > 0)
            {
                _interactHintsRemaining--;
                announcement += " " + Loc.Get("interact_hint", GetInteractKeyName());
            }

            DebugLogger.Log(LogCategory.Handler, "Hotspot", "in range: " + announcement);
            ScreenReader.SayCoalesced(announcement, true, true);
        }

        /// <summary>Speech name of the first key bound to the interact action.</summary>
        private static string GetInteractKeyName()
        {
            T_6FCAE66C inputManager = GameRefs.InputManager;
            if (inputManager != null)
            {
                T_9005A419 binding = inputManager.GetKeyBinding(eGameInput.kInteract);
                if (binding != null && binding.m_key != null && binding.m_key.Count > 0)
                {
                    return FocusHandler.PrettyKeyName(binding.m_key[0]);
                }
            }

            return Loc.Get("key_unknown");
        }

        private static T_6FD30C1C GetInteractHotspot()
        {
            T_F8FE3E1C freeRoam = GameRefs.FreeRoamWindow;
            if (freeRoam != null && freeRoam.gameObject.activeInHierarchy
                && freeRoam.m_interactUI != null && freeRoam.m_interactUI.gameObject.activeInHierarchy)
            {
                return freeRoam.m_interactUI.hotSpotObj;
            }

            return null;
        }

        private void CycleBrowser(int step)
        {
            List<T_6FD30C1C> hotspots = CollectBrowseableHotspots();
            if (hotspots.Count == 0)
            {
                _browserTarget = null;
                ScreenReader.Say(Loc.Get("no_hotspots"));
                return;
            }

            int index = hotspots.IndexOf(_browserTarget);
            if (index < 0)
            {
                // No current target: start with the nearest for ], the farthest for [.
                index = step > 0 ? 0 : hotspots.Count - 1;
            }
            else
            {
                index = ((index + step) % hotspots.Count + hotspots.Count) % hotspots.Count;
            }

            _browserTarget = hotspots[index];
            AnnounceBrowserTarget(refreshOnly: false, index + 1, hotspots.Count);
        }

        private void AnnounceBrowserTarget(bool refreshOnly, int position = 0, int total = 0)
        {
            if (CurrentTarget == null)
            {
                _browserTarget = null;
                ScreenReader.Say(Loc.Get("no_browser_target"));
                return;
            }

            string name = GetHotspotName(_browserTarget);
            string where = DescribeRelativePosition(_browserTarget);
            string text = refreshOnly || total == 0
                ? Loc.Get("hotspot_detail", name, where)
                : Loc.Get("hotspot_cycle", position, total, name, where);

            string verbs = DescribeVerbs(_browserTarget);
            if (verbs.Length > 0)
                text += " " + verbs;

            DebugLogger.Log(LogCategory.Handler, "HotspotBrowser", text);
            ScreenReader.Say(text, true, true, true);
        }

        /// <summary>
        /// Enabled hotspots with at least one active verb in any list, nearest
        /// first. Verb population doubles as the game's availability gating
        /// (user-verified: only the fence had verbs until it was hopped, then the
        /// rest of the yard populated — and routes to the others were partial
        /// until then). Listing verb-less hotspots showed unreachable clutter, so
        /// the verb filter is intentional; do not "fix" it again.
        /// </summary>
        private static List<T_6FD30C1C> CollectBrowseableHotspots()
        {
            List<T_6FD30C1C> result = new List<T_6FD30C1C>();
            T_6FD30C1C[] all = Object.FindObjectsOfType<T_6FD30C1C>();
            if (all == null)
                return result;

            Vector3 origin = SpatialUtil.PlayerPosition();
            for (int i = 0; i < all.Length; i++)
            {
                T_6FD30C1C hotspot = all[i];
                if (hotspot == null)
                    continue;

                if (hotspot.m_hotspotObj != null && !hotspot.m_hotspotObj.Enabled)
                    continue;

                if (CountActiveVerbs(hotspot) == 0)
                    continue;

                result.Add(hotspot);
            }

            result.Sort((a, b) =>
                (a.transform.position - origin).sqrMagnitude.CompareTo(
                    (b.transform.position - origin).sqrMagnitude));
            return result;
        }

        private static int CountActiveVerbs(T_6FD30C1C hotspot)
        {
            return CountActive(hotspot.m_activeVerbs)
                + CountActive(hotspot.m_nonInteractVerbs)
                + CountActive(hotspot.m_localTriggerVerbs);
        }

        private static int CountActive(List<T_6160F3EF> verbs)
        {
            int count = 0;
            if (verbs != null)
            {
                for (int i = 0; i < verbs.Count; i++)
                {
                    if (verbs[i] != null && verbs[i].active)
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Verb options with their action directions, e.g. "up: Look. right: Speak."
        /// (positions match W/D/S/A action buttons). All three verb lists are read,
        /// deduplicated by label.
        /// </summary>
        private static string DescribeVerbs(T_6FD30C1C hotspot)
        {
            StringBuilder builder = new StringBuilder();
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            AppendVerbs(builder, seen, hotspot.m_activeVerbs);
            AppendVerbs(builder, seen, hotspot.m_nonInteractVerbs);
            AppendVerbs(builder, seen, hotspot.m_localTriggerVerbs);
            return builder.ToString();
        }

        private static void AppendVerbs(StringBuilder builder, Dictionary<string, bool> seen, List<T_6160F3EF> verbs)
        {
            if (verbs == null)
                return;

            for (int i = 0; i < verbs.Count; i++)
            {
                T_6160F3EF verb = verbs[i];
                if (verb == null || !verb.active)
                    continue;

                string label = GetVerbLabel(verb);
                if (label.Length == 0 || seen.ContainsKey(label))
                    continue;

                seen[label] = true;
                if (builder.Length > 0)
                    builder.Append(" ");
                builder.Append(Loc.Get("choice_entry", DirectionName(verb.verbPosition), label)).Append(".");
            }
        }

        private static string GetVerbLabel(T_6160F3EF verb)
        {
            string label = null;
            if (T_A7F32A9D.s_useLocalization && !IsBlank(verb.localizationKey))
            {
                label = T_A7F32A9D.Get(verb.localizationKey, verb.LocalizationSheet);
            }

            if (IsBlank(label))
                label = verb.nodeName;

            return TextUtil.CleanLabel(label);
        }

        /// <summary>
        /// Display name using the game's own resolution: localized name key when
        /// available, the authored friendly name otherwise.
        /// </summary>
        public static string GetHotspotName(T_6FD30C1C hotspot)
        {
            string name = null;
            if (T_A7F32A9D.s_useLocalization
                && !IsBlank(hotspot.m_nameKey) && !IsBlank(hotspot.m_localizationSheet))
            {
                name = T_A7F32A9D.Get(hotspot.m_nameKey, hotspot.m_localizationSheet);
            }

            if (IsBlank(name))
                name = hotspot.m_friendlyName;

            if (IsBlank(name))
                name = hotspot.gameObject.name;

            return TextUtil.CleanLabel(name);
        }

        /// <summary>
        /// "{distance} meters at {clock} o'clock[, slightly up]" from the player.
        /// Elevation is measured camera-to-aim-point (the hotspot's m_pointAt when
        /// authored) so it directly answers "where do I point the camera".
        /// </summary>
        private static string DescribeRelativePosition(T_6FD30C1C hotspot)
        {
            Vector3 origin = SpatialUtil.PlayerPosition();
            Vector3 aimPoint = GetAimPoint(hotspot);
            float distance = (aimPoint - origin).magnitude;
            int hour = SpatialUtil.ClockHour(origin, SpatialUtil.PlayerForward(), aimPoint);
            string text = Loc.Get("distance_clock", SpatialUtil.FormatDistance(distance), hour);

            string elevation = SpatialUtil.DescribeElevation(aimPoint);
            if (elevation.Length > 0)
                text += ", " + elevation;

            return text;
        }

        /// <summary>The point the game wants the camera aimed at for this hotspot.</summary>
        internal static Vector3 GetAimPoint(T_6FD30C1C hotspot)
        {
            if (hotspot.m_pointAt != null)
                return hotspot.m_pointAt.transform.position;

            return hotspot.transform.position;
        }

        /// <summary>
        /// Interact-state snapshot for the F10 diagnostic dump: everything needed to
        /// diagnose "says in range but interact does nothing" reports.
        /// </summary>
        public static void DumpInteractState()
        {
            Main.Log.LogInfo("--- Interact state ---");
            Main.Log.LogInfo("s_inHotspotRange=" + T_408CFC35.s_inHotspotRange
                + " controlState=" + T_408CFC35.s_currentControlState
                + " scheme=" + T_408CFC35.CurrentScheme);

            T_6B664603 modeManager = GameRefs.GameModeManager;
            Main.Log.LogInfo("gameMode=" + (modeManager != null ? modeManager.CurrentMode.ToString() : "?")
                + " blockInput=" + (modeManager != null ? modeManager.blockInput.ToString() : "?"));

            Main.Log.LogInfo("s_currentTriggers count=" + T_F8FE3E1C.s_currentTriggers.Count);

            T_6FD30C1C interactTarget = GetInteractHotspot();
            if (interactTarget == null)
            {
                Main.Log.LogInfo("interact UI hotspot: none");
            }
            else
            {
                Vector3 origin = SpatialUtil.PlayerPosition();
                Main.Log.LogInfo("interact UI hotspot='" + GetHotspotName(interactTarget)
                    + "' obj=" + interactTarget.gameObject.name
                    + " playerDistance=" + (interactTarget.transform.position - origin).magnitude.ToString("0.00")
                    + " interactDistanceOverride=" + interactTarget.m_interactDistanceOverride
                    + " usesAngleInteract=" + interactTarget.usesAngleInteract
                    + " leftAngle=" + interactTarget.m_LeftInteractAngle
                    + " rightAngle=" + interactTarget.m_RightInteractAngle
                    + " activeVerbs=" + CountActive(interactTarget.m_activeVerbs)
                    + " nonInteractVerbs=" + CountActive(interactTarget.m_nonInteractVerbs)
                    + " localTriggerVerbs=" + CountActive(interactTarget.m_localTriggerVerbs));
            }

            List<T_6FD30C1C> browseable = CollectBrowseableHotspots();
            Vector3 player = SpatialUtil.PlayerPosition();
            Main.Log.LogInfo("browseable hotspots=" + browseable.Count);
            for (int i = 0; i < browseable.Count; i++)
            {
                T_6FD30C1C h = browseable[i];
                Main.Log.LogInfo("  " + (i + 1) + ". '" + GetHotspotName(h) + "' obj=" + h.gameObject.name
                    + " dist=" + (h.transform.position - player).magnitude.ToString("0.0")
                    + " verbs(a/n/l)=" + CountActive(h.m_activeVerbs)
                    + "/" + CountActive(h.m_nonInteractVerbs)
                    + "/" + CountActive(h.m_localTriggerVerbs));
            }

            // Also list active hotspots we filtered OUT, to catch missing objects
            // (like a sign with all verbs inactive at the time).
            T_6FD30C1C[] all = Object.FindObjectsOfType<T_6FD30C1C>();
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    T_6FD30C1C h = all[i];
                    if (h == null || browseable.Contains(h))
                        continue;

                    Main.Log.LogInfo("  filtered-out: '" + GetHotspotName(h) + "' obj=" + h.gameObject.name
                        + " enabled=" + (h.m_hotspotObj == null || h.m_hotspotObj.Enabled)
                        + " dist=" + (h.transform.position - player).magnitude.ToString("0.0")
                        + " verbs(a/n/l)=" + CountActive(h.m_activeVerbs)
                        + "/" + CountActive(h.m_nonInteractVerbs)
                        + "/" + CountActive(h.m_localTriggerVerbs));
                }
            }

            Main.Log.LogInfo("--- End interact state ---");
        }

        private static string DirectionName(int position)
        {
            switch (position)
            {
                case 0:
                    return Loc.Get("dir_up");
                case 1:
                    return Loc.Get("dir_right");
                case 2:
                    return Loc.Get("dir_down");
                case 3:
                    return Loc.Get("dir_left");
                default:
                    return string.Empty;
            }
        }

        private static bool IsBlank(string value)
        {
            return value == null || value.Trim().Length == 0;
        }
    }
}
