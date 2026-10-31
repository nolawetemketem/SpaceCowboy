using System;
using UnityEngine;

namespace SpaceCowboy
{
    /// <summary>A fixed time of day, or Auto to follow the player's clock (day from 6:00 to 18:00).</summary>
    public enum TimeOfDaySetting { Auto, Day, Night }

    /// <summary>
    /// Day and night (ported from the web game's palette.ts). Day is the scene as you built it: its sun
    /// and sky are remembered when the game starts. Night is the web game's deep purple sky with a dim,
    /// cool moon. The start menu's TIME OF DAY option switches it; in Auto it follows the clock, live.
    /// </summary>
    public class TimeOfDay : MonoBehaviour
    {
        public TimeOfDaySetting setting = TimeOfDaySetting.Auto;
        [Tooltip("The key light: the sun by day, the moon by night.")]
        public Light sun;

        [Header("Day")]
        [Tooltip("Light the day like the web game: its sun's direction and warm white color. Off = your scene's own sun.")]
        public bool webDaySun = true;
        public Color webSunColor = Palette.Hex("#fff0d8");
        [Tooltip("2 = the toon shader shows colors exactly as they are.")]
        public float webSunIntensity = 2f;
        [Tooltip("The way the web game's sunlight travels (from the sky toward the ground).")]
        public Vector3 webSunDirection = new Vector3(25f, -50f, 15f);

        [Header("Night")]
        public Color nightSkyTop = Palette.Hex("#2c2236");
        public Color nightSkyMid = Palette.Hex("#594863");
        public Color nightHorizon = Palette.Hex("#6b4a5e"); // dusk glow, faded
        public Color moonColor = Palette.Hex("#aab4e6");
        public float moonIntensity = 0.65f;
        [Tooltip("The way the moonlight travels (from the sky toward the ground).")]
        public Vector3 moonDirection = new Vector3(-20f, -40f, -25f);

        [Header("Haze")]
        [Tooltip("The ground fades into the horizon color between these distances, like the web game's fog.")]
        public bool fog = true;
        public float fogStart = 288f;
        public float fogEnd = 1216f;

        const int DayStart = 6, DayEnd = 18;
        const float ClockCheckSeconds = 30f;

        public bool IsNight { get; private set; }

        /// <summary>
        /// The web game's sun and moon directions are given relative to the field (its +Z points at goal A).
        /// The game sets this to the Training Arena, so the light falls on the field the same way here.
        /// </summary>
        public Transform field;

        // The day look, as the scene was built.
        Quaternion dayRotation;
        Color dayColor;
        float dayIntensity;
        bool dayUsesTemperature;
        Material sky;
        Color dayTop, dayMid, dayHorizon;
        float untilClockCheck;

        void Awake()
        {
            if (!sun) sun = RenderSettings.sun;
            if (sun)
            {
                dayRotation = sun.transform.rotation;
                dayColor = webDaySun ? webSunColor : sun.color;
                dayIntensity = webDaySun ? webSunIntensity : sun.intensity;
                dayUsesTemperature = !webDaySun && sun.useColorTemperature;
            }
            // Work on a copy of the sky material, so switching to night never changes the asset itself.
            if (RenderSettings.skybox && RenderSettings.skybox.HasProperty("_Top"))
            {
                sky = new Material(RenderSettings.skybox);
                RenderSettings.skybox = sky;
                dayTop = sky.GetColor("_Top");
                dayMid = sky.GetColor("_Mid");
                dayHorizon = sky.GetColor("_Horizon");
            }
            Set(setting);
        }

        void OnDestroy()
        {
            Shader.SetGlobalFloat("_ToonNight", 0f); // leave the editor looking like day
        }

        /// <summary>Light the field the way the web game does, turned to match where the field sits.</summary>
        public void UseField(Transform arena)
        {
            field = arena;
            Apply(IsNight);
        }

        /// <summary>Day, night, or Auto. The start menu calls this.</summary>
        public void Set(TimeOfDaySetting value)
        {
            setting = value;
            untilClockCheck = ClockCheckSeconds;
            Apply(value == TimeOfDaySetting.Night || (value == TimeOfDaySetting.Auto && ClockSaysNight()));
        }

        void Update()
        {
            // In Auto, follow the player's clock: switch to night at 18:00 and back at 6:00.
            untilClockCheck -= Time.unscaledDeltaTime;
            if (setting != TimeOfDaySetting.Auto || untilClockCheck > 0f) return;
            untilClockCheck = ClockCheckSeconds;
            if (ClockSaysNight() != IsNight) Apply(!IsNight);
        }

        static bool ClockSaysNight()
        {
            int hour = DateTime.Now.Hour;
            return hour < DayStart || hour >= DayEnd;
        }

        void Apply(bool night)
        {
            IsNight = night;
            // Every toon material slides its shadows toward its night shadow color.
            Shader.SetGlobalFloat("_ToonNight", night ? 1f : 0f);
            if (sun)
            {
                Quaternion turn = field ? Quaternion.Euler(0f, field.eulerAngles.y, 0f) : Quaternion.identity;
                if (night) sun.transform.rotation = Quaternion.LookRotation(turn * moonDirection.normalized);
                else sun.transform.rotation = webDaySun ? Quaternion.LookRotation(turn * webSunDirection.normalized) : dayRotation;
                sun.useColorTemperature = !night && dayUsesTemperature;
                sun.color = night ? moonColor : dayColor;
                sun.intensity = night ? moonIntensity : dayIntensity;
            }
            if (fog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.fogColor = night ? nightHorizon : (sky ? dayHorizon : RenderSettings.fogColor);
            }
            if (sky)
            {
                sky.SetColor("_Top", night ? nightSkyTop : dayTop);
                sky.SetColor("_Mid", night ? nightSkyMid : dayMid);
                sky.SetColor("_Horizon", night ? nightHorizon : dayHorizon);
            }
        }
    }
}
