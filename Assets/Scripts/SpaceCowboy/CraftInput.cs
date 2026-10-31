using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceCowboy
{
    /// <summary>
    /// Everything the craft needs from the player this frame, from the keyboard or a gamepad
    /// (ported from the web game's Input.ts). Both work at once; plug a controller in any time.
    ///
    ///   Keyboard: W/S or ↑/↓ throttle and brake/reverse, A/D or ←/→ steer, hold Space to rise and
    ///             C to fall, Left Shift boost, R reset the craft, B bring the ball back.
    ///   Gamepad:  right trigger throttle, left trigger brake/reverse, left stick steer,
    ///             hold A/Cross or RB to rise, Y/Triangle or LB to drop, B/Circle boost,
    ///             View/Share reset the craft, X/Square reset the ball.
    /// </summary>
    public class CraftInput : MonoBehaviour
    {
        [Tooltip("Stick and trigger values below this count as 0, so a resting stick doesn't drift.")]
        public float deadZone = 0.15f;

        /// <summary>-1 (brake/reverse) .. 1 (accelerate).</summary>
        public float Throttle { get; private set; }
        /// <summary>-1 (left) .. 1 (right).</summary>
        public float Steer { get; private set; }
        /// <summary>1 while rising (held), -1 while falling, 0 to hold the current height.</summary>
        public float Lift { get; private set; }

        // One-shot presses wait here until something uses them. Physics doesn't run every frame,
        // so a press read on a frame with no FixedUpdate would otherwise be lost.
        bool boost, resetCraft, resetBall;

        void Update()
        {
            float throttle = 0f, steer = 0f, lift = 0f;

            var keys = Keyboard.current;
            if (keys != null)
            {
                throttle = Axis(keys.sKey.isPressed || keys.downArrowKey.isPressed, keys.wKey.isPressed || keys.upArrowKey.isPressed);
                steer = Axis(keys.aKey.isPressed || keys.leftArrowKey.isPressed, keys.dKey.isPressed || keys.rightArrowKey.isPressed);
                lift = Axis(keys.cKey.isPressed, keys.spaceKey.isPressed);
                boost |= keys.leftShiftKey.wasPressedThisFrame;
                resetCraft |= keys.rKey.wasPressedThisFrame;
                resetBall |= keys.bKey.wasPressedThisFrame;
            }

            // The gamepad adds on top of the keyboard. Triggers are analog: a light squeeze is a light push.
            var pad = Gamepad.current;
            if (pad != null)
            {
                throttle += DeadZone(pad.rightTrigger.ReadValue()) - DeadZone(pad.leftTrigger.ReadValue());
                steer += DeadZone(pad.leftStick.ReadValue().x);
                // Rise and drop: face buttons, or the bumpers so your thumbs can stay on the sticks.
                lift += Axis(pad.buttonNorth.isPressed || pad.leftShoulder.isPressed, pad.buttonSouth.isPressed || pad.rightShoulder.isPressed);
                boost |= pad.buttonEast.wasPressedThisFrame;
                resetCraft |= pad.selectButton.wasPressedThisFrame;
                resetBall |= pad.buttonWest.wasPressedThisFrame;
            }

            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Steer = Mathf.Clamp(steer, -1f, 1f);
            Lift = Mathf.Clamp(lift, -1f, 1f);
        }

        /// <summary>True once per press: reading it uses it up.</summary>
        public bool TakeBoost() => Take(ref boost);
        public bool TakeResetCraft() => Take(ref resetCraft);
        public bool TakeResetBall() => Take(ref resetBall);

        /// <summary>Drop any waiting presses made on the menu.</summary>
        public void Clear() => boost = resetCraft = resetBall = false;

        static bool Take(ref bool pressed)
        {
            bool was = pressed;
            pressed = false;
            return was;
        }

        static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        float DeadZone(float v) => Mathf.Abs(v) < deadZone ? 0f : v;
    }
}
