using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SpaceCowboy
{
    /// <summary>
    /// Which input does which mechanic. Every mechanic has one keyboard key and one controller input,
    /// and the player can change either. The rest of the game asks "is Boost pressed?" and never needs
    /// to know what Boost is mapped to. Saved on this computer, like the settings.
    ///
    /// Esc and the controller's Menu button are never mapped to a mechanic: they always open and close
    /// the settings, so a player can't lock themselves out.
    /// </summary>
    public static class ControlBindings
    {
        /// <summary>One input a mechanic listens to, on one kind of device.</summary>
        public class Slot
        {
            readonly string saveKey, defaultPath;
            string path;
            InputDevice foundOn;
            ButtonControl control;

            public Slot(string saveKey, string defaultPath)
            {
                this.saveKey = saveKey;
                this.defaultPath = defaultPath;
            }

            /// <summary>
            /// The input's name on the device, like "leftShift" or "rightStick/up".
            /// Only a change from the default is saved. A mapping the player never changed isn't saved at all,
            /// so it always follows the default written in this file, even after that default is edited.
            /// </summary>
            public string Path
            {
                get
                {
                    if (path == null)
                    {
                        ForgetSavedDefaults();
                        path = PlayerPrefs.GetString(saveKey, defaultPath);
                    }
                    return path;
                }
                set
                {
                    path = value;
                    foundOn = null; // look it up again next time it's read
                    if (value == defaultPath) PlayerPrefs.DeleteKey(saveKey);
                    else PlayerPrefs.SetString(saveKey, value);
                }
            }

            public void Reset() => Path = defaultPath;

            /// <summary>The actual button on this device. Remembered, because it's read every frame.</summary>
            public ButtonControl On(InputDevice device)
            {
                if (device == null) return null;
                if (device != foundOn)
                {
                    foundOn = device;
                    control = device.TryGetChildControl<ButtonControl>(Path);
                }
                return control;
            }
        }

        /// <summary>Something the ship can do, and the inputs that do it.</summary>
        public class Mechanic
        {
            public readonly string label;
            public readonly Slot keyboard, controller;

            public Mechanic(string saveKey, string label, string defaultKey, string defaultController)
            {
                this.label = label;
                keyboard = new Slot($"SpaceCowboy.Bind.{saveKey}.Keyboard", defaultKey);
                controller = new Slot($"SpaceCowboy.Bind.{saveKey}.Controller", defaultController);
            }

            ButtonControl Key => keyboard.On(Keyboard.current);
            ButtonControl Pad => controller.On(Gamepad.current);

            public bool KeyHeld => Key != null && Key.isPressed;
            public bool KeyPressedThisFrame => Key != null && Key.wasPressedThisFrame;

            /// <summary>0 to 1 from the controller. Triggers and sticks are analog: a light squeeze is a small value.</summary>
            public float ControllerValue => Pad != null ? DeadZone(Pad.ReadValue()) : 0f;

            /// <summary>0 to 1 from whichever device is pushing harder.</summary>
            public float Value => Mathf.Max(KeyHeld ? 1f : 0f, ControllerValue);
            public bool Held => KeyHeld || (Pad != null && Pad.isPressed);
            public bool PressedThisFrame => KeyPressedThisFrame || (Pad != null && Pad.wasPressedThisFrame);

            public string KeyName => NameOfKey(keyboard.Path);
            public string ControllerName => NameOfControllerInput(controller.Path);
        }

        public static readonly Mechanic NoseUp = new("NoseUp", "NOSE UP", "s", "leftStick/down");
        public static readonly Mechanic NoseDown = new("NoseDown", "NOSE DOWN", "w", "leftStick/up");
        public static readonly Mechanic TurnLeft = new("TurnLeft", "TURN LEFT", "a", "leftStick/left");
        public static readonly Mechanic TurnRight = new("TurnRight", "TURN RIGHT", "d", "leftStick/right");
        public static readonly Mechanic Accelerate = new("Accelerate", "ACCELERATE", "space", "rightTrigger");
        public static readonly Mechanic Brake = new("Brake", "BRAKE / REVERSE", "c", "leftTrigger");
        public static readonly Mechanic Fire = new("Fire", "FIRE", "enter", "buttonSouth");
        public static readonly Mechanic Boost = new("Boost", "BOOST", "leftShift", "buttonEast");
        public static readonly Mechanic FlipUp = new("FlipUp", "FLIP UP", "downArrow", "rightStick/down");
        public static readonly Mechanic FlipDown = new("FlipDown", "FLIP DOWN", "upArrow", "rightStick/up");
        public static readonly Mechanic FlipLeft = new("FlipLeft", "FLIP LEFT", "leftArrow", "rightStick/left");
        public static readonly Mechanic FlipRight = new("FlipRight", "FLIP RIGHT", "rightArrow", "rightStick/right");
        public static readonly Mechanic ResetShip = new("ResetShip", "RESET SHIP", "r", "select");

        // The first version of the mapping saved every input, even unchanged ones, so a saved copy of an
        // old default would hide a new one. Once, forget any saved input that's still that first default;
        // the inputs a player really changed stay.
        const string ForgotDefaultsKey = "SpaceCowboy.Bind.ForgotSavedDefaults";
        static bool forgotDefaults;
        static readonly (string mechanic, string keyboard, string controller)[] FirstDefaults =
        {
            ("NoseUp", "w", "leftStick/up"), ("NoseDown", "s", "leftStick/down"),
            ("TurnLeft", "a", "leftStick/left"), ("TurnRight", "d", "leftStick/right"),
            ("Accelerate", "space", "rightTrigger"), ("Brake", "c", "leftTrigger"),
            ("Fire", "enter", "buttonSouth"), ("Boost", "leftShift", "buttonEast"),
            ("FlipUp", "upArrow", "rightStick/up"), ("FlipDown", "downArrow", "rightStick/down"),
            ("FlipLeft", "leftArrow", "rightStick/left"), ("FlipRight", "rightArrow", "rightStick/right"),
            ("ResetShip", "r", "select"),
        };

        static void ForgetSavedDefaults()
        {
            if (forgotDefaults) return;
            forgotDefaults = true;
            if (PlayerPrefs.GetInt(ForgotDefaultsKey, 0) == 1) return;
            foreach (var (mechanic, keyboard, controller) in FirstDefaults)
            {
                Forget($"SpaceCowboy.Bind.{mechanic}.Keyboard", keyboard);
                Forget($"SpaceCowboy.Bind.{mechanic}.Controller", controller);
            }
            PlayerPrefs.SetInt(ForgotDefaultsKey, 1);
            PlayerPrefs.Save();

            static void Forget(string key, string firstDefault)
            {
                if (PlayerPrefs.GetString(key, null) == firstDefault) PlayerPrefs.DeleteKey(key);
            }
        }

        public static readonly Mechanic[] All =
        {
            NoseUp, NoseDown, TurnLeft, TurnRight, Accelerate, Brake, Fire, Boost,
            FlipUp, FlipDown, FlipLeft, FlipRight, ResetShip,
        };

        /// <summary>Everything on a controller that can be mapped, and what to call it on screen.</summary>
        static readonly (string path, string name)[] ControllerInputs =
        {
            ("buttonSouth", "A"), ("buttonEast", "B"), ("buttonWest", "X"), ("buttonNorth", "Y"),
            ("leftShoulder", "LB"), ("rightShoulder", "RB"), ("leftTrigger", "LT"), ("rightTrigger", "RT"),
            ("select", "VIEW"),
            ("leftStickPress", "LEFT STICK CLICK"), ("rightStickPress", "RIGHT STICK CLICK"),
            ("dpad/up", "D-PAD UP"), ("dpad/down", "D-PAD DOWN"), ("dpad/left", "D-PAD LEFT"), ("dpad/right", "D-PAD RIGHT"),
            ("leftStick/up", "LEFT STICK UP"), ("leftStick/down", "LEFT STICK DOWN"),
            ("leftStick/left", "LEFT STICK LEFT"), ("leftStick/right", "LEFT STICK RIGHT"),
            ("rightStick/up", "RIGHT STICK UP"), ("rightStick/down", "RIGHT STICK DOWN"),
            ("rightStick/left", "RIGHT STICK LEFT"), ("rightStick/right", "RIGHT STICK RIGHT"),
        };

        /// <summary>An input the player has just pressed while choosing a new one.</summary>
        public readonly struct PressedInput
        {
            public readonly bool isController;
            public readonly string path, name;

            public PressedInput(bool isController, string path, string name)
            {
                this.isController = isController;
                this.path = path;
                this.name = name;
            }
        }

        /// <summary>Esc or the controller's Menu button.</summary>
        public static bool MenuButtonPressed
        {
            get
            {
                var keys = Keyboard.current;
                var pad = Gamepad.current;
                return (keys != null && keys.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            }
        }

        /// <summary>Did the player press anything mappable this frame? Used while waiting for a new input.</summary>
        public static bool TryGetPressedInput(out PressedInput pressed)
        {
            var keys = Keyboard.current;
            if (keys != null)
                foreach (var key in keys.allKeys)
                {
                    if (key == null || key == keys.escapeKey || !key.wasPressedThisFrame) continue;
                    pressed = new PressedInput(false, key.name, NameOfKey(key.name));
                    return true;
                }

            var pad = Gamepad.current;
            if (pad != null)
                foreach (var (path, name) in ControllerInputs)
                {
                    var control = pad.TryGetChildControl<ButtonControl>(path);
                    if (control == null || !control.wasPressedThisFrame) continue;
                    pressed = new PressedInput(true, path, name);
                    return true;
                }

            pressed = default;
            return false;
        }

        /// <summary>Which other mechanic already uses this input? Null if none does.</summary>
        public static Mechanic AlsoUsedBy(Mechanic mechanic, PressedInput input)
        {
            foreach (var other in All)
                if (other != mechanic && SlotFor(other, input).Path == input.path) return other;
            return null;
        }

        /// <summary>Give this mechanic the input. If another mechanic had it, the two swap, so nothing is left without a control.</summary>
        public static void Assign(Mechanic mechanic, PressedInput input)
        {
            var slot = SlotFor(mechanic, input);
            var other = AlsoUsedBy(mechanic, input);
            if (other != null) SlotFor(other, input).Path = slot.Path;
            slot.Path = input.path;
        }

        public static void ResetAll()
        {
            foreach (var mechanic in All)
            {
                mechanic.keyboard.Reset();
                mechanic.controller.Reset();
            }
        }

        /// <summary>Analog inputs below the player's dead zone count as 0.</summary>
        public static float DeadZone(float value) => Mathf.Abs(value) < GameSettings.DeadZoneAmount ? 0f : value;

        static Slot SlotFor(Mechanic mechanic, PressedInput input) => input.isController ? mechanic.controller : mechanic.keyboard;

        static string NameOfKey(string path)
        {
            // Asking the keyboard gives the name printed on the player's own key.
            string shown = Keyboard.current?.TryGetChildControl(path)?.displayName;
            return (string.IsNullOrEmpty(shown) ? path : shown).ToUpperInvariant();
        }

        static string NameOfControllerInput(string path)
        {
            foreach (var input in ControllerInputs)
                if (input.path == path) return input.name;
            return path.ToUpperInvariant();
        }
    }
}
