using HarmonyLib;
using UnityEngine;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Backtalk challenge feedback: points won and lost (with running score),
    /// win/loss results, and a low-time warning while the answer timer runs.
    /// Choice options themselves are read by the choice announcer.
    /// </summary>
    public class BacktalkHandler
    {
        private const float PollInterval = 0.2f;
        private const float LowTimeFraction = 0.3f;
        private const float RearmFraction = 0.6f;

        private float _nextPollTime;
        private bool _lowTimeArmed = true;
        private bool _wasInBacktalk;

        /// <summary>Polls the backtalk timer for the low-time warning.</summary>
        public void Update()
        {
            if (Time.unscaledTime < _nextPollTime)
                return;

            _nextPollTime = Time.unscaledTime + PollInterval;

            T_6D7C1D07 backtalk = GameRefs.BacktalkManager;
            bool inBacktalk = backtalk != null && backtalk.BacktalkRunning;

            if (inBacktalk != _wasInBacktalk)
            {
                _wasInBacktalk = inBacktalk;
                _lowTimeArmed = true;
                BacktalkPatches.ResetScoreTracking();
                if (inBacktalk)
                {
                    string opponent = backtalk.Opponent;
                    ScreenReader.SayCoalesced(Loc.Get("bt_started",
                        string.IsNullOrEmpty(opponent) ? Loc.Get("bt_unknown_opponent") : opponent,
                        backtalk.RequiredPoints), true, true);
                }
                return;
            }

            if (!inBacktalk)
                return;

            T_3A129DE7 timer = backtalk.m_pointsTimer;
            if (timer == null || !timer.Running || timer.TimeSet <= 0f)
                return;

            float remaining = 1f - timer.TimeElapsed / timer.TimeSet;
            if (_lowTimeArmed && remaining < LowTimeFraction)
            {
                _lowTimeArmed = false;
                ScreenReader.SayCoalesced(Loc.Get("bt_time_low"), false, false);
            }
            else if (!_lowTimeArmed && remaining > RearmFraction)
            {
                // Timer was reset for the next round: allow the warning again.
                _lowTimeArmed = true;
            }
        }
    }

    /// <summary>
    /// Harmony patches on the DialogWindow score displays. The game calls these
    /// repeatedly while tweens run, so both patches announce only on actual change.
    /// </summary>
    internal static class BacktalkPatches
    {
        private static int _lastPoints = -1;
        private static int _lastPointsLost = -1;

        internal static void ResetScoreTracking()
        {
            _lastPoints = -1;
            _lastPointsLost = -1;
        }

        [HarmonyPatch(typeof(T_A35DE5E1), "UpdatePoints")]
        internal static class PointsWonPatch
        {
            private static void Postfix(int _163886DF5)
            {
                try
                {
                    if (_163886DF5 == _lastPoints)
                        return;

                    _lastPoints = _163886DF5;
                    T_6D7C1D07 backtalk = GameRefs.BacktalkManager;
                    if (backtalk == null || !backtalk.InBacktalk || _163886DF5 <= 0)
                        return;

                    if (_163886DF5 >= backtalk.RequiredPoints)
                    {
                        ScreenReader.Say(Loc.Get("bt_won"), true, true, true);
                    }
                    else
                    {
                        ScreenReader.Say(Loc.Get("bt_point_won", _163886DF5, backtalk.RequiredPoints), false, true);
                    }
                }
                catch (System.Exception ex)
                {
                    Main.Log.LogWarning("PointsWonPatch failed: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(T_A35DE5E1), "UpdatePointsLost")]
        internal static class PointsLostPatch
        {
            private static void Postfix(int _1B4719EFE)
            {
                try
                {
                    if (_1B4719EFE == _lastPointsLost)
                        return;

                    _lastPointsLost = _1B4719EFE;
                    T_6D7C1D07 backtalk = GameRefs.BacktalkManager;
                    if (backtalk == null || !backtalk.InBacktalk || _1B4719EFE <= 0)
                        return;

                    int allowed = backtalk.PointsPossible - backtalk.RequiredPoints + 1;
                    if (_1B4719EFE >= allowed)
                    {
                        ScreenReader.Say(Loc.Get("bt_lost"), true, true, true);
                    }
                    else
                    {
                        ScreenReader.Say(Loc.Get("bt_point_lost", _1B4719EFE, allowed), false, true);
                    }
                }
                catch (System.Exception ex)
                {
                    Main.Log.LogWarning("PointsLostPatch failed: " + ex.Message);
                }
            }
        }
    }
}
