using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceCowboy
{
    /// <summary>
    /// Everything the craft needs from the player this frame, from the keyboard or a gamepad
    /// (ported from the web game's Input.ts). Both work at once; plug a controller in any time.
    ///
    /// Every mechanic comes from ControlBindings, so the player can remap all of it.
    /// The defaults are listed there. Tap a flip for 90°; hold it for 180°.
    /// </summary>
    /// 
    public class CraftInput : MonoBehaviour
    {
        /// <summary>-1 (brake/reverse) .. 1 (accelerate).</summary>
        public float Throttle { get; private set; }
        /// <summary>Negative is left, positive is right. 1 is the ship's normal turn rate; the player's settings can take it past 1.</summary>
        public float Steer { get; private set; }

        /// <summary>Space flight: negative is nose down, positive is nose up. 1 is normal; settings can take it past 1.</summary>
        public float Pitch { get; private set; }
        /// <summary>Space flight: true while the fire button is held.</summary>
        public bool FireHeld { get; private set; }

        // Quick flips. The stick counts as flicked past FlickOn and must come back under FlickOff to flick again.
        const float FlickOn = 0.6f, FlickOff = 0.3f;
        bool flipPressed, stickFlicked;
        Vector2Int flipPress, stickDirection;

        // One-shot presses wait here until something uses them. Physics doesn't run every frame,
        // so a press read on a frame with no FixedUpdate would otherwise be lost.
        bool boost, resetCraft;

        void Update()
        {
            ReadFlight();

            boost |= ControlBindings.Boost.PressedThisFrame;
            resetCraft |= ControlBindings.ResetShip.PressedThisFrame;
        }

        /// <summary>Space flight: every mechanic comes from the player's mapping, and each sensitivity applies to every device.</summary>
        void ReadFlight()
        {
            Pitch = (ControlBindings.NoseUp.Value - ControlBindings.NoseDown.Value) * GameSettings.UpDown.Multiplier;
            Steer = (ControlBindings.TurnRight.Value - ControlBindings.TurnLeft.Value) * GameSettings.LeftRight.Multiplier;
            Throttle = ThrottleFrom(ControlBindings.Accelerate.Value, ControlBindings.Brake.Value);
            FireHeld = ControlBindings.Fire.Held;
            ReadFlips();
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
    }
}
