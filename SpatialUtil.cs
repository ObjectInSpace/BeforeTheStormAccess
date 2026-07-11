using UnityEngine;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Shared spatial helpers: player pose and speech-friendly distance/direction.
    /// </summary>
    public static class SpatialUtil
    {
        /// <summary>Player position from the main character mesh; camera fallback.</summary>
        public static Vector3 PlayerPosition()
        {
            GameObject player = T_C3DD66D9.s_mainCharacterMesh;
            if (player != null)
                return player.transform.position;

            Camera camera = Camera.main;
            return camera != null ? camera.transform.position : Vector3.zero;
        }

        /// <summary>Player facing from the main character mesh; camera fallback.</summary>
        public static Vector3 PlayerForward()
        {
            GameObject player = T_C3DD66D9.s_mainCharacterMesh;
            if (player != null)
                return player.transform.forward;

            Camera camera = Camera.main;
            return camera != null ? camera.transform.forward : Vector3.forward;
        }

        /// <summary>
        /// Clock hour (1-12) of `to` as seen from `origin` facing `forward`:
        /// 12 ahead, 3 right, 6 behind, 9 left. Unity 5.5 has no
        /// Vector3.SignedAngle; computed on the ground plane via Atan2.
        /// </summary>
        public static int ClockHour(Vector3 origin, Vector3 forward, Vector3 to)
        {
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z).normalized;
            Vector3 delta = to - origin;
            Vector3 flatTo = new Vector3(delta.x, 0f, delta.z).normalized;
            float angle = Mathf.Atan2(
                Vector3.Cross(flatForward, flatTo).y,
                Vector3.Dot(flatForward, flatTo)) * Mathf.Rad2Deg;

            int hour = Mathf.RoundToInt(angle / 30f);
            hour = ((hour % 12) + 12) % 12;
            return hour == 0 ? 12 : hour;
        }

        /// <summary>Distance formatted for speech: one decimal under 10 meters.</summary>
        public static string FormatDistance(float meters)
        {
            return meters < 10f ? meters.ToString("0.0") : Mathf.RoundToInt(meters).ToString();
        }
    }
}
