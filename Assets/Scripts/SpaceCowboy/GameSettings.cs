using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>
    /// The settings a player can change, named after what the ship does rather than what you hold.
    /// Each one is a value from 0 to 10 and applies to the keyboard and controller alike.
    /// They're saved on this computer, so they're still there next time.
    /// The panel lists whatever is in All, so a new setting only has to be added here.
    /// </summary>
    public static class GameSettings
    {
        public class Setting
        {
            public const float Min = 0f, Max = 10f, Step = 0.5f;

            public readonly string label;
            public readonly float defaultValue;
            readonly string saveKey;
            float? value;

            public Setting(string saveKey, string label, float defaultValue)
            {
                this.saveKey = "SpaceCowboy." + saveKey;
                this.label = label;
                this.defaultValue = defaultValue;
            }

            public float Value
            {
                get
                {
                    if (value == null)
                    {
                        value = PlayerPrefs.GetFloat(saveKey, defaultValue);
                        // A saved copy of the default is forgotten, so a default edited in this file takes effect.
                        if (Mathf.Approximately(value.Value, defaultValue)) PlayerPrefs.DeleteKey(saveKey);
                    }
                    return value.Value;
                }
                set
                {
                    this.value = Mathf.Clamp(Mathf.Round(value / Step) * Step, Min, Max);
                    // Only a change from the default is saved, so editing a default in this file still takes effect.
                    if (Mathf.Approximately(this.value.Value, defaultValue)) PlayerPrefs.DeleteKey(saveKey);
                    else PlayerPrefs.SetFloat(saveKey, this.value.Value);
                }
            }

            /// <summary>Where the value sits between 0 (nothing) and 10 (all the way), as 0 to 1.</summary>
            public float Fraction
            {
                get => Value / Max;
                set => Value = value * Max;
            }

            /// <summary>
            /// What a sensitivity does to the ship: 5 is the ship's normal rate, 10 is double,
            /// and 0 is a quarter, so the ship can still always turn and move.
            /// </summary>
            public float Multiplier => Value <= 5f ? Mathf.Lerp(0.25f, 1f, Value / 5f) : Mathf.Lerp(1f, 2f, (Value - 5f) / 5f);
        }

        /// <summary>How fast the nose moves up and down.</summary>
        public static readonly Setting UpDown = new("Sensitivity.UpDown", "UP / DOWN SENSITIVITY", 5f);
        /// <summary>How fast the ship turns left and right.</summary>
        public static readonly Setting LeftRight = new("Sensitivity.LeftRight", "LEFT / RIGHT SENSITIVITY", 5f);
        /// <summary>How much thrust a press gives. Below 5, a full press is gentler; above 5, a light press reaches full thrust.</summary>
        public static readonly Setting Thrust = new("Sensitivity.Thrust", "THRUST SENSITIVITY", 5f);
        /// <summary>The same, for braking and reversing.</summary>
        public static readonly Setting Brake = new("Sensitivity.Brake", "BRAKE SENSITIVITY", 5f);
        /// <summary>How far an analog input must move before it counts, so a resting stick doesn't drift.</summary>
        public static readonly Setting DeadZone = new("Sensitivity.DeadZone", "DEAD ZONE", 3f);

        public static readonly Setting[] All = { UpDown, LeftRight, Thrust, Brake, DeadZone };

        /// <summary>The dead zone as a share of an input's travel: 10 on the slider is half way.</summary>
        public static float DeadZoneAmount => DeadZone.Value * 0.05f;

        public static void ResetAll()
        {
            foreach (var setting in All) setting.Value = setting.defaultValue;
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
