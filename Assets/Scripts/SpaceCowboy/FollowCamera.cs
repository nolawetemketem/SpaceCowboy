using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceCowboy
{
    /// <summary>
    /// Third-person camera behind the craft. It keeps the Unity version's feel: it sits up and back
    /// (the old radius and phi) and lerps after the craft, so it lags a little and catches up. Like the
    /// web version it follows on its own: it swings round behind the heading, so you never have to steer it.
    /// The right stick looks around; let go and it eases back behind the craft.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FollowCamera : MonoBehaviour
    {
        public HoverCraft target;

        [Header("Where it sits")]
        [Tooltip("How far from the craft (the old camera's radius).")]
        public float distance = 25f;
        [Tooltip("Angle down from straight overhead (the old camera's phi). 90 = level with the craft.")]
        [Range(10f, 90f)] public float pitch = 70f;
        [Tooltip("How far ahead of the craft it looks, so you can see where you're going (and the ball).")]
        public float lookAhead = 12f;
        public float lookHeight = 2f;

        [Header("Lag")]
        [Tooltip("How quickly the position catches up (1/s). Lower = more lag. The old camera used 5.")]
        public float followSpeed = 5f;
        [Tooltip("How quickly it swings round behind a new heading (1/s).")]
        public float turnSpeed = 6f;

        [Header("Look around (right stick)")]
        [Tooltip("Degrees per second at full stick.")]
        public float lookSpeed = 140f;
        [Tooltip("How quickly it eases back behind the craft once the stick is let go (1/s).")]
        public float recenterSpeed = 3f;

        Vector3 position;
        Vector3 look;
        float yaw;
        float lookOffset;
        // The swoop from the menu's overhead shot down to the craft.
        Vector3 glideFromPosition, glideFromLook;
        float glide = 1f, glideSeconds = 1f;

        /// <summary>Cut straight to the spot behind the craft (after a reset).</summary>
        public void Snap()
        {
            yaw = HeadingYaw();
            lookOffset = 0f;
            Desired(out position, out look);
            Apply(position, look);
        }

        /// <summary>Ease from where the camera is now to its place behind the craft.</summary>
        public void GlideFrom(Vector3 fromPosition, Vector3 fromLook, float seconds)
        {
            glideFromPosition = fromPosition;
            glideFromLook = fromLook;
            glideSeconds = Mathf.Max(0.01f, seconds);
            glide = 0f;
        }

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.deltaTime;

            float stick = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue().x : 0f;
            if (Mathf.Abs(stick) > 0.15f) lookOffset += stick * lookSpeed * dt;
            else lookOffset *= Mathf.Exp(-recenterSpeed * dt);
            lookOffset = Mathf.Clamp(lookOffset, -180f, 180f);

            // Frame-rate independent smoothing: the same lag at 60 Hz and 144 Hz.
            yaw = Mathf.LerpAngle(yaw, HeadingYaw(), 1f - Mathf.Exp(-turnSpeed * dt));
            Desired(out var wantPosition, out var wantLook);
            float follow = 1f - Mathf.Exp(-followSpeed * dt);
            position = Vector3.Lerp(position, wantPosition, follow);
            look = Vector3.Lerp(look, wantLook, follow);

            if (glide < 1f)
            {
                glide = Mathf.Min(1f, glide + dt / glideSeconds);
                float t = glide * glide * (3f - 2f * glide); // ease in and out
                Apply(Vector3.Lerp(glideFromPosition, position, t), Vector3.Lerp(glideFromLook, look, t));
            }
            else Apply(position, look);
        }

        void Desired(out Vector3 wantPosition, out Vector3 wantLook)
        {
            Vector3 craft = target.transform.position;
            Quaternion around = Quaternion.Euler(0f, yaw + lookOffset, 0f);
            Vector3 back = around * Vector3.back;
            float p = pitch * Mathf.Deg2Rad;
            wantPosition = craft + back * (distance * Mathf.Sin(p)) + Vector3.up * (distance * Mathf.Cos(p));
            wantLook = craft + (around * Vector3.forward) * lookAhead + Vector3.up * lookHeight;
        }

        void Apply(Vector3 at, Vector3 lookAt)
        {
            transform.position = at;
            transform.rotation = Quaternion.LookRotation(lookAt - at, Vector3.up);
        }

        float HeadingYaw()
        {
            Vector3 h = target.Heading;
            return Mathf.Atan2(h.x, h.z) * Mathf.Rad2Deg;
        }
    }
}
