using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceCowboy
{
    /// <summary>
    /// Everything the craft needs from the player this frame, from the keyboard or a gamepad
    /// (ported from the web game's Input.ts). Both work at once; plug a controller in any time.
    ///
    /// Space flight reads each mechanic from ControlBindings, so the player can remap all of it.
    /// The defaults are listed there. Tap a flip for 90°; hold it for 180°.
    ///
    /// Hovercraft (Space Flight off on the HoverCraft):
    ///   Keyboard: W/S or ↑/↓ throttle and brake/reverse, A/D or ←/→ steer, hold Space to rise and
    ///             C to fall, Left Shift boost, R reset the craft.
    ///   Gamepad:  right trigger throttle, left trigger brake/reverse, left stick steer,
    ///             hold A/Cross or RB to rise, Y/Triangle or LB to drop, B/Circle boost,
    ///             View/Share reset the craft.
    /// </summary>
    public class CraftInput : MonoBehaviour
    {
        /// <summary>-1 (brake/reverse) .. 1 (accelerate).</summary>
        public float Throttle { get; private set; }
        /// <summary>Negative is left, positive is right. 1 is the ship's normal turn rate; the player's settings can take it past 1.</summary>
        public float Steer { get; private set; }
        /// <summary>1 while rising (held), -1 while falling, 0 to hold the current height.</summary>
        public float Lift { get; private set; }
        /// <summary>Space flight: negative is nose down, positive is nose up. 1 is normal; settings can take it past 1.</summary>
        public float Pitch { get; private set; }
        /// <summary>Space flight: true while the fire button is held.</summary>
        public bool FireHeld { get; private set; }

        HoverCraft craft;

        void Awake() => craft = GetComponent<HoverCraft>();

        // Quick flips. The stick counts as flicked past FlickOn and must come back under FlickOff to flick again.
        const float FlickOn = 0.6f, FlickOff = 0.3f;
        bool flipPressed, stickFlicked;
        Vector2Int flipPress, stickDirection;

        // One-shot presses wait here until something uses them. Physics doesn't run every frame,
        // so a press read on a frame with no FixedUpdate would otherwise be lost.
        bool boost, resetCraft;

        void Update()
        {
            if (craft && craft.spaceFlight) ReadFlight();
            else ReadHover();

            boost |= ControlBindings.Boost.PressedThisFrame;
            resetCraft |= ControlBindings.ResetShip.PressedThisFrame;
        }

        /// <summary>Space flight: every mechanic comes from the player's mapping, and each sensitivity applies to every device.</summary>
        void ReadFlight()
        {
            Pitch = (ControlBindings.NoseUp.Value - ControlBindings.NoseDown.Value) * GameSettings.UpDown.Multiplier;
            Steer = (ControlBindings.TurnRight.Value - ControlBindings.TurnLeft.Value) * GameSettings.LeftRight.Multiplier;
            Throttle = ThrottleFrom(ControlBindings.Accelerate.Value, ControlBindings.Brake.Value);
            Lift = 0f;
            FireHeld = ControlBindings.Fire.Held;
            ReadFlips();
        }

        /// <summary>The hovercraft has its own fixed controls. It isn't part of the mapping, but the sensitivities still apply.</summary>
        void ReadHover()
        {
            float forward = 0f, back = 0f, turn = 0f, lift = 0f;

            var keys = Keyboard.current;
            if (keys != null)
            {
                forward = keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1f : 0f;
                back = keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1f : 0f;
                turn = Axis(keys.aKey.isPressed || keys.leftArrowKey.isPressed, keys.dKey.isPressed || keys.rightArrowKey.isPressed);
                lift = Axis(keys.cKey.isPressed, keys.spaceKey.isPressed);
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                forward = Mathf.Max(forward, ControlBindings.DeadZone(pad.rightTrigger.ReadValue()));
                back = Mathf.Max(back, ControlBindings.DeadZone(pad.leftTrigger.ReadValue()));
                turn += ControlBindings.DeadZone(pad.leftStick.ReadValue().x);
                // Rise and drop: face buttons, or the bumpers so your thumbs can stay on the sticks.
                lift += Axis(pad.buttonNorth.isPressed || pad.leftShoulder.isPressed, pad.buttonSouth.isPressed || pad.rightShoulder.isPressed);
            }

            Throttle = ThrottleFrom(forward, back);
            Steer = Mathf.Clamp(turn, -1f, 1f) * GameSettings.LeftRight.Multiplier;
            Lift = Mathf.Clamp(lift, -1f, 1f);
            Pitch = 0f;
            FireHeld = false;
        }

        static float ThrottleFrom(float forward, float back) =>
            Mathf.Clamp01(forward * GameSettings.Thrust.Multiplier) - Mathf.Clamp01(back * GameSettings.Brake.Multiplier);

        /// <summary>True once per press: reading it uses it up.</summary>
        public bool TakeBoost() => Take(ref boost);
        public bool TakeResetCraft() => Take(ref resetCraft);

        /// <summary>A flip waiting to start (or to be extended): true once per press, with its direction.</summary>
        public bool TakeFlip(out Vector2Int direction)
        {
            direction = flipPress;
            return Take(ref flipPressed);
        }

        /// <summary>Is the stick or arrow key for this flip direction being held right now?</summary>
        public bool IsFlipHeld(Vector2Int direction)
        {
            if (stickFlicked && stickDirection == direction) return true;
            foreach (var (mechanic, way) in Flips)
                if (way == direction) return mechanic.KeyHeld;
            return false;
        }

        void PressFlip(Vector2Int direction)
        {
            flipPress = direction;
            flipPressed = true;
        }

        static readonly (ControlBindings.Mechanic mechanic, Vector2Int way)[] Flips =
        {
            (ControlBindings.FlipUp, Vector2Int.up),
            (ControlBindings.FlipDown, Vector2Int.down),
            (ControlBindings.FlipLeft, Vector2Int.left),
            (ControlBindings.FlipRight, Vector2Int.right),
        };

        void ReadFlips()
        {
            // Keys flip the moment they're pressed. On a controller the strongest direction wins:
            // flips are along one axis at a time, and it works the same if a flip is mapped to a button.
            float strongest = 0f;
            Vector2Int direction = default;
            foreach (var (mechanic, way) in Flips)
            {
                if (mechanic.KeyPressedThisFrame) PressFlip(way);
                if (mechanic.ControllerValue <= strongest) continue;
                strongest = mechanic.ControllerValue;
                direction = way;
            }

            if (stickFlicked)
            {
                if (strongest < FlickOff) stickFlicked = false;
                return;
            }
            if (strongest < FlickOn) return;
            stickDirection = direction;
            stickFlicked = true;
            PressFlip(direction);
        }

        /// <summary>Drop any waiting presses made on the menu.</summary>
        public void Clear() => boost = resetCraft = flipPressed = false;

        static bool Take(ref bool pressed)
        {
            bool was = pressed;
            pressed = false;
            return was;
        }

        static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
