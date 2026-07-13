using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Route guidance to the hotspot browser target using the game's own NavMesh
    /// (the player character walks on it via a NavMeshAgent, so paths match where
    /// the player can actually go).
    ///
    /// N: compute and announce the full route once — total distance, then each leg
    /// as "turn to {clock} o'clock, {distance} meters".
    /// Shift+N: toggle continuous guidance — a short spoken beacon that re-plans
    /// from the current position: on course changes "{clock} o'clock, {distance}
    /// meters", periodic progress otherwise, "Arrived" at the end.
    /// </summary>
    public class NavigationHandler
    {
        private const float SampleRadius = 4f;
        private const int MaxSpokenLegs = 6;
        private const float GuidanceInterval = 0.4f;
        private const float MinArriveDistance = 1.5f;
        private const float MinLegLength = 0.4f;

        private readonly HotspotHandler _hotspots;

        private bool _guiding;
        private T_6FD30C1C _guidanceTarget;
        private float _nextGuidanceTime;
        private int _lastSpokenHour = -1;
        private float _lastAnnounceTime;

        public NavigationHandler(HotspotHandler hotspots)
        {
            _hotspots = hotspots;
        }

        /// <summary>Handles route keys and the guidance loop. Called every frame.</summary>
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.N))
            {
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftHeld)
                {
                    ToggleGuidance();
                }
                else
                {
                    AnnounceRoute();
                }
            }

            if (_guiding)
            {
                GuidanceTick();
            }
        }

        private void ToggleGuidance()
        {
            if (_guiding)
            {
                StopGuidance(announce: true);
                return;
            }

            T_6FD30C1C target = _hotspots.CurrentTarget;
            if (target == null)
            {
                ScreenReader.Say(Loc.Get("no_browser_target"));
                return;
            }

            _guiding = true;
            _guidanceTarget = target;
            _lastSpokenHour = -1;
            _lastAnnounceTime = 0f;
            _nextGuidanceTime = 0f;
            ScreenReader.Say(Loc.Get("nav_guidance_on", HotspotHandler.GetHotspotName(target)), true, true, true);
        }

        private void StopGuidance(bool announce)
        {
            _guiding = false;
            _guidanceTarget = null;
            if (announce)
            {
                ScreenReader.Say(Loc.Get("nav_guidance_off"));
            }
        }

        /// <summary>
        /// Continuous beacon: re-plans from the current position and speaks course
        /// corrections (clock hour changes) immediately, progress periodically.
        /// Course is always relative to the player's CURRENT facing, so it
        /// self-corrects when the player drifts or turns.
        /// </summary>
        private void GuidanceTick()
        {
            if (Time.unscaledTime < _nextGuidanceTime)
                return;

            _nextGuidanceTime = Time.unscaledTime + GuidanceInterval;

            if (_guidanceTarget == null || !_guidanceTarget.gameObject.activeInHierarchy)
            {
                StopGuidance(announce: true);
                return;
            }

            // Only guide while actually walking around; menus/dialogue pause the beacon.
            T_6B664603 modeManager = GameRefs.GameModeManager;
            if (modeManager == null || modeManager.CurrentMode != eGameMode.kFreeRoam)
                return;

            NavMeshPath path = ComputePath(_guidanceTarget, out float total);
            if (path == null)
            {
                ScreenReader.SayCoalesced(Loc.Get("nav_route_lost"), false, false);
                StopGuidance(announce: false);
                return;
            }

            if (total <= ArrivalDistanceFor(_guidanceTarget))
            {
                // Arrival includes where to point the camera: clock hour and
                // elevation to the aim point. Both are measured from the CAMERA
                // (position + forward), because interactability is gated on the
                // camera projecting the aim point on-screen, not on body facing.
                Vector3 aimPoint = HotspotHandler.GetAimPoint(_guidanceTarget);
                int aimHour = SpatialUtil.ClockHour(
                    SpatialUtil.CameraPosition(), SpatialUtil.CameraForward(), aimPoint);
                string arrival = Loc.Get("nav_arrived", HotspotHandler.GetHotspotName(_guidanceTarget))
                    + " " + Loc.Get("nav_aim", aimHour);
                string elevation = SpatialUtil.DescribeElevation(aimPoint);
                if (elevation.Length > 0)
                    arrival += ", " + elevation;

                ScreenReader.Say(arrival + ".", true, true, true);
                StopGuidance(announce: false);
                return;
            }

            Vector3 playerPos = SpatialUtil.PlayerPosition();
            Vector3 steer = FirstMeaningfulCorner(path, playerPos);
            // Steer relative to the CAMERA: movement is camera-relative, so a clock
            // hour the player can act on must be measured against camera forward, not
            // the body (which lags and can face elsewhere while standing/strafing).
            int hour = SpatialUtil.ClockHour(playerPos, SpatialUtil.CameraForward(), steer);

            if (hour != _lastSpokenHour)
            {
                _lastSpokenHour = hour;
                _lastAnnounceTime = Time.unscaledTime;
                ScreenReader.SayCoalesced(
                    Loc.Get("nav_tick_hour", hour, SpatialUtil.FormatDistance(total)), false, false);
            }
            else if (Time.unscaledTime - _lastAnnounceTime > ModConfig.GuidanceProgressSeconds.Value)
            {
                _lastAnnounceTime = Time.unscaledTime;
                // Progress tick appends (no interrupt) so it never cuts off dialogue.
                ScreenReader.Say(Loc.Get("nav_tick_dist", SpatialUtil.FormatDistance(total)), false, false);
            }
        }

        /// <summary>
        /// How close (path length to the aim point) counts as "arrived" for this
        /// target: the hotspot's own m_interactDistanceOverride — the same radius the
        /// interact gate uses — floored at MinArriveDistance so guidance never demands
        /// standing implausibly on top of the object. Fixed 2.0m before, which under-
        /// or over-shot objects whose interact distance differed.
        /// </summary>
        private static float ArrivalDistanceFor(T_6FD30C1C target)
        {
            float interactDistance = target.m_interactDistanceOverride;
            return interactDistance > MinArriveDistance ? interactDistance : MinArriveDistance;
        }

        /// <summary>The first path corner far enough away to steer toward.</summary>
        private static Vector3 FirstMeaningfulCorner(NavMeshPath path, Vector3 playerPos)
        {
            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                if ((corners[i] - playerPos).magnitude >= MinLegLength)
                    return corners[i];
            }

            return corners[corners.Length - 1];
        }

        /// <summary>
        /// NavMesh path from the player to the hotspot, with both ends snapped to
        /// the mesh. Returns null (total 0) when no usable path exists.
        ///
        /// Routes to the hotspot's AIM POINT (m_pointAt), not its root transform:
        /// the game judges interactability from the aim point, so pathing there is
        /// what actually lands the player inside the interact gate. Aim points are
        /// often authored offset from the pivot (and can float off the mesh); the
        /// SamplePosition snap below pulls the goal onto the nearest walkable cell,
        /// which is exactly the "where to stand to interact" spot.
        /// </summary>
        private static NavMeshPath ComputePath(T_6FD30C1C target, out float total)
        {
            total = 0f;

            Vector3 from = SpatialUtil.PlayerPosition();
            Vector3 to = HotspotHandler.GetAimPoint(target);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(from, out hit, SampleRadius, NavMesh.AllAreas))
                from = hit.position;
            if (NavMesh.SamplePosition(to, out hit, SampleRadius, NavMesh.AllAreas))
                to = hit.position;

            NavMeshPath path = new NavMeshPath();
            bool ok = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);
            if (!ok || path.status == NavMeshPathStatus.PathInvalid
                || path.corners == null || path.corners.Length < 2)
            {
                return null;
            }

            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
                total += (corners[i] - corners[i - 1]).magnitude;

            return path;
        }

        private void AnnounceRoute()
        {
            T_6FD30C1C target = _hotspots.CurrentTarget;
            if (target == null)
            {
                ScreenReader.Say(Loc.Get("no_browser_target"));
                return;
            }

            float total;
            NavMeshPath path = ComputePath(target, out total);
            if (path == null)
            {
                ScreenReader.Say(Loc.Get("nav_no_route", HotspotHandler.GetHotspotName(target)));
                return;
            }

            Vector3[] corners = path.corners;

            StringBuilder builder = new StringBuilder();
            builder.Append(Loc.Get("nav_route_header",
                HotspotHandler.GetHotspotName(target), SpatialUtil.FormatDistance(total)));

            if (path.status == NavMeshPathStatus.PathPartial)
            {
                builder.Append(" ").Append(Loc.Get("nav_partial"));
            }

            // First leg is relative to the CAMERA's current facing (movement is
            // camera-relative, so that's the frame the player steers in); later legs
            // are relative to the direction just walked, matching how a person turns.
            Vector3 heading = SpatialUtil.CameraForward();
            int spokenLegs = 0;
            for (int i = 1; i < corners.Length && spokenLegs < MaxSpokenLegs; i++)
            {
                Vector3 legStart = corners[i - 1];
                Vector3 legEnd = corners[i];
                float legLength = (legEnd - legStart).magnitude;
                if (legLength < MinLegLength)
                    continue;

                int hour = SpatialUtil.ClockHour(legStart, heading, legEnd);
                builder.Append(" ").Append(Loc.Get("nav_leg", hour, SpatialUtil.FormatDistance(legLength))).Append(".");

                heading = legEnd - legStart;
                spokenLegs++;
            }

            if (spokenLegs == MaxSpokenLegs && corners.Length - 1 > MaxSpokenLegs)
            {
                builder.Append(" ").Append(Loc.Get("nav_more_legs"));
            }

            string text = builder.ToString();
            DebugLogger.Log(LogCategory.Handler, "Navigation", text);
            ScreenReader.Say(text, true, true, true);
        }
    }
}
